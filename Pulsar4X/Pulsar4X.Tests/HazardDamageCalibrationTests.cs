using NUnit.Framework;
using Pulsar4X.Hazards;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge that hazard damage makes PHYSICAL sense: a star corona's heat follows real radiative flux (∝ 1/dist²)
    /// between the star's surface and the zone edge, so danger is concentrated tight against the star. A normal
    /// planetary orbit takes ZERO (it's outside the zone), the outer corona is a near-harmless warning band, and
    /// only a genuine close dive (within a few stellar radii) accumulates real damage. Pure test on the falloff
    /// curve — <see cref="SpaceHazardProcessor.ProximityIntensity"/> — no ship/clock needed.
    ///
    /// Numbers are Sol-scale: star radius ≈ 6.96e8 m, corona edge ≈ 0.12 AU (1.795e10 m), Mercury ≈ 0.39 AU (5.79e10 m).
    /// </summary>
    [TestFixture]
    public class HazardDamageCalibrationTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[hazard-calib] " + m);

        private const double SunRadius_m   = 6.96e8;
        private const double CoronaEdge_m  = 1.795e10;  // ~0.12 AU
        private const double Mercury_m     = 5.79e10;   // ~0.39 AU

        [Test]
        [Description("A ship in a normal orbit (Mercury, 0.39 AU) is OUTSIDE the ~0.12 AU corona → exactly zero " +
                     "intensity → zero accumulated damage. The developer's 'don't cook me in a normal orbit' rule.")]
        public void NormalOrbit_IsOutsideTheZone_TakesZero()
        {
            double atMercury = SpaceHazardProcessor.ProximityIntensity(Mercury_m, CoronaEdge_m, SunRadius_m);
            Log($"intensity at Mercury (0.39 AU) = {atMercury} (expect 0)");
            Assert.That(atMercury, Is.EqualTo(0.0), "a normal orbit outside the corona must take no damage");
        }

        [Test]
        [Description("Inverse-square flux: full at the star's surface, near-zero across the outer corona, climbing " +
                     "steeply only as you close on the star — far steeper than a flat/linear band, so a shallow dip " +
                     "into the outer zone barely scratches you while a genuine close dive cooks.")]
        public void CoronaIntensity_IsConcentratedNearTheStar()
        {
            double atSurface = SpaceHazardProcessor.ProximityIntensity(SunRadius_m, CoronaEdge_m, SunRadius_m);
            double halfway   = SpaceHazardProcessor.ProximityIntensity(CoronaEdge_m * 0.5, CoronaEdge_m, SunRadius_m);
            double fewRadii  = SpaceHazardProcessor.ProximityIntensity(SunRadius_m * 3.0, CoronaEdge_m, SunRadius_m);

            Log($"intensity: surface={atSurface:0.000}  3 stellar-radii={fewRadii:0.000}  halfway-in={halfway:0.0000}");

            Assert.That(atSurface, Is.EqualTo(1.0).Within(1e-6), "at the star's surface the heat is full");
            Assert.That(halfway, Is.LessThan(0.05), "across the outer corona the heat is near-zero (a warning band, not a wall)");
            Assert.That(fewRadii, Is.GreaterThan(halfway), "danger climbs steeply as you close on the star");
            // Far steeper than a linear band would be: a linear falloff is 0.5 at halfway; inverse-square is ~100x less.
            Assert.That(halfway, Is.LessThan(0.5 * 0.2), "the curve concentrates damage near the star, unlike a flat band");
        }

        [Test]
        [Description("Back-compat: a proximity hazard with NO inner radius keeps the original linear band (0.5 at " +
                     "halfway), so only the corona (which sets an inner radius) gets the inverse-square curve.")]
        public void NoInnerRadius_FallsBackToLinearBand()
        {
            double halfwayLinear = SpaceHazardProcessor.ProximityIntensity(CoronaEdge_m * 0.5, CoronaEdge_m, 0.0);
            Assert.That(halfwayLinear, Is.EqualTo(0.5).Within(1e-6), "without an inner radius the band stays linear");
        }
    }
}
