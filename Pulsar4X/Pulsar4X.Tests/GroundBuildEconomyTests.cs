using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Colonies;
using Pulsar4X.GroundCombat;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;   // ComponentInstancesDB (namespace ≠ folder)
using Pulsar4X.Factions;
using Pulsar4X.Industry;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// C-track ECONOMY WIRE (C3) — building on a mini-hex tile goes through the REAL production system, not free
    /// direct-placement. Two isolated gauges: (A) `GroundBuild.ReconcileBody` lays an available footprint building
    /// onto its reserved tile and clears the reservation (the reconcile→place logic, no industry dependency), and
    /// (B) `GroundBuild.QueueBuildOnTile` queues a GENUINE industry job (installed on the colony) AND reserves the tile
    /// (the economy wiring). Composed with `ProductionBuildTests` (a queued job completes + installs), these cover the
    /// whole chain without a slow, fragile clock-advance. Design: docs/GROUND-CITY-AND-WARMAP-DESIGN.md.
    /// </summary>
    [TestFixture]
    public class GroundBuildEconomyTests
    {
        private const string BunkerDesignId = "default-design-bunker";

        private static ComponentInstance InstallBunker(TestScenario s)
        {
            var fi = s.Faction.GetDataBlob<FactionInfoDB>();
            var design = (ComponentDesign)fi.IndustryDesigns[BunkerDesignId];
            var inst = new ComponentInstance(design);
            s.Colony.AddComponent(inst);
            return inst;
        }

        [Test]
        [Description("C3 (reconcile→place, isolated): with a tile reservation on the body and an unplaced footprint building of that design on the colony, ReconcileBody lays it on the reserved tile (footprint-aware), rolls it up to the operational hex, and clears the reservation.")]
        public void ReconcileBody_LaysAnAvailableBuilding_OnItsReservedTile()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            var grid = PlanetGridFactory.EnsureGridForBody(body);
            int gQ = PlanetGridFactory.BandCentreColumn(0, grid.Cols, regionsDB.Regions.Count);
            int gR = grid.Rows / 2;
            var hex = grid.HexAt(gQ, gR);

            InstallBunker(s);   // an available (unplaced) footprint building — stands in for a completed industry job

            var queue = new GroundBuildQueueDB();
            queue.Reservations.Add(new GroundBuildReservation(s.Colony.Id, BunkerDesignId, hex.Q, hex.R, 1, 0));
            body.SetDataBlob(queue);

            Assert.That(GroundBuild.ReconcileBody(body), Is.EqualTo(1), "the reservation was satisfied");
            int placedId = hex.CityGrid.TileAt(1, 0).BuildingInstanceId;
            Assert.That(placedId, Is.Not.EqualTo(-1), "a building now occupies the reserved tile (anchor)");
            Assert.That(hex.InstallationIds, Does.Contain(placedId), "and rolls up to the operational hex");
            Assert.That(body.GetDataBlob<GroundBuildQueueDB>().Reservations, Is.Empty, "reservation cleared");
            Assert.That(GroundBuild.ReconcileBody(body), Is.EqualTo(0), "nothing left to satisfy");
        }

        [Test]
        [Description("C3 (economy wiring, isolated): QueueBuildOnTile queues a REAL industry job for the design on the colony's production line, installed on the colony (so the build consumes materials + time and becomes a real building), AND reserves the tile. Refuses a second reservation on the same tile.")]
        public void QueueBuildOnTile_QueuesARealIndustryJob_InstalledOnTheColony()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            var grid = PlanetGridFactory.EnsureGridForBody(body);
            int gQ = PlanetGridFactory.BandCentreColumn(1, grid.Cols, regionsDB.Regions.Count);
            int gR = grid.Rows / 2;

            var industry = s.Colony.GetDataBlob<IndustryAbilityDB>();
            int jobsBefore = industry.ProductionLines.Values.Sum(l => l.Jobs.Count);

            Assert.That(GroundBuild.QueueBuildOnTile(s.Colony, gQ, gR, 1, 0, BunkerDesignId), Is.True, "queued the tile-targeted build");

            int jobsAfter = industry.ProductionLines.Values.Sum(l => l.Jobs.Count);
            Assert.That(jobsAfter, Is.EqualTo(jobsBefore + 1), "a REAL production job was queued (materials + build-time), not free placement");
            var job = industry.ProductionLines.Values.SelectMany(l => l.Jobs).FirstOrDefault(j => j.ItemGuid == BunkerDesignId);
            Assert.That(job, Is.Not.Null, "the queued job builds the bunker design");
            Assert.That(job.InstallOn, Is.EqualTo(s.Colony), "installed on the colony when done (so it becomes a real building)");
            Assert.That(body.GetDataBlob<GroundBuildQueueDB>().Reservations, Has.Count.EqualTo(1), "and the tile is reserved");

            Assert.That(GroundBuild.QueueBuildOnTile(s.Colony, gQ, gR, 1, 0, BunkerDesignId), Is.False, "the same tile can't be reserved twice");
        }
    }
}
