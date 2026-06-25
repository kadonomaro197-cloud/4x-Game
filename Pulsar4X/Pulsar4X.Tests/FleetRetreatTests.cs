using System.Linq;
using NUnit.Framework;
using Pulsar4X.Blueprints;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// MVP combat spine, step 7 — retreat (docs/COMBAT-DESIGN.md System 5). v1 is a MATH OUTCOME: a fleet that
    /// breaks off gets a <see cref="FleetRetreatDB"/> recording the flag + a withdraw vector, and the engagement
    /// ends — no movement order is issued (that's a v2 layer). Two triggers, both proven here:
    ///   • posture  — the fleet flies a withdraw doctrine (fighting-withdrawal, IsRetreat),
    ///   • threshold — the fleet has lost at least <see cref="CombatEngagement.RetreatCasualtyThreshold"/> of its ships.
    /// Engine-only -> runs in CI.
    /// </summary>
    [TestFixture]
    public class FleetRetreatTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[retreat] " + m);

        private static Entity AddShip(TestScenario s, Entity faction, Entity fleet, double fp, double tough, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name); // build under player faction
            ship.FactionOwnerID = faction.Id;                                            // then assign true owner
            ship.SetDataBlob(new ShipCombatValueDB(fp, tough, 1.0));
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return ship;
        }

        private static Entity MakeFleetWithShip(TestScenario s, Entity faction, double fp, double tough, string name)
        {
            var fleet = FleetFactory.Create(s.StartingSystem, faction.Id, name + " Fleet");
            AddShip(s, faction, fleet, fp, tough, name);
            return fleet;
        }

        [Test]
        [Description("A fleet on a withdraw posture (fighting-withdrawal, IsRetreat) breaks off: it gets a FleetRetreatDB, the engagement ends, and it survives intact.")]
        public void WithdrawPosture_BreaksOff_AndSurvives()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var runner = MakeFleetWithShip(s, s.Faction, 0, 1_000_000, "Runner"); // disengages by posture
            var enemy = MakeFleetWithShip(s, enemyFaction, 50_000, 1_000_000, "Hunter");

            var withdraw = s.Game.StartingGameData.CombatDoctrines["fighting-withdrawal"];
            Assert.That(withdraw.IsRetreat, Is.True, "fighting-withdrawal should be a retreat posture");
            Assert.That(FleetDoctrine.TrySetDoctrine(runner, withdraw, runner.StarSysDateTime), Is.True);

            CombatEngagement.StartEngagement(runner, enemy);
            for (int i = 0; i < 100 && runner.HasDataBlob<FleetCombatStateDB>(); i++)
                CombatEngagement.StepEngagement(runner, enemy, 5);

            Log($"posture: retreated={runner.HasDataBlob<FleetRetreatDB>()} ships={CombatEngagement.GetFleetShips(runner).Count} engaged={runner.HasDataBlob<FleetCombatStateDB>()}");

            Assert.That(runner.HasDataBlob<FleetRetreatDB>(), Is.True, "the withdraw posture should record a retreat");
            Assert.That(runner.HasDataBlob<FleetCombatStateDB>(), Is.False, "retreating ends the engagement");
            Assert.That(CombatEngagement.GetFleetShips(runner).Count, Is.EqualTo(1), "the fleet retreats intact, not wiped");
            Assert.That(runner.GetDataBlob<FleetRetreatDB>().FledFromFleetId, Is.EqualTo(enemy.Id), "it records who it fled from");
        }

        [Test]
        [Description("A fleet that loses half its ships breaks off (casualty threshold) instead of fighting to the last — it retreats with its survivors.")]
        public void CasualtyThreshold_BreaksOff_WithSurvivors()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            // Four fragile, unarmed ships (100 kJ toughness each) vs one strong, near-unkillable gun.
            var militia = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Militia");
            for (int i = 0; i < 4; i++)
                AddShip(s, s.Faction, militia, 0, 100_000, "M" + i);

            var battleship = MakeFleetWithShip(s, enemyFaction, 40_000, 10_000_000, "Battleship"); // 40k/step -> 2 kills/step

            CombatEngagement.StartEngagement(militia, battleship);
            for (int i = 0; i < 100 && militia.HasDataBlob<FleetCombatStateDB>(); i++)
                CombatEngagement.StepEngagement(militia, battleship, 5);

            int survivors = CombatEngagement.GetFleetShips(militia).Count;
            Log($"threshold: retreated={militia.HasDataBlob<FleetRetreatDB>()} survivors={survivors}/4 engaged={militia.HasDataBlob<FleetCombatStateDB>()}");

            Assert.That(militia.HasDataBlob<FleetRetreatDB>(), Is.True, "losing half the fleet should trigger a retreat");
            Assert.That(militia.HasDataBlob<FleetCombatStateDB>(), Is.False, "retreating ends the engagement");
            Assert.That(survivors, Is.GreaterThan(0), "the fleet retreats with survivors, not fought to extinction");
            Assert.That(survivors, Is.LessThan(4), "but only after taking losses past the threshold");
        }
    }
}
