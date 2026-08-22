using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// DS-AI-HOOK gauge — the NPC brain's ground-haul rung (<see cref="GroundHaulAI.TryConsolidateOre"/>). Proves the
    /// AI presses the SAME standing-haul-route button a player clicks: when a hex holds a surplus of ore, it sets a
    /// route consolidating it toward the faction's built-up "depot" hex. Mirrors <c>GrowEconomyResolverTests</c> (build
    /// a colony, snapshot the faction state, call the pure decision, assert Kind + no-side-effect-before-Execute + the
    /// delta after Execute) plus the hex-setup idiom from <c>GroundForcesTests</c>.
    ///
    /// The four gauges: (1) flag OFF → null even with a surplus present (byte-identical); (2) flag ON but a stock game
    /// with no ore on any hex → null (inert until per-hex mining fills a bucket); (3) a real surplus → one route set to
    /// the depot, and nothing mutated before Execute; (4) idempotent — a second decision with the route already present
    /// returns null (no duplicate route stacking, the AddRoute no-dedup guard).
    /// </summary>
    [TestFixture]
    public class GroundHaulAITests
    {
        private const int TestGood = 42;      // an arbitrary cargoable id — the decision reads the key, never resolves it
        private const long Surplus = 500;     // well above GroundHaulAI.MinHaulSurplus (100)

        [TearDown]
        public void ResetFlag() => GroundHaulAI.EnableGroundHaulAI = false;   // never leak the flag into a sibling fixture

        /// <summary>Stand up the start colony's body, its surface grid, a guaranteed-max DEPOT hex (many buildings) and a
        /// SOURCE hex holding a surplus of <see cref="TestGood"/>. Returns the body + the two hexes.</summary>
        private static (Entity body, GroundHex depot, GroundHex source) SetUpDepotAndSurplus(TestScenario s)
        {
            // Regions + the cylinder grid (idempotent; the grid TryConsolidateOre reads is cached on the body).
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = GroundReinforcement.GarrisonBodyOf(s.Colony);
            Assert.NotNull(body, "start colony must sit on a planet body");
            var grid = PlanetGridFactory.EnsureGridForBody(body);
            Assert.NotNull(grid);
            Assert.That(grid.Hexes.Count, Is.GreaterThan(1), "need at least two hexes to route between");

            var depot = grid.Hexes[0];
            var source = grid.Hexes[grid.Hexes.Count / 2];
            Assert.That(source.Q != depot.Q || source.R != depot.R, "depot and source must be distinct hexes");

            // Guarantee the depot is the MOST built-up hex regardless of any installations the colony already located.
            for (int i = 0; i < 100; i++) depot.InstallationIds.Add(900000 + i);
            // Put a real surplus on the source hex (the only hex holding ore → the deterministic source).
            source.AddToStockpile(TestGood, Surplus);

            return (body, depot, source);
        }

        [Test]
        public void FlagOff_ReturnsNull_EvenWithSurplus()
        {
            var s = TestScenario.CreateWithColony();
            SetUpDepotAndSurplus(s);
            GroundHaulAI.EnableGroundHaulAI = false;   // explicit — the guard the byte-identity guarantee rests on

            var action = GroundHaulAI.TryConsolidateOre(FactionState.Snapshot(s.Faction));
            Assert.IsNull(action, "flag off must be a no-op (byte-identical) even when a surplus exists");
        }

        [Test]
        public void StockGame_NoOreOnAnyHex_ReturnsNull()
        {
            var s = TestScenario.CreateWithColony();
            // No SetUp — a fresh colony has empty hex stockpiles (nothing fills a bucket until per-hex mining is on).
            GroundHaulAI.EnableGroundHaulAI = true;

            var action = GroundHaulAI.TryConsolidateOre(FactionState.Snapshot(s.Faction));
            Assert.IsNull(action, "with no ore on any hex the rung is inert (stock game byte-identical)");
        }

        [Test]
        public void Surplus_SetsOneRouteToTheDepot_NoSideEffectBeforeExecute()
        {
            var s = TestScenario.CreateWithColony();
            var (body, depot, source) = SetUpDepotAndSurplus(s);
            GroundHaulAI.EnableGroundHaulAI = true;

            var action = GroundHaulAI.TryConsolidateOre(FactionState.Snapshot(s.Faction));
            Assert.IsNotNull(action, "a surplus hex + a depot hex should produce a haul decision");
            Assert.AreEqual("HaulOre", action.Kind);

            // The resolver is a pure decision — the route is NOT added until the processor runs Execute.
            bool beforeHasRoute = body.TryGetDataBlob<GroundHaulRouteDB>(out var pre) && pre.Routes.Count > 0;
            Assert.IsFalse(beforeHasRoute, "no route may exist before Execute (the decision must not mutate the sim)");

            action.Execute();

            Assert.IsTrue(body.TryGetDataBlob<GroundHaulRouteDB>(out var db), "Execute must create the route roster");
            Assert.AreEqual(1, db.Routes.Count, "exactly one standing route set");
            var r = db.Routes[0];
            Assert.AreEqual(source.Q, r.SrcQ);
            Assert.AreEqual(source.R, r.SrcR);
            Assert.AreEqual(depot.Q, r.DstQ);
            Assert.AreEqual(depot.R, r.DstR);
            Assert.AreEqual(TestGood, r.CargoableId);
            Assert.AreEqual(s.Faction.Id, r.FactionOwnerID);
        }

        [Test]
        public void Idempotent_SecondDecisionWithRoutePresent_ReturnsNull()
        {
            var s = TestScenario.CreateWithColony();
            var (body, _, _) = SetUpDepotAndSurplus(s);
            GroundHaulAI.EnableGroundHaulAI = true;

            // First decision sets the route.
            var first = GroundHaulAI.TryConsolidateOre(FactionState.Snapshot(s.Faction));
            Assert.IsNotNull(first);
            first.Execute();
            Assert.IsTrue(body.TryGetDataBlob<GroundHaulRouteDB>(out var db) && db.Routes.Count == 1);

            // Second decision: the surplus is still on the hex (Execute set a route, it did not move ore), but the route
            // already exists → the rung must NOT stack a duplicate (AddRoute appends unconditionally, so the guard is here).
            var second = GroundHaulAI.TryConsolidateOre(FactionState.Snapshot(s.Faction));
            Assert.IsNull(second, "an existing route for the same src/dst/good must not be re-added");
            Assert.AreEqual(1, db.Routes.Count, "no duplicate route stacked");
        }
    }
}
