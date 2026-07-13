using System.Linq;
using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.GroundCombat;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-3c — the player Load/Land orders (docs/SITE-ENGINE-DESIGN.md §6). These wrap the engine-only
    /// GroundTransport helpers in real EntityCommands so a player (and, later, an NPC) can lift troops onto a transport
    /// and land them on a world's region — the way troops actually reach a surface site (SE-3b) or an invasion.
    /// Drives the orders' Execute directly (deterministic); the underlying transport mechanics are covered by
    /// GroundTransportTests, so this proves the ORDER wiring end to end.
    /// </summary>
    [TestFixture]
    public class TroopOrderTests
    {
        private const string TroopBay = "default-design-troop-bay";

        private static GroundUnitDesign InfantryDesign() => new GroundUnitDesign
        {
            UniqueID = "se3c-infantry", Name = "1st Rifles", UnitType = GroundUnitType.Infantry,
            Attack = 100, Defense = 10, HitPoints = 500,
        };

        private static Entity TransportAt(TestScenario s, Entity body)
        {
            var shipDesign = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(shipDesign, s.Faction, body, "Trooper");
            var bayDesign = (ComponentDesign)s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns[TroopBay];
            ship.AddComponent(new ComponentInstance(bayDesign));
            return ship;
        }

        [Test]
        [Description("SE-3c: the LoadTroopsOrder loads a ground unit onto a transport, and the LandTroopsOrder puts it back down on a target region — the order path round-trip.")]
        public void LoadThenLandOrders_MoveTheUnit_OffAndOnThePlanet()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var when = s.StartingSystem.StarSysDateTime;

            var unit = GroundForces.RaiseUnit(body, InfantryDesign(), s.Faction.Id, regionIndex: 0, name: "1st Rifles");
            var forces = body.GetDataBlob<GroundForcesDB>();
            var ship = TransportAt(s, body);

            // LOAD via the order
            var load = LoadTroopsOrder.CreateCommand(ship, body, unit.UnitId);
            Assert.That(load.IsValidCommand(s.Game), Is.True, "issued to a ship → a valid command");
            load.Execute(when);

            Assert.That(load.IsFinished(), Is.True, "the load order completed");
            Assert.That(forces.Units, Does.Not.Contain(unit), "the unit left the planet roster");
            Assert.That(ship.GetDataBlob<GroundTransportDB>().LoadedUnits, Does.Contain(unit), "it is aboard the ship");

            // LAND via the order onto region 1 (orbit uncontested — only our ship present)
            var land = LandTroopsOrder.CreateCommand(ship, body, unit.UnitId, regionIndex: 1);
            land.Execute(when);

            Assert.That(land.IsFinished(), Is.True, "the land order completed");
            Assert.That(ship.GetDataBlob<GroundTransportDB>().LoadedUnits, Does.Not.Contain(unit), "it left the ship");
            Assert.That(forces.Units, Does.Contain(unit), "it is back on the ground");
            Assert.That(unit.RegionIndex, Is.EqualTo(1), "at the region it was landed on");
            Assert.That(unit.Health, Is.EqualTo(500), "with its health intact across the trip");
        }

        [Test]
        [Description("SE-3c: a Load order naming a unit that isn't there is a safe no-op (never finishes, nothing moves).")]
        public void LoadOrder_UnknownUnit_IsSafeNoOp()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            GroundForces.RaiseUnit(body, InfantryDesign(), s.Faction.Id, regionIndex: 0);
            var ship = TransportAt(s, body);

            var load = LoadTroopsOrder.CreateCommand(ship, body, unitId: 999999);
            load.Execute(s.StartingSystem.StarSysDateTime);

            Assert.That(load.IsFinished(), Is.False, "no such unit → the order safely does nothing");
            Assert.That(!ship.HasDataBlob<GroundTransportDB>()
                        || ship.GetDataBlob<GroundTransportDB>().LoadedUnits.Count == 0, Is.True,
                "nothing was loaded");
        }
    }
}
