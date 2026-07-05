using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.DataStructures;
using Pulsar4X.Datablobs;   // ComponentInstancesDB
using Pulsar4X.Ships;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// TRANSPORT &amp; INVASION track (T1) — the lift half of "you can take a planet": a ship with a bay carries ground
    /// units off-world so an army built at home can reach an enemy world. T1a (this file, for now) proves the base-mod
    /// **troop bay** loads from JSON and binds its <see cref="GroundBayAtb"/> — the gotcha-10 sensor for the new ship
    /// component (the <see cref="RailgunWeaponTests"/> equivalent). T1b adds the load→fly→land round-trip on top.
    /// Engine-only → runs in CI. Design: docs/GROUND-COMBAT-MAP-DESIGN.md → transport.
    /// </summary>
    [TestFixture]
    public class GroundTransportTests
    {
        private const string TroopBay = "default-design-troop-bay";
        private static void Log(string m) => TestContext.Progress.WriteLine("[transport] " + m);

        [Test]
        [Description("T1a: the base-mod troop bay loads onto the start faction, binds a GroundBayAtb from JSON (Personnel class, non-zero capacity), and mounts as a ShipComponent so it can go on a transport ship.")]
        public void TroopBayDesign_LoadsFromJson_BindsTheBayAtb_AsAShipComponent()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            Assert.That(designs.ContainsKey(TroopBay), Is.True,
                "the troop bay loads onto the faction (template + design + earth.json entry wired up)");
            var design = designs[TroopBay] as ComponentDesign;
            Assert.That(design, Is.Not.Null, "the troop bay is a ComponentDesign (rides the shared designer)");

            Assert.That(design.HasAttribute<GroundBayAtb>(), Is.True,
                "the JSON groundBayAtbArgs bound a GroundBayAtb (gotcha-10 template→atb path works)");
            var bay = design.GetAttribute<GroundBayAtb>();
            Log($"{TroopBay}: capacity={bay.Capacity:0} class={bay.CarryClass} mount={design.ComponentMountType}");

            Assert.That(bay.CarryClass, Is.EqualTo(GroundCarryClass.Personnel), "a troop bay carries personnel");
            Assert.That(bay.Capacity, Is.GreaterThan(0), "it has real carry-room");
            Assert.That(design.ComponentMountType.HasFlag(ComponentMountType.ShipComponent), Is.True,
                "it mounts as a ShipComponent — so it can be installed on a transport ship");
        }

        // ── T1b: the load → fly → land round-trip ────────────────────────────────────────────────────────────────

        private static GroundUnitDesign InfantryDesign() => new GroundUnitDesign
        {
            UniqueID = "test-infantry", Name = "Infantry", UnitType = GroundUnitType.Infantry,
            Attack = 100, Defense = 10, HitPoints = 500,
        };

        /// <summary>A ship orbiting the body, owned by <paramref name="factionId"/>, with a troop bay installed.</summary>
        private static Entity TransportAt(TestScenario s, Entity body, int factionId)
        {
            var shipDesign = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(shipDesign, s.Faction, body, "Trooper");
            ship.FactionOwnerID = factionId;
            var bayDesign = (ComponentDesign)s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns[TroopBay];
            ship.AddComponent(new ComponentInstance(bayDesign));   // install a troop bay
            return ship;
        }

        [Test]
        [Description("T1b round-trip: an infantry unit on the home world loads onto a ship with a troop bay (leaves the planet roster, rides the ship), then lands on a target region (re-joins a body roster with its health intact, leaves the ship). The middle link of 'you can take a planet'.")]
        public void Infantry_LoadsOntoATransport_ThenLandsOnATargetRegion()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var unit = GroundForces.RaiseUnit(body, InfantryDesign(), s.Faction.Id, 0, "1st Rifles");
            var forces = body.GetDataBlob<GroundForcesDB>();
            int onPlanetBefore = forces.Units.Count;

            var ship = TransportAt(s, body, s.Faction.Id);
            Assert.That(GroundTransport.ShipIsAtBody(ship, body), Is.True, "the transport is at the body (orbiting it)");
            Assert.That(GroundTransport.BayCapacity(ship, GroundCarryClass.Personnel), Is.GreaterThan(0), "its troop bay gives personnel capacity");

            // LOAD
            Assert.That(GroundTransport.TryLoadUnit(ship, body, unit), Is.True, "the unit loads onto the transport");
            Assert.That(forces.Units, Does.Not.Contain(unit), "it left the planet's roster");
            Assert.That(ship.GetDataBlob<GroundTransportDB>().LoadedUnits, Does.Contain(unit), "it is aboard the ship");
            Assert.That(forces.Units.Count, Is.EqualTo(onPlanetBefore - 1), "one fewer unit on the ground");

            // LAND on region 1 (a different region), orbit uncontested (only our ship present)
            Assert.That(GroundTransport.HasOrbitalControl(ship, body), Is.True, "we hold the orbit (no foreign ship)");
            Assert.That(GroundTransport.TryLandUnit(ship, body, unit, 1), Is.True, "the unit lands");
            Assert.That(ship.GetDataBlob<GroundTransportDB>().LoadedUnits, Does.Not.Contain(unit), "it left the ship's bay");
            Assert.That(forces.Units, Does.Contain(unit), "it re-joined the ground roster");
            Assert.That(unit.RegionIndex, Is.EqualTo(1), "at the region it was landed on");
            Assert.That(unit.Health, Is.EqualTo(500), "with its health intact across the trip");
        }

        [Test]
        [Description("T1b capacity: a troop bay only holds units up to its carry-room — once full, the next load is refused (size-based, per the developer's bay-size × unit-size call).")]
        public void TroopBay_RefusesToOverfill()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var ship = TransportAt(s, body, s.Faction.Id);
            double cap = GroundTransport.BayCapacity(ship, GroundCarryClass.Personnel);

            int loaded = 0;
            // infantry carry-size is 1, so a capacity-6 bay takes 6 of them, then refuses the next
            for (int i = 0; i < (int)cap + 3; i++)
            {
                var u = GroundForces.RaiseUnit(body, InfantryDesign(), s.Faction.Id, 0);
                if (GroundTransport.TryLoadUnit(ship, body, u)) loaded++;
            }
            Assert.That(loaded, Is.EqualTo((int)cap), $"loaded exactly the bay's capacity ({(int)cap}) infantry, then refused the rest");
            Assert.That(GroundTransport.FreeCapacity(ship, GroundCarryClass.Personnel), Is.LessThan(1), "no room left for another");
        }

        [Test]
        [Description("T1b orbit gate: you must hold the orbit to land — a foreign faction's ship present at the target body blocks the drop (space dominance enables invasion).")]
        public void Landing_IsBlocked_WhileAForeignShipHoldsTheOrbit()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var unit = GroundForces.RaiseUnit(body, InfantryDesign(), s.Faction.Id, 0);
            var ship = TransportAt(s, body, s.Faction.Id);
            Assert.That(GroundTransport.TryLoadUnit(ship, body, unit), Is.True);

            // with the orbit uncontested (only our ship), landing would be allowed
            Assert.That(GroundTransport.HasOrbitalControl(ship, body), Is.True, "orbit ours before the enemy arrives");

            // a foreign (non-neutral) faction's ship shows up over the body → we no longer hold the orbit
            TransportAt(s, body, s.Faction.Id + 4242);
            Assert.That(GroundTransport.HasOrbitalControl(ship, body), Is.False, "a foreign ship now holds the orbit");
            Assert.That(GroundTransport.TryLandUnit(ship, body, unit, 1), Is.False, "so the landing is refused (space dominance gates invasion)");
            Assert.That(ship.GetDataBlob<GroundTransportDB>().LoadedUnits, Does.Contain(unit), "the unit is still aboard — it wasn't landed");
        }
    }
}
