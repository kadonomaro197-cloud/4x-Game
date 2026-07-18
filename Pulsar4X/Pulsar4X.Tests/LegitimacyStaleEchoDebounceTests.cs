using System;
using NUnit.Framework;
using Pulsar4X.Colonies;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for Operation Earthfall P3.2 (findings/A3-objective-flip.md): the two engine fixes that stop a ONE-MONTH
    /// transient morale dip from cascading into a phantom legitimacy collapse and a one-sample rebellion (the chain
    /// that locked the UMF AI into a 180-day Defend at the Survive floor).
    ///
    ///   (a) REBELLION DEBOUNCE — <see cref="LegitimacyProcessor.EnableRebellionDebounce"/>: a rebellion begins only
    ///       after <see cref="LegitimacyProcessor.RebellionDebounceReads"/> CONSECUTIVE monthly collapsing reads, so a
    ///       transient dip (one collapsing read, then recovery) is ignored while a sustained collapse still rebels.
    ///   (b) FRESH-MORALE LEGITIMACY — <see cref="LegitimacyProcessor.ReadCurrentMorale"/>: legitimacy recomputes THIS
    ///       cycle's morale instead of echoing the one-cycle-stale <see cref="ColonyMoraleDB.Morale"/> field, so a
    ///       stale trough can't print a legit cliff a month after morale has already recovered.
    ///
    /// Both fixes ship default-OFF (byte-identical to the shipped behaviour — the existing LegitimacyProcessorTests /
    /// RebellionTests keep their single-sample, field-read contract). These tests flip the flags on with try/finally so
    /// the static flags never leak to a sibling fixture in the same shard process.
    /// </summary>
    [TestFixture]
    public class LegitimacyStaleEchoDebounceTests
    {
        private static readonly DateTime T0 = new DateTime(2200, 1, 1);

        [Test]
        [Description("(a) Debounce: a transient dip (one collapsing read then recovery) never rebels; a sustained collapse rebels on the Nth consecutive collapsing read. Flag defaults off (byte-identical single-sample trigger).")]
        public void Debounce_TransientDipIgnored_SustainedRebels()
        {
            Assert.That(LegitimacyProcessor.EnableRebellionDebounce, Is.False, "the debounce ships default-off (byte-identity)");
            Assert.That(LegitimacyProcessor.ReadCurrentMorale, Is.False, "fresh-morale reads ship default-off (byte-identity)");

            var saved = LegitimacyProcessor.EnableRebellionDebounce;
            LegitimacyProcessor.EnableRebellionDebounce = true;
            try
            {
                // TRANSIENT: one collapsing read, then a recovery read resets the counter → never rebels.
                var reb = new RebellionDB();
                var leg = new LegitimacyDB();
                LegitimacyProcessor.UpdateRebellion(reb, leg, 15.0, T0);   // 1st collapsing sample (< 20)
                Assert.That(reb.IsRebelling, Is.False, "one collapsing sample must NOT trigger under debounce");
                Assert.That(leg.ConsecutiveCollapsingReads, Is.EqualTo(1));

                LegitimacyProcessor.UpdateRebellion(reb, leg, 55.0, T0);   // recovered → counter resets
                Assert.That(reb.IsRebelling, Is.False, "a transient dip never becomes a rebellion");
                Assert.That(leg.ConsecutiveCollapsingReads, Is.EqualTo(0), "any non-collapsing read resets the debounce counter");

                // SUSTAINED: RebellionDebounceReads consecutive collapsing reads → rebels on the last one.
                var reb2 = new RebellionDB();
                var leg2 = new LegitimacyDB();
                for (int i = 1; i < LegitimacyProcessor.RebellionDebounceReads; i++)
                {
                    LegitimacyProcessor.UpdateRebellion(reb2, leg2, 15.0, T0);
                    Assert.That(reb2.IsRebelling, Is.False, "still debouncing before the threshold read");
                }
                LegitimacyProcessor.UpdateRebellion(reb2, leg2, 15.0, T0);   // the Nth consecutive collapsing read
                Assert.That(reb2.IsRebelling, Is.True, "a SUSTAINED collapse still triggers the rebellion");
                Assert.That(reb2.ReactionWindowEnds, Is.EqualTo(T0 + TimeSpan.FromDays(RebellionDB.ReactionWindowDays)));
            }
            finally { LegitimacyProcessor.EnableRebellionDebounce = saved; }
        }

        [Test]
        [Description("(a) With the debounce OFF (default), a single collapsing read still begins a rebellion immediately — the existing single-sample contract is byte-identical.")]
        public void Debounce_Off_SingleSampleStillRebels_ByteIdentical()
        {
            Assert.That(LegitimacyProcessor.EnableRebellionDebounce, Is.False);
            var reb = new RebellionDB();
            var leg = new LegitimacyDB();
            LegitimacyProcessor.UpdateRebellion(reb, leg, 10.0, T0);   // one collapsing read
            Assert.That(reb.IsRebelling, Is.True, "debounce off → one sample begins a rebellion, as before");
            Assert.That(leg.ConsecutiveCollapsingReads, Is.EqualTo(0), "the debounce counter is never touched while the flag is off");
        }

        [Test]
        [Description("(b) Fresh-morale legitimacy: a stale morale field can't print a legit cliff (transient dip); and a genuine SUSTAINED collapse still crashes legitimacy AND — with the debounce — begins a rebellion on the 2nd consecutive read.")]
        public void ReadsCurrentMorale_KillsStaleEcho_SustainedStillCollapsesAndRebels()
        {
            var s = TestScenario.CreateWithColony();
            var moraleDB = s.Colony.GetDataBlob<ColonyMoraleDB>();
            var legit = s.Colony.GetDataBlob<LegitimacyDB>();
            var reb = s.Colony.GetDataBlob<RebellionDB>();
            var sustenance = s.Colony.GetDataBlob<ColonySustenanceDB>();
            var econ = s.Colony.GetDataBlob<ColonyEconomyDB>();

            var savedRead = LegitimacyProcessor.ReadCurrentMorale;
            var savedDebounce = LegitimacyProcessor.EnableRebellionDebounce;
            try
            {
                // --- Phase 1: the STALE ECHO (a one-month-old trough left in the morale FIELD) ---
                // The hospitable start colony's CURRENT computed morale is healthy (~neutral); only the stored field
                // is a stale low value, exactly the A3 lag.
                moraleDB.Morale = 10.0;

                // Default (field read): legitimacy echoes the stale trough → a phantom collapse.
                LegitimacyProcessor.ReadCurrentMorale = false;
                LegitimacyProcessor.RecalcLegitimacy(s.Colony);
                Assert.That(LegitimacyDB.IsCollapsing(legit.Legitimacy), Is.True,
                    "default field-read reproduces the stale-echo legit cliff");

                // Fresh-morale read: legitimacy reflects THIS cycle's (healthy) morale → no cliff.
                LegitimacyProcessor.ReadCurrentMorale = true;
                LegitimacyProcessor.RecalcLegitimacy(s.Colony);
                Assert.That(LegitimacyDB.IsCollapsing(legit.Legitimacy), Is.False,
                    "reading current morale kills the stale echo — a transient field trough no longer collapses legitimacy");
                Assert.That(legit.Legitimacy, Is.GreaterThan(LegitimacyDB.CollapseThreshold));

                // --- Phase 2: a GENUINE SUSTAINED collapse still crashes legit AND rebels ---
                LegitimacyProcessor.EnableRebellionDebounce = true;
                legit.ConsecutiveCollapsingReads = 0;             // fresh debounce window
                Assert.That(reb.IsRebelling, Is.False, "not rebelling entering the sustained phase");

                // Drive the colony's REAL current morale to the floor (total famine + blackout + max tax) so the
                // collapse is input-driven, not a stale field — well below the collapse line regardless of any
                // installation morale bonuses.
                sustenance.FoodShortage = 1.0;
                sustenance.PowerShortage = 1.0;
                econ.TaxRate = 1.0;

                // First collapsing read: legitimacy crashes, but the debounce holds the rebellion.
                LegitimacyProcessor.RecalcLegitimacy(s.Colony);
                Assert.That(LegitimacyDB.IsCollapsing(legit.Legitimacy), Is.True,
                    "a real famine collapses legitimacy through the current-morale path");
                Assert.That(reb.IsRebelling, Is.False, "one sustained collapsing read is still debounced");

                // Second consecutive collapsing read with the collapse still present: the rebellion fires.
                LegitimacyProcessor.RecalcLegitimacy(s.Colony);
                Assert.That(reb.IsRebelling, Is.True, "a SUSTAINED collapse still begins a rebellion");
            }
            finally
            {
                LegitimacyProcessor.ReadCurrentMorale = savedRead;
                LegitimacyProcessor.EnableRebellionDebounce = savedDebounce;
            }
        }
    }
}
