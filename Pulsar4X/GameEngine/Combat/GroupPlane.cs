using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar4X.Orbital;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// The 2D group-plane math (docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md, slice S0).
    ///
    /// PLAIN ENGLISH: picture a flat sheet of graph paper laid over a battle, measured in metres. It is NEVER
    /// drawn — it only exists as numbers. Every fighting GROUP (a sub-fleet in space, a formation on the ground)
    /// gets ONE point on that sheet, and the distance between two points decides who can shoot whom. This class is
    /// the pure arithmetic that (1) lays the sheet down once from the real 3D positions, (2) places a group's point
    /// as "where its fleet sits, plus a doctrine nudge in a direction," and (3) measures the gap between two points.
    ///
    /// It is like a chief engineer's plot table: the real ships are scattered in 3D space, but for working out "who
    /// is in range of whom" we flatten them onto one board and read straight-line gaps off it. The board is drawn
    /// ONCE at the start of the fight and frozen — you never re-draw it as ships die, or the gaps would jump around.
    ///
    /// PURE + DETERMINISTIC BY CONSTRUCTION: no random numbers, no reading the clock, no dependence on the order the
    /// inputs arrive in (the seed is sorted by entity id first). The same inputs always give the same board and the
    /// same gaps — so a battle resolves identically whether you watch it slowly or fast-forward past it.
    ///
    /// NOTHING CALLS THIS YET (slice S0). It is a stand-alone toolbox; wiring it into the space resolver is slice S1,
    /// the range gate S2, the role table S3. Because no live code invokes it, adding this file cannot change any
    /// existing behaviour — every current test is byte-for-byte unaffected.
    /// </summary>
    public static class GroupPlane
    {
        /// <summary>Numerical tolerance for "is this vector essentially zero" checks. A pure float-comparison
        /// epsilon, NOT a gameplay/balance value — battle distances are in metres (up to ~1e9), a genuine zero is
        /// exactly 0.0, so 1e-9 cleanly separates "degenerate" from "real."</summary>
        private const double Epsilon = 1e-9;

        /// <summary>
        /// The frozen board: the flat 2D sheet's origin (the battle's centre) and its two in-plane axes, expressed
        /// as 3D unit vectors. A real 3D position <c>P</c> lands on the sheet at
        /// <c>(u, v) = (Dot(P-Origin, XAxis), Dot(P-Origin, YAxis))</c> — see <see cref="Project"/>. Seeded ONCE at
        /// the start of a battle by <see cref="SeedFrame"/> and then never recomputed; latecomers are placed with
        /// this same stored frame so distances stay consistent as the fight develops.
        /// </summary>
        public readonly struct BattleFrame
        {
            /// <summary>The centre of the battle (centroid of the seed positions), in real 3D metres.</summary>
            public readonly Vector3 Origin;
            /// <summary>First in-plane axis (unit). By rule it points from the lowest-entity-id seed toward the centre.</summary>
            public readonly Vector3 XAxis;
            /// <summary>Second in-plane axis (unit), orthogonal to <see cref="XAxis"/>. Chosen along the battle's
            /// widest spread perpendicular to XAxis; for a plain two-sides-facing-off fight there is no such spread,
            /// so it is a deterministic perpendicular and every point's v-coordinate comes out ~0 (the 1-D collapse).</summary>
            public readonly Vector3 YAxis;

            public BattleFrame(Vector3 origin, Vector3 xAxis, Vector3 yAxis)
            {
                Origin = origin;
                XAxis = xAxis;
                YAxis = yAxis;
            }
        }

        /// <summary>
        /// Lay the board down ONCE from the fighters' real 3D positions. Deterministic basis rule:
        ///   • Origin = the centroid (average) of all seed positions.
        ///   • XAxis  = direction from the LOWEST-entity-id seed toward the centre (the "lowest-id rule").
        ///   • YAxis  = the direction, perpendicular to XAxis, along which the battle spreads WIDEST (id tie-break);
        ///              if nothing spreads off the X line (the ordinary two-sides case, all points collinear with
        ///              the centre), a deterministic perpendicular is chosen and every v-coordinate collapses to ~0.
        ///
        /// DEGENERATE FALLBACKS (never throws, always returns an orthonormal-ish frame):
        ///   • no seeds                → Origin 0, XAxis = UnitX, YAxis = UnitY.
        ///   • lowest-id seed AT centre → fall back to the farthest-from-centre seed (id tie-break) for XAxis;
        ///                                if ALL seeds coincide, XAxis = UnitX.
        ///   • no perpendicular spread  → YAxis = <see cref="AnyPerpendicular"/>(XAxis).
        ///
        /// ORDER-INDEPENDENT: the seeds are sorted by id before ANY arithmetic (including the centroid sum), so a
        /// shuffled input list produces a bit-identical frame. This is what makes the board reproducible.
        /// </summary>
        /// <param name="seeds">(entity id, real 3D position) for each fleet/body present when the battle starts.</param>
        public static BattleFrame SeedFrame(IReadOnlyList<(int Id, Vector3 Position)> seeds)
        {
            if (seeds == null || seeds.Count == 0)
                return new BattleFrame(Vector3.Zero, Vector3.UnitX, Vector3.UnitY);

            // Sort by id so every step below is independent of the caller's ordering.
            var ordered = seeds.OrderBy(s => s.Id).ToList();

            // Origin = centroid, summed in id order for full determinism.
            Vector3 sum = Vector3.Zero;
            foreach (var s in ordered)
                sum += s.Position;
            Vector3 origin = sum / ordered.Count;

            // XAxis: from the lowest-id seed toward the centre.
            Vector3 xdir = origin - ordered[0].Position;
            if (xdir.Length() < Epsilon)
            {
                // Lowest-id seed sits on the centre: use the farthest-from-centre seed (id tie-break via id order).
                Vector3 farRel = Vector3.Zero;
                double farLen = 0.0;
                foreach (var s in ordered)
                {
                    Vector3 rel = s.Position - origin;
                    double l = rel.Length();
                    if (l > farLen + Epsilon)
                    {
                        farLen = l;
                        farRel = rel;
                    }
                }
                xdir = farLen < Epsilon ? Vector3.UnitX : farRel; // all coincident → arbitrary but fixed axis
            }
            Vector3 xAxis = Vector3.Normalise(xdir);

            // YAxis: the widest spread perpendicular to XAxis (id tie-break via id order — first/lowest wins on a tie).
            Vector3 bestPerp = Vector3.Zero;
            double bestLen = 0.0;
            foreach (var s in ordered)
            {
                Vector3 rel = s.Position - origin;
                Vector3 perp = rel - Vector3.Dot(rel, xAxis) * xAxis; // strip the XAxis component
                double l = perp.Length();
                if (l > bestLen + Epsilon)
                {
                    bestLen = l;
                    bestPerp = perp;
                }
            }

            Vector3 ydir;
            if (bestLen < Epsilon)
            {
                // No spread off the X line: the two-sides-facing-off case → any fixed perpendicular; all v ~0.
                ydir = AnyPerpendicular(xAxis);
            }
            else
            {
                // Re-orthogonalise against XAxis (bestPerp is already perpendicular by construction; be safe).
                ydir = bestPerp - Vector3.Dot(bestPerp, xAxis) * xAxis;
                if (ydir.Length() < Epsilon)
                    ydir = AnyPerpendicular(xAxis);
            }
            Vector3 yAxis = Vector3.Normalise(ydir);

            return new BattleFrame(origin, xAxis, yAxis);
        }

        /// <summary>
        /// Flatten a real 3D position onto the frozen board, giving its 2D point (u, v) in metres. A JOINER that
        /// arrives mid-battle is placed with this SAME stored frame — never a freshly-recomputed one — so its gaps
        /// to everyone else are measured on the same graph paper.
        /// </summary>
        public static Vector2 Project(BattleFrame frame, Vector3 position)
        {
            Vector3 rel = position - frame.Origin;
            double u = Vector3.Dot(rel, frame.XAxis);
            double v = Vector3.Dot(rel, frame.YAxis);
            return new Vector2(u, v);
        }

        /// <summary>
        /// The deterministic "which way is the enemy" direction: a unit vector on the board pointing from a group's
        /// anchor toward the NEAREST enemy group's anchor. Ties (two enemies equally close) are broken by LOWEST
        /// entity id, so the direction can't flip-flop between two equidistant foes and can't depend on input order.
        /// Returns <see cref="Vector2.Zero"/> when there are no enemies or the nearest one sits exactly on the anchor.
        /// </summary>
        /// <param name="anchor">the group's own anchor point on the board.</param>
        /// <param name="enemies">(enemy group id, enemy anchor point) for every hostile group.</param>
        public static Vector2 EnemyDirection(Vector2 anchor, IReadOnlyList<(int Id, Vector2 Anchor)> enemies)
        {
            if (enemies == null || enemies.Count == 0)
                return Vector2.Zero;

            bool found = false;
            double bestD2 = double.PositiveInfinity;
            int bestId = int.MaxValue;
            Vector2 bestTo = Vector2.Zero;

            foreach (var e in enemies)
            {
                Vector2 to = e.Anchor - anchor;
                double d2 = to.LengthSquared();
                if (!found)
                {
                    found = true;
                    bestD2 = d2;
                    bestId = e.Id;
                    bestTo = to;
                    continue;
                }

                // Relative tolerance so genuinely-equal distances tie regardless of tiny float noise.
                double tol = 1e-6 * Math.Max(Math.Max(d2, bestD2), 1.0);
                if (d2 < bestD2 - tol)
                {
                    bestD2 = d2;
                    bestId = e.Id;
                    bestTo = to;
                }
                else if (d2 <= bestD2 + tol && e.Id < bestId)
                {
                    // Equidistant → the lower id wins (the anti-oscillation tie-break).
                    bestD2 = Math.Min(bestD2, d2);
                    bestId = e.Id;
                    bestTo = to;
                }
            }

            return bestTo.Length() < Epsilon ? Vector2.Zero : Vector2.Normalise(bestTo);
        }

        /// <summary>
        /// A group's doctrine NUDGE off its anchor, as pure trigonometry — no coordinate the player ever touches.
        /// The nudge is built relative to <paramref name="enemyDir"/> (which way the enemy is):
        ///   • <paramref name="bearingDeg"/>   — the role's primary heading relative to the enemy: 0° = straight AT
        ///     the enemy (a brawler/Line), ±90° = out to the side (a flank/Screen), 180° = behind (a rear guard).
        ///   • <paramref name="alongStandoff"/> — how far to travel along that heading. POSITIVE closes toward the
        ///     enemy (Line pushes in); NEGATIVE hangs back (Artillery kites at its own longest range).
        ///   • <paramref name="perpSpread"/>    — a sideways spread PERPENDICULAR to the heading, so several groups
        ///     sharing one role don't stack on the exact same point.
        ///
        /// Result is the offset VECTOR on the board; the group's point is <c>anchor + this</c> (see <see cref="Place"/>).
        /// If <paramref name="enemyDir"/> is zero (no enemy to face) a fixed default facing is used so the trig is
        /// still well-defined.
        /// </summary>
        public static Vector2 RoleOffset(Vector2 enemyDir, double bearingDeg, double alongStandoff, double perpSpread)
        {
            // Face the enemy; if there's no enemy direction, use a fixed default so this never divides by zero.
            Vector2 facing = enemyDir.Length() < Epsilon ? new Vector2(1, 0) : Vector2.Normalise(enemyDir);

            double rad = bearingDeg * Math.PI / 180.0;
            Vector2 primary = Rotate(facing, rad);       // the role's heading
            Vector2 perp = Rotate(primary, Math.PI / 2.0); // 90° left of the heading

            return primary * alongStandoff + perp * perpSpread;
        }

        /// <summary>A group's point on the board = its anchor plus its role offset. Trivial, but names the concept.</summary>
        public static Vector2 Place(Vector2 anchor, Vector2 offset) => anchor + offset;

        /// <summary>The straight-line 2D gap between two group points, in metres. This single scalar is the ONLY
        /// thing the plane ever hands to the (unchanged, 1-D) damage kernel — the range gate compares it to weapon
        /// reach.</summary>
        public static double PairDistance(Vector2 a, Vector2 b) => Vector2.Distance(a, b);

        /// <summary>Rotate a 2D vector counter-clockwise by <paramref name="radians"/>.</summary>
        private static Vector2 Rotate(Vector2 v, double radians)
        {
            double c = Math.Cos(radians);
            double s = Math.Sin(radians);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        /// <summary>
        /// A deterministic unit vector perpendicular to <paramref name="axis"/> — used for the YAxis when a battle
        /// has no spread of its own (the two-sides case). Picks the world axis LEAST aligned with <paramref
        /// name="axis"/> and crosses with it, so the result is always well-conditioned and reproducible.
        /// </summary>
        private static Vector3 AnyPerpendicular(Vector3 axis)
        {
            Vector3 a = Vector3.Normalise(axis);
            double ax = Math.Abs(a.X);
            double ay = Math.Abs(a.Y);
            double az = Math.Abs(a.Z);

            // The world axis with the smallest projection onto 'a' is the most orthogonal to it.
            Vector3 seed;
            if (ax <= ay && ax <= az) seed = Vector3.UnitX;
            else if (ay <= az) seed = Vector3.UnitY;
            else seed = Vector3.UnitZ;

            Vector3 perp = Vector3.Cross(a, seed);
            if (perp.Length() < Epsilon)
            {
                // Extremely defensive: 'a' was parallel to the chosen seed. Try the other world axes.
                perp = Vector3.Cross(a, Vector3.UnitY);
                if (perp.Length() < Epsilon)
                    perp = Vector3.Cross(a, Vector3.UnitX);
            }
            return Vector3.Normalise(perp);
        }
    }
}
