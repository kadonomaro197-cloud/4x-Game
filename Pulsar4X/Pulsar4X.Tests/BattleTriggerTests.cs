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
    /// Coverage:
    ///  - <see cref="Engagement_StrongerFleet_WipesWeaker"/> drives the engagement directly (Start + Step),
    ///    deterministically, to prove the salvo math, casualties, and end-state (no AdvanceTime timing).
    ///  - <see cref="Trigger_AutoEngages_HostileFleetsInRange"/> advances the clock and lets the auto-discovered
    ///    processor detect + resolve the fight — proving the live-loop hook fires (and doesn't throw).
    ///  - <see cref="SameFactionFleets_DoNotEngage"/> proves friend/foe: same-faction fleets never fight.
    ///
    /// Ships are built under the PLAYER faction (which has the full material/cargo setup needed to build a hull),
    /// then their FactionOwnerID is flipped to the intended owner — combat only reads that int. Each ship is then
    /// stamped with a KNOWN <see cref="ShipCombatValueDB"/>, so outcomes are deterministic. TestScenario builds no
    /// starting fleets (wizard path), so the only fleets present are the ones these tests create.
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
            // Build with the player faction — a bare CreateBasicFaction enemy can't build a hull (its thruster
            // fuel-material lookup is empty -> throws). Combat only reads FactionOwnerID, so flip it after.
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.FactionOwnerID = faction.Id;
            ship.SetDataBlob(new ShipCombatValueDB(firepower, toughness, 1.0));
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return ship;
        }

        [Test]
        [Description("Driven directly: a 3-ship fleet wipes a lone unarmed enemy, takes no losses, and the engagement state clears on both fleets.")]
        public void Engagement_StrongerFleet_WipesWeaker()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var strongFleet = MakeFleet(s, enemyFaction, "Red Fleet");
            AddShip(s, enemyFaction, strongFleet, 50_000, 1_000_000, "Red 1");
            AddShip(s, enemyFaction, strongFleet, 50_000, 1_000_000, "Red 2");
            AddShip(s, enemyFaction, strongFleet, 50_000, 1_000_000, "Red 3");

            var weakFleet = MakeFleet(s, s.Faction, "Blue Fleet");
            var weakShip = AddShip(s, s.Faction, weakFleet, 0, 1_000_000, "Blue 1"); // unarmed -> loses

            // Sanity: assignment actually populated the fleets before we fight.
            Assert.That(CombatEngagement.GetFleetShips(strongFleet).Count, Is.EqualTo(3), "strong fleet should have 3 ships");
            Assert.That(CombatEngagement.GetFleetShips(weakFleet).Count, Is.EqualTo(1), "weak fleet should have 1 ship");

            CombatEngagement.StartEngagement(weakFleet, strongFleet);
            Assert.That(weakFleet.HasDataBlob<FleetCombatStateDB>(), Is.True, "engagement should have started");

            int steps = 0;
            for (; steps < 1000 && weakFleet.HasDataBlob<FleetCombatStateDB>(); steps++)
                CombatEngagement.StepEngagement(weakFleet, strongFleet, 5);

            Log($"resolved in {steps} steps; weak={CombatEngagement.GetFleetShips(weakFleet).Count} strong={CombatEngagement.GetFleetShips(strongFleet).Count}");

            Assert.That(weakFleet.HasDataBlob<FleetCombatStateDB>(), Is.False, "engagement should have ended");
            Assert.That(strongFleet.HasDataBlob<FleetCombatStateDB>(), Is.False, "engagement should have ended on both sides");
            Assert.That(CombatEngagement.GetFleetShips(weakFleet).Count, Is.EqualTo(0), "the weaker fleet should be wiped");
            Assert.That(CombatEngagement.GetFleetShips(strongFleet).Count, Is.EqualTo(3), "the stronger fleet should take no losses from an unarmed enemy");
            Assert.That(weakShip.IsValid, Is.False, "the destroyed ship should be invalid");
        }

        [Test]
        [Description("Live trigger: two hostile fleets in range auto-engage as the clock advances; the unarmed player ship is destroyed and the enemy survives.")]
        public void Trigger_AutoEngages_HostileFleetsInRange()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var enemyFleet = MakeFleet(s, enemyFaction, "Red Fleet");
            AddShip(s, enemyFaction, enemyFleet, 50_000, 1_000_000, "Red 1");
            AddShip(s, enemyFaction, enemyFleet, 50_000, 1_000_000, "Red 2");
            AddShip(s, enemyFaction, enemyFleet, 50_000, 1_000_000, "Red 3");

            var playerFleet = MakeFleet(s, s.Faction, "Blue Fleet");
            var playerShip = AddShip(s, s.Faction, playerFleet, 0, 1_000_000, "Blue 1");

            // Let the auto-discovered BattleTriggerProcessor detect + resolve the fight over game-time.
            s.AdvanceTime(TimeSpan.FromMinutes(10));

            Log($"after advance: playerValid={playerShip.IsValid} enemyShips={CombatEngagement.GetFleetShips(enemyFleet).Count}");

            Assert.That(playerShip.IsValid, Is.False, "the live trigger should have engaged and destroyed the unarmed player ship");
            Assert.That(CombatEngagement.GetFleetShips(enemyFleet).Count, Is.GreaterThan(0), "the armed enemy fleet should survive");
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
