using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauges <see cref="FleetTools.CollapsedFleetMemberShipIds"/> — the engine half of "draw a fleet as ONE icon".
    /// The map collapses each multi-ship fleet to a single representative (its flagship); this asserts the helper
    /// names exactly the OTHER members to hide, never the flagship, and never a lone ship. The start (colony-earth)
    /// has a Military fleet of two gunships, so collapse is actually exercised. Client wiring is CI-blind; this locks
    /// the decision logic that drives it.
    /// </summary>
    [TestFixture]
    public class FleetCollapseTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[fleet-collapse] " + m);

        [Test]
        [Description("A multi-ship fleet hides all members except its representative (flagship); a lone ship is never " +
                     "hidden; no flagship is ever hidden.")]
        public void CollapsedMembers_HideAllButTheFlagship_NeverALoneShip()
        {
            var s = TestScenario.CreateWithColony();
            int factionId = s.Faction.Id;

            var hidden = FleetTools.CollapsedFleetMemberShipIds(s.StartingSystem, factionId);

            // Walk the fleets ourselves to know what SHOULD happen, and check the helper against it.
            var fleets = s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>()
                .Where(f => f.FactionOwnerID == factionId).ToList();

            int multiShipFleets = 0;
            int expectedHidden = 0;
            foreach (var fleetEntity in fleets)
            {
                var fleetDB = fleetEntity.GetDataBlob<FleetDB>();
                var shipIds = fleetDB.Children
                    .Where(c => c != null && c.IsValid && c.HasDataBlob<ShipInfoDB>())
                    .Select(c => c.Id).ToList();
                Log($"fleet #{fleetEntity.Id}: {shipIds.Count} ship(s), flagship={fleetDB.FlagShipID}");

                if (shipIds.Count < 2)
                {
                    // A lone ship must NOT be hidden.
                    foreach (var id in shipIds)
                        Assert.That(hidden.Contains(id), Is.False, $"lone ship #{id} must stay visible");
                    continue;
                }

                multiShipFleets++;
                int rep = shipIds.Contains(fleetDB.FlagShipID) ? fleetDB.FlagShipID : shipIds[0];
                expectedHidden += shipIds.Count - 1;

                Assert.That(hidden.Contains(rep), Is.False,
                    $"the representative (flagship) #{rep} must stay visible — it IS the fleet's icon");
                foreach (var id in shipIds.Where(i => i != rep))
                    Assert.That(hidden.Contains(id), Is.True,
                        $"non-flagship member #{id} of a multi-ship fleet must be hidden (collapsed into the fleet icon)");
            }

            Log($"multi-ship fleets={multiShipFleets}, hidden={hidden.Count} (expected {expectedHidden})");
            Assert.That(multiShipFleets, Is.GreaterThan(0),
                "the start should have at least one multi-ship fleet (Military = 2 gunships) to exercise collapse");
            Assert.That(hidden.Count, Is.EqualTo(expectedHidden),
                "the helper must hide exactly (shipCount - 1) per multi-ship fleet — one representative each");

            // Every hidden id is a real ship of this faction (never a body/fleet entity).
            var shipSet = s.StartingSystem.GetAllEntitiesWithDataBlob<ShipInfoDB>().Select(e => e.Id).ToHashSet();
            foreach (var id in hidden)
                Assert.That(shipSet.Contains(id), Is.True, $"hidden id #{id} must be an actual ship");
        }

        [Test]
        [Description("FleetShipCountFor returns the fleet size for a representative in a multi-ship fleet, and 1 for a " +
                     "ship that isn't (so a lone ship's label is unaffected).")]
        public void FleetShipCountFor_ReportsSizeForRepresentative_Else1()
        {
            var s = TestScenario.CreateWithColony();
            int factionId = s.Faction.Id;

            var fleets = s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>()
                .Where(f => f.FactionOwnerID == factionId);

            bool checkedAMultiShip = false;
            foreach (var fleetEntity in fleets)
            {
                var fleetDB = fleetEntity.GetDataBlob<FleetDB>();
                var shipIds = fleetDB.Children
                    .Where(c => c != null && c.IsValid && c.HasDataBlob<ShipInfoDB>())
                    .Select(c => c.Id).ToList();
                if (shipIds.Count < 2) continue;

                checkedAMultiShip = true;
                int rep = shipIds.Contains(fleetDB.FlagShipID) ? fleetDB.FlagShipID : shipIds[0];
                int count = FleetTools.FleetShipCountFor(s.StartingSystem, factionId, rep);
                Log($"representative #{rep} reports fleet size {count} (actual {shipIds.Count})");
                Assert.That(count, Is.EqualTo(shipIds.Count), "the representative reports its fleet's ship count");
            }

            Assert.That(checkedAMultiShip, Is.True, "the start should have a multi-ship fleet to check");
            // A nonexistent ship id reports 1 (best-effort default — a lone/unknown ship's label is unchanged).
            Assert.That(FleetTools.FleetShipCountFor(s.StartingSystem, factionId, -12345), Is.EqualTo(1));
        }
    }
}
