using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Colonies;
using Pulsar4X.GroundCombat;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;   // ComponentInstancesDB (namespace ≠ folder)
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// C-track ECONOMY WIRE (C3) — building on a mini-hex tile goes through the REAL production system, not free
    /// direct-placement. `GroundBuild.QueueBuildOnTile` queues a genuine industry job (materials + build-time) AND
    /// reserves the tile; `GroundBuildQueueProcessor` lays the finished building on the reserved tile. These gauges
    /// prove: (1) the reserve→reconcile→place logic deterministically (no clock), and (2) the FULL path end-to-end —
    /// queue a build, advance a game-year, the colony builds a new building AND it lands on the reserved tile.
    /// Design: docs/GROUND-CITY-AND-WARMAP-DESIGN.md.
    /// </summary>
    [TestFixture]
    public class GroundBuildEconomyTests
    {
        private const string BunkerDesignId = "default-design-bunker";

        private static int BunkerCount(Entity colony)
            => colony.GetDataBlob<ComponentInstancesDB>().AllComponents.Values
                .Count(i => i.Design != null && i.Design.UniqueID == BunkerDesignId);

        [Test]
        [Description("C3 (reserve→reconcile→place, deterministic): QueueBuildOnTile reserves the tile + queues a job; ReconcileBody lays an available footprint building of that design onto the reserved tile and clears the reservation.")]
        public void QueueAndReconcile_PlacesTheBuildingOnTheReservedTile()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            var grid = PlanetGridFactory.EnsureGridForBody(body);
            int gQ = PlanetGridFactory.BandCentreColumn(0, grid.Cols, regionsDB.Regions.Count);
            int gR = grid.Rows / 2;
            var hex = grid.HexAt(gQ, gR);

            // a built-but-unplaced bunker in the colony's stock (stands in for a completed industry job)
            var fi = s.Faction.GetDataBlob<FactionInfoDB>();
            var design = (ComponentDesign)fi.IndustryDesigns[BunkerDesignId];
            var inst = new ComponentInstance(design);
            s.Colony.AddComponent(inst);

            Assert.That(GroundBuild.QueueBuildOnTile(s.Colony, gQ, gR, 1, 0, BunkerDesignId), Is.True, "queued a build reservation on the tile");
            Assert.That(body.GetDataBlob<GroundBuildQueueDB>().Reservations, Has.Count.EqualTo(1));
            Assert.That(GroundBuild.QueueBuildOnTile(s.Colony, gQ, gR, 1, 0, BunkerDesignId), Is.False, "the same tile can't be reserved twice");

            int satisfied = GroundBuild.ReconcileBody(body);
            Assert.That(satisfied, Is.EqualTo(1), "the reservation was satisfied");
            Assert.That(hex.CityGrid.TileAt(1, 0).BuildingInstanceId, Is.Not.EqualTo(-1), "a building now occupies the reserved tile (anchor)");
            Assert.That(hex.InstallationIds, Does.Contain(hex.CityGrid.TileAt(1, 0).BuildingInstanceId), "and rolls up to the operational hex");
            Assert.That(body.GetDataBlob<GroundBuildQueueDB>().Reservations, Is.Empty, "reservation cleared");
        }

        [Test]
        [Description("C3 (FULL economy path): queue a tile-targeted build, advance a game-year — the colony BUILDS a new bunker through the real production line (materials + time) and the hourly reconciler lays it on the reserved tile. The cradle-to-grave wire, end to end.")]
        public void BuildOnTile_ThroughIndustry_BuildsAndLandsOnTheReservedTile()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            var grid = PlanetGridFactory.EnsureGridForBody(body);
            int gQ = PlanetGridFactory.BandCentreColumn(1, grid.Cols, regionsDB.Regions.Count);
            int gR = grid.Rows / 2;
            var hex = grid.HexAt(gQ, gR);

            int before = BunkerCount(s.Colony);
            Assert.That(GroundBuild.QueueBuildOnTile(s.Colony, gQ, gR, 1, 0, BunkerDesignId), Is.True, "queued the tile-targeted build");

            s.AdvanceTime(TimeSpan.FromDays(365));   // let the industry job finish + the hourly reconciler run

            Assert.That(BunkerCount(s.Colony), Is.GreaterThanOrEqualTo(before + 1), "the colony built a new bunker through the economy");
            Assert.That(hex.CityGrid?.TileAt(1, 0)?.BuildingInstanceId, Is.Not.Null.And.Not.EqualTo(-1), "and it landed on the reserved tile");
            Assert.That(body.GetDataBlob<GroundBuildQueueDB>().Reservations, Is.Empty, "the reservation was satisfied");
        }
    }
}
