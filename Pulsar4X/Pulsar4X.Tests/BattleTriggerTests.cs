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
    /// These drive <see cref="CombatEngagement"/> directly (no clock advance), which exercises the exact
    /// detection + engagement + resolution logic the hotloop processor runs each tick — deterministically. The
    /// processor itself is a 3-line wrapper that calls <c>CombatEngagement.Tick</c>; the engine arms every hotloop
    /// processor at manager init, and <c>GameLoopSmokeTests</c> proves it runs during a clock advance without
    /// throwing, so live auto-triggering is covered without an AdvanceTime test here.
    ///
    /// Why no AdvanceTime: a bare <c>CreateBasicFaction</c> test enemy can't build a hull, so its ships are built
    /// under the player faction and have FactionOwnerID flipped (combat only reads that int). Those flipped ships
    /// don't survive movement processing across a clock advance — a TEST artifact (real NPC factions are set up
    /// fully), not a combat issue. Driving Tick directly avoids it. Each ship is stamped with a known
    /// <see cref="ShipCombatValueDB"/> so outcomes are deterministic.
    /// </summary>
    [TestFixture]
    public class BattleTriggerTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[battle-trigger] " + m);

        private static Entity MakeFleet(TestScenario s, Entity faction, string name)
            => FleetFactory.Create(s.StartingSystem, faction.Id, name);

        private static Entity AddShip(TestScenario s, Entity faction, Entity fleet, double firepower, double toughness, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name); // build under player faction
            ship.FactionOwnerID = faction.Id;                                            // then assign true owner
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
        [Description("CombatEngagement.Tick detects two hostile fleets in range, engages them, and resolves the fight — the unarmed fleet is wiped.")]
        public void Tick_DetectsEngagesAndResolves_HostileFleetsInRange()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var enemyFleet = MakeFleet(s, enemyFaction, "Red Fleet");
            AddShip(s, enemyFaction, enemyFleet, 50_000, 1_000_000, "Red 1");
            AddShip(s, enemyFaction, enemyFleet, 50_000, 1_000_000, "Red 2");
            AddShip(s, enemyFaction, enemyFleet, 50_000, 1_000_000, "Red 3");

            var playerFleet = MakeFleet(s, s.Faction, "Blue Fleet");
            var playerShip = AddShip(s, s.Faction, playerFleet, 0, 1_000_000, "Blue 1");

            Log($"setup: factions enemy={enemyFaction.Id} player={s.Faction.Id}; fleetOwners enemy={enemyFleet.FactionOwnerID} player={playerFleet.FactionOwnerID}; " +
                $"ships enemy={CombatEngagement.GetFleetShips(enemyFleet).Count} player={CombatEngagement.GetFleetShips(playerFleet).Count}");

            // First tick: detect the hostile pair in range and start an engagement.
            CombatEngagement.Tick(s.StartingSystem, 5);
            Assert.That(playerFleet.HasDataBlob<FleetCombatStateDB>(), Is.True, "the trigger should have engaged the hostile fleets");
            Assert.That(enemyFleet.HasDataBlob<FleetCombatStateDB>(), Is.True);

            // Keep ticking; the fight resolves and wipes the unarmed player fleet.
            int ticks = 1;
            for (; ticks < 1000 && playerShip.IsValid; ticks++)
                CombatEngagement.Tick(s.StartingSystem, 5);

            Log($"resolved in {ticks} ticks; playerValid={playerShip.IsValid} enemyShips={CombatEngagement.GetFleetShips(enemyFleet).Count}");

            Assert.That(playerShip.IsValid, Is.False, "the trigger should resolve the fight and destroy the unarmed player ship");
            Assert.That(CombatEngagement.GetFleetShips(enemyFleet).Count, Is.EqualTo(3), "the armed enemy fleet should take no losses");
            Assert.That(enemyFleet.HasDataBlob<FleetCombatStateDB>(), Is.False, "the engagement should have ended");
        }

        [Test]
        [Description("Two fleets of the SAME faction never engage when the trigger runs.")]
        public void Tick_SameFactionFleets_DoNotEngage()
        {
            var s = TestScenario.CreateWithColony();

            var fleet1 = MakeFleet(s, s.Faction, "Home Guard A");
            AddShip(s, s.Faction, fleet1, 50_000, 1_000_000, "A1");
            var fleet2 = MakeFleet(s, s.Faction, "Home Guard B");
            AddShip(s, s.Faction, fleet2, 50_000, 1_000_000, "B1");

            for (int i = 0; i < 5; i++)
                CombatEngagement.Tick(s.StartingSystem, 5);

            Assert.That(fleet1.HasDataBlob<FleetCombatStateDB>(), Is.False, "friendly fleets must not engage");
            Assert.That(fleet2.HasDataBlob<FleetCombatStateDB>(), Is.False, "friendly fleets must not engage");
            Assert.That(CombatEngagement.GetFleetShips(fleet1).Count, Is.EqualTo(1), "no friendly losses");
            Assert.That(CombatEngagement.GetFleetShips(fleet2).Count, Is.EqualTo(1), "no friendly losses");
        }
    }
}
