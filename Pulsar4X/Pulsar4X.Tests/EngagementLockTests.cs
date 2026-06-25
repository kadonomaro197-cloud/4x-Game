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
    /// MVP combat spine, step 11 — the engagement lock. Once a fleet is in a battle (it carries a
    /// <see cref="FleetCombatStateDB"/>), the order handler REFUSES its regular orders; only doctrine changes —
    /// a direct <see cref="FleetDoctrine"/> call, not an order — still apply. When the engagement ends, orders
    /// work again. This is what makes "you set the fight up, then you can only steer it with doctrine" real.
    /// Engine-only -> runs in CI.
    /// </summary>
    [TestFixture]
    public class EngagementLockTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[engagement-lock] " + m);

        private static CombatDoctrineBlueprint Doctrine(string id, double fp)
            => new CombatDoctrineBlueprint
            {
                UniqueID = id, DisplayName = id, Family = "Test", FirepowerMult = fp, ToughnessMult = 1.0, CooldownSeconds = 0
            };

        private static Entity BuildShip(TestScenario s, Entity faction, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name); // build under player faction
            ship.FactionOwnerID = faction.Id;                                            // then assign true owner
            ship.SetDataBlob(new ShipCombatValueDB(1000, 1_000_000, 1.0));
            return ship;
        }

        [Test]
        [Description("A fleet locked in an engagement refuses regular orders (ship assignment), but a doctrine change still applies; once the engagement ends, the order goes through.")]
        public void EngagedFleet_RefusesOrders_ButDoctrineStillApplies()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Locked Fleet");

            // First ship assigns normally — the fleet is not engaged yet.
            var ship1 = BuildShip(s, s.Faction, "S1");
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, fleet, ship1));
            Assert.That(CombatEngagement.GetFleetShips(fleet).Count, Is.EqualTo(1), "first ship assigns before the engagement");

            // Lock the fleet into a battle.
            fleet.SetDataBlob(new FleetCombatStateDB(-1));

            // A regular order (assign another ship) is REFUSED while engaged...
            var ship2 = BuildShip(s, s.Faction, "S2");
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, fleet, ship2));
            Assert.That(CombatEngagement.GetFleetShips(fleet).Count, Is.EqualTo(1), "regular orders are locked out during an engagement");

            // ...but a doctrine change still applies (it's a direct call, not an order).
            Assert.That(FleetDoctrine.TrySetDoctrine(fleet, Doctrine("aggro", 2.0), fleet.StarSysDateTime), Is.True);
            Assert.That(FleetDoctrine.FirepowerMult(fleet), Is.EqualTo(2.0), "doctrine changes apply during combat");

            // End the engagement; the SAME order now goes through.
            fleet.RemoveDataBlob<FleetCombatStateDB>();
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, fleet, ship2));
            Log($"after-unlock ships={CombatEngagement.GetFleetShips(fleet).Count}");
            Assert.That(CombatEngagement.GetFleetShips(fleet).Count, Is.EqualTo(2), "orders work again once the engagement ends");
        }
    }
}
