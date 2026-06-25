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
    /// MVP combat spine, step 6 — fleet components & per-component doctrine (docs/COMBAT-DESIGN.md System 4,
    /// detailed design). A "component" is just a sub-fleet (<see cref="FleetDB"/> nests via TreeHierarchyDB), and
    /// each component can run its OWN doctrine — so a fleet's Front Line can fight offensively while its Rear
    /// Guard sits defensive, all in one engagement.
    ///
    /// These prove the new <see cref="CombatEngagement.GetCombatShips"/> tags every ship with the multipliers of
    /// the component it sits in (not the whole fleet's), and that the per-component multiplier actually changes a
    /// battle's outcome. Engine-only -> runs in CI. (Step 5's <c>FleetDoctrineTests</c> covers WHOLE-fleet
    /// doctrine; this is the per-component layer on top.)
    /// </summary>
    [TestFixture]
    public class FleetComponentTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[fleet-component] " + m);

        private static Entity MakeFleet(TestScenario s, Entity faction, string name)
            => FleetFactory.Create(s.StartingSystem, faction.Id, name);

        /// <summary>Make a sub-fleet (component) and nest it under a parent fleet via the real ChangeParent order.</summary>
        private static Entity MakeComponent(TestScenario s, Entity faction, Entity parentFleet, string name)
        {
            var component = FleetFactory.Create(s.StartingSystem, faction.Id, name);
            s.Game.OrderHandler.HandleOrder(FleetOrder.ChangeParent(faction.Id, component, parentFleet));
            return component;
        }

        private static Entity AddShip(TestScenario s, Entity faction, Entity fleet, double fp, double tough, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name); // build under player faction
            ship.FactionOwnerID = faction.Id;                                            // then assign true owner
            ship.SetDataBlob(new ShipCombatValueDB(fp, tough, 1.0));
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return ship;
        }

        private static CombatDoctrineBlueprint Offensive(double firepowerMult)
            => new CombatDoctrineBlueprint
            {
                UniqueID = "test-offensive", DisplayName = "Test Offensive", Family = "Offensive",
                FirepowerMult = firepowerMult, ToughnessMult = 1.0, CooldownSeconds = 0
            };

        [Test]
        [Description("A ship in an offensive sub-component fights with that component's multiplier; a ship directly in the parent fleet uses the parent's — doctrine is per-component, not whole-fleet.")]
        public void Doctrine_IsAppliedPerComponent_NotWholeFleet()
        {
            var s = TestScenario.CreateWithColony();

            var taskForce = MakeFleet(s, s.Faction, "Task Force");
            var strikeWing = MakeComponent(s, s.Faction, taskForce, "Strike Wing");

            var strikeShip = AddShip(s, s.Faction, strikeWing, 10_000, 1_000_000, "Strike 1"); // in the component
            var supportShip = AddShip(s, s.Faction, taskForce, 10_000, 1_000_000, "Support 1"); // directly in the fleet

            // Only the Strike Wing component adopts the aggressive posture.
            Assert.That(FleetDoctrine.TrySetDoctrine(strikeWing, Offensive(2.0), strikeWing.StarSysDateTime), Is.True);

            var combatShips = CombatEngagement.GetCombatShips(taskForce);
            Log($"combatShips={combatShips.Count}; " +
                string.Join(", ", combatShips.Select(c => $"{c.Ship.Id}:fp×{c.FirepowerMult}")));

            Assert.That(combatShips.Count, Is.EqualTo(2), "both the component ship and the loose fleet ship should be collected");
            var strike = combatShips.First(c => c.Ship.Id == strikeShip.Id);
            var support = combatShips.First(c => c.Ship.Id == supportShip.Id);
            Assert.That(strike.FirepowerMult, Is.EqualTo(2.0), "the Strike Wing ship fights at its component's x2 posture");
            Assert.That(support.FirepowerMult, Is.EqualTo(1.0), "the ship directly in the fleet keeps the fleet's neutral posture");
        }

        [Test]
        [Description("A component's offensive doctrine flips a battle the fleet would otherwise lose — the per-component multiplier flows all the way through to who wins.")]
        public void ComponentDoctrine_ChangesCombatOutcome()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            // Attacker's gun is WEAKER than the defender's (6k vs 10k) — on raw firepower it loses.
            var attacker = MakeFleet(s, s.Faction, "Attacker");
            var strikeWing = MakeComponent(s, s.Faction, attacker, "Strike Wing");
            AddShip(s, s.Faction, strikeWing, 6_000, 1_000_000, "Strike 1");

            var defender = MakeFleet(s, enemyFaction, "Defender");
            AddShip(s, enemyFaction, defender, 10_000, 1_000_000, "Def 1");

            // The Strike Wing component goes all-out (x2): 6k -> 12k effective, now ABOVE the defender's 10k.
            Assert.That(FleetDoctrine.TrySetDoctrine(strikeWing, Offensive(2.0), strikeWing.StarSysDateTime), Is.True);

            CombatEngagement.StartEngagement(attacker, defender);
            for (int i = 0; i < 2000 && attacker.HasDataBlob<FleetCombatStateDB>(); i++)
                CombatEngagement.StepEngagement(attacker, defender, 5);

            Log($"outcome: attacker={CombatEngagement.GetFleetShips(attacker).Count} defender={CombatEngagement.GetFleetShips(defender).Count}");

            Assert.That(CombatEngagement.GetFleetShips(defender).Count, Is.EqualTo(0),
                "the component's x2 firepower should win a fight the raw 6k-vs-10k hull would have lost");
            Assert.That(CombatEngagement.GetFleetShips(attacker).Count, Is.GreaterThan(0),
                "and the attacker should survive");
        }
    }
}
