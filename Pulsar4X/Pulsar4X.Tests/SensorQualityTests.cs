using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Sensors;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge on <see cref="SensorTools.DetectonQuality"/> — the <c>SignalQuality</c> value (0..1, "how well
    /// resolved is this contact"). It is NOT cosmetic: planet/star SURVEY reveal gates on it
    /// (<c>SystemBodyInfoDB</c>/<c>StarInfoDB</c> read it against 0.20 for body type / 0.80 for tectonics &amp;
    /// star detail), so a wrong quality value silently breaks what a survey tells you.
    ///
    /// This guards the Phase-0 fix for a long-standing overflow bug: the old formula built quality on a 0..100
    /// scale and passed it to <c>PercentValue</c>, which stores <c>value * 255</c> in a <c>byte</c> — so a ~100
    /// value overflowed the byte and WRAPPED, making quality effectively random (and survey reveal random with
    /// it). After the fix, quality is a true 0..1 fraction: a well-tuned signal scores ~1, an off-band one lower.
    ///
    /// Pure unit test — <c>DetectonQuality</c> is a static function of (receiver, signal), so no game/harness is
    /// needed.
    /// </summary>
    [TestFixture]
    public class SensorQualityTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[sensor-quality] " + m);

        // A receiver tuned to a 500 nm peak with a wide 400 nm band (detectable ~300..700 nm), sensitive enough
        // (1 W best-threshold) to pick up the strong signals below.
        private static SensorReceiverAtb TunedReceiver()
            => new SensorReceiverAtb(peakWaveLength: 500, bandwidth: 400, bestSensitivity: 1, worstSensitivity: 100, resolution: 1, scanTime: 5);

        // One signal band: a triangle wave (min, avg/peak, max), loud enough (10 kW) to clear the receiver threshold.
        private static Dictionary<EMWaveForm, double> Signal(double min, double avg, double max, double magnitude_kW = 10.0)
            => new Dictionary<EMWaveForm, double> { { new EMWaveForm(min, avg, max), magnitude_kW } };

        [Test]
        [Description("A strong signal whose peak sits exactly on the receiver's tuned wavelength resolves at FULL " +
                     "quality (1.0). Before the fix this overflowed PercentValue's byte and came out ~0.74 — this " +
                     "is the regression guard that a perfect signal no longer reads as a wrapped-byte artifact.")]
        public void PerfectlyTunedSignal_ResolvesAtFullQuality()
        {
            var receiver = TunedReceiver();
            // Signal peak (500) == receiver peak (500): zero mis-tune ⇒ quality should be 1.0.
            float q = SensorTools.DetectonQuality(receiver, Signal(300, 500, 700)).SignalQuality;
            Log($"perfectly-tuned quality = {q} (expect 1.0)");
            Assert.That(q, Is.EqualTo(1.0f).Within(0.01f),
                "a signal centred on the receiver's tuned wavelength must resolve at full quality, not a byte-overflow artifact");
        }

        [Test]
        [Description("SignalQuality is always a real 0..1 fraction AND rises with alignment: a centred signal " +
                     "out-resolves an off-band one. Guards both the range (no byte overflow) and the meaning " +
                     "(better-tuned ⇒ higher quality) that survey reveal depends on.")]
        public void Quality_IsZeroToOne_AndRisesWithAlignment()
        {
            var receiver = TunedReceiver();

            float aligned = SensorTools.DetectonQuality(receiver, Signal(300, 500, 700)).SignalQuality;  // peak on-tune
            float offBand = SensorTools.DetectonQuality(receiver, Signal(600, 650, 700)).SignalQuality;  // peak off toward the band edge

            Log($"aligned = {aligned}, off-band = {offBand}");

            foreach (var q in new[] { aligned, offBand })
                Assert.That(q, Is.InRange(0.0f, 1.0f), "SignalQuality must be a 0..1 fraction (no byte overflow)");

            Assert.That(aligned, Is.GreaterThan(offBand),
                "a better-tuned signal must resolve at higher quality than an off-band one");
        }
    }
}
