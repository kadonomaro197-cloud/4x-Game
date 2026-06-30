using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Stations;
using Pulsar4X.Datablobs;
using Pulsar4X.Storage;
using Pulsar4X.Industry;
using Pulsar4X.Names;
using Pulsar4X.Colonies;
using Pulsar4X.Components;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The station-host foundation gauge. A space station is the deliberate PARALLEL to a colony
    /// (docs/SPACE-STATIONS-DESIGN.md): its OWN chassis, but carrying the SAME shared component-equipment
    /// layer a colony does. These tests assert StationFactory wires that chassis correctly — the same blob
    /// set a colony gets, registered on the faction's parallel Stations registry — which is the precondition
    /// for the economy processors (which discover work by ability component, not by host type) to process a
    /// station for free in the next slice.
    /// </summary>
    [TestFixture]
    public class StationFactoryTests
    {
        [Test]
        [Description("A station built on a body carries the shared infrastructure chassis and is registered on the faction.")]
        public void CreateStation_WiresSharedChassis_AndRegistersOnFaction()
        {
            var s = TestScenario.CreateWithColony();

            var station = StationFactory.CreateStation(s.Faction, s.StartingBody);

            Assert.That(station, Is.Not.Null);
            Assert.That(station, Is.Not.EqualTo(Entity.InvalidEntity));

            // Core station host
            Assert.That(station.HasDataBlob<StationInfoDB>(), Is.True, "station missing StationInfoDB host");
            var info = station.GetDataBlob<StationInfoDB>();
            Assert.That(info.HostingBodyEntity, Is.EqualTo(s.StartingBody),
                "station's hosting body should be the body it was built on");

            // The SHARED equipment chassis — same blobs a colony carries, so the economy processors
            // (which key off these ability blobs, not the host type) can act on a station.
            Assert.That(station.HasDataBlob<NameDB>(), Is.True, "missing NameDB");
            Assert.That(station.HasDataBlob<ComponentInstancesDB>(), Is.True, "missing ComponentInstancesDB (the equipment layer)");
            Assert.That(station.HasDataBlob<CargoStorageDB>(), Is.True, "missing CargoStorageDB");
            Assert.That(station.HasDataBlob<MiningDB>(), Is.True, "missing MiningDB");
            Assert.That(station.HasDataBlob<InfrastructureDB>(), Is.True, "missing InfrastructureDB");

            // Registered as a station on the faction (the parallel registry to Colonies)
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            Assert.That(factionInfo.Stations, Does.Contain(station), "station not registered in FactionInfoDB.Stations");
            Assert.That(factionInfo.Colonies, Does.Not.Contain(station), "a station must NOT land in the Colonies registry");
            Assert.That(station.FactionOwnerID, Is.EqualTo(s.Faction.Id), "station not owned by the faction");

            // It lives in the same star-system manager as the body, like a colony does.
            Assert.That(station.Manager, Is.EqualTo(s.StartingBody.Manager));
        }

        [Test]
        [Description("A manned station carries population; an automated one defaults to empty.")]
        public void CreateStation_PopulationOptional()
        {
            var s = TestScenario.CreateWithColony();

            var automated = StationFactory.CreateStation(s.Faction, s.StartingBody);
            Assert.That(automated.GetDataBlob<StationInfoDB>().Population, Is.Empty,
                "an automated platform should start unmanned");

            var manned = StationFactory.CreateStation(s.Faction, s.StartingBody, 5000, s.Species);
            var pop = manned.GetDataBlob<StationInfoDB>().Population;
            Assert.That(pop.ContainsKey(s.Species.Id), Is.True, "manned station should house the given species");
            Assert.That(pop[s.Species.Id], Is.EqualTo(5000));
        }

        [Test]
        [Description("StationInfoDB clones deeply so it survives entity transfer / save-load.")]
        public void StationInfoDB_ClonesDeeply()
        {
            var hostingBody = Entity.Create();
            var original = new StationInfoDB(hostingBody);
            original.Population[42] = 1234;
            original.ComponentStockpile["widget"] = 7;

            var clone = (StationInfoDB)original.Clone();
            Assert.That(clone.HostingBodyEntity, Is.EqualTo(hostingBody));
            Assert.That(clone.Population[42], Is.EqualTo(1234));
            Assert.That(clone.ComponentStockpile["widget"], Is.EqualTo(7));

            // Deep copy: mutating the clone must not touch the original's collections.
            clone.Population[42] = 9999;
            Assert.That(original.Population[42], Is.EqualTo(1234), "Population dictionary was shared, not cloned");
        }

        [Test]
        [Description("The host-agnostic mining seam: TryGetMiningBody resolves a colony's planet and a station's hosting body, and rejects a plain body.")]
        public void TryGetMiningBody_ResolvesColonyAndStation()
        {
            var s = TestScenario.CreateWithColony();
            var planet = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;

            Assert.That(MiningHelper.TryGetMiningBody(s.Colony, out var colonyBody), Is.True);
            Assert.That(colonyBody, Is.EqualTo(planet), "a colony's mining body is its planet");

            var station = StationFactory.CreateStation(s.Faction, planet);
            Assert.That(MiningHelper.TryGetMiningBody(station, out var stationBody), Is.True);
            Assert.That(stationBody, Is.EqualTo(planet), "a station's mining body is its hosting body");

            // A bare body (neither colony nor station) is not a mining host.
            Assert.That(MiningHelper.TryGetMiningBody(planet, out _), Is.False);
        }

        [Test]
        [Description("A station carrying the colony's own mine + cargo modules mines its hosting body — the economy comes for free, no station-specific code (task #17).")]
        public void Station_WithMiningModule_MinesItsHostingBody()
        {
            var s = TestScenario.CreateWithColony();
            var planet = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;
            Assert.That(planet.HasDataBlob<MineralsDB>(), Is.True, "precondition: the colony's planet has minerals to mine");

            // Reuse the SAME mine + cargo-store designs the starting colony already runs — proves a station
            // rides the identical component chassis with no station-aware economy code.
            var colonyComponents = s.Colony.GetDataBlob<ComponentInstancesDB>();
            Assert.That(colonyComponents.TryGetComponentsByAttribute<MineResourcesAtbDB>(out var mineInstances), Is.True,
                "precondition: the starting colony has a mine to copy");
            Assert.That(colonyComponents.TryGetComponentsByAttribute<CargoStorageAtb>(out var cargoInstances), Is.True,
                "precondition: the starting colony has cargo storage to copy");
            var mineDesign = mineInstances.First().Design;
            var cargoDesign = cargoInstances.First().Design;

            var station = StationFactory.CreateStation(s.Faction, planet);
            station.AddComponent(cargoDesign); // a hold to mine into (a bare station has zero storage)
            station.AddComponent(mineDesign);  // the mine — AddComponent triggers ReCalc → sets ActualMiningRate

            var stockpile = station.GetDataBlob<CargoStorageDB>();
            double before = stockpile.TotalStoredMass;

            s.AdvanceTime(TimeSpan.FromDays(60));

            Assert.That(stockpile.TotalStoredMass, Is.GreaterThan(before),
                "a station with a mine + hold should have extracted minerals off its hosting body");
        }
    }
}
