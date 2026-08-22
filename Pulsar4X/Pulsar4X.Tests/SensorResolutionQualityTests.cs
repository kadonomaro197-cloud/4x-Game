using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Sensors;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge on the RESOLUTION → FIDELITY wire (the "finish the sensor Resolution dial" slice). A receiver's
    /// resolving power (<see cref="SensorReceiverAtb.Resolution"/>, MegaPixels) now caps how well a contact is
    /// RESOLVED — a low-res sensor detects *something* but not *what* (the field's documented job, previously in a
    /// commented-out block). It scales <c>SignalQuality</c> only; it never changes whether a target is detected.
    ///
    /// Flag-gated: <see cref="SensorTools.EnableResolutionQuality"/> defaults OFF → Resolution is ignored and quality
    /// is the pure band-alignment score (byte-identical to before). Client turns it on via NewGameMenu.
    ///
    /// Pure unit test — <c>DetectonQuality</c> is a static function of (receiver, signal), no game/harness needed.
    /// Mirrors <see cref="SensorQualityTests"/>. Resets the static flag in finally (the shard-safety discipline).
    /// </summary>
    [TestFixture]
    public class SensorResolutionQualityTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[sensor-resolution] " + m);

        // A receiver tuned to a 500 nm peak, 400 nm band, sensitive enough to pick up the strong signal below.
        // Resolution varies per test (the dial under test).
        private static SensorReceiverAtb Receiver(float resolution)
            => new SensorReceiverAtb(peakWaveLength: 500, bandwidth: 400, bestSensitivity: 1, worstSensitivity: 100, resolution: resolution, scanTime: 5);

        // One loud, perfectly-centred signal band (peak 500 == receiver peak → band-alignment quality ~1.0, so the
        // resolution factor is what moves the result).
        private static Dictionary<EMWaveForm, double> CentredSignal(double magnitude_kW = 10.0)
            => new Dictionary<EMWaveForm, double> { { new EMWaveForm(300, 500, 700), magnitude_kW } };

        [Test]
        [Description("The flag defaults OFF → the live detection path ignores Resolution → byte-identical to before.")]
        public void ResolutionQuality_DefaultsOff()
        {
            Assert.That(SensorTools.EnableResolutionQuality, Is.False,
                "EnableResolutionQuality must default OFF — it changes detection fidelity and must be turned on deliberately");
        }

        [Test]
        [Description("Flag OFF: a 1-MP and a 200-MP receiver read the SAME quality on the same signal (Resolution not read) — the byte-identity guarantee.")]
        public void FlagOff_ByteIdentical_ResolutionIgnored()
        {
            SensorTools.EnableResolutionQuality = false;
            try
            {
                float lowRes = SensorTools.DetectonQuality(Receiver(1), CentredSignal()).SignalQuality;
                float highRes = SensorTools.DetectonQuality(Receiver(200), CentredSignal()).SignalQuality;
                Log($"flag OFF: res1={lowRes}, res200={highRes} (expect equal)");
                Assert.That(lowRes, Is.EqualTo(highRes).Within(0.0001f),
                    "with the flag off, Resolution must not affect SignalQuality (byte-identical to the pre-wire behaviour)");
            }
            finally { SensorTools.EnableResolutionQuality = false; }
        }

        [Test]
        [Description("Flag ON: a higher-Resolution receiver resolves the same signal at HIGHER quality than a low-res one, and every value stays in [0,1].")]
        public void FlagOn_HigherResolution_ResolvesBetter()
        {
            SensorTools.EnableResolutionQuality = true;
            try
            {
                float lowRes = SensorTools.DetectonQuality(Receiver(1), CentredSignal()).SignalQuality;
                float highRes = SensorTools.DetectonQuality(Receiver(200), CentredSignal()).SignalQuality;
                Log($"flag ON: res1={lowRes}, res200={highRes} (expect res200 > res1)");
                foreach (var q in new[] { lowRes, highRes })
                    Assert.That(q, Is.InRange(0.0f, 1.0f), "SignalQuality must stay a 0..1 fraction");
                Assert.That(highRes, Is.GreaterThan(lowRes),
                    "a higher-resolution receiver must resolve the same contact at higher quality");
            }
            finally { SensorTools.EnableResolutionQuality = false; }
        }

        [Test]
        [Description("Survey-safety guard (flag ON): a high-res sensor (100 MP) stays near full-ID (>0.80, above the survey-detail gate) while a 1-MP sensor caps at partial ID (<0.80). Pins the one cross-system consequence so a knee re-tune that breaks homeworld survey fails CI.")]
        public void FlagOn_HighResStaysNearFullId_LowResCapsPartial()
        {
            SensorTools.EnableResolutionQuality = true;
            try
            {
                float hi = SensorTools.DetectonQuality(Receiver(100), CentredSignal()).SignalQuality;
                float lo = SensorTools.DetectonQuality(Receiver(1), CentredSignal()).SignalQuality;
                Log($"flag ON survey-guard: res100={hi} (expect >0.80), res1={lo} (expect <0.80)");
                Assert.That(hi, Is.GreaterThan(0.80f),
                    "a high-resolution sensor must stay above the survey-detail gate (0.80) so homeworld survey is unaffected");
                Assert.That(lo, Is.LessThan(0.80f),
                    "a low-resolution (1 MP) sensor must cap below full ID — the whole point of the dial");
            }
            finally { SensorTools.EnableResolutionQuality = false; }
        }
    }
}
