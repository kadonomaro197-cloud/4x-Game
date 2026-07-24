using NUnit.Framework;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Mini-hex tactical grid — M1 (docs/ground/GROUND-SURFACE-MAP-DESIGN.md Layer 5). Gauges the pure continuous-position
    /// math that makes a unit's coarse global hex + its mini-hex offset into ONE real position, so distance is measured
    /// across coarse-hex boundaries (the developer's "transitional" continuity) — the ground echo of space's
    /// Separation_m. ADDITIVE + UNREAD by the resolver → live combat byte-identical; M2 flips the range gate to read
    /// GroundMiniHex.RealGapMetres.
    /// </summary>
    [TestFixture]
    public class GroundMiniHexTests
    {
        private const double CoarsePitchKm = 477.0;   // ~Earth global (coarse) hex pitch
        private const int R = 6;                        // CityGridFactory.CityPatchRadius (13 mini-hexes across a coarse hex)

        [Test]
        [Description("A mini-hex is coarsePitch / (2r+1) — ~37 km under a ~477 km Earth coarse hex (the 10-50 km target). Degenerate pitch → 0, no divide-by-zero.")]
        public void MiniPitch_IsCoarseOverAcross()
        {
            Assert.That(GroundMiniHex.MiniPitchKm(CoarsePitchKm, R), Is.EqualTo(CoarsePitchKm / 13.0).Within(1e-9));
            Assert.That(GroundMiniHex.MiniPitchKm(CoarsePitchKm, R), Is.EqualTo(36.69).Within(0.05), "~37 km/mini-hex on Earth");
            Assert.That(GroundMiniHex.MiniPitchKm(0.0, R), Is.EqualTo(0.0), "no pitch → 0 (no divide-by-zero)");
            Assert.That(GroundMiniHex.MiniPitchKm(CoarsePitchKm, 0), Is.EqualTo(CoarsePitchKm / 3.0).Within(1e-9), "radius clamps to >=1 → 3 across");
        }

        [Test]
        [Description("Two units in the SAME coarse hex, N mini-hexes apart along an axis, read a gap of N × mini-pitch — the fine tactical resolution.")]
        public void SameCoarseHex_GapIsMiniHexDistanceTimesMiniPitch()
        {
            double mini_m = GroundMiniHex.MiniPitchKm(CoarsePitchKm, R) * 1000.0;
            double gap_m = GroundMiniHex.RealGapMetres(0, 0, 0, 0,   0, 0, 2, 0,   CoarsePitchKm, R);
            Assert.That(gap_m, Is.EqualTo(2 * mini_m).Within(1e-3), "2 mini-hexes apart in one coarse hex = 2 × mini-pitch");
        }

        [Test]
        [Description("THE transitional-continuity gauge: two units at the shared EDGE of ADJACENT coarse hexes are physically ~1 mini-hex apart, NOT a full coarse hex — so they can see + engage across the border, with no wall. And that edge gap is far smaller than if both sat at their coarse-hex centres.")]
        public void AdjacentCoarseHexEdges_ReadASmallGap_NotAWall()
        {
            double mini_m = GroundMiniHex.MiniPitchKm(CoarsePitchKm, R) * 1000.0;
            double coarse_m = CoarsePitchKm * 1000.0;

            // A at the EAST edge of coarse hex (0,0); B at the WEST edge of its east-neighbour hex (1,0).
            double edgeGap_m = GroundMiniHex.RealGapMetres(0, 0, +R, 0,   1, 0, -R, 0,   CoarsePitchKm, R);
            Assert.That(edgeGap_m, Is.LessThan(2 * mini_m),
                "two units at a shared coarse-hex edge are ~1 mini-hex apart (transitional continuity), not a wall");
            Assert.That(edgeGap_m, Is.LessThan(0.2 * coarse_m),
                "the gap is a small fraction of a coarse hex — the coarse boundary is not a wall");

            // If instead both sat at their coarse-hex CENTRES, they'd be a full coarse hex apart.
            double centreGap_m = GroundMiniHex.RealGapMetres(0, 0, 0, 0,   1, 0, 0, 0,   CoarsePitchKm, R);
            Assert.That(centreGap_m, Is.EqualTo(coarse_m).Within(1.0), "coarse-hex centres one apart = one coarse pitch");
            Assert.That(edgeGap_m, Is.LessThan(centreGap_m), "standing at the touching edges is closer than standing at the centres");
        }

        [Test]
        [Description("K2 — the sub-mini-hex OFFSET makes the field CONTINUOUS below the mini-tile: two units in the SAME coarse hex AND the same mini-hex, but with different real km offsets, read a gap of EXACTLY the offset difference (× 1000 m/km). This is why a real conventional weapon range (< a ~37 km mini-tile) can decide the fight. Offset (0,0) on both → byte-identical to the offset-free gap. docs/combat/REAL-DISTANCE-COMBAT-DESIGN.md.")]
        public void SubMiniOffset_ShiftsRealGap_ByExactlyTheOffset()
        {
            // Same coarse hex (0,0), same mini-hex (0,0); A offset 0, B offset +5 km east → gap exactly 5 km.
            double gap_m = GroundMiniHex.RealGapMetres(0, 0, 0, 0, 0.0, 0.0,
                                                       0, 0, 0, 0, 5.0, 0.0,
                                                       CoarsePitchKm, R);
            Assert.That(gap_m, Is.EqualTo(5000.0).Within(1e-6), "a 5 km east offset within one mini-hex reads a 5 km real gap");

            // A pure-Y offset reads the same (distance is isotropic).
            double gapY_m = GroundMiniHex.RealGapMetres(0, 0, 0, 0, 0.0, 0.0,
                                                        0, 0, 0, 0, 0.0, 3.0,
                                                        CoarsePitchKm, R);
            Assert.That(gapY_m, Is.EqualTo(3000.0).Within(1e-6), "a 3 km north offset reads a 3 km real gap");

            // Offsets (0,0) on both → byte-identical to the offset-free overload (the additive/byte-identity guarantee).
            double withZero = GroundMiniHex.RealGapMetres(0, 0, 2, 0, 0.0, 0.0,
                                                          0, 0, 0, 0, 0.0, 0.0,
                                                          CoarsePitchKm, R);
            double without = GroundMiniHex.RealGapMetres(0, 0, 2, 0,   0, 0, 0, 0,   CoarsePitchKm, R);
            Assert.That(withZero, Is.EqualTo(without).Within(1e-9), "zero offsets → the offset overload equals the offset-free one");

            // The ContinuousPos offset overload just adds the offset to the offset-free position.
            var (bx, by) = GroundMiniHex.ContinuousPosKm(0, 0, 0, 0, 7.0, 2.0, CoarsePitchKm, R);
            var (px, py) = GroundMiniHex.ContinuousPosKm(0, 0, 0, 0, CoarsePitchKm, R);
            Assert.That(bx - px, Is.EqualTo(7.0).Within(1e-9));
            Assert.That(by - py, Is.EqualTo(2.0).Within(1e-9));
        }

        [Test]
        [Description("K2 save-safety — a GroundUnit deep-copies its sub-mini-hex offset (MiniOffX_km/MiniOffY_km) through the copy-ctor, so a save/manager-move keeps a unit's exact continuous position.")]
        public void GroundUnit_DeepCopies_TheSubMiniOffset()
        {
            var u = new GroundUnit { MiniQ = 1, MiniR = 0, MiniOffX_km = 12.5, MiniOffY_km = -3.25 };
            var clone = new GroundUnit(u);
            Assert.That(clone.MiniOffX_km, Is.EqualTo(12.5), "the copy-ctor carries MiniOffX_km");
            Assert.That(clone.MiniOffY_km, Is.EqualTo(-3.25), "the copy-ctor carries MiniOffY_km");
            Assert.That(clone.MiniQ, Is.EqualTo(1));
        }
    }
}
