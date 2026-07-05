using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Colonies;
using Pulsar4X.Datablobs;
using Pulsar4X.Components;
using Pulsar4X.Factions;
using Pulsar4X.Industry;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// The C-track ECONOMY WIRE — build an installation on a SPECIFIC mini-hex tile through the real production system,
    /// not the free direct-placement of <see cref="PlaceInstallationInRegionOrder"/>. <see cref="QueueBuildOnTile"/>
    /// queues a genuine <see cref="IndustryJob"/> (consumes materials, takes build-time, rides the colony's production
    /// line) AND reserves the target tile in the body's <see cref="GroundBuildQueueDB"/>. When the build finishes,
    /// <see cref="ReconcileBody"/> (run hourly by <see cref="GroundBuildQueueProcessor"/>) finds the freshly-built
    /// footprint building and lays it on the reserved tile — closing the cradle-to-grave chain (mineral → material →
    /// production → component → installed → located on the tile → bombed = lost). Defensive throughout.
    /// Design: docs/GROUND-CITY-AND-WARMAP-DESIGN.md.
    /// </summary>
    public static class GroundBuild
    {
        /// <summary>Queue a REAL industry build of installation <paramref name="designId"/> targeted at the GLOBAL hex
        /// (<paramref name="gQ"/>,<paramref name="gR"/>) mini-hex tile (<paramref name="tileQ"/>,<paramref name="tileR"/>):
        /// adds a production job (materials + time, installed on the colony) AND a tile reservation. Returns false if the
        /// colony/design/body/tile can't be resolved, the design isn't a buildable installation the faction has, there's
        /// no production line for it, the tile is occupied, or that tile is already reserved. Never throws.</summary>
        public static bool QueueBuildOnTile(Entity colony, int gQ, int gR, int tileQ, int tileR, string designId)
        {
            if (colony == null || string.IsNullOrEmpty(designId)) return false;
            if (!colony.TryGetDataBlob<ColonyInfoDB>(out var ci)) return false;
            var body = ci.PlanetEntity;
            if (body == null || !body.IsValid) return false;
            var game = colony.Manager?.Game;
            if (game == null) return false;
            if (!game.Factions.TryGetValue(colony.FactionOwnerID, out var factionEntity)) return false;
            if (!factionEntity.TryGetDataBlob<FactionInfoDB>(out var factionInfo)) return false;
            if (!factionInfo.IndustryDesigns.TryGetValue(designId, out var design) || design is not ComponentDesign) return false;
            if (!colony.TryGetDataBlob<IndustryAbilityDB>(out var industry)) return false;

            // find a production line on the colony that can build this design's industry type (mirrors TestScenario.QueueProductionJob)
            string lineId = null;
            foreach (var kv in industry.ProductionLines)
                if (kv.Value.IndustryTypeRates.ContainsKey(design.IndustryTypeID)) { lineId = kv.Key; break; }
            if (lineId == null) return false;

            // the target tile must exist and be empty
            var grid = PlanetGridFactory.EnsureGridForBody(body);
            var hex = grid?.HexAt(gQ, gR);
            if (hex == null) return false;
            var city = CityGridFactory.EnsureCityForGlobalHex(body, gQ, gR);
            var tile = city?.TileAt(tileQ, tileR);
            if (tile == null || tile.BuildingInstanceId != -1) return false;

            var queue = EnsureQueue(body);
            foreach (var r in queue.Reservations)
                if (r.GQ == hex.Q && r.GR == hex.R && r.TileQ == tileQ && r.TileR == tileR) return false;   // tile already reserved

            // queue a REAL industry job (materials + build-time), installed on the colony when done
            var job = new IndustryJob(factionInfo, designId);
            job.InitialiseJob(1, false);
            job.InstallOn = colony;
            IndustryTools.AddJob(colony, lineId, job);

            queue.Reservations.Add(new GroundBuildReservation(colony.Id, designId, hex.Q, hex.R, tileQ, tileR));
            return true;
        }

        /// <summary>Try to satisfy the body's pending tile-targeted builds: for each reservation, if the colony now holds
        /// a not-yet-placed footprint building of that design (its industry job finished), lay it on the reserved tile
        /// and clear the reservation. Returns how many were satisfied this pass. Run hourly by
        /// <see cref="GroundBuildQueueProcessor"/>; never throws.</summary>
        public static int ReconcileBody(Entity body)
        {
            if (body == null || !body.TryGetDataBlob<GroundBuildQueueDB>(out var queue) || queue.Reservations.Count == 0) return 0;
            if (!body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB) || regionsDB.SurfaceGrid == null) return 0;
            var grid = regionsDB.SurfaceGrid;

            // building ids already sitting on SOME city tile across the whole grid (so we don't place one twice)
            var placed = new HashSet<int>();
            if (grid.Hexes != null)
                foreach (var h in grid.Hexes)
                    if (h.CityGrid?.Tiles != null)
                        foreach (var t in h.CityGrid.Tiles) if (t.BuildingInstanceId != -1) placed.Add(t.BuildingInstanceId);

            int satisfied = 0;
            var survivors = new List<GroundBuildReservation>();
            foreach (var r in queue.Reservations)
            {
                var colony = FindColony(body, r.ColonyId);
                if (colony == null) continue;   // colony gone → drop the reservation
                int id = FindUnplacedFootprintOfDesign(colony, r.DesignId, placed);
                if (id < 0) { survivors.Add(r); continue; }   // its build hasn't finished yet — keep waiting
                if (CityBuilder.PlaceBuildingOnGlobalTile(body, r.GQ, r.GR, r.TileQ, r.TileR, id))
                { placed.Add(id); satisfied++; }
                else survivors.Add(r);   // tile blocked / no footprint room right now — keep trying
            }
            queue.Reservations = survivors;
            return satisfied;
        }

        // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────

        private static GroundBuildQueueDB EnsureQueue(Entity body)
        {
            if (!body.TryGetDataBlob<GroundBuildQueueDB>(out var q))
            {
                q = new GroundBuildQueueDB();
                body.SetDataBlob(q);   // SetDataBlob schedules the reconciler processor (GameEngine gotcha 5)
            }
            return q;
        }

        private static Entity FindColony(Entity body, int colonyId)
        {
            if (body?.Manager == null) return null;
            foreach (var c in body.Manager.GetAllEntitiesWithDataBlob<ColonyInfoDB>())
                if (c.Id == colonyId) return c;
            return null;
        }

        /// <summary>A footprint ComponentInstance of the given design on the colony that isn't yet on a tile, or -1.</summary>
        private static int FindUnplacedFootprintOfDesign(Entity colony, string designId, HashSet<int> placed)
        {
            if (!colony.TryGetDataBlob<ComponentInstancesDB>(out var comps)) return -1;
            foreach (var inst in comps.AllComponents.Values)
                if (inst.Design != null && inst.Design.UniqueID == designId
                    && GroundBuildings.IsFootprint(inst.Design) && !placed.Contains(inst.ID))
                    return inst.ID;
            return -1;
        }
    }
}
