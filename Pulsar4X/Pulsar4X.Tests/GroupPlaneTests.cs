using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Orbital;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Slice S0 of the 2D group-plane resolver (docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md §13).
    ///
    /// PLAIN ENGLISH: these prove the invisible "battle graph paper" math behaves. The sheet is laid down ONCE from
    /// the real 3D positions and frozen; a group's point is its fleet's spot plus a doctrine nudge; the gap between
    /// two points is a straight-line distance. The things that MUST hold: the board is the same no matter what order
    /// the fleets are handed in; a plain two-sides fight collapses to a 1-D tug-of-war (the byte-identical path); the
    /// doctrine nudge points where the trig says; a latecomer is placed with the SAME frozen board (not a redrawn
    /// one); and "which enemy is nearest" never flip-flops between two equal foes (lowest-id wins).
    ///
    /// <see cref="GroupPlane"/> is pure static math with NO live caller (S0), so this fixture is self-contained — no
    /// game, no entities, no clock.
    /// </summary>
    [TestFixture]
    public class GroupPlaneTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[groupplane] " + m);

        private static void AssertVec3(Vector3 actual, Vector3 expected, double tol, string msg)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(tol), msg + " (X)");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(tol), msg + " (Y)");
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(tol), msg + " (Z)");
        }

        // A spread-out 3D battle (not collinear) so the YAxis is genuinely defined.
        private static List<(int, Vector3)> SpreadBattle() => new List<(int, Vector3)>
        {
            (1, new Vector3(0,     0,     0)),
            (2, new Vector3(1e8,   0,     0)),
            (3, new Vector3(5e7,   8e7,   0)),
            (4, new Vector3(2e7,  -3e7,   4e7)),
        };

        /// <summary>Frame determinism: shuffling the seed list must produce a bit-identical board (the seeds are
        /// sorted by id before any arithmetic). Without this, the gaps would depend on iteration order and
        /// fast-forward could disagree with watch.</summary>
        [Test]
        public void SeedFrame_IsDeterministic_RegardlessOfInputOrder()
        {
            var inOrder = SpreadBattle();
            var shuffled = new List<(int, Vector3)> { inOrder[2], inOrder[0], inOrder[3], inOrder[1] };

            var f1 = GroupPlane.SeedFrame(inOrder);
            var f2 = GroupPlane.SeedFrame(shuffled);

            AssertVec3(f2.Origin, f1.Origin, 1e-9, "origin must not depend on input order");
            AssertVec3(f2.XAxis, f1.XAxis, 1e-12, "x-axis must not depend on input order");
            AssertVec3(f2.YAxis, f1.YAxis, 1e-12, "y-axis must not depend on input order");
            Log($"deterministic frame: origin={f1.Origin}, x={f1.XAxis}, y={f1.YAxis}");
        }

        /// <summary>The board's two axes are a proper orthonormal pair (each unit length, mutually perpendicular),
        /// so projecting onto them is a clean flatten with no scale or skew.</summary>
        [Test]
        public void SeedFrame_AxesAreOrthonormal()
        {
            var f = GroupPlane.SeedFrame(SpreadBattle());
            Assert.That(f.XAxis.Length(), Is.EqualTo(1.0).Within(1e-12), "x-axis unit length");
            Assert.That(f.YAxis.Length(), Is.EqualTo(1.0).Within(1e-12), "y-axis unit length");
            Assert.That(Vector3.Dot(f.XAxis, f.YAxis), Is.EqualTo(0.0).Within(1e-9), "axes perpendicular");
        }

        /// <summary>Two sides facing off collapse to ONE dimension: both fleets land on the x-line (v ~ 0), so the
        /// plane degrades exactly to today's single-number tug-of-war — the byte-identical path.</summary>
        [Test]
        public void SeedFrame_TwoSidesFacingOff_CollapsesToOneDimension()
        {
            var p1 = new Vector3(0,   0,   0);
            var p2 = new Vector3(6e8, 8e8, 0); // distance to p1 is exactly 1e9
            var seeds = new List<(int, Vector3)> { (1, p1), (2, p2) };

            var f = GroupPlane.SeedFrame(seeds);
            var a = GroupPlane.Project(f, p1);
            var b = GroupPlane.Project(f, p2);

            // The perpendicular (second) axis carries essentially nothing — the 1-D collapse.
            Assert.That(a.Y, Is.EqualTo(0.0).Within(1.0), "fleet A has ~0 on the second axis");
            Assert.That(b.Y, Is.EqualTo(0.0).Within(1.0), "fleet B has ~0 on the second axis");
            // The first axis carries the full separation (1e9 m between them).
            Assert.That(GroupPlane.PairDistance(a, b), Is.EqualTo(1e9).Within(1.0), "the x-axis carries the whole gap");
            Assert.That(System.Math.Abs(a.X - b.X), Is.EqualTo(1e9).Within(1.0), "separation lives on the 1-D axis");
            Log($"collapse: A=({a.X},{a.Y}) B=({b.X},{b.Y})");
        }

        /// <summary>The doctrine offset is pure trig off the enemy-facing direction: 0° pushes toward the enemy,
        /// ±90° swings to the flank, 180° drops behind; a negative standoff kites back; perpSpread fans sideways.</summary>
        [Test]
        public void RoleOffset_TrigonometryMatchesBearings()
        {
            var enemyDir = new Vector2(1, 0); // enemy is toward +x
            const double D = 1000.0;
            const double S = 500.0;

            var line = GroupPlane.RoleOffset(enemyDir, 0, D, 0);   // brawler: straight at the enemy
            Assert.That(line.X, Is.EqualTo(D).Within(1e-6), "Line pushes toward the enemy (+x)");
            Assert.That(line.Y, Is.EqualTo(0.0).Within(1e-6), "Line has no sideways component");

            var flank = GroupPlane.RoleOffset(enemyDir, 90, D, 0); // screen: 90° to the side
            Assert.That(flank.X, Is.EqualTo(0.0).Within(1e-6), "Flank swings off the enemy axis");
            Assert.That(flank.Y, Is.EqualTo(D).Within(1e-6), "Flank sits out to the +y side");

            var rear = GroupPlane.RoleOffset(enemyDir, 180, D, 0); // rear guard: behind
            Assert.That(rear.X, Is.EqualTo(-D).Within(1e-6), "Rear guard sits behind the anchor (-x)");
            Assert.That(rear.Y, Is.EqualTo(0.0).Within(1e-6), "Rear guard stays on the axis");

            var kite = GroupPlane.RoleOffset(enemyDir, 0, -D, 0);  // artillery: hangs back
            Assert.That(kite.X, Is.EqualTo(-D).Within(1e-6), "a negative standoff hangs back from the enemy");

            var spread = GroupPlane.RoleOffset(enemyDir, 0, 0, S); // pure sideways spread
            Assert.That(spread.X, Is.EqualTo(0.0).Within(1e-6), "perpSpread adds nothing along the axis");
            Assert.That(spread.Y, Is.EqualTo(S).Within(1e-6), "perpSpread fans to the side");

            var combined = GroupPlane.RoleOffset(enemyDir, 0, D, S);
            Assert.That(combined.X, Is.EqualTo(D).Within(1e-6), "along + perp compose independently (along)");
            Assert.That(combined.Y, Is.EqualTo(S).Within(1e-6), "along + perp compose independently (perp)");
        }

        /// <summary>A zero enemy-direction (no one to face) still gives a well-defined offset via a fixed default
        /// facing — the trig never divides by zero or returns NaN.</summary>
        [Test]
        public void RoleOffset_ZeroEnemyDirection_UsesDefaultFacing_NoNaN()
        {
            var o = GroupPlane.RoleOffset(Vector2.Zero, 0, 1000, 0);
            Assert.That(double.IsNaN(o.X) || double.IsNaN(o.Y), Is.False, "no NaN when there is no enemy direction");
            Assert.That(o.Length(), Is.EqualTo(1000.0).Within(1e-6), "the offset magnitude still equals the standoff");
        }

        /// <summary>A latecomer is placed with the STORED (frozen) board, not a redrawn one. Two things must hold:
        /// (1) projecting the joiner uses the frame's own origin+axes (matches the hand-computed dot products); and
        /// (2) if we had instead RE-SEEDED to include the joiner, an existing fleet's coordinate would JUMP — which
        /// is exactly why the frame is frozen (design weakness #6).</summary>
        [Test]
        public void Project_Joiner_UsesStoredFrame_NotARecomputedOne()
        {
            var initial = SpreadBattle();
            var frame = GroupPlane.SeedFrame(initial);

            var joinerPos = new Vector3(9e7, -6e7, 3e7);

            // (1) The stored-frame projection is exactly the dot products against the frozen origin + axes.
            var proj = GroupPlane.Project(frame, joinerPos);
            var rel = joinerPos - frame.Origin;
            double expectedU = Vector3.Dot(rel, frame.XAxis);
            double expectedV = Vector3.Dot(rel, frame.YAxis);
            Assert.That(proj.X, Is.EqualTo(expectedU).Within(1e-3), "joiner U is measured on the stored x-axis");
            Assert.That(proj.Y, Is.EqualTo(expectedV).Within(1e-3), "joiner V is measured on the stored y-axis");

            // (2) Re-seeding WITH the joiner shifts the board; an existing fleet's point would move. Freezing avoids that.
            var withJoiner = new List<(int, Vector3)>(initial) { (5, joinerPos) };
            var reframed = GroupPlane.SeedFrame(withJoiner);

            var fleetAPos = initial[0].Item2;
            var storedA = GroupPlane.Project(frame, fleetAPos);
            var reframedA = GroupPlane.Project(reframed, fleetAPos);
            double drift = GroupPlane.PairDistance(storedA, reframedA);
            Assert.That(drift, Is.GreaterThan(1e6),
                "re-seeding with the joiner would jump an existing fleet's coordinate — the frozen frame prevents it");
            Log($"stored-vs-reseeded drift for fleet A = {drift:e3} m (why we freeze the frame)");
        }

        /// <summary>Nearest-enemy tie-break stability: two equidistant enemies resolve to the LOWEST id, and the
        /// answer does not depend on the order they were listed in — so "which way is the enemy" can't oscillate.</summary>
        [Test]
        public void EnemyDirection_EquidistantEnemies_PickLowestId_OrderIndependent()
        {
            var anchor = Vector2.Zero;
            var e7 = (7, new Vector2(0,  1000)); // up
            var e3 = (3, new Vector2(0, -1000)); // down, same distance, lower id

            var dirA = GroupPlane.EnemyDirection(anchor, new List<(int, Vector2)> { e7, e3 });
            var dirB = GroupPlane.EnemyDirection(anchor, new List<(int, Vector2)> { e3, e7 }); // shuffled

            // Lower id (3, the downward enemy) wins in BOTH orderings.
            Assert.That(dirA.X, Is.EqualTo(0.0).Within(1e-9));
            Assert.That(dirA.Y, Is.EqualTo(-1.0).Within(1e-9), "lowest id (downward) chosen");
            Assert.That(dirB.X, Is.EqualTo(dirA.X).Within(1e-12), "tie-break is order-independent (X)");
            Assert.That(dirB.Y, Is.EqualTo(dirA.Y).Within(1e-12), "tie-break is order-independent (Y)");
        }

        /// <summary>A genuinely nearer enemy is chosen regardless of id (the tie-break only applies to ties); and the
        /// degenerate inputs (no enemies / an enemy sitting on the anchor) return a zero direction, never NaN.</summary>
        [Test]
        public void EnemyDirection_NearestWins_AndDegenerateReturnsZero()
        {
            var anchor = Vector2.Zero;
            var far = (1, new Vector2(0, 5000));   // low id, but far
            var near = (9, new Vector2(0, 1000));  // high id, but near

            var dir = GroupPlane.EnemyDirection(anchor, new List<(int, Vector2)> { far, near });
            Assert.That(dir.Y, Is.EqualTo(1.0).Within(1e-9), "the nearer enemy wins even though its id is higher");

            var none = GroupPlane.EnemyDirection(anchor, new List<(int, Vector2)>());
            Assert.That(none.X, Is.EqualTo(0.0).Within(1e-12), "no enemies → zero direction (X)");
            Assert.That(none.Y, Is.EqualTo(0.0).Within(1e-12), "no enemies → zero direction (Y)");

            var onTop = GroupPlane.EnemyDirection(anchor, new List<(int, Vector2)> { (1, Vector2.Zero) });
            Assert.That(onTop.X, Is.EqualTo(0.0).Within(1e-12), "an enemy on the anchor → zero direction, no NaN (X)");
            Assert.That(onTop.Y, Is.EqualTo(0.0).Within(1e-12), "an enemy on the anchor → zero direction, no NaN (Y)");
        }

        /// <summary>Degenerate seeding never throws and always yields a usable orthonormal frame: no seeds gives the
        /// identity board; all-coincident seeds still give unit, perpendicular axes.</summary>
        [Test]
        public void SeedFrame_DegenerateInputs_ReturnUsableFrame()
        {
            var empty = GroupPlane.SeedFrame(new List<(int, Vector3)>());
            AssertVec3(empty.Origin, Vector3.Zero, 0, "empty → origin at zero");
            AssertVec3(empty.XAxis, Vector3.UnitX, 0, "empty → x = UnitX");
            AssertVec3(empty.YAxis, Vector3.UnitY, 0, "empty → y = UnitY");

            var coincident = new List<(int, Vector3)>
            {
                (1, new Vector3(3e8, 3e8, 3e8)),
                (2, new Vector3(3e8, 3e8, 3e8)),
            };
            var f = GroupPlane.SeedFrame(coincident);
            Assert.That(f.XAxis.Length(), Is.EqualTo(1.0).Within(1e-12), "coincident seeds still yield a unit x-axis");
            Assert.That(f.YAxis.Length(), Is.EqualTo(1.0).Within(1e-12), "coincident seeds still yield a unit y-axis");
            Assert.That(Vector3.Dot(f.XAxis, f.YAxis), Is.EqualTo(0.0).Within(1e-9), "and they stay perpendicular");
        }

        /// <summary>PairDistance is a plain 2D Euclidean gap — the single scalar the plane hands the damage kernel.</summary>
        [Test]
        public void PairDistance_IsEuclidean()
        {
            Assert.That(GroupPlane.PairDistance(new Vector2(0, 0), new Vector2(3, 4)), Is.EqualTo(5.0).Within(1e-12));
            Assert.That(GroupPlane.PairDistance(new Vector2(2, 2), new Vector2(2, 2)), Is.EqualTo(0.0).Within(1e-12));
        }
    }
}
