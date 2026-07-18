using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Movement;   // PositionDB (the active class — NOT Pulsar4X.Datablobs; see Tests/CLAUDE.md namespace map)
using Pulsar4X.Orbital;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Slice S1 of the 2D group-plane resolver (docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md §13; Operation
    /// Earthfall T2.1). S0 built the pure <see cref="GroupPlane"/> math with no caller; S1 WIRES it into the space
    /// engagement: at a fight's start a battle-local 2D plane is seeded from the fleets' real 3D positions and
    /// FROZEN, each fleet's anchor is its projected point, and <see cref="CombatEngagement.AdvanceClosing"/> slides
    /// the controller's anchor along the enemy-facing direction as the fight closes.
    ///
    /// PLAIN ENGLISH — what these gauges prove:
    ///  • Flag ON: the frame + anchor a fleet gets seeded with are EXACTLY what the pure GroupPlane math produces
    ///    from the same positions (no drift between the resolver's seeding and the tested S0 math), and a fleet that
    ///    JOINS copies the SAME frozen board (it doesn't redraw its own — that's the design's "copy the frame to
    ///    joiners", the reason gaps don't jump as ships die).
    ///  • Flag OFF (the byte-identity tripwire): no plane data is EVER written — <see cref="FleetCombatStateDB.HasFrame"/>
    ///    stays false, the anchor stays Zero, the group list stays empty — so the fight is the unchanged scalar
    ///    <see cref="FleetCombatStateDB.Separation_m"/> path, exactly as before this slice existed. (The existing
    ///    <c>ClosingTests</c> — which never touch EnableGroupPlane — are the broader tripwire.)
    ///  • Anchor MOVEMENT: with closing + plane both on, the faster (controller) fleet's anchor slides toward the
    ///    enemy by exactly the amount the scalar gap changed, and the anchor-to-anchor gap tracks the scalar gap —
    ///    the 2D generalization of "slide one number" is faithful. The stationary side's anchor holds.
    ///
    /// Every fixture opts the static flags in and RESETS them in finally, so the plane never leaks into another test.
    /// </summary>
    [TestFixture]
    public class EfGroupPlaneAnchorTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[gp-anchor] " + m);

        private static Entity MakeFleet(TestScenario s, Entity faction, string name)
            => FleetFactory.Create(s.StartingSystem, faction.Id, name);

        /// <summary>Build a corvette under the player, flip its owner, assign it to the fleet, stamp a CONTROLLED
        /// combat value (one weapon of the given range; known evasion), and stamp a known absolute position so the
        /// plane seeds from real, distinct points (a fleet's position = its first ship's position, per production's
        /// <c>TryGetFleetPosition</c>).</summary>
        private static Entity AddShipAt(TestScenario s, Entity owner, Entity fleet, Vector3 pos, double range_m,
            double evasion, double firepower = 1e6, double toughness = 1e8)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns["default-ship-design-test-corvette"];
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "ship");
            ship.FactionOwnerID = owner.Id;
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(owner.Id, fleet, ship));

            var cv = new ShipCombatValueDB(firepower, toughness, 1.0);
            cv.Evasion = evasion;
            cv.Weapons = new List<WeaponProfile> { new WeaponProfile(firepower, 3e8, 1.0, 1.0, range_m) };
            ship.SetDataBlob(cv);

            ship.GetDataBlob<PositionDB>().AbsolutePosition = pos;
            return ship;
        }

        /// <summary>The real position a fleet seeds from (the same value production reads via TryGetFleetPosition).
        /// Read it BACK from the ship rather than trusting the stamped literal, so the expected math is bit-identical
        /// to what the resolver computed (a body-relative absolute round-trips through a ~1e11 m parent, which loses a
        /// hair of precision — reading back eliminates that as a source of test flake).</summary>
        private static Vector3 FleetPos(Entity ship) => ship.GetDataBlob<PositionDB>().AbsolutePosition;

        private static FleetCombatStateDB State(Entity fleet) => fleet.GetDataBlob<FleetCombatStateDB>();

        private static void AssertVec2(Vector2 actual, Vector2 expected, double tol, string msg)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(tol), msg + " (X)");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(tol), msg + " (Y)");
        }

        private static void AssertVec3(Vector3 actual, Vector3 expected, double tol, string msg)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(tol), msg + " (X)");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(tol), msg + " (Y)");
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(tol), msg + " (Z)");
        }

        // ─── Flag ON: seeding matches the pure GroupPlane math ───────────────────────────────────────────────────

        [Test]
        [Description("Flag ON: StartEngagement seeds each fleet's frozen frame + anchor to EXACTLY what GroupPlane's " +
                     "own SeedFrame/Project produce from the same real positions — the resolver's seeding and the S0 " +
                     "math cannot drift. Both fleets share ONE frozen board.")]
        public void SeedFrame_FlagOn_MatchesGroupPlaneMath()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var fleetA = MakeFleet(s, s.Faction, "A");
            var fleetB = MakeFleet(s, reds, "B");
            // Distinct, non-degenerate positions so the frame + anchors are genuinely defined.
            var shipA = AddShipAt(s, s.Faction, fleetA, new Vector3(0, 0, 0), range_m: 100_000, evasion: 0.5);
            var shipB = AddShipAt(s, reds, fleetB, new Vector3(8_000_000, 6_000_000, 0), range_m: 100_000, evasion: 0.5);

            bool prev = CombatEngagement.EnableGroupPlane;
            CombatEngagement.EnableGroupPlane = true;   // seeding is gated on THIS flag alone (independent of closing)
            try
            {
                CombatEngagement.StartEngagement(fleetA, fleetB);

                // Expected: the SAME pure math, from the positions the resolver actually read (read back from ships).
                var seeds = new List<(int, Vector3)> { (fleetA.Id, FleetPos(shipA)), (fleetB.Id, FleetPos(shipB)) };
                var expFrame = GroupPlane.SeedFrame(seeds);
                var expAnchorA = GroupPlane.Project(expFrame, FleetPos(shipA));
                var expAnchorB = GroupPlane.Project(expFrame, FleetPos(shipB));

                var a = State(fleetA);
                var b = State(fleetB);
                Log($"A anchor {a.Anchor}  B anchor {b.Anchor}  origin {a.FrameOrigin}");

                Assert.That(a.HasFrame, Is.True, "flag on → A's plane is seeded");
                Assert.That(b.HasFrame, Is.True, "flag on → B's plane is seeded");

                // Frame is identical to the pure math (a hair of tolerance for the projection arithmetic).
                AssertVec3(a.FrameOrigin, expFrame.Origin, 1e-6, "A frame origin");
                AssertVec3(a.FrameXAxis, expFrame.XAxis, 1e-9, "A frame XAxis");
                AssertVec3(a.FrameYAxis, expFrame.YAxis, 1e-9, "A frame YAxis");
                AssertVec2(a.Anchor, expAnchorA, 1e-6, "A anchor == Project(frame, A-pos)");
                AssertVec2(b.Anchor, expAnchorB, 1e-6, "B anchor == Project(frame, B-pos)");

                // Both fleets sit on ONE shared frozen board.
                AssertVec3(b.FrameOrigin, a.FrameOrigin, 0, "both fleets share the same frozen origin");
                AssertVec3(b.FrameXAxis, a.FrameXAxis, 0, "both fleets share the same frozen XAxis");
                AssertVec3(b.FrameYAxis, a.FrameYAxis, 0, "both fleets share the same frozen YAxis");

                // S1: the single whole-fleet group sits at the anchor.
                Assert.That(a.GroupPositions.Count, Is.EqualTo(1), "S1: one group = the whole fleet");
                AssertVec2(a.GroupPositions[0], a.Anchor, 0, "the group point IS the anchor at S1");
            }
            finally { CombatEngagement.EnableGroupPlane = prev; }
        }

        // ─── Flag OFF: no plane data is ever written (byte-identity tripwire) ─────────────────────────────────────

        [Test]
        [Description("Flag OFF (default): the plane is never seeded — HasFrame stays false, the anchor stays Zero, the " +
                     "group list stays empty — even across a full combat step. Proves the slice degrades EXACTLY to the " +
                     "scalar Separation_m path (the state is identical to before the plane fields existed).")]
        public void SeedFrame_FlagOff_LeavesPlaneStateAtDefault()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var fleetA = MakeFleet(s, s.Faction, "A");
            var fleetB = MakeFleet(s, reds, "B");
            AddShipAt(s, s.Faction, fleetA, new Vector3(0, 0, 0), range_m: 100_000, evasion: 0.5);
            AddShipAt(s, reds, fleetB, new Vector3(50_000, 0, 0), range_m: 100_000, evasion: 0.5);

            // EnableGroupPlane stays FALSE (default). A normal fight.
            CombatEngagement.StartEngagement(fleetA, fleetB);
            CombatEngagement.StepEngagement(fleetA, fleetB, 5.0);

            foreach (var (name, fleet) in new[] { ("A", fleetA), ("B", fleetB) })
            {
                if (!fleet.TryGetDataBlob<FleetCombatStateDB>(out var st)) continue;   // fight may have ended — nothing to check
                Log($"{name}: HasFrame={st.HasFrame} anchor={st.Anchor} groups={st.GroupPositions.Count} origin={st.FrameOrigin}");
                Assert.That(st.HasFrame, Is.False, name + ": flag off → plane not seeded");
                AssertVec2(st.Anchor, Vector2.Zero, 0, name + ": anchor stays Zero");
                AssertVec3(st.FrameOrigin, Vector3.Zero, 0, name + ": frame origin stays Zero");
                Assert.That(st.GroupPositions.Count, Is.EqualTo(0), name + ": group list stays empty");
            }
        }

        // ─── Joiner copies the FROZEN board, doesn't redraw its own ──────────────────────────────────────────────

        [Test]
        [Description("Flag ON: a fleet JOINING an existing engagement (EnsureInCombat) COPIES the frozen frame the " +
                     "first pair laid down — it does NOT reseed a fresh board from itself. Proven by the joiner's frame " +
                     "origin being bit-identical to the existing (A+B centroid) board, which differs from a would-be " +
                     "(joiner+A) reseed. This is why gaps don't jump as fleets come and go.")]
        public void Joiner_CopiesTheFrozenFrame_NotAReseed()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var fleetA = MakeFleet(s, s.Faction, "A");
            var fleetB = MakeFleet(s, reds, "B");
            var shipA = AddShipAt(s, s.Faction, fleetA, new Vector3(0, 0, 0), range_m: 100_000, evasion: 0.5);
            var shipB = AddShipAt(s, reds, fleetB, new Vector3(1_000_000, 0, 0), range_m: 100_000, evasion: 0.5);

            var fleetC = MakeFleet(s, s.Faction, "C");   // a reinforcement joining A's side
            var shipC = AddShipAt(s, s.Faction, fleetC, new Vector3(0, 4_000_000, 0), range_m: 100_000, evasion: 0.5);

            bool prev = CombatEngagement.EnableGroupPlane;
            CombatEngagement.EnableGroupPlane = true;
            try
            {
                CombatEngagement.StartEngagement(fleetA, fleetB);   // seeds the frozen A+B board
                var boardOrigin = State(fleetA).FrameOrigin;

                // The would-be reseed a joiner would produce IF it drew its own board (C + its representative opponent A).
                var reseed = GroupPlane.SeedFrame(new List<(int, Vector3)>
                    { (fleetC.Id, FleetPos(shipC)), (fleetA.Id, FleetPos(shipA)) });

                CombatEngagement.EnsureInCombat(fleetC, fleetA.Id);   // C JOINS the fight

                var c = State(fleetC);
                Log($"joiner C origin {c.FrameOrigin}  shared board {boardOrigin}  would-be reseed {reseed.Origin}");

                Assert.That(c.HasFrame, Is.True, "C's plane is seeded on join");
                // Copied the frozen board, bit-identical.
                AssertVec3(c.FrameOrigin, boardOrigin, 0, "joiner copies the FROZEN shared board");
                AssertVec3(c.FrameXAxis, State(fleetA).FrameXAxis, 0, "joiner copies the frozen XAxis");
                AssertVec3(c.FrameYAxis, State(fleetA).FrameYAxis, 0, "joiner copies the frozen YAxis");
                // ...and did NOT redraw its own (the A+B centroid is not the C+A centroid).
                Assert.That((c.FrameOrigin - reseed.Origin).Length(), Is.GreaterThan(1.0),
                    "the copied board is genuinely different from a self-reseed — proves it COPIED, not redrew");
                // Anchor is this fleet's own position projected onto the copied board.
                var expAnchorC = GroupPlane.Project(GroupPlane.SeedFrame(new List<(int, Vector3)>
                    { (fleetA.Id, FleetPos(shipA)), (fleetB.Id, FleetPos(shipB)) }), FleetPos(shipC));
                AssertVec2(c.Anchor, expAnchorC, 1e-6, "joiner's anchor = its own position on the shared board");
            }
            finally { CombatEngagement.EnableGroupPlane = prev; }
        }

        // ─── Anchor MOVEMENT: the controller slides toward the enemy by the scalar gap change ────────────────────

        [Test]
        [Description("Closing + plane ON: the faster (controller) fleet's anchor slides toward the enemy anchor by " +
                     "exactly the amount the scalar gap shrank, and the resulting anchor-to-anchor gap tracks the " +
                     "scalar Separation_m — the 2D generalization of AdvanceClosing's 'slide one number' is faithful. " +
                     "The stationary (slower) side's anchor holds.")]
        public void AdvanceClosing_FlagOn_SlidesControllerAnchorTowardEnemy()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var fast = MakeFleet(s, s.Faction, "FastShort");    // controller: fast, short-ranged → forces the merge
            var slow = MakeFleet(s, reds, "SlowLong");          // slower, long-ranged → fires across the gap, holds position
            // 50 km apart so the frozen board's pair-distance starts EQUAL to the scalar gap (they stay consistent).
            AddShipAt(s, s.Faction, fast, new Vector3(0, 0, 0), range_m: 1_000, evasion: 0.9);
            AddShipAt(s, reds, slow, new Vector3(50_000, 0, 0), range_m: 100_000, evasion: 0.1);

            bool prevPlane = CombatEngagement.EnableGroupPlane;
            bool prevClose = CombatEngagement.EnableClosingRange;
            double prevScale = CombatEngagement.ClosingSpeedScale_mps;
            CombatEngagement.EnableGroupPlane = true;
            CombatEngagement.EnableClosingRange = true;         // anchor movement only runs under closing
            CombatEngagement.ClosingSpeedScale_mps = 100_000.0;
            try
            {
                CombatEngagement.StartEngagement(fast, slow);   // seeds gap from the real ~50 km distance + the plane
                State(fast).ManeuverBudget = 1e9;               // ample budget so the kiting clock doesn't gate the merge
                State(slow).ManeuverBudget = 1e9;

                var anchorFastBefore = State(fast).Anchor;
                var anchorSlowBefore = State(slow).Anchor;
                double gapBefore = State(fast).Separation_m;
                var enemyDir = GroupPlane.EnemyDirection(anchorFastBefore,
                    new List<(int, Vector2)> { (slow.Id, anchorSlowBefore) });

                CombatEngagement.StepEngagement(fast, slow, 5.0);

                Assert.That(fast.HasDataBlob<FleetCombatStateDB>(), Is.True, "the controller is still in the fight after one step");
                double gapAfter = State(fast).Separation_m;
                double moved = gapBefore - gapAfter;
                var anchorFastAfter = State(fast).Anchor;
                Log($"gap {gapBefore:N0} -> {gapAfter:N0} (moved {moved:N0})  anchor {anchorFastBefore} -> {anchorFastAfter}");

                Assert.That(moved, Is.GreaterThan(0), "the fast short-range fleet CLOSED the gap (controller forces the merge)");

                // The anchor slid along the enemy-facing direction by exactly the scalar gap change.
                var expectedAnchor = GroupPlane.Place(anchorFastBefore, enemyDir * moved);
                AssertVec2(anchorFastAfter, expectedAnchor, 1e-3,
                    "controller anchor slid toward the enemy by the scalar gap delta");

                // The stationary side's anchor did NOT move (only the faster side closes).
                AssertVec2(State(slow).Anchor, anchorSlowBefore, 0, "the slower side's anchor holds");

                // The 2D anchor-to-anchor gap now tracks the scalar gap — the plane is a faithful generalization.
                double planeGap = GroupPlane.PairDistance(anchorFastAfter, State(slow).Anchor);
                Log($"2D anchor gap after = {planeGap:N0}  (scalar gap {gapAfter:N0})");
                Assert.That(planeGap, Is.EqualTo(gapAfter).Within(1.0),
                    "the anchor pair-distance tracks the scalar gap (seeded consistent → stays consistent)");
            }
            finally
            {
                CombatEngagement.EnableGroupPlane = prevPlane;
                CombatEngagement.EnableClosingRange = prevClose;
                CombatEngagement.ClosingSpeedScale_mps = prevScale;
            }
        }
    }
}
