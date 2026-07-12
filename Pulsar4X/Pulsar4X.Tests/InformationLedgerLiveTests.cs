using System;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Sensors;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-3.1 gauge (docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md — the Information Ledger made LIVE + the "is this
    /// rival RISING over time" read). The sibling of the F-C3a data-model gauge (<see cref="InformationLedgerTests"/>):
    /// that one pins Inferred→Confirmed→Stale; this one pins the new trend read + the live driver + byte-identity.
    /// Proves:
    /// (a) the pure RISING read reports UP only when successive DETECTED-strength samples INCREASE (one-sample / flat /
    /// falling → false), at every layer — <see cref="IntelRecord.IsRising"/>, <see cref="InformationLedgerDB.IsRising"/>,
    /// <see cref="ThreatAssessment.IsRising"/>;
    /// (b) the ledger POPULATES + DECAYS when driven by <see cref="NPCDecisionProcessor.UpdateInformationLedger"/>
    /// (a detected rival is Confirmed + sampled → the trend accrues; a cold record goes Stale);
    /// (c) the default path is BYTE-IDENTICAL — the write gate defaults OFF and the attached ledger starts empty/inert.
    /// </summary>
    [TestFixture]
    public class InformationLedgerLiveTests
    {
        private static Entity MakeShip(EntityManager mgr, int factionId)
        {
            var e = Entity.Create();
            e.FactionOwnerID = factionId;
            mgr.AddEntity(e);
            return e;
        }

        // Fabricate a live sensor contact so the observer "sees" the given entity at a set signal strength.
        private static void SeeContact(FactionInfoDB observerInfo, Entity actual, double strength)
        {
            var info = new SensorInfoDB { LatestDetectionQuality = new SensorReturnValues { SignalStrength_kW = strength } };
            observerInfo.SensorContacts[actual.Id] =
                new SensorContact { ActualEntity = actual, ActualEntityId = actual.Id, SensorInfo = info };
        }

        // ---- (a) the pure RISING read, at every layer ----

        [Test]
        [Description("IntelRecord.IsRising: UP only when the last sample exceeds the prior; one-sample / flat / falling read false.")]
        public void IsRising_PureRecord_TrueOnlyWhenSuccessiveSamplesIncrease()
        {
            var rec = new IntelRecord();
            Assert.That(rec.IsRising, Is.False, "no samples → no trend");

            rec.RecordSample(100);
            Assert.That(rec.IsRising, Is.False, "one sample → no trend yet (can't call it rising from a single point)");

            rec.RecordSample(150);
            Assert.That(rec.IsRising, Is.True, "150 > 100 → rising");

            rec.RecordSample(150);
            Assert.That(rec.IsRising, Is.False, "flat (150 == 150) → not rising");

            rec.RecordSample(120);
            Assert.That(rec.IsRising, Is.False, "120 < 150 → falling → not rising");
        }

        [Test]
        [Description("The ledger + ThreatAssessment RISING reads delegate to the record; unknown rival / null ledger → false, no throw.")]
        public void IsRising_LedgerAndThreatAssessment_ReadTheTrend()
        {
            var ledger = new InformationLedgerDB();
            const int rivalId = 4242;

            // Unknown rival / null ledger are safe (no trend).
            Assert.That(ledger.IsRising(rivalId), Is.False, "no record for this rival → false");
            Assert.That(ThreatAssessment.IsRising(null, rivalId), Is.False, "null ledger → false, no throw");
            Assert.That(ThreatAssessment.IsRising(ledger, rivalId), Is.False);

            var when = new DateTime(2050, 1, 1);
            ledger.Confirm(rivalId, IntelFacet.Military, when, detectedStrength: 300);
            Assert.That(ledger.IsRising(rivalId), Is.False, "one sample → no trend");

            ledger.Confirm(rivalId, IntelFacet.Military, when, detectedStrength: 500);   // grew
            Assert.That(ledger.IsRising(rivalId), Is.True, "500 > 300 → the rival is rising");
            Assert.That(ThreatAssessment.IsRising(ledger, rivalId), Is.True, "ThreatAssessment agrees (same read)");

            // A different, un-sampled facet on the same rival reports no trend.
            Assert.That(ledger.IsRising(rivalId, IntelFacet.Economy), Is.False, "an un-sampled facet has no trend");
        }

        // ---- (b) the ledger POPULATES + DECAYS when driven ----

        [Test]
        [Description("UpdateInformationLedger Confirms + samples a detected rival; successive growing samples make it read RISING.")]
        public void UpdateInformationLedger_PopulatesAndTrends_ForADetectedRival()
        {
            var s = TestScenario.CreateWithColony();
            var observer = s.Faction;
            var observerInfo = observer.GetDataBlob<FactionInfoDB>();
            var ledger = observer.GetDataBlob<InformationLedgerDB>();     // attached by FactionFactory (Phase-3.1)

            var rival = FactionFactory.CreateBasicFaction(s.Game, "Riser", "RIS", 0);
            var rivalShip = MakeShip(s.Game.GlobalManager, rival.Id);

            // Cycle 1: we see the rival at strength 200 → the driver Confirms the Military facet + records the sample.
            SeeContact(observerInfo, rivalShip, strength: 200);
            NPCDecisionProcessor.UpdateInformationLedger(observer);

            Assert.That(ledger.LevelOf(rival.Id, IntelFacet.Military), Is.EqualTo(IntelLevel.Confirmed),
                "a detected rival's Military facet is Confirmed");
            Assert.That(ledger.IsRising(rival.Id), Is.False, "one sample → no trend yet");

            // Cycle 2: the rival has grown (strength 350) → the persistent ledger now reads it as RISING.
            SeeContact(observerInfo, rivalShip, strength: 350);
            NPCDecisionProcessor.UpdateInformationLedger(observer);

            Assert.That(ledger.IsRising(rival.Id), Is.True, "350 > 200 → the persistent ledger sees the rival rising");
            Assert.That(ThreatAssessment.IsRising(ledger, rival.Id), Is.True);
        }

        [Test]
        [Description("DecayStale (the call the driver makes) drops a cold Confirmed record to Stale; a just-refreshed one stays Confirmed.")]
        public void UpdateInformationLedger_DecaysAColdRecordToStale()
        {
            var ledger = new InformationLedgerDB();
            const int rivalId = 7;
            var t0 = new DateTime(2050, 1, 1);

            ledger.Confirm(rivalId, IntelFacet.Military, t0, detectedStrength: 100);
            Assert.That(ledger.LevelOf(rivalId, IntelFacet.Military), Is.EqualTo(IntelLevel.Confirmed));

            // Not refreshed for two years > the one-year stale window → the driver's DecayStale drops it to Stale.
            ledger.DecayStale(t0 + TimeSpan.FromDays(730), NPCDecisionProcessor.IntelStaleAfter);
            Assert.That(ledger.LevelOf(rivalId, IntelFacet.Military), Is.EqualTo(IntelLevel.Stale),
                "a Confirmed record left cold past the stale window decays to Stale");

            // A refresh re-Confirms it; DecayStale immediately after (now − now = 0) leaves it Confirmed.
            var tRefresh = t0 + TimeSpan.FromDays(730);
            ledger.Confirm(rivalId, IntelFacet.Military, tRefresh, detectedStrength: 120);
            ledger.DecayStale(tRefresh, NPCDecisionProcessor.IntelStaleAfter);
            Assert.That(ledger.LevelOf(rivalId, IntelFacet.Military), Is.EqualTo(IntelLevel.Confirmed),
                "a just-refreshed record is safe from decay");
        }

        // ---- (c) the default path is BYTE-IDENTICAL ----

        [Test]
        [Description("The Phase-3.1 write gate defaults OFF and the attached ledger starts empty — the default path is byte-identical.")]
        public void Gate_DefaultsOff_AndAttachedLedgerStartsEmpty()
        {
            Assert.That(NPCDecisionProcessor.EnableIntelLedger, Is.False,
                "the ledger is populated only when a client/test opts in — keeps every existing test byte-identical");

            var s = TestScenario.CreateWithColony();
            Assert.That(s.Faction.HasDataBlob<InformationLedgerDB>(), Is.True,
                "every faction carries an InformationLedgerDB (mirrors DiplomacyDB/GovernmentDB)");
            Assert.That(s.Faction.GetDataBlob<InformationLedgerDB>().Ledger, Is.Empty,
                "with the gate off nothing writes to it — the attached blob is inert");
        }
    }
}
