using System.Collections.Generic;
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

        [Test]
        [Description("Phase-3.2: PickGreatestThreat returns the strongest DETECTED rival above our own strength; none-above → (-1,0); null/empty is safe.")]
        public void PickGreatestThreat_PicksTheStrongestRivalAboveOwn()
        {
            var seen = new Dictionary<int, double> { { 1, 50 }, { 2, 200 }, { 3, 80 } };
            var (id, str) = ThreatAssessment.PickGreatestThreat(seen, ownStrength: 100);
            Assert.That(id, Is.EqualTo(2), "the strongest rival that out-muscles us is the one we fear");
            Assert.That(str, Is.EqualTo(200.0));

            var (noneId, noneStr) = ThreatAssessment.PickGreatestThreat(
                new Dictionary<int, double> { { 1, 50 }, { 3, 80 } }, ownStrength: 100);
            Assert.That(noneId, Is.EqualTo(-1), "nobody out-muscles us → no threat");
            Assert.That(noneStr, Is.EqualTo(0.0));

            Assert.That(ThreatAssessment.PickGreatestThreat(new Dictionary<int, double>(), 100).rivalId, Is.EqualTo(-1));
            Assert.That(ThreatAssessment.PickGreatestThreat(null, 100).rivalId, Is.EqualTo(-1), "null → no throw, no threat");
        }

        [Test]
        [Description("Phase-3.2: GreatestThreatTo wires the fog read — a huge detected rival (a registered faction) is named the greatest threat.")]
        public void GreatestThreatTo_NamesTheHugeDetectedRival()
        {
            var s = TestScenario.CreateWithColony();
            var observer = s.Faction;
            var observerInfo = observer.GetDataBlob<FactionInfoDB>();

            // A real, registered rival faction, seen by the observer at overwhelming strength (well above our own).
            var rival = FactionFactory.CreateBasicFaction(s.Game, "Colossus", "COL", 0);
            SeeContact(observerInfo, MakeShip(s.Game.GlobalManager, rival.Id), strength: 1e9, memory: false);

            var (id, str) = ThreatAssessment.GreatestThreatTo(observer);
            Assert.That(id, Is.EqualTo(rival.Id), "the overwhelming detected rival is the greatest threat");
            Assert.That(str, Is.EqualTo(1e9).Within(1e-3));
        }
    }
}
