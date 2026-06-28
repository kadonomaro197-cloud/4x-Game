using NUnit.Framework;
using Pulsar4X.Damage;
using Pulsar4X.Hazards;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge on the KEYSTONE — the coarse <see cref="DamageSignature"/> vocabulary that lets hazards and weapons
    /// speak one shared "damage flavour" language (so one piece of armour resists a flavour from BOTH). This first
    /// slice only DEFINES the vocabulary and proves it ALIGNS with what already exists (the three hazard damage
    /// kinds + the armour wavelength bands); no damage path or weapon behaviour changes yet. Pure static logic —
    /// no game/harness needed.
    /// </summary>
    [TestFixture]
    public class DamageSignatureTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[damage-signature] " + m);

        [Test]
        [Description("The keystone maps onto the existing hazard effect kinds: the three DAMAGE kinds each carry a " +
                     "signature (Heat→Thermal, Radiation→HardRadiation, Kinetic→Kinetic); the three STAT kinds " +
                     "(SensorJam/Drag/WarpInhibit) carry none — they aren't damage.")]
        public void HazardEffect_DamageKinds_CarryASignature_StatKindsDoNot()
        {
            Assert.That(new HazardEffect(HazardEffectType.HeatDamage, 1).Signature, Is.EqualTo(DamageSignature.Thermal));
            Assert.That(new HazardEffect(HazardEffectType.RadiationDamage, 1).Signature, Is.EqualTo(DamageSignature.HardRadiation));
            Assert.That(new HazardEffect(HazardEffectType.KineticDamage, 1).Signature, Is.EqualTo(DamageSignature.Kinetic));

            foreach (var stat in new[] { HazardEffectType.SensorJam, HazardEffectType.MovementDrag, HazardEffectType.WarpInhibit })
                Assert.That(new HazardEffect(stat, 1).Signature, Is.Null, $"{stat} is a stat effect, not damage → no signature");
        }

        [Test]
        [Description("UsesWavelengthArmorPath splits the six signatures: the three that already deposit through the " +
                     "wavelength-armour sim (Thermal/HardRadiation/Kinetic) vs the three with no wavelength that " +
                     "still need their own application site built (EMStorm/Gravimetric/Corrosive) — the build-plan gap.")]
        public void Signatures_SplitInto_WavelengthPath_vs_NeedsOwnSite()
        {
            foreach (var s in new[] { DamageSignature.Thermal, DamageSignature.HardRadiation, DamageSignature.Kinetic })
                Assert.That(DamageSignatures.UsesWavelengthArmorPath(s), Is.True, $"{s} already deposits through the wavelength-armour path");
            foreach (var s in new[] { DamageSignature.EMStorm, DamageSignature.Gravimetric, DamageSignature.Corrosive })
                Assert.That(DamageSignatures.UsesWavelengthArmorPath(s), Is.False, $"{s} has no wavelength → needs its own damage site");
        }

        [Test]
        [Description("Representative wavelengths land each wavelength-based signature in the right armour band " +
                     "(HardRadiation < 400 nm = UV; Thermal ≥ 5000 nm = far-IR; Kinetic = 0, the kinetic " +
                     "convention); the three non-wavelength signatures return -1 (no photon).")]
        public void RepresentativeWavelengths_AlignWithArmorBands()
        {
            Assert.That(DamageSignatures.RepresentativeWavelength_nm(DamageSignature.HardRadiation), Is.LessThan(400), "hard radiation = UV band");
            Assert.That(DamageSignatures.RepresentativeWavelength_nm(DamageSignature.Thermal), Is.GreaterThanOrEqualTo(5000), "thermal = far-IR band");
            Assert.That(DamageSignatures.RepresentativeWavelength_nm(DamageSignature.Kinetic), Is.EqualTo(0.0), "kinetic uses the wavelength-0 convention");
            foreach (var s in new[] { DamageSignature.EMStorm, DamageSignature.Gravimetric, DamageSignature.Corrosive })
                Assert.That(DamageSignatures.RepresentativeWavelength_nm(s), Is.LessThan(0), $"{s} has no wavelength");
            Log("alignment ok: HardRadiation→UV, Thermal→FIR, Kinetic→0, EMStorm/Gravimetric/Corrosive→none");
        }
    }
}
