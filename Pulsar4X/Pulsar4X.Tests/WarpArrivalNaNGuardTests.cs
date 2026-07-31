using System;
using NUnit.Framework;
using Pulsar4X.Orbital;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The warp-arrival "Speed Result is NaN" [FATAL] guard (2026-07-23, from a committed game_logs/ crash trace).
    /// A fleet "move to body" warp arrives CO-LOCATED with its target: WarpMovingDB.ExitPointrelative is a literal
    /// (0,0,0). Under StrictNewtonion (the default) SetOrbitHereSimpleNewt fed that zero position into
    /// KeplerFromPositionAndVelocity, whose SemiMajorAxis then collapsed to +0; vis-viva downstream computed
    /// |2/0 - 1/0| = NaN and THREW. Because that ran on the background sim thread, the throw was an UNOBSERVED
    /// [FATAL] that killed the game clock (it fired 7+ times in the developer's play session — the same failure
    /// shape as the 2026-07-04 0-speed-warp [FATAL], Movement/CLAUDE.md gotcha #5).
    ///
    /// Two-layer fix, both gauged here at the math level (pure — no colony harness, so it always runs in CI):
    ///   (1) PRIMARY (WarpMoveProcessor.SetOrbitHereSimpleNewt): a literal-zero / degenerate-SMA orbit is detected
    ///       and the ship settles into a clean circular orbit instead. This test verifies the CHECK fires on
    ///       exactly the zero-position crash geometry.
    ///   (2) DEFENSIVE (OrbitalMath): the two throw sites in the crash trace now BAIL to a finite value instead of
    ///       throwing, so a degenerate orbit can never again become a clock-killing [FATAL].
    /// Before the fix InstantaneousOrbitalSpeed(sgp, 0, 0) THREW; after, it returns finite — red-before/green-after.
    /// </summary>
    [TestFixture]
    public class WarpArrivalNaNGuardTests
    {
        static readonly DateTime Epoch = new DateTime(2050, 1, 1);

        [Test]
        [Description("The degenerate-orbit math primitives no longer throw the clock-killing [FATAL]; the warp " +
                     "fallback's degenerate-orbit CHECK fires on the exact zero-position crash geometry.")]
        public void DegenerateWarpArrivalGeometry_DoesNotThrowNaN_AndIsFlaggedDegenerate()
        {
            // (2) DEFENSIVE GUARD — the exact throw site in the crash trace (OrbitalMath.InstantaneousOrbitalSpeed).
            // distance AND semiMajAxis both 0 -> |2/0 - 1/0| = NaN. It must now return a finite value, not throw
            // (a throw here becomes an unobserved [FATAL] on the sim thread that stops the clock).
            Assert.DoesNotThrow(() => OrbitalMath.InstantaneousOrbitalSpeed(1e14, 0.0, 0.0),
                "vis-viva on a zero-radius/zero-SMA orbit must not throw");
            double spd = OrbitalMath.InstantaneousOrbitalSpeed(1e14, 0.0, 0.0);
            Assert.That(double.IsFinite(spd), Is.True, "degenerate orbital speed must bail to a finite value, not NaN/Inf");

            // The literal-zero warp exit: KeplerFromPositionAndVelocity(sgp, 0, 0) is exactly what
            // SetOrbitHereSimpleNewt fed on a co-located arrival. It must produce a DEGENERATE orbit (SMA collapsed
            // to +0 / non-finite) — which is precisely what the primary fix's CHECK keys on to divert into the
            // clean circular fallback (SetOrbitHereNoNewt) instead of building a crashing NewtonSimpleMoveDB.
            double sgp = GeneralMath.StandardGravitationalParameter(5.97e24); // ~Earth's mass
            KeplerElements ke = OrbitalMath.KeplerFromPositionAndVelocity(sgp, new Vector3(), new Vector3(), Epoch);
            bool degenerate = ke.SemiMajorAxis == 0
                              || !double.IsFinite(ke.SemiMajorAxis)
                              || !double.IsFinite(ke.Eccentricity);
            Assert.That(degenerate, Is.True,
                "a zero-position Kepler solve must read as a degenerate orbit — the geometry the warp fallback diverts");

            // (2) DEFENSIVE GUARD, second site (OrbitalMath.GetStateVectors): on that degenerate orbit a zero radius
            // yields a NaN heading -> NaN velocity, which also used to throw. It must now bail to zero velocity.
            Assert.DoesNotThrow(() => OrbitalMath.GetStateVectors(ke, Epoch),
                "state vectors of a degenerate orbit must not throw (the second [FATAL] site in the crash trace)");
        }
    }
}
