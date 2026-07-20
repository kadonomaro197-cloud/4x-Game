using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// The RADAR reveal — the payoff of units-as-entities (Option A). A unit that carries a <see cref="GroundSensorAtb"/>
    /// (a radar) on its backing entity REVEALS THE MAP within the radar's reach each tick. The ability FALLS OUT of the
    /// component store: <see cref="GroundUnitEntity.TryGetBacking"/> → <c>TryGetComponentsByAttribute&lt;GroundSensorAtb&gt;</c>,
    /// exactly the way a ship finds its sensors — no per-unit special-case, no "scout type".
    ///
    /// The radar's REAL range (km) is TRANSLATED to a hex reach on the planet map (<c>Range_km / HexPitchKm(region)</c>,
    /// since a hex's real size differs body to body), and that reach in hexes is mapped to how many REGION bands it
    /// covers (a region is a column band of width ~<c>cols/regionCount</c>). Revealing = flipping <c>Region.Surveyed</c>,
    /// which surfaces that region's terrain + mineral deposits. Run every tick by <see cref="GroundForcesProcessor"/>;
    /// idempotent (an already-known region is a no-op), defensive (never throws in the hotloop — L4).
    ///
    /// GROUND-FOG SLICE 3 (`docs/ground/SURFACE-FOG-AND-RECON-DESIGN.md`): this radar IS the "recon component" the design
    /// called for — a designed/buildable/losable component (the base-mod <c>ground-radar</c>), so nothing new is built;
    /// the reveal is WIRED into the per-faction fog. Alongside the world-level <c>RevealRegion</c> (kept, additive) it now
    /// (a) reveals each reached region to the SCOUTING FACTION only (<see cref="PlanetRegionsDB.RevealRegionFor"/>), and
    /// (b) unmasks the deposit ASSAY (<see cref="GroundHex.RevealDepositAssay"/> → the Full tier — exact amount) of the
    /// deposit hexes in the region a scout physically STANDS in (boots-on-the-deposit: the ground-scout tier that the
    /// orbital survey's Partial "located, un-assayed" grant does not give). So an enemy without a scout on the ground
    /// reads neither the region nor the assay — real per-faction ground fog.
    /// </summary>
    public static class GroundSensors
    {
        /// <summary>PURE geometry — the region indices a radar at global column <paramref name="globalQ"/> in
        /// <paramref name="regionIndex"/> reaches, given its real <paramref name="rangeKm"/>. Always includes the unit's
        /// own region; extends outward one ring band per region-width the reach spans (translated km → hex → bands).</summary>
        public static List<int> RegionsInReach(Region region, int regionIndex, int globalQ, double rangeKm, int cols, int regionCount)
        {
            var result = new List<int>();
            if (regionCount <= 0) return result;
            int own = ((regionIndex % regionCount) + regionCount) % regionCount;
            result.Add(own);   // you always know the ground you stand on

            double pitch = region != null ? GroundRangeTools.HexPitchKm(region) : 0;
            if (pitch <= 0 || rangeKm <= 0 || cols <= 0) return result;

            double reachHexes = rangeKm / pitch;        // real range → hexes on THIS body
            int hexReach = reachHexes >= int.MaxValue ? int.MaxValue : (int)reachHexes;   // guard the cast (no overflow)
            int bandWidth = Math.Max(1, cols / regionCount);
            int bands = hexReach / bandWidth;           // whole region-widths the radar sees past
            for (int d = 1; d <= bands && d < regionCount; d++)
            {
                result.Add(((own + d) % regionCount + regionCount) % regionCount);
                result.Add(((own - d) % regionCount + regionCount) % regionCount);
            }
            return result;
        }

        /// <summary>The radar reach of <paramref name="unit"/> in HEXES on <paramref name="body"/> — its best mounted
        /// <see cref="GroundSensorAtb"/> range (km) translated to hexes on THIS body (<c>Range_km / HexPitchKm</c> of the
        /// unit's region, since a hex's real size differs body to body — the same km→hex conversion the per-tick reveal
        /// uses). The reach the Force/planet-map window highlights green (R1 gap; CI-testable). 0 when the unit carries
        /// no radar, has no backing store, stands off-grid, or the region has no hex geometry yet. Read-only, never
        /// throws. NOTE: the unit's own hex is always within reach even at 0 — this is the OUTWARD reach, not "can I see
        /// my own hex."</summary>
        public static double RadarReachHexes(Entity body, GroundUnit unit)
        {
            try
            {
                if (body == null || unit == null) return 0;
                if (!GroundUnitEntity.TryGetBacking(body, unit, out var backing)) return 0;
                if (!backing.TryGetDataBlob<Pulsar4X.Datablobs.ComponentInstancesDB>(out var cidb)) return 0;
                if (!cidb.TryGetComponentsByAttribute<GroundSensorAtb>(out var radars) || radars.Count == 0) return 0;

                double rangeKm = 0;
                foreach (var r in radars)
                {
                    var atb = r.Design?.GetAttribute<GroundSensorAtb>();
                    if (atb != null && atb.Range_km > rangeKm) rangeKm = atb.Range_km;   // best radar mounted
                }
                if (rangeKm <= 0) return 0;

                if (!body.TryGetDataBlob<PlanetRegionsDB>(out var regions)
                    || unit.RegionIndex < 0 || unit.RegionIndex >= regions.Regions.Count) return 0;
                double pitch = GroundRangeTools.HexPitchKm(regions.Regions[unit.RegionIndex]);
                return pitch > 0 ? rangeKm / pitch : 0;
            }
            catch { return 0; }
        }

        /// <summary>Reveal every region within radar reach of a radar-carrying unit. Returns the number of regions newly
        /// revealed this pass. No-op on a body with no region layer / no forces. Never throws.</summary>
        public static int RevealFromUnits(Entity body)
        {
            try
            {
                if (body == null
                    || !body.TryGetDataBlob<GroundForcesDB>(out var forces) || forces.Units == null
                    || !body.TryGetDataBlob<PlanetRegionsDB>(out var regions))
                    return 0;

                var grid = PlanetGridFactory.EnsureGridForBody(body);
                int cols = grid?.Cols ?? 0;
                int regionCount = regions.Regions.Count;
                int revealed = 0;
                bool any = false;

                // Slice 3: the regions each faction's scouts REACH (per-faction geography) and the regions a faction
                // physically STANDS a scout in (where it earns the deposit ASSAY — boots on the deposit).
                var reachedByFaction = new Dictionary<int, HashSet<int>>();
                var standingByFaction = new Dictionary<int, HashSet<int>>();

                foreach (var unit in forces.Units)
                {
                    if (unit == null || unit.MovingToRegion >= 0) continue;                 // reveals where it has arrived
                    if (!GroundUnitEntity.TryGetBacking(body, unit, out var backing)) continue;
                    if (!backing.TryGetDataBlob<Pulsar4X.Datablobs.ComponentInstancesDB>(out var cidb)) continue;
                    if (!cidb.TryGetComponentsByAttribute<GroundSensorAtb>(out var radars) || radars.Count == 0) continue;

                    double rangeKm = 0;
                    foreach (var r in radars)
                    {
                        var atb = r.Design?.GetAttribute<GroundSensorAtb>();
                        if (atb != null && atb.Range_km > rangeKm) rangeKm = atb.Range_km;   // best radar mounted
                    }
                    if (unit.RegionIndex < 0 || unit.RegionIndex >= regions.Regions.Count) continue;
                    var region = regions.Regions[unit.RegionIndex];

                    foreach (var ri in RegionsInReach(region, unit.RegionIndex, unit.GlobalQ, rangeKm, cols, regionCount))
                    {
                        if (regions.RevealRegion(ri)) { revealed++; any = true; }   // world-level (kept, additive)
                        regions.RevealRegionFor(unit.FactionOwnerID, ri);           // per-faction geography (slice 3)
                        AddTo(reachedByFaction, unit.FactionOwnerID, ri);
                    }
                    AddTo(standingByFaction, unit.FactionOwnerID, unit.RegionIndex); // the region it earns the assay in
                }

                if (any) PlanetGridFactory.EnsureGridForBody(body);   // surface deposits in the newly-known regions

                // Slice 3: unmask the deposit ASSAY (Full tier) of deposit hexes in the region a scout STANDS in, for
                // that scout's faction only. One grid pass, gated by the faction→standing-region map (cheap: usually
                // 1–2 factions with ground radar on a body). Needs the faction BIT mask, resolved once per faction.
                var g = body.Manager?.Game;
                if (g != null && grid?.Hexes != null && standingByFaction.Count > 0 && cols > 0 && regionCount > 0)
                {
                    var maskByFaction = new Dictionary<int, int>();
                    foreach (var fid in standingByFaction.Keys)
                        maskByFaction[fid] = g.Factions.TryGetValue(fid, out var facEnt)
                            && facEnt.TryGetDataBlob<FactionInfoDB>(out var fi) ? fi.FactionMask : 0;

                    foreach (var hex in grid.Hexes)
                    {
                        if (hex == null || hex.DepositMineralId < 0) continue;
                        int hexRegion = PlanetGridFactory.RegionOfColumn(hex.Q, cols, regionCount);
                        foreach (var kv in standingByFaction)
                            if (kv.Value.Contains(hexRegion) && maskByFaction.TryGetValue(kv.Key, out var mask) && mask != 0)
                                hex.RevealDepositAssay(mask);   // boots-on-the-deposit → exact tonnage, this faction only
                    }
                }
                return revealed;
            }
            catch { return 0; }   // radar reveal is a nicety — never break the ground hotloop over it
        }

        /// <summary>Accumulate <paramref name="value"/> into the set for <paramref name="key"/> (create-on-demand).</summary>
        private static void AddTo(Dictionary<int, HashSet<int>> map, int key, int value)
        {
            if (!map.TryGetValue(key, out var set)) map[key] = set = new HashSet<int>();
            set.Add(value);
        }
    }
}
