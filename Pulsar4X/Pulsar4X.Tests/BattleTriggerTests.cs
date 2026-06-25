using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// MVP combat spine, step 4 — the in-game battle trigger (<see cref="CombatEngagement"/> +
    /// <see cref="BattleTriggerProcessor"/>).
    ///
    /// Two HOSTILE fleets that come within range auto-engage and fight to a finish over game-time; SAME-faction
    /// fleets never engage. These are integration tests: they advance the clock and let the auto-discovered
    /// BattleTriggerProcessor run, so they also prove the live-loop hook doesn't throw. Engine-only -> runs in CI.
    ///
    /// Ships are built real (so they are valid, positioned entities the engine can destroy) and then stamped with
    /// KNOWN combat values, so the outcome is deterministic regardless of what the starting designs rate.
    /// </summary>
    [TestFixture]
    public class BattleTriggerTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[battle-trigger] " + m);

        private static Entity MakeFleet(TestScenario s, Entity faction, string name)
        {
            return FleetFactory.Create(s.StartingSystem, faction.Id, name);
        }

        private static Entity AddShip(TestScenario s, Entity faction, Entity fleet, double firepower, double toughness, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, faction, s.StartingBody, name);
            ship.SetDataBlob(new ShipCombatValueDB(firepower, toughness, 1.0));
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return ship;
        }

        [Test]
        [Description("Two hostile fleets within range auto-engage; the stronger wipes the weaker and the engagement ends (state cleared on both fleets).")]
        public void HostileFleetsInRange_AutoEngage_StrongerWins()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var enemyFleet = MakeFleet(s, enemyFaction, "Red Fleet");
            AddShip(s, enemyFaction, enemyFleet, 50_000, 1_000_000, "Red 1");
            AddShip(s, enemyFaction, enemyFleet, 50_000, 1_000_000, "Red 2");
            AddShip(s, enemyFaction, enemyFleet, 50_000, 1_000_000, "Red 3");

            var playerFleet = MakeFleet(s, s.Faction, "Blue Fleet");
            var playerShip = AddShip(s, s.Faction, playerFleet, 0, 1_000_000, "Blue 1"); // unarmed -> loses

            // Let the auto-discovered BattleTriggerProcessor detect + resolve the fight over game-time.
            s.AdvanceTime(TimeSpan.FromMinutes(5));

            int enemyShips = CombatEngagement.GetFleetShips(enemyFleet).Count;
            int playerShips = CombatEngagement.GetFleetShips(playerFleet).Count;
            Log($"after battle: enemyShips={enemyShips} playerShips={playerShips} " +
                $"enemyEngaged={enemyFleet.HasDataBlob<FleetCombatStateDB>()} playerEngaged={playerFleet.HasDataBlob<FleetCombatStateDB>()}");

            Assert.That(playerShips, Is.EqualTo(0), "the unarmed fleet should have been destroyed");
            Assert.That(enemyShips, Is.EqualTo(3), "the armed fleet should lose nobody to an unarmed enemy");
            Assert.That(enemyFleet.HasDataBlob<FleetCombatStateDB>(), Is.False, "engagement should have ended (state cleared)");
            Assert.That(playerFleet.HasDataBlob<FleetCombatStateDB>(), Is.False, "engagement should have ended (state cleared)");
            Assert.That(playerShip.IsValid, Is.False, "the destroyed ship should be invalid");
        }

        [Test]
        [Description("Two fleets of the SAME faction never engage each other.")]
        public void SameFactionFleets_DoNotEngage()
        {
            var s = TestScenario.CreateWithColony();

            var fleet1 = MakeFleet(s, s.Faction, "Home Guard A");
            AddShip(s, s.Faction, fleet1, 50_000, 1_000_000, "A1");
            var fleet2 = MakeFleet(s, s.Faction, "Home Guard B");
            AddShip(s, s.Faction, fleet2, 50_000, 1_000_000, "B1");

            s.AdvanceTime(TimeSpan.FromMinutes(2));

            Assert.That(fleet1.HasDataBlob<FleetCombatStateDB>(), Is.False, "friendly fleets must not engage");
            Assert.That(fleet2.HasDataBlob<FleetCombatStateDB>(), Is.False, "friendly fleets must not engage");
            Assert.That(CombatEngagement.GetFleetShips(fleet1).Count, Is.EqualTo(1), "no friendly losses");
            Assert.That(CombatEngagement.GetFleetShips(fleet2).Count, Is.EqualTo(1), "no friendly losses");
        }
    }
}
