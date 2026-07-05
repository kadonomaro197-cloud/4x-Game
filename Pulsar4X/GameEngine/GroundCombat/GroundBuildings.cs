using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Colonies;
using Pulsar4X.Datablobs;
using Pulsar4X.Components;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// The WAR-MAP building axis (W1) — the operational-hex half of the two-zoom model
    /// (docs/GROUND-CITY-AND-WARMAP-DESIGN.md). A strategic building (one whose design carries a
    /// <see cref="GroundFootprintAtb"/>) gets a home on a specific <see cref="GroundHex"/> — the "ship icon" on the war
    /// map — so the ground war has something worth fighting over: <b>capturing the hex captures what's on it, bombing
    /// the hex damages what's on it.</b>
    ///
    /// This is the region-level <see cref="GroundInstallations"/> reconciliation taken ONE ZOOM FINER: those buildings
    /// still live in <see cref="Region.InstallationIds"/> for the economy + fortification; a FOOTPRINT building ALSO
    /// records its instance id on the region's centre/muster hex (guaranteed passable land — see
    /// <c>PlanetHexFactory</c>). Nothing about the working economy is disturbed. Everything is defensive (these run
    /// in/near the hourly ground hotloop — L4: never throw).
    /// </summary>
    public static class GroundBuildings
    {
        /// <summary>True if this design is a strategic FOOTPRINT building (carries a <see cref="GroundFootprintAtb"/>) —
        /// i.e. it shows on the operational war map and is a capture/bombard target. A solar panel is not.</summary>
        public static bool IsFootprint(ComponentDesign design)
            => design != null && design.HasAttribute<GroundFootprintAtb>();   // GetAttribute THROWS on a missing atb — HasAttribute is the safe check

        /// <summary>Drop a colony's FOOTPRINT installations onto their region's centre hex (0,0) — the finer location the
        /// war map fights over. Reads the region-level placement (<see cref="Region.InstallationIds"/>, already set by
        /// <see cref="GroundInstallations.LocateColonyInstallations"/> / <c>PlaceInstallationInRegionOrder</c>) and, for
        /// each id whose design is a footprint, also records it on that region's muster hex. Idempotent (an id already
        /// on any hex is skipped) and non-footprint buildings are left off the map. Returns how many were newly placed.
        /// Defensive: no colony / no region layer / no hexes → does nothing.</summary>
        public static int LocateFootprintsOnHexes(Entity colony)
        {
            if (colony == null || !colony.TryGetDataBlob<ColonyInfoDB>(out var ci)) return 0;
            var body = ci.PlanetEntity;
            if (body == null || !body.IsValid || !body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB) || regionsDB.Regions.Count == 0)
                return 0;
            if (!colony.TryGetDataBlob<ComponentInstancesDB>(out var comps)) return 0;

            // instance id → is it a footprint design? (walk the colony's components once)
            var footprintIds = new HashSet<int>();
            foreach (var inst in comps.AllComponents.Values)
                if (IsFootprint(inst.Design)) footprintIds.Add(inst.ID);
            if (footprintIds.Count == 0) return 0;

            // ids already on SOME hex anywhere (idempotency — don't double-place).
            var onAHex = new HashSet<int>();
            foreach (var r in regionsDB.Regions)
                if (r.Hexes != null)
                    foreach (var h in r.Hexes)
                        if (h.InstallationIds != null)
                            foreach (var id in h.InstallationIds) onAHex.Add(id);

            int added = 0;
            for (int i = 0; i < regionsDB.Regions.Count; i++)
            {
                var region = regionsDB.Regions[i];
                if (region.InstallationIds == null || region.Hexes == null) continue;
                var centre = CentreHex(region);
                if (centre == null) continue;
                foreach (var id in region.InstallationIds)
                {
                    if (!footprintIds.Contains(id) || onAHex.Contains(id)) continue;
                    centre.InstallationIds.Add(id);
                    onAHex.Add(id);
                    added++;
                }
            }
            return added;
        }

        /// <summary>Global-grid twin of <see cref="LocateFootprintsOnHexes"/> (C-track) — drop a colony's FOOTPRINT
        /// installations onto their region BAND's muster hex on the continuous cylinder (band-centre column, middle row —
        /// the same hex units muster on, so a base and its garrison share a tile). Idempotent (an id already on any
        /// global hex is skipped). Returns how many were newly placed. Defensive.</summary>
        public static int LocateFootprintsOnGlobalHexes(Entity colony)
        {
            if (colony == null || !colony.TryGetDataBlob<ColonyInfoDB>(out var ci)) return 0;
            var body = ci.PlanetEntity;
            if (body == null || !body.IsValid || !body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB) || regionsDB.Regions.Count == 0)
                return 0;
            if (!colony.TryGetDataBlob<ComponentInstancesDB>(out var comps)) return 0;
            var grid = PlanetGridFactory.EnsureGridForBody(body);
            if (grid == null || grid.Cols <= 0) return 0;

            var footprintIds = new HashSet<int>();
            foreach (var inst in comps.AllComponents.Values)
                if (IsFootprint(inst.Design)) footprintIds.Add(inst.ID);
            if (footprintIds.Count == 0) return 0;

            // ids already on SOME global hex (idempotency — don't double-place).
            var onAHex = new HashSet<int>();
            foreach (var h in grid.Hexes)
                if (h.InstallationIds != null)
                    foreach (var id in h.InstallationIds) onAHex.Add(id);

            int rc = regionsDB.Regions.Count;
            int added = 0;
            for (int i = 0; i < rc; i++)
            {
                var region = regionsDB.Regions[i];
                if (region.InstallationIds == null) continue;
                var hex = grid.HexAt(PlanetGridFactory.BandCentreColumn(i, grid.Cols, rc), grid.Rows / 2);
                if (hex == null) continue;
                foreach (var id in region.InstallationIds)
                {
                    if (!footprintIds.Contains(id) || onAHex.Contains(id)) continue;
                    hex.InstallationIds.Add(id);
                    onAHex.Add(id);
                    added++;
                }
            }
            return added;
        }

        /// <summary>The footprint building ids on a hex (never null).</summary>
        public static IReadOnlyList<int> BuildingsOnHex(GroundHex hex)
            => hex?.InstallationIds ?? (IReadOnlyList<int>)System.Array.Empty<int>();

        /// <summary>Capture the CONTENTS of a just-captured region's hexes: every hex that holds a footprint building
        /// flips its <see cref="GroundHex.OwnerFactionID"/> to <paramref name="newOwner"/> — "capturing the hex captures
        /// what's on it." (The buildings are still the colony's components; the hex-ownership flip is the war-map record
        /// of who holds the base — a later slice transfers the component itself when the whole colony falls.) Returns the
        /// number of footprint buildings whose hex changed hands. Defensive.</summary>
        public static int CaptureRegionHexContents(PlanetRegionsDB regionsDB, int regionIndex, int newOwner)
        {
            if (regionsDB == null || regionIndex < 0 || regionIndex >= regionsDB.Regions.Count) return 0;
            var region = regionsDB.Regions[regionIndex];
            if (region?.Hexes == null) return 0;
            int captured = 0;
            foreach (var h in region.Hexes)
            {
                if (h.InstallationIds == null || h.InstallationIds.Count == 0) continue;
                if (h.OwnerFactionID != newOwner)
                {
                    h.OwnerFactionID = newOwner;
                    captured += h.InstallationIds.Count;
                }
            }
            return captured;
        }

        /// <summary>Bomb an operational hex — the "bombing it damages what's on it" primitive (the grave rung for a
        /// footprint building). Drains <paramref name="strength"/> health (1.0 = a full building) from the footprint
        /// buildings on the (<paramref name="regionIndex"/>, <paramref name="q"/>, <paramref name="r"/>) hex, in order;
        /// a building reduced to ≤ 0 is DESTROYED — removed from its colony and from the hex. Returns how many were
        /// destroyed. Defensive (no body / no region layer / no such hex → 0); reuses the real component-removal, so a
        /// bombed base is genuinely gone from the economy too.</summary>
        public static int BombardHex(Entity body, int regionIndex, int q, int r, double strength)
        {
            if (body == null || !body.IsValid || !body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB)) return 0;
            if (regionIndex < 0 || regionIndex >= regionsDB.Regions.Count) return 0;
            var region = regionsDB.Regions[regionIndex];
            if (region?.Hexes == null) return 0;

            GroundHex hex = null;
            foreach (var h in region.Hexes) if (h.Q == q && h.R == r) { hex = h; break; }
            return BombardResolvedHex(body, hex, region, strength);
        }

        /// <summary>Bomb the GLOBAL hex at (<paramref name="gQ"/>,<paramref name="gR"/>) on the cylinder grid — the
        /// G-track twin of <see cref="BombardHex"/> (same drain/destroy/roll-up, addressed by global coords instead of
        /// region+local). The economy removal (<c>region.InstallationIds</c>) targets the column BAND the hex falls in.
        /// Returns how many footprint buildings were destroyed. Defensive.</summary>
        public static int BombardGlobalHex(Entity body, int gQ, int gR, double strength)
        {
            if (body == null || !body.IsValid || !body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB)) return 0;
            var hex = CityGridFactory.ResolveGlobalHex(body, gQ, gR);
            if (hex == null) return 0;
            int cols = regionsDB.SurfaceGrid?.Cols ?? 0;
            int band = PlanetGridFactory.RegionOfColumn(hex.Q, cols, regionsDB.Regions.Count);
            Region region = (band >= 0 && band < regionsDB.Regions.Count) ? regionsDB.Regions[band] : null;
            return BombardResolvedHex(body, hex, region, strength);
        }

        /// <summary>Shared bombard core (used by both the per-region and the global address paths). Drains
        /// <paramref name="strength"/> across the hex's footprint buildings; a building reduced to ≤0 is removed from its
        /// colony, from the region economy list, and from its fine city tile (roll-up). Returns how many were destroyed.</summary>
        private static int BombardResolvedHex(Entity body, GroundHex hex, Region region, double strength)
        {
            if (hex == null || hex.InstallationIds == null || hex.InstallationIds.Count == 0) return 0;

            // Index the body's colony components by instance id, so we can damage the real building behind a hex id.
            var byId = IndexBodyComponents(body);

            int destroyed = 0;
            double budget = strength;
            var survivors = new List<int>();
            foreach (var id in hex.InstallationIds)
            {
                if (budget <= 0 || !byId.TryGetValue(id, out var pair))
                {
                    survivors.Add(id);   // out of budget, or the component's already gone — keep the ref as-is
                    continue;
                }
                var (comps, inst) = pair;
                double take = budget < inst.HealthPercent ? budget : inst.HealthPercent;
                inst.HealthPercent -= (float)take;
                budget -= take;
                if (inst.HealthPercent <= 0f)
                {
                    comps.RemoveComponentInstance(inst);
                    region?.InstallationIds?.Remove(id);
                    CityBuilder.ClearBuildingFromCity(hex, id);   // roll-up: a bombed base also empties its fine city tile
                    destroyed++;
                }
                else survivors.Add(id);
            }
            hex.InstallationIds = survivors;
            if (destroyed > 0)
            {
                var owner = FindOwningColony(body);
                if (owner != null) ReCalcProcessor.ReCalcAbilities(owner);
            }
            return destroyed;
        }

        // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The region's centre/muster hex (0,0) — where units are raised and a base's presence sits.</summary>
        private static GroundHex CentreHex(Region region)
        {
            if (region?.Hexes == null) return null;
            foreach (var h in region.Hexes) if (h.Q == 0 && h.R == 0) return h;
            return null;
        }

        /// <summary>instance id → (its colony's ComponentInstancesDB, the instance). Walks the body's colonies once.</summary>
        private static Dictionary<int, (ComponentInstancesDB comps, ComponentInstance inst)> IndexBodyComponents(Entity body)
        {
            var map = new Dictionary<int, (ComponentInstancesDB, ComponentInstance)>();
            if (body?.Manager == null) return map;
            foreach (var colony in body.Manager.GetAllEntitiesWithDataBlob<ColonyInfoDB>())
            {
                if (!colony.TryGetDataBlob<ColonyInfoDB>(out var ci) || ci.PlanetEntity == null || ci.PlanetEntity.Id != body.Id) continue;
                if (!colony.TryGetDataBlob<ComponentInstancesDB>(out var comps)) continue;
                foreach (var inst in comps.AllComponents.Values)
                    map[inst.ID] = (comps, inst);
            }
            return map;
        }

        /// <summary>Building instance id → design name, across the body's colonies — a PUBLIC engine accessor for the
        /// client's city-tile readout. (<c>ComponentInstancesDB.AllComponents</c> is INTERNAL, so the client can't walk
        /// it directly — client CLAUDE.md rule #2; this keeps the lookup engine-side.) Defensive; never throws.</summary>
        public static Dictionary<int, string> BuildingNamesOnBody(Entity body)
        {
            var map = new Dictionary<int, string>();
            if (body?.Manager == null) return map;
            foreach (var colony in body.Manager.GetAllEntitiesWithDataBlob<ColonyInfoDB>())
            {
                if (!colony.TryGetDataBlob<ColonyInfoDB>(out var ci) || ci.PlanetEntity == null || ci.PlanetEntity.Id != body.Id) continue;
                if (!colony.TryGetDataBlob<ComponentInstancesDB>(out var comps)) continue;
                foreach (var inst in comps.AllComponents.Values)
                    map[inst.ID] = inst.Design?.Name ?? ("building #" + inst.ID);
            }
            return map;
        }

        private static Entity FindOwningColony(Entity body)
        {
            if (body?.Manager == null) return null;
            foreach (var colony in body.Manager.GetAllEntitiesWithDataBlob<ColonyInfoDB>())
                if (colony.TryGetDataBlob<ColonyInfoDB>(out var ci) && ci.PlanetEntity != null && ci.PlanetEntity.Id == body.Id)
                    return colony;
            return null;
        }
    }
}
