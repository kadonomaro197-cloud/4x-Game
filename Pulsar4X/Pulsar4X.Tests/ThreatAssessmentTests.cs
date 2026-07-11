using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Movement;
using Pulsar4X.Sensors;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// F-B1b gauge (docs/AI-BRAIN-BUILD-TRACKER.md, the "eyes"): the fog-limited enemy-strength read. Proves the
    /// estimate (a) sums only the observer's LIVE contacts belonging to the queried rival, (b) EXCLUDES a stale
    /// "memory" contact (current strength ≠ what you last saw), a DIFFERENT rival's contacts, and the observer's OWN
    /// ships, and (c) reads 0 for a rival the observer can't see — the fog (you under-read a hidden enemy).
    /// </summary>
    [TestFixture]
    public class ThreatAssessmentTests
    {
        private static Entity MakeShip(EntityManager mgr, int factionId)
        {
            var e = Entity.Create();
            e.FactionOwnerID = factionId;
            mgr.AddEntity(e);
            return e;
        }

        // Fabricate a contact the observer "sees" for a given entity, with a set signal strength; optionally stale.
        private static void SeeContact(FactionInfoDB observerInfo, Entity actual, double strength, bool memory)
        {
            var info = new SensorInfoDB
            {
                LatestDetectionQuality = new SensorReturnValues { SignalStrength_kW = strength },
            };
            var contact = new SensorContact { ActualEntity = actual, ActualEntityId = actual.Id, SensorInfo = info };
            if (memory)
            {
                contact.Position = new SensorPositionDB(new PositionDB(0, 0, 0));
                contact.Position.GetDataFrom = DataFrom.Memory; // coasting on a last-known position
            }
            observerInfo.SensorContacts[actual.Id] = contact;
        }

        [Test]
        [Description("DetectedStrengthOf sums only the queried rival's LIVE contacts; memory/other-rival/own are excluded; unseen reads 0.")]
        public void DetectedStrengthOf_IsFogLimited_ToLiveContactsOfThatRival()
        {
            var s = TestScenario.CreateWithColony();
            var mgr = s.Game.GlobalManager;
            var observer = s.Faction;
            var observerInfo = observer.GetDataBlob<FactionInfoDB>();

            const int rivalId = 4242;
            const int otherRivalId = 7777;

            SeeContact(observerInfo, MakeShip(mgr, rivalId), strength: 300, memory: false);
            SeeContact(observerInfo, MakeShip(mgr, rivalId), strength: 200, memory: false);
            SeeContact(observerInfo, MakeShip(mgr, rivalId), strength: 1000, memory: true);   // stale → excluded
            SeeContact(observerInfo, MakeShip(mgr, otherRivalId), strength: 999, memory: false); // other rival
            SeeContact(observerInfo, MakeShip(mgr, observer.Id), strength: 500, memory: false);   // our own ship

            Assert.That(ThreatAssessment.DetectedStrengthOf(observer, rivalId), Is.EqualTo(500.0).Within(1e-6),
                "only the queried rival's LIVE contacts count (300+200); a stale-memory contact, a different rival, and own ships are excluded");
            Assert.That(ThreatAssessment.DetectedStrengthOf(observer, otherRivalId), Is.EqualTo(999.0).Within(1e-6),
                "each rival is read independently");
            Assert.That(ThreatAssessment.DetectedStrengthOf(observer, 123456), Is.EqualTo(0.0),
                "a rival the observer can't see reads 0 — the fog (you under-read a hidden enemy)");
        }
    }
}
