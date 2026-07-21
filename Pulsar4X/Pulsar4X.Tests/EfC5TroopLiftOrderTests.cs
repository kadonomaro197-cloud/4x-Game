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
    /// Operation Earthfall C5.1 — the embark/land CLIENT CONTRACT gauge. The FleetWindow "Embark / land troops" order
    /// (the previously-missing invasion control panel, MVP Stage 4) draws its Load/Land buttons against a fixed slice of
    /// the engine: the exact <see cref="LoadTroopsOrder.CreateCommand"/>/<see cref="LandTroopsOrder.CreateCommand"/>
    /// overload ARITIES, the per-class <see cref="GroundTransport.BayCapacity"/> / <see cref="GroundTransport.CanLoad"/>
    /// readout the "bay capacity vs unit size" UI shows, and the RegionIndex the land-region picker rides on the order.
    /// Since CI can't run the SDL client, this pins that contract so an engine change that would silently break the
    /// buttons reds CI instead (the same "pin the client's engine contract" role EfC3BattalionRegistryTests plays for
    /// the Battalions tab). The round-trip mechanics themselves are covered by GroundTransportTests/TroopOrderTests;
    /// this asserts the specific surface the C5.1 buttons call, so it stands as its own regression tripwire.
    /// Engine-only → runs in CI. The client change is purely additive (a new order type + draw methods), engine
    /// byte-identical (no engine file touched).
    /// </summary>
    [TestFixture]
    public class EfC5TroopLiftOrderTests
    {
        private const string TroopBay = "default-design-troop-bay";
        private static void Log(string m) => TestContext.Progress.WriteLine("[c5.1] " + m);

        private static GroundUnitDesign InfantryDesign() => new GroundUnitDesign
        {
            UniqueID = "c5-infantry", Name = "1st Rifles", UnitType = GroundUnitType.Infantry,
            Attack = 100, Defense = 10, HitPoints = 500,
        };

        private static GroundUnitDesign ArmorDesign() => new GroundUnitDesign
        {
            UniqueID = "c5-armor", Name = "1st Armor", UnitType = GroundUnitType.Armor,
            Attack = 140, Defense = 15, HitPoints = 700,
        };

        // A ship orbiting the body with a (Personnel-class) troop bay installed — what the C5.1 panel enumerates.
        private static Entity TroopShipAt(TestScenario s, Entity body)
        {
            var shipDesign = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(shipDesign, s.Faction, body, "Trooper");
            var bayDesign = (ComponentDesign)s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns[TroopBay];
            ship.AddComponent(new ComponentInstance(bayDesign));
            return ship;
        }

        [Test]
        [Description("C5.1 EMBARK button contract: the bay-capacity-vs-unit-size readout the panel shows, and the 3-arg LoadTroopsOrder.CreateCommand(ship, body, unitId) the Load button issues — carries the right ids and, on execute, lifts the unit off the roster.")]
        public void EmbarkButton_ReadoutAndOrder_LoadTheUnit()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var when = s.StartingSystem.StarSysDateTime;

            var unit = GroundForces.RaiseUnit(body, InfantryDesign(), s.Faction.Id, regionIndex: 0, name: "1st Rifles");
            var forces = body.GetDataBlob<GroundForcesDB>();
            var ship = TroopShipAt(s, body);

            // ── the readout the "Troop bay (infantry): used/cap" row + the Load button's enable-state draw from ──
            double pCap = GroundTransport.BayCapacity(ship, GroundCarryClass.Personnel);
            double vCap = GroundTransport.BayCapacity(ship, GroundCarryClass.Vehicle);
            Log($"bay: personnel={pCap:0} vehicle={vCap:0}; infantry carry-size={GroundTransport.CarrySizeOf(unit):0} class={GroundTransport.CarryClassOf(unit.UnitType)}");
            Assert.That(pCap, Is.GreaterThan(0), "the troop bay gives personnel room (the panel shows the Troop-bay row)");
            Assert.That(GroundTransport.CarryClassOf(unit.UnitType), Is.EqualTo(GroundCarryClass.Personnel),
                "infantry is a Personnel-class unit → it fits the troop bay");
            Assert.That(GroundTransport.CanLoad(ship, unit), Is.True, "there's room → the Load button is enabled");
            Assert.That(GroundTransport.FreeCapacity(ship, GroundCarryClass.Personnel), Is.EqualTo(pCap),
                "free == capacity before anything is loaded (the 'N free' readout)");

            // ── the order the Load button issues — the 3-arg overload, carrying exactly the ids the panel passes ──
            var load = LoadTroopsOrder.CreateCommand(ship, body, unit.UnitId);
            Assert.That(load.BodyEntityId, Is.EqualTo(body.Id), "the order names the body the unit stands on");
            Assert.That(load.UnitId, Is.EqualTo(unit.UnitId), "the order names the chosen unit");
            Assert.That(load.IsValidCommand(s.Game), Is.True, "issued to a ship → a valid command (HandleOrder accepts it)");

            load.Execute(when);
            Assert.That(load.IsFinished(), Is.True, "the load completed");
            Assert.That(forces.Units, Does.Not.Contain(unit), "the unit left the ground roster");
            Assert.That(ship.GetDataBlob<GroundTransportDB>().LoadedUnits, Does.Contain(unit), "it is aboard the ship");
            // the readout after loading: one troop-slot used
            Assert.That(GroundTransport.UsedCapacity(ship, GroundCarryClass.Personnel),
                Is.EqualTo(GroundTransport.CarrySizeOf(unit)), "used == the loaded unit's carry-size");
        }

        [Test]
        [Description("C5.1 LAND button + region-picker contract: the 4-arg LandTroopsOrder.CreateCommand(ship, targetBody, unitId, regionIndex) carries the PICKED region (not a default), the orbital-control gate matches the button's disabled-state, and on execute the unit lands in exactly the picked region.")]
        public void LandButton_RegionPicker_CarriesTheChosenRegion()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var when = s.StartingSystem.StarSysDateTime;

            var unit = GroundForces.RaiseUnit(body, InfantryDesign(), s.Faction.Id, regionIndex: 0, name: "1st Rifles");
            var ship = TroopShipAt(s, body);
            Assert.That(GroundTransport.TryLoadUnit(ship, body, unit), Is.True, "loaded aboard for the drop");

            // the picker's region value rides on the order verbatim — pin the 4-arg mapping with a NON-default index (2)
            var probe = LandTroopsOrder.CreateCommand(ship, body, unit.UnitId, regionIndex: 2);
            Assert.That(probe.TargetBodyEntityId, Is.EqualTo(body.Id), "the order names the target body (the world the ship is over)");
            Assert.That(probe.UnitId, Is.EqualTo(unit.UnitId), "the order names the loaded unit");
            Assert.That(probe.RegionIndex, Is.EqualTo(2), "the picked region is carried on the order (the region-picker wire)");

            // the button's enabled-state is the orbital-control gate (only our ship present → we hold the orbit)
            Assert.That(GroundTransport.HasOrbitalControl(ship, body), Is.True, "orbit uncontested → the Land button is enabled");

            // execute the drop into the picked region and confirm the unit lands there
            var land = LandTroopsOrder.CreateCommand(ship, body, unit.UnitId, regionIndex: 2);
            land.Execute(when);
            Assert.That(land.IsFinished(), Is.True, "the land completed");
            Assert.That(ship.GetDataBlob<GroundTransportDB>().LoadedUnits, Does.Not.Contain(unit), "it left the ship");
            var forces = body.GetDataBlob<GroundForcesDB>();
            Assert.That(forces.Units, Does.Contain(unit), "it's back on the ground");
            Assert.That(unit.RegionIndex, Is.EqualTo(2), "in the region the picker chose");
            Log($"landed '{unit.Name}' in region {unit.RegionIndex}");
        }

        [Test]
        [Description("C5.1 per-class gating: a troop-bay-only ship reports zero Vehicle room, so CanLoad greys the Load button for a tank (Vehicle class) but enables it for infantry (Personnel class) — the bay-only-carries-its-own-class rule the readout draws.")]
        public void VehicleUnit_OnATroopBayOnlyShip_LoadButtonIsDisabled()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;

            var infantry = GroundForces.RaiseUnit(body, InfantryDesign(), s.Faction.Id, regionIndex: 0, name: "1st Rifles");
            var tank = GroundForces.RaiseUnit(body, ArmorDesign(), s.Faction.Id, regionIndex: 0, name: "1st Armor");
            var ship = TroopShipAt(s, body);   // Personnel-class bay only

            Assert.That(GroundTransport.CarryClassOf(tank.UnitType), Is.EqualTo(GroundCarryClass.Vehicle),
                "armour is a Vehicle-class unit");
            Assert.That(GroundTransport.BayCapacity(ship, GroundCarryClass.Vehicle), Is.EqualTo(0),
                "a troop-bay-only ship has no vehicle room (the panel shows no Vehicle-bay row)");
            Assert.That(GroundTransport.CanLoad(ship, tank), Is.False,
                "→ the tank's Load button is greyed (no room of its class)");
            Assert.That(GroundTransport.CanLoad(ship, infantry), Is.True,
                "while the infantry's Load button is enabled (Personnel room exists)");
        }
    }
}
