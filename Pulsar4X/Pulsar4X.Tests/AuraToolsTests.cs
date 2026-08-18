using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Orbital;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// E14 — AURAS, Phase A (Operation Blueprint-to-Steel, 2026-08-18). The PURE aura math (<see cref="AuraTools"/>) +
    /// the <see cref="AuraAtb"/> component (the buildable projector), byte-identical and NOT yet swept (the
    /// ShipMagazineAtb Phase-A pattern — the per-tick neighbour sweep is Phase B). These gauge the radius test, the
    /// linear magnitude falloff, the take-the-best-not-sum guard-rail, and the component's save-safe ctor/Clone shape.
    /// </summary>
    [TestFixture]
    public class AuraToolsTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[aura] " + m);

        [Test]
        [Description("The radius test: a unit inside the radius (and exactly at the edge) is affected; one outside is "
                     + "not; a zero-radius aura reaches nothing.")]
        public void InRange_TrueInsideAndAtEdge_FalseOutside()
        {
            var centre = new Vector3(0, 0, 0);
            Assert.That(AuraTools.InRange(centre, new Vector3(50, 0, 0), 100), Is.True, "inside the radius");
            Assert.That(AuraTools.InRange(centre, new Vector3(100, 0, 0), 100), Is.True, "exactly at the edge is in range");
            Assert.That(AuraTools.InRange(centre, new Vector3(150, 0, 0), 100), Is.False, "outside the radius");
            Assert.That(AuraTools.InRange(centre, new Vector3(60, 80, 0), 100), Is.True, "3-4-5: 100 m away, at the edge");
            Assert.That(AuraTools.InRange(centre, new Vector3(1, 0, 0), 0), Is.False, "a zero-radius aura reaches nothing");
        }

        [Test]
        [Description("The magnitude falloff: full strength at the centre, linear taper to 0 at the edge, 0 beyond.")]
        public void MagnitudeAt_FullAtCentre_ZeroAtEdge_LinearBetween()
        {
            Assert.That(AuraTools.MagnitudeAt(10, 0, 100), Is.EqualTo(10).Within(1e-9), "full strength at the centre");
            Assert.That(AuraTools.MagnitudeAt(10, 50, 100), Is.EqualTo(5).Within(1e-9), "half strength at half radius");
            Assert.That(AuraTools.MagnitudeAt(10, 100, 100), Is.EqualTo(0).Within(1e-9), "zero at the edge");
            Assert.That(AuraTools.MagnitudeAt(10, 150, 100), Is.EqualTo(0).Within(1e-9), "zero beyond the edge");
            Assert.That(AuraTools.MagnitudeAt(10, 0, 0), Is.EqualTo(0.0), "a zero-radius aura has no magnitude");
            Log("aura falloff: full at centre, 0 at edge, linear between");
        }

        [Test]
        [Description("The take-the-BEST-not-sum guard-rail: overlapping auras don't stack — the strongest single field "
                     + "wins.")]
        public void BestOf_TakesTheStrongest_NeverTheSum()
        {
            Assert.That(AuraTools.BestOf(new[] { 3.0, 7.0, 5.0 }), Is.EqualTo(7.0), "the strongest field wins (never 15)");
            Assert.That(AuraTools.BestOf(new double[0]), Is.EqualTo(0.0), "no auras → 0");
            Assert.That(AuraTools.BestOf(null), Is.EqualTo(0.0), "null → 0 (defensive)");
        }

        [Test]
        [Description("The AuraAtb component: the NCalc double-arg ctor clamps a negative radius, maps the enum args, and "
                     + "Clone deep-copies every dial (the save-safe ShipMagazineAtb shape).")]
        public void AuraAtb_Ctor_ClampsRadius_MapsEnums_ClonesDeep()
        {
            var a = new AuraAtb(500, 2.5, (double)(int)AuraEffect.Ward, (double)(int)AuraTarget.Friends);
            Assert.That(a.Radius_m, Is.EqualTo(500));
            Assert.That(a.Magnitude, Is.EqualTo(2.5));
            Assert.That(a.Effect, Is.EqualTo(AuraEffect.Ward));
            Assert.That(a.Target, Is.EqualTo(AuraTarget.Friends));

            var neg = new AuraAtb(-10, 1, (double)(int)AuraEffect.Command, (double)(int)AuraTarget.Foes);
            Assert.That(neg.Radius_m, Is.EqualTo(0), "a negative radius clamps to 0");
            Assert.That(neg.Effect, Is.EqualTo(AuraEffect.Command));
            Assert.That(neg.Target, Is.EqualTo(AuraTarget.Foes));

            var clone = (AuraAtb)a.Clone();
            Assert.That(clone.Radius_m, Is.EqualTo(a.Radius_m));
            Assert.That(clone.Magnitude, Is.EqualTo(a.Magnitude));
            Assert.That(clone.Effect, Is.EqualTo(a.Effect));
            Assert.That(clone.Target, Is.EqualTo(a.Target));
        }
    }
}
