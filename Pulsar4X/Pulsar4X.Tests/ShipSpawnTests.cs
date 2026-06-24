using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;
using Pulsar4X.Orbits;
using Pulsar4X.Movement;
using Pulsar4X.Galaxy;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The ship-spawn gauge — gives the (otherwise DARK) Ships system its first automated coverage, and
    /// bisects the 2026-06-22 live-test report "spawned ships don't appear." It exercises the EXACT engine
    /// path the DevTools "Spawn Ship" button uses — `ShipFactory.CreateShip(design, faction, parent, name)` —
    /// and proves the ship lands in the star system with its core parts.
    ///
    /// If this is green, engine-side spawning works, which pins the live issue on the CLIENT (the DevTools
    /// dropdown cached the ship-design list until "Refresh Lists" — fixed separately, verified live). CI can't
    /// see the UI; this is the closest automated proof we can get to "spawning works."
    /// </summary>
    [TestFixture]
    public class ShipSpawnTests
    {
        [Test]
        [Description("ShipFactory.CreateShip (the DevTools spawn path) puts a ship into the system with its parts.")]
        public void SpawnShip_LandsInSystem_WithCoreBlobs()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();

            Assert.That(factionInfo.ShipDesigns, Is.Not.Empty,
                "The faction has no ship designs to spawn — the colony blueprint should unlock some.");
            var design = factionInfo.ShipDesigns.Values.First();

            int shipsBefore = s.StartingSystem.GetAllEntitiesWithDataBlob<ShipInfoDB>().Count;

            // Same overload the DevTools "Spawn Ship" button calls.
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Gauge Test Ship");

            // It exists and is parented into the starting system (this is what the spawn list + map renderer read).
            Assert.That(ship, Is.Not.Null, "CreateShip returned null.");
            Assert.That(ship.Id, Is.GreaterThanOrEqualTo(0), "Spawned ship has an invalid entity id.");
            Assert.That(ship.Manager, Is.SameAs(s.StartingSystem), "Spawned ship is not in the starting system.");

            // Core blobs every ship needs (and that the UI reads to draw/inspect it).
            Assert.That(ship.HasDataBlob<ShipInfoDB>(), Is.True, "Spawned ship has no ShipInfoDB.");
            Assert.That(ship.HasDataBlob<PositionDB>(), Is.True, "Spawned ship has no PositionDB (won't render).");
            Assert.That(ship.HasDataBlob<OrbitDB>(), Is.True, "Spawned ship has no OrbitDB.");
            Assert.That(ship.HasDataBlob<MassVolumeDB>(), Is.True, "Spawned ship has no MassVolumeDB.");

            // Queryable in the system exactly the way every consumer finds ships — the count went up by one
            // and our specific ship is in the result. This is the assertion that would have caught a spawn
            // that silently failed to land in the manager.
            var shipsAfter = s.StartingSystem.GetAllEntitiesWithDataBlob<ShipInfoDB>();
            Assert.That(shipsAfter.Count, Is.EqualTo(shipsBefore + 1),
                "System ship count did not increase by exactly one after the spawn.");
            Assert.That(shipsAfter.Any(e => e.Id == ship.Id), Is.True,
                "The spawned ship is not in the system's ShipInfoDB query result.");
        }

        [Test]
        [Description("A spawned ship survives the simulation advancing (no processor throws on it).")]
        public void SpawnedShip_SurvivesTimeAdvance()
        {
            var s = TestScenario.CreateWithColony();
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Gauge Test Ship");

            Assert.DoesNotThrow(() => s.AdvanceTime(TimeSpan.FromDays(5)),
                "Advancing the clock with a freshly-spawned ship in the system threw.");

            Assert.That(s.StartingSystem.GetAllEntitiesWithDataBlob<ShipInfoDB>().Any(e => e.Id == ship.Id), Is.True,
                "The spawned ship vanished from the system after a short time advance.");
        }
    }
}
