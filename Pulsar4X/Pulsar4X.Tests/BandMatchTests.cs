using NUnit.Framework;
using Pulsar4X.Sensors;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Sensor BAND-MATCH FIX (docs sensorsderived.html — the "Yours to call" ruling). Pins the corrected wavelength-overlap
    /// gate against the legacy bug, both as pure functions, and guards the flag default so combat detection is byte-identical
    /// until the developer turns it on (paired with an infrared receiver, then live-verified — CI can't run detection).
    ///
    /// THE BUG: the shipped gate's RHS `Math.Max(sigMin,sigMax)` reduces to `sigMax`, so the receiver's UPPER edge is never
    /// consulted — a VISIBLE-light receiver (475–725 nm) "detects" the reactor's ~1705 nm INFRARED emission. Detection
    /// quietly depends on this leak; the CORRECT test `max(recvMin,sigMin) &lt; min(recvMax,sigMax)` closes it.
    /// </summary>
    [TestFixture]
    public class BandMatchTests
    {
        // A base-mod VISIBLE-light receiver window, and the reactor's INFRARED emission band (peak ~1705 nm).
        private const double VisMin = 475, VisMax = 725;
        private const double ReactorIrMin = 1600, ReactorIrMax = 1800;

        [Test]
        [Description("The flag defaults OFF so the live detection path uses the legacy gate → byte-identical until the developer turns the fix on.")]
        public void BandMatchFix_DefaultsOff_ByteIdentical()
        {
            Assert.That(SensorTools.EnableBandMatchFix, Is.False,
                "EnableBandMatchFix must default OFF — the fix changes combat detection and must be turned on deliberately");
        }

        [Test]
        [Description("THE BUG vs THE FIX: a visible-light receiver WRONGLY 'sees' the reactor's infrared under the legacy gate, and correctly does NOT under the fixed gate (the receiver's upper edge 725 nm is below the 1600 nm signal).")]
        public void VisibleReceiver_DoesNotSeeInfraredReactor_OnlyUnderTheFix()
        {
            // Legacy: max(475,1600)=1600 < max(1600,1800)=1800 → TRUE — the visible receiver "detects" a 1705 nm reactor (the bug).
            Assert.That(SensorTools.BandsOverlapLegacy(VisMin, VisMax, ReactorIrMin, ReactorIrMax), Is.True,
                "the legacy gate leaks: a visible receiver wrongly overlaps an infrared signal (upper edge never checked)");

            // Correct: max(475,1600)=1600 < min(725,1800)=725 → FALSE — no overlap, the honest answer.
            Assert.That(SensorTools.BandsOverlapCorrect(VisMin, VisMax, ReactorIrMin, ReactorIrMax), Is.False,
                "the fixed gate consults BOTH edges: a visible receiver does NOT overlap an infrared signal");
        }

        [Test]
        [Description("An INFRARED receiver DOES overlap the reactor's infrared band under the fixed gate — the reason the fix must ship WITH an IR receiver, or detection collapses.")]
        public void InfraredReceiver_SeesTheReactor_UnderTheFix()
        {
            // An IR receiver window that spans the reactor band.
            const double IrMin = 1000, IrMax = 2000;
            Assert.That(SensorTools.BandsOverlapCorrect(IrMin, IrMax, ReactorIrMin, ReactorIrMax), Is.True,
                "an infrared receiver correctly overlaps the reactor's infrared band → the paired IR receiver restores detection");
        }

        [Test]
        [Description("A signal that GENUINELY overlaps the receiver band is detected by BOTH gates (the fix doesn't lose real detections) — and a fully-out-of-band signal is rejected by both.")]
        public void GenuineOverlapAgrees_AndOutOfBandRejectedByBoth()
        {
            // A 500–600 nm signal sits inside the 475–725 nm visible window: a real overlap both gates catch.
            Assert.That(SensorTools.BandsOverlapLegacy(VisMin, VisMax, 500, 600), Is.True);
            Assert.That(SensorTools.BandsOverlapCorrect(VisMin, VisMax, 500, 600), Is.True,
                "the fix keeps a genuine in-band detection");

            // A 200–300 nm (ultraviolet, below the window) signal overlaps neither — both correctly reject it.
            Assert.That(SensorTools.BandsOverlapLegacy(VisMin, VisMax, 200, 300), Is.False);
            Assert.That(SensorTools.BandsOverlapCorrect(VisMin, VisMax, 200, 300), Is.False,
                "a fully out-of-band (shorter-wavelength) signal is rejected by both gates");
        }
    }
}
