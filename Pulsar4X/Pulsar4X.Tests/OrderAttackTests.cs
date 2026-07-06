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
    /// Gauges <see cref="CombatEngagement.OrderAttack"/> — the player's explicit "this fleet attacks that one" order.
    /// The auto-trigger leaves two fleets "staring at each other" when one holds fire or the enemy has broken off;
    /// OrderAttack is the deliberate override: it re-commits the attacker (clears its retreat), flips it Weapons Free,
    /// and forces both into combat now. Engine-only -> CI (the Fleet-window button is a thin call on top, CI-blind).
    /// </summary>
    [TestFixture]
    public class OrderAttackTests
    {
        private static Entity AddShip(TestScenario s, Entity faction, Entity fleet, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name); // build under player faction
            ship.FactionOwnerID = faction.Id;                                            // then assign true owner
            ship.SetDataBlob(new ShipCombatValueDB(40_000, 100_000, 1.0)                 // a plain armed value
            { Weapons = { new WeaponProfile(40_000, 50_000, 0.05, 5) } });
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return ship;
        }

        [Test]
        [Description("OrderAttack forces a fleet to engage a hostile it was NOT fighting — even when the attacker is " +
                     "holding fire AND had retreated (the 'staring at each other' standoff). It clears the attacker's " +
                     "retreat, flips it Weapons Free, and puts both fleets in combat.")]
        public void OrderAttack_ForcesEngagement_BypassingHoldAndRetreat()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var mine = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Striker");
            AddShip(s, s.Faction, mine, "Striker-1");
            var enemy = FleetFactory.Create(s.StartingSystem, reds.Id, "Prey");
            AddShip(s, reds, enemy, "Prey-1");

            // The standoff: my fleet holds fire AND had broken off a previous fight — so the auto-trigger won't grab it.
            FleetDoctrine.SetEngagementPosture(mine, EngagementPosture.WeaponsHold);
            mine.SetDataBlob(new FleetRetreatDB());
            Assert.That(mine.HasDataBlob<FleetCombatStateDB>(), Is.False, "no fight yet — they're just staring");

            CombatEngagement.OrderAttack(mine, enemy);

            Assert.That(mine.HasDataBlob<FleetCombatStateDB>(), Is.True, "the attack order forces MY fleet into combat");
            Assert.That(enemy.HasDataBlob<FleetCombatStateDB>(), Is.True, "...and the target too");
            Assert.That(FleetDoctrine.PostureOf(mine), Is.EqualTo(EngagementPosture.WeaponsFree),
                "attacking flips me to Weapons Free — or I'd keep holding fire");
            Assert.That(mine.HasDataBlob<FleetRetreatDB>(), Is.False,
                "attacking clears my retreat — I'm re-committing to the fight");
        }

        [Test]
        [Description("Fog of war: OrderAttack can't conjure a target out of the dark. With detection-gated combat on " +
                     "and no sensor contact, ordering an attack on an undetected hostile no-ops; with fog off it engages.")]
        public void OrderAttack_RespectsFog_NoOpOnUndetectedEnemy()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var mine = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Striker");
            AddShip(s, s.Faction, mine, "Striker-1");
            var enemy = FleetFactory.Create(s.StartingSystem, reds.Id, "Prey");
            AddShip(s, reds, enemy, "Prey-1");

            bool prev = CombatEngagement.RequireDetectionToEngage;
            CombatEngagement.RequireDetectionToEngage = true;   // fog on; no scan has run -> nobody is detected
            try
            {
                CombatEngagement.OrderAttack(mine, enemy);
                Assert.That(mine.HasDataBlob<FleetCombatStateDB>(), Is.False,
                    "can't attack an undetected enemy with fog on");

                CombatEngagement.RequireDetectionToEngage = false; // fog off -> you can always engage
                CombatEngagement.OrderAttack(mine, enemy);
                Assert.That(mine.HasDataBlob<FleetCombatStateDB>(), Is.True, "fog off -> the attack lands");
            }
            finally { CombatEngagement.RequireDetectionToEngage = prev; }   // never leak the static
        }

        [Test]
        [Description("OrderAttack no-ops on a friendly target — you can't order a fleet to attack your own.")]
        public void OrderAttack_OnAFriendly_DoesNothing()
        {
            var s = TestScenario.CreateWithColony();
            var mine = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Striker");
            AddShip(s, s.Faction, mine, "Striker-1");
            var friend = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Friend");
            AddShip(s, s.Faction, friend, "Friend-1");

            CombatEngagement.OrderAttack(mine, friend);

            Assert.That(mine.HasDataBlob<FleetCombatStateDB>(), Is.False, "no engaging your own faction");
            Assert.That(friend.HasDataBlob<FleetCombatStateDB>(), Is.False);
        }

        [Test]
        [Description("The Fleet-window 'Engage' helper finds the nearest hostile fleet and attacks it — the one-click " +
                     "answer to a standoff. Returns the engaged target; null when no hostile fleet is present.")]
        public void OrderAttackNearestHostile_EngagesAHostile_NullWhenNone()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var mine = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Striker");
            AddShip(s, s.Faction, mine, "Striker-1");

            // No hostile present yet -> nothing to attack.
            Assert.That(CombatEngagement.OrderAttackNearestHostile(mine), Is.Null, "no hostile fleet -> null, no engagement");
            Assert.That(mine.HasDataBlob<FleetCombatStateDB>(), Is.False);

            // Stand up a hostile, then engage the nearest.
            var enemy = FleetFactory.Create(s.StartingSystem, reds.Id, "Prey");
            AddShip(s, reds, enemy, "Prey-1");

            var target = CombatEngagement.OrderAttackNearestHostile(mine);
            Assert.That(target, Is.EqualTo(enemy), "engaged the (only) hostile fleet");
            Assert.That(mine.HasDataBlob<FleetCombatStateDB>(), Is.True, "and we're now in combat");
            Assert.That(enemy.HasDataBlob<FleetCombatStateDB>(), Is.True);
        }
    }
}
