using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauges <see cref="FleetTools.CollapsedForeignFleetContacts"/> — the engine half of "group ALL fleets" (the
    /// developer's ruling), extending the own-fleet collapse to DETECTED foreign fleets so a rival's fleet draws as ONE
    /// contact instead of a scatter of named blips. Reuses the start's multi-ship Military fleet as a "foreign" fleet by
    /// viewing it from a DIFFERENT faction's id — so the gauge needs no second faction. Asserts: collapse when fully
    /// detected, NO collapse when only partly spotted (fog-honest), the viewer's own fleets are never touched, and the
    /// own-fleet primitive stays byte-identical (the tripwire). Client wiring is CI-blind; this locks the decision logic.
    /// </summary>
    [TestFixture]
    public class FleetForeignCollapseTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[foreign-collapse] " + m);

        [Test]
        [Description("A detected FOREIGN multi-ship fleet collapses to one representative + a detected-member count; a " +
                     "partly-spotted fleet is NOT collapsed (fog-honest); the viewer's OWN fleets are never touched; and " +
                     "the own-fleet collapse helper stays byte-identical.")]
        public void ForeignFleet_CollapsesDetectedMembers_FogHonest_OwnUntouched()
        {
            var s = TestScenario.CreateWithColony();
            int ownerId = s.Faction.Id;
            int viewerId = ownerId + 1000; // a DIFFERENT faction's view — the colony's fleets are "foreign" to it
            Assert.That(viewerId, Is.Not.EqualTo(ownerId));
            Assert.That(viewerId, Is.Not.EqualTo(Game.NeutralFactionId));

            // The start (colony-earth) has a multi-ship Military fleet (2 gunships). Find it (any 2+-ship fleet).
            var multi = s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>()
                .Where(f => f.FactionOwnerID == ownerId)
                .Select(f => new { f, db = f.GetDataBlob<FleetDB>() })
                .Select(x => new
                {
                    x.f, x.db,
                    ships = x.db.Children.Where(c => c != null && c.IsValid && c.HasDataBlob<ShipInfoDB>())
                                          .Select(c => c.Id).ToList()
                })
                .FirstOrDefault(x => x.ships.Count >= 2);
            Assert.That(multi, Is.Not.Null, "the start should have a multi-ship fleet (Military = 2 gunships) to exercise collapse");
            var shipIds = multi.ships;
            Log($"foreign fleet #{multi.f.Id}: {shipIds.Count} ships, flagship={multi.db.FlagShipID}");

            // The representative the helper should pick: flagship if detected, else the LOWEST detected id.
            int Rep(IEnumerable<int> detected)
            {
                var d = detected.ToList();
                return d.Contains(multi.db.FlagShipID) ? multi.db.FlagShipID : d.Min();
            }

            // (1) FULLY detected → collapse to one representative; the rest hidden; count = detected members.
            var allDetected = new HashSet<int>(shipIds);
            var hidden = FleetTools.CollapsedForeignFleetContacts(s.StartingSystem, viewerId, allDetected, out var counts);
            int rep = Rep(shipIds);
            Assert.That(hidden.Contains(rep), Is.False, "the representative stays visible — it IS the fleet marker");
            foreach (var id in shipIds.Where(i => i != rep))
                Assert.That(hidden.Contains(id), Is.True, $"detected non-rep member #{id} is hidden into the marker");
            Assert.That(counts.ContainsKey(rep), Is.True, "the representative reports a detected-member count");
            Assert.That(counts[rep], Is.EqualTo(shipIds.Count), "the count is the number of DETECTED members");
            Log($"fully-detected: hidden={hidden.Count}, rep #{rep} count={counts[rep]}");

            // (2) PARTLY detected (only ONE ship of the fleet) → NOT collapsed (can't group what you can't see).
            var onlyOne = new HashSet<int> { shipIds[0] };
            var hidden2 = FleetTools.CollapsedForeignFleetContacts(s.StartingSystem, viewerId, onlyOne, out var counts2);
            Assert.That(hidden2, Is.Empty, "a fleet with only 1 detected ship isn't collapsed (fog-honest)");
            Assert.That(counts2, Is.Empty);

            // (3) The viewer's OWN fleets are never grouped by the FOREIGN helper (owner == viewer → skipped).
            var hiddenOwn = FleetTools.CollapsedForeignFleetContacts(s.StartingSystem, ownerId, allDetected, out _);
            Assert.That(shipIds.Any(id => hiddenOwn.Contains(id)), Is.False,
                "the foreign helper must NOT touch the viewer's own fleet");

            // (4) Byte-identity tripwire: the OWN-fleet collapse helper is unchanged (hides all-but-representative).
            var ownHidden = FleetTools.CollapsedFleetMemberShipIds(s.StartingSystem, ownerId);
            int ownRep = shipIds.Contains(multi.db.FlagShipID) ? multi.db.FlagShipID : shipIds[0];
            Assert.That(ownHidden.Contains(ownRep), Is.False, "own-fleet: the representative stays visible");
            foreach (var id in shipIds.Where(i => i != ownRep))
                Assert.That(ownHidden.Contains(id), Is.True, "own-fleet collapse still hides non-flagship members");
        }
    }
}
