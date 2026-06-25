using System;
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
    /// MVP combat spine, step 5 — switchable doctrine (<see cref="FleetDoctrineDB"/> + the moddable
    /// <see cref="CombatDoctrineBlueprint"/> catalog).
    ///
    /// Proves: the catalog loads from JSON (combatDoctrines.json) into the game's data store; setting a fleet's
    /// posture applies its multipliers and honours the switch cooldown; and a doctrine's firepower multiplier
    /// actually changes who wins a battle. Engine-only -> runs in CI.
    /// </summary>
    [TestFixture]
    public class FleetDoctrineTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[doctrine] " + m);

        private static CombatDoctrineBlueprint Doctrine(string id, double fp, double tough, double cooldown)
            => new CombatDoctrineBlueprint
            {
                UniqueID = id, DisplayName = id, Family = "Test",
                FirepowerMult = fp, ToughnessMult = tough, CooldownSeconds = cooldown
            };

        private static Entity MakeFleetWithShip(TestScenario s, Entity faction, double fp, double tough, string name)
        {
            var fleet = FleetFactory.Create(s.StartingSystem, faction.Id, name + " Fleet");
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name); // build under player faction
            ship.FactionOwnerID = faction.Id;                                            // then assign true owner
            ship.SetDataBlob(new ShipCombatValueDB(fp, tough, 1.0));
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return fleet;
        }

        [Test]
        [Description("The moddable combat-doctrine catalog (combatDoctrines.json) loads into the game's data store.")]
        public void BaseMod_LoadsCombatDoctrineCatalog()
        {
            var s = TestScenario.CreateWithColony();
            var catalog = s.Game.StartingGameData.CombatDoctrines;
            Log($"loaded {catalog.Count} combat doctrines: {string.Join(", ", catalog.Keys)}");

            Assert.That(catalog, Is.Not.Empty, "base mod should define combat doctrines");
            Assert.That(catalog.ContainsKey("all-out-attack"), Is.True, "the Offensive posture should be loaded");
            Assert.That(catalog.ContainsKey("fighting-withdrawal"), Is.True, "the Withdraw posture should be loaded");
            Assert.That(catalog["fighting-withdrawal"].IsRetreat, Is.True, "the withdraw posture is a retreat");
        }

        [Test]
        [Description("Setting a fleet's doctrine applies its multipliers; the switch cooldown blocks an immediate change.")]
        public void TrySetDoctrine_AppliesMultipliers_AndHonoursCooldown()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Fleet");
            var now = fleet.StarSysDateTime;

            Assert.That(FleetDoctrine.TrySetDoctrine(fleet, Doctrine("aggro", 1.5, 0.8, 300), now), Is.True);
            Assert.That(FleetDoctrine.FirepowerMult(fleet), Is.EqualTo(1.5));
            Assert.That(FleetDoctrine.ToughnessMult(fleet), Is.EqualTo(0.8));

            // Still on cooldown 10s later -> switch refused, posture unchanged.
            Assert.That(FleetDoctrine.TrySetDoctrine(fleet, Doctrine("def", 0.8, 1.5, 300), now + TimeSpan.FromSeconds(10)), Is.False);
            Assert.That(FleetDoctrine.FirepowerMult(fleet), Is.EqualTo(1.5), "posture must not change while on cooldown");

            // After the cooldown -> switch succeeds.
            Assert.That(FleetDoctrine.TrySetDoctrine(fleet, Doctrine("def", 0.8, 1.5, 300), now + TimeSpan.FromSeconds(301)), Is.True);
            Assert.That(FleetDoctrine.FirepowerMult(fleet), Is.EqualTo(0.8));
        }

        [Test]
        [Description("A fleet on an aggressive (firepower x2) doctrine beats the identical enemy fleet that has none.")]
        public void OffensiveDoctrine_ChangesWhoWins()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var playerFleet = MakeFleetWithShip(s, s.Faction, 10_000, 1_000_000, "Blue");
            var enemyFleet = MakeFleetWithShip(s, enemyFaction, 10_000, 1_000_000, "Red"); // identical base stats

            // Player adopts an aggressive posture: x2 firepower, same hull otherwise.
            Assert.That(FleetDoctrine.TrySetDoctrine(playerFleet, Doctrine("aggro", 2.0, 1.0, 0), playerFleet.StarSysDateTime), Is.True);

            CombatEngagement.StartEngagement(playerFleet, enemyFleet);
            for (int i = 0; i < 2000 && playerFleet.HasDataBlob<FleetCombatStateDB>(); i++)
                CombatEngagement.StepEngagement(playerFleet, enemyFleet, 5);

            Log($"doctrine battle: player={CombatEngagement.GetFleetShips(playerFleet).Count} enemy={CombatEngagement.GetFleetShips(enemyFleet).Count}");
            Assert.That(CombatEngagement.GetFleetShips(enemyFleet).Count, Is.EqualTo(0), "the doctrine-boosted fleet should win");
            Assert.That(CombatEngagement.GetFleetShips(playerFleet).Count, Is.GreaterThan(0), "and survive (it out-damaged an identical hull)");
        }
    }
}
