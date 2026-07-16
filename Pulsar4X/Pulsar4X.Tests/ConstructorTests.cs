using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Storage;
using Pulsar4X.Ships;
using Pulsar4X.Fleets;
using Pulsar4X.Galaxy;
using Pulsar4X.Stations;
using Pulsar4X.Construction;
using Pulsar4X.Datablobs; // ComponentInstancesDB
using Pulsar4X.DataStructures; // ComponentMountType

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The gauge for MODEL A — "build the components, haul them, a constructor assembles the entity on site."
    /// Proves the three rungs the developer named:
    ///   1) a base-mod <c>constructor</c> component binds a <see cref="ConstructorAtb"/> from JSON (gotcha-10) — a
    ///      constructor is a designed/built/mounted COMPONENT (CONVENTIONS §6), not an engine flag.
    ///   2) a constructor ship, with a station recipe's components in a FLEET-mate's hold (the developer's "or is part
    ///      of a fleet that has the components"), assembles the station ON SITE and CONSUMES those carried components.
    ///   3) if the pooled holds are short even one component, the build is REFUSED — no station, nothing consumed.
    /// Engine-only → runs in CI. Submits through the real order handler (the player path), like the deploy tests.
    /// </summary>
    [TestFixture]
    public class ConstructorTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[constructor] " + m);

        private static ComponentDesign Design(TestScenario s, string id)
            => (ComponentDesign)s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns[id];

        /// <summary>Build a ship carrying a cargo hold, parked at <paramref name="parent"/>.</summary>
        private static Entity CargoShip(TestScenario s, Entity parent, string name)
        {
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            foreach (var kv in factionInfo.ShipDesigns)
            {
                var candidate = ShipFactory.CreateShip(kv.Value, s.Faction, parent, name);
                if (candidate.HasDataBlob<CargoStorageDB>()) return candidate;
                candidate.Destroy();
            }
            return ShipFactory.CreateShip(factionInfo.ShipDesigns.Values.First(), s.Faction, parent, name);
        }

        /// <summary>Put <paramref name="count"/> units of a built component into the ship's hold, mounting warehouses
        /// until the general-storage store has room (so the test never fails on hold size rather than the logic).</summary>
        private static void SeedComponentCargo(TestScenario s, Entity ship, ComponentDesign comp, long count)
        {
            var warehouse = Design(s, "default-design-warehouse");
            var hold = ship.GetDataBlob<CargoStorageDB>();
            int guard = 0;
            while (hold.GetFreeUnitSpace(comp, true) < count && guard++ < 100)
                ship.AddComponent(warehouse);
            hold = ship.GetDataBlob<CargoStorageDB>();
            hold.AddCargoByUnit(comp, count);
        }

        [Test]
        [Description("The base-mod constructor loads onto the start faction and binds a ConstructorAtb from JSON " +
                     "(gotcha-10 template->atb) with a real construction capacity, and mounts as a ship component.")]
        public void Constructor_LoadsFromJson_BindsTheAtb()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            Assert.That(designs.ContainsKey("default-design-constructor"), Is.True,
                "the constructor loads onto the faction (the six-point chain is wired)");
            var constructor = designs["default-design-constructor"] as ComponentDesign;
            Assert.That(constructor, Is.Not.Null, "the constructor is a ComponentDesign");
            Assert.That(constructor.HasAttribute<ConstructorAtb>(), Is.True,
                "the JSON constructorAtbArgs bound a ConstructorAtb (gotcha-10 path works)");

            var atb = constructor.GetAttribute<ConstructorAtb>();
            Log($"constructor: capacity={atb.ConstructionCapacity:0} mount={constructor.ComponentMountType}");
            Assert.That(atb.ConstructionCapacity, Is.GreaterThan(0), "it carries a real construction capacity from the template");
            Assert.That(constructor.ComponentMountType.HasFlag(ComponentMountType.ShipComponent), Is.True,
                "a constructor mounts on a ship (so it appears in the ship assembler)");
        }

        [Test]
        [Description("MODEL A: a constructor ship with a station recipe's components in its FLEET-mate's hold assembles " +
                     "the station on site and consumes the carried components (the pieces are drawn from the fleet pool, " +
                     "not conjured). Proves the constructor gate AND fleet-pooled supply.")]
        public void OnSiteConstruction_AssemblesStation_FromFleetCarriedComponents()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            Entity star = s.StartingSystem.GetAllEntitiesWithDataBlob<StarInfoDB>().First();

            // A small station recipe: chassis + one reactor (both haulable — general-storage).
            var chassis = Design(s, "default-design-station-chassis");
            var reactor = Design(s, "default-design-reactor-2t");
            var recipe = StationDesign.RegisterStationDesign(faction, "test-onsite-station", "Test On-Site Station",
                chassis, new List<(ComponentDesign, int)> { (reactor, 1) });

            // The CONSTRUCTOR ship carries the field constructor but NONE of the parts.
            var constructor = CargoShip(s, star, "Constructor");
            constructor.AddComponent(Design(s, "default-design-constructor"));

            // A FREIGHTER fleet-mate carries the recipe's components (chassis + reactor).
            var freighter = CargoShip(s, star, "Freighter");
            SeedComponentCargo(s, freighter, chassis, 1);
            SeedComponentCargo(s, freighter, reactor, 1);

            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Build Group");
            fleet.GetDataBlob<FleetDB>().AddChild(constructor);
            fleet.GetDataBlob<FleetDB>().AddChild(freighter);

            var freighterHold = freighter.GetDataBlob<CargoStorageDB>();
            Assert.That(freighterHold.GetUnitsStored(chassis, false), Is.EqualTo(1), "precondition: the freighter carries the chassis");
            Assert.That(freighterHold.GetUnitsStored(reactor, false), Is.EqualTo(1), "precondition: the freighter carries the reactor");

            int before = faction.Stations.Count;
            var cmd = OnSiteConstructionOrder.CreateCommand(constructor, recipe.UniqueID);
            Assert.That(cmd.IsValidCommand(s.Game), Is.True, "a ship carrying a field constructor, parked at a body, is a valid builder");
            s.Game.OrderHandler.HandleOrder(cmd); // the player path (InstantOrder → synchronous)

            Assert.That(faction.Stations.Count, Is.EqualTo(before + 1),
                "the constructor assembles exactly one station from the fleet-carried components");
            var station = faction.Stations.Last();
            Assert.That(station.GetDataBlob<StationInfoDB>().HostingBodyEntity.Id, Is.EqualTo(star.Id),
                "the station is built where the constructor is parked (the star)");

            // The carried components were CONSUMED from the fleet-mate's hold (not conjured).
            Assert.That(freighterHold.GetUnitsStored(chassis, false), Is.EqualTo(0), "the chassis was drawn from the freighter");
            Assert.That(freighterHold.GetUnitsStored(reactor, false), Is.EqualTo(0), "the reactor was drawn from the freighter");

            // The modules were installed on the deployed station.
            var comps = station.GetDataBlob<ComponentInstancesDB>();
            Log($"assembled station modules = {comps.AllComponents.Count}");
            Assert.That(comps.AllComponents.Count, Is.GreaterThanOrEqualTo(2), "the chassis + reactor were installed on the station");
        }

        [Test]
        [Description("MODEL A refusal: with a component missing from the constructor and its fleet, the on-site build is " +
                     "REFUSED — no station, and nothing is consumed (a real supply gate, not a suggestion).")]
        public void OnSiteConstruction_ShortComponents_RefusesBuild()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            Entity star = s.StartingSystem.GetAllEntitiesWithDataBlob<StarInfoDB>().First();

            var chassis = Design(s, "default-design-station-chassis");
            var reactor = Design(s, "default-design-reactor-2t");
            var recipe = StationDesign.RegisterStationDesign(faction, "test-onsite-short", "Test Short Station",
                chassis, new List<(ComponentDesign, int)> { (reactor, 1) });

            // The constructor carries the field constructor + the CHASSIS, but NOT the reactor (short one part).
            var constructor = CargoShip(s, star, "Constructor");
            constructor.AddComponent(Design(s, "default-design-constructor"));
            SeedComponentCargo(s, constructor, chassis, 1);
            var hold = constructor.GetDataBlob<CargoStorageDB>();
            Assert.That(hold.GetUnitsStored(reactor, false), Is.EqualTo(0), "precondition: no reactor carried");

            int before = faction.Stations.Count;
            var cmd = OnSiteConstructionOrder.CreateCommand(constructor, recipe.UniqueID);
            s.Game.OrderHandler.HandleOrder(cmd);

            Assert.That(faction.Stations.Count, Is.EqualTo(before),
                "missing a component → the build must be REFUSED, no station created");
            Assert.That(hold.GetUnitsStored(chassis, false), Is.EqualTo(1),
                "a refused build consumes NOTHING — the chassis is still in the hold");
        }
    }
}
