using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
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
                        if (regions.RevealRegion(ri)) { revealed++; any = true; }
                }

                if (any) PlanetGridFactory.EnsureGridForBody(body);   // surface deposits in the newly-known regions
                return revealed;
            }
            catch { return 0; }   // radar reveal is a nicety — never break the ground hotloop over it
        }
    }
}
