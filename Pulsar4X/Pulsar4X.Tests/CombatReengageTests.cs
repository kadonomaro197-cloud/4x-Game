using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Orbital;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The engage/disengage THRASH fix (2026-06-27). A fleet that LEAVES a fight but stays physically in range was
    /// re-grabbed by the battle trigger every tick — enter, leave, enter, leave. Two causes, both guarded here:
    ///   (1) a STALEMATE — two fleets that can't damage each other (no firepower) entered, froze, and disengaged
    ///       every tick;
    ///   (2) a RETREAT — a fleet that broke off (FleetRetreatDB) but couldn't sail away (v1 records the vector, no
    ///       move order) was pulled back in every tick.
    /// The trigger now skips a no-firepower pair, and skips a fleet that has broken off (its FleetRetreatDB holds
    /// while a threat is in range; it clears once the threat is gone, so the fleet can fight again later).
    /// Engine-only -> runs in CI.
    /// </summary>
    [TestFixture]
    public class CombatReengageTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[reengage] " + m);

        private static void ClearExistingFleets(TestScenario s)
        {
            foreach (var fleet in s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>().ToList())
                fleet.Destroy();
        }

        private static Entity MakeFleet(TestScenario s, Entity faction, string name)
            => FleetFactory.Create(s.StartingSystem, faction.Id, name);

        private static Entity AddShip(TestScenario s, Entity faction, Entity fleet, double firepower, double toughness, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.FactionOwnerID = faction.Id;
            ship.SetDataBlob(new ShipCombatValueDB(firepower, toughness, 1.0)); // firepower 0 => unarmed
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return ship;
        }

        [Test]
        [Description("Two UNARMED hostile fleets in range do NOT engage — a fight neither side can resolve would just thrash (enter, freeze, disengage every tick). The trigger skips a no-firepower pair.")]
        public void Tick_TwoUnarmedFleets_NeverEngage()
        {
            var s = TestScenario.CreateWithColony();
            ClearExistingFleets(s);
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var blues = FactionFactory.CreateBasicFaction(s.Game, "Blues", "BLU", 0);

            var a = MakeFleet(s, reds, "Red Unarmed");
            AddShip(s, reds, a, 0, 1_000_000, "Red 1");      // unarmed
            var b = MakeFleet(s, blues, "Blue Unarmed");
            AddShip(s, blues, b, 0, 1_000_000, "Blue 1");     // unarmed

            for (int i = 0; i < 5; i++) CombatEngagement.Tick(s.StartingSystem, 5);

            Log($"after 5 ticks: a engaged={a.HasDataBlob<FleetCombatStateDB>()} b engaged={b.HasDataBlob<FleetCombatStateDB>()}");
            Assert.That(a.HasDataBlob<FleetCombatStateDB>(), Is.False, "two unarmed fleets must not enter combat (no fight to resolve)");
            Assert.That(b.HasDataBlob<FleetCombatStateDB>(), Is.False);
        }

        [Test]
        [Description("A fleet that has broken off (FleetRetreatDB) is NOT re-grabbed into the fight while the enemy is in range — stops the retreat thrash. Once the threat is gone the flag clears, so it can fight again.")]
        public void Tick_RetreatedFleet_NotReEngaged_ThenClearsWhenThreatGone()
        {
            var s = TestScenario.CreateWithColony();
            ClearExistingFleets(s);
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var blues = FactionFactory.CreateBasicFaction(s.Game, "Blues", "BLU", 0);

            var runner = MakeFleet(s, reds, "Red Runner");
            AddShip(s, reds, runner, 50_000, 1_000_000, "Red 1");        // armed (so firepower isn't the reason it's skipped)
            var enemy = MakeFleet(s, blues, "Blue Hunter");
            var enemyShip = AddShip(s, blues, enemy, 50_000, 1_000_000, "Blue 1"); // armed

            // Simulate that 'runner' already broke off.
            runner.SetDataBlob(new FleetRetreatDB(Vector3.Zero, enemy.Id));

            CombatEngagement.Tick(s.StartingSystem, 5);
            Log($"with enemy present: runner engaged={runner.HasDataBlob<FleetCombatStateDB>()} stillRetreated={runner.HasDataBlob<FleetRetreatDB>()}");
            Assert.That(runner.HasDataBlob<FleetCombatStateDB>(), Is.False, "a broken-off fleet must not be re-grabbed while the enemy is in range");
            Assert.That(runner.HasDataBlob<FleetRetreatDB>(), Is.True, "the retreat flag holds while the threat is present");

            // Remove the threat — destroy the enemy's ship — then tick: the stale retreat flag should clear.
            enemyShip.Destroy();
            CombatEngagement.Tick(s.StartingSystem, 5);
            Log($"threat gone: runner stillRetreated={runner.HasDataBlob<FleetRetreatDB>()}");
            Assert.That(runner.HasDataBlob<FleetRetreatDB>(), Is.False, "with no hostile in range, the stale retreat flag clears so the fleet can fight again");
        }
    }
}
