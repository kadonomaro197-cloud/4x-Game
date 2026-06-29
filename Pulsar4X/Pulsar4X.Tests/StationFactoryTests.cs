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
    }
}
