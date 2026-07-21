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
    /// Slice S2 of the 2D group-plane resolver (docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md §13 S2; Operation
    /// Earthfall T3.1). S1 laid the frozen plane down and gave each fleet an anchor; S2 makes the RANGE GATE read
    /// that plane: with <see cref="CombatEngagement.EnableGroupPlane"/> on, <c>SeparationOf</c> and
    /// <c>WithinWeaponRange</c> measure the straight-line 2D distance between two fleets' group anchors — the real
    /// per-fleet-pair gap — instead of the one shared scalar. The damage kernel is UNCHANGED and still 1-D: the plane
    /// only supplies the single scalar <c>d</c> it reads.
    ///
    /// PLAIN ENGLISH — what these gauges prove:
    ///  • THE DIRECTED GATE (the design's headline): on the plane, a LONG-range group fires a SHORT-range group that
    ///    cannot answer — the long gun reaches the pair-distance, the short one doesn't. The test rigs the SCALAR gap
    ///    to a value at which the short gun WOULD fire, then shows it still can't — proving the resolver read the 2D
    ///    ANCHOR distance, not the scalar (the whole point of S2).
    ///  • FLAG OFF (byte-identity tripwire): with the plane off the SAME geometry lets the short gun fire (the scalar
    ///    drives the gate, exactly as before this slice existed) — the opposite outcome, so the flag genuinely gates.
    ///  • WithinWeaponRange reads the anchors: moving a framed fleet's anchor flips the gate while the real 3D
    ///    separation is unchanged — the gate measures the plane, not the ships' physical positions.
    ///  • Plane on but CLOSING off: <c>SeparationOf</c> stays 0 (the range gate is a no-op) even though the plane is
    ///    seeded — so turning the plane on without the closing model can never activate range gating (byte-identical).
    ///
    /// Every fixture opts the static flags in and RESETS them in finally, so the plane never leaks into another test.
    /// </summary>
    [TestFixture]
    public class EfGroupPlaneRangeGateTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[gp-rangegate] " + m);

        private static Entity MakeFleet(TestScenario s, Entity faction, string name)
            => FleetFactory.Create(s.StartingSystem, faction.Id, name);

        /// <summary>Build a corvette under the player, flip its owner, assign it to the fleet, stamp a CONTROLLED
        /// combat value (one weapon of the given range; known evasion), and stamp a known absolute position so the
        /// plane seeds from real, distinct points (a fleet's position = its first ship's position).</summary>
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

        private static FleetCombatStateDB State(Entity fleet) => fleet.GetDataBlob<FleetCombatStateDB>();

        private static double Pool(Entity fleet)
            => fleet.TryGetDataBlob<FleetCombatStateDB>(out var st) ? st.DamageTakenPool : -1;

        // ─── The directed gate ON THE PLANE — long fires short, short can't answer ────────────────────────────────

        [Test]
        [Description("Flag ON: on the frozen plane a LONG-range fleet (100 km) fires a SHORT-range fleet (1 km) 50 km " +
                     "away — the long gun reaches the anchor pair-distance, the short one can't. Decisive: the SCALAR " +
                     "gap is set to 500 m (well inside the short gun's 1 km reach), yet the short side STILL deals " +
                     "nothing — proving the resolver read the 2D ANCHOR distance (50 km), not the 500 m scalar. That is " +
                     "S2: SeparationOf now measures the plane.")]
        public void RangeGate_PlaneOn_LongFiresShort_ReadsAnchorNotScalar()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var longFleet = MakeFleet(s, s.Faction, "Long");    // 100 km gun
            var shortFleet = MakeFleet(s, reds, "Short");       // 1 km gun
            AddShipAt(s, s.Faction, longFleet, new Vector3(0, 0, 0), range_m: 100_000, evasion: 0);
            AddShipAt(s, reds, shortFleet, new Vector3(50_000, 0, 0), range_m: 1_000, evasion: 0);

            bool prevPlane = CombatEngagement.EnableGroupPlane;
            bool prevClose = CombatEngagement.EnableClosingRange;
            double prevScale = CombatEngagement.ClosingSpeedScale_mps;
            CombatEngagement.EnableGroupPlane = true;
            CombatEngagement.EnableClosingRange = true;   // SeparationOf is gated on closing; the plane supplies the gap
            CombatEngagement.ClosingSpeedScale_mps = 0;   // FREEZE the scalar gap — isolate the range gate
            try
            {
                CombatEngagement.StartEngagement(longFleet, shortFleet);   // seeds the plane + the scalar gap (~50 km)

                // THE DISCRIMINATOR: force the SCALAR gap small enough that the short gun WOULD fire on the scalar path.
                // The plane anchors stay ~50 km apart (seeded from the real positions), so the resolver's choice shows.
                State(longFleet).Separation_m = 500;
                State(shortFleet).Separation_m = 500;

                double planeGap = GroupPlane.PairDistance(State(longFleet).Anchor, State(shortFleet).Anchor);
                Log($"anchor pair-distance = {planeGap:N0} m   scalar Separation_m = {State(longFleet).Separation_m:N0} m");
                Assert.That(planeGap, Is.GreaterThan(40_000).And.LessThan(60_000),
                    "the plane pair-distance is ~50 km (the real gap), NOT the 500 m scalar — the two sources diverge");

                CombatEngagement.StepEngagement(longFleet, shortFleet, 5.0);

                Log($"after one salvo: short-fleet pool={Pool(shortFleet):E2}  long-fleet pool={Pool(longFleet):E2}");
                Assert.That(Pool(shortFleet), Is.GreaterThan(0),
                    "the 100 km gun reaches across the 50 km PLANE gap and hits the short fleet");
                Assert.That(Pool(longFleet), Is.EqualTo(0),
                    "the 1 km gun can't reach the 50 km PLANE gap — even though the 500 m SCALAR would let it fire; " +
                    "so the resolver read the anchor distance (S2), not the scalar (the directed gate on the plane)");
            }
            finally
            {
                CombatEngagement.EnableGroupPlane = prevPlane;
                CombatEngagement.EnableClosingRange = prevClose;
                CombatEngagement.ClosingSpeedScale_mps = prevScale;
            }
        }

        // ─── Flag OFF: the SCALAR drives the gate (byte-identity contrast) ───────────────────────────────────────

        [Test]
        [Description("Flag OFF (byte-identity tripwire): the SAME geometry as the plane-on gauge, but with the plane " +
                     "off no plane is seeded and SeparationOf reads the scalar gap. With the scalar set to 500 m the " +
                     "1 km short gun DOES fire, so the long fleet takes damage — the OPPOSITE outcome of the plane-on " +
                     "case. Proves the plane genuinely gates the behaviour (and the pre-S2 scalar path is untouched).")]
        public void RangeGate_PlaneOff_ScalarDrivesGate()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var longFleet = MakeFleet(s, s.Faction, "Long");
            var shortFleet = MakeFleet(s, reds, "Short");
            AddShipAt(s, s.Faction, longFleet, new Vector3(0, 0, 0), range_m: 100_000, evasion: 0);
            AddShipAt(s, reds, shortFleet, new Vector3(50_000, 0, 0), range_m: 1_000, evasion: 0);

            bool prevClose = CombatEngagement.EnableClosingRange;
            double prevScale = CombatEngagement.ClosingSpeedScale_mps;
            // EnableGroupPlane stays FALSE (default).
            CombatEngagement.EnableClosingRange = true;
            CombatEngagement.ClosingSpeedScale_mps = 0;
            try
            {
                CombatEngagement.StartEngagement(longFleet, shortFleet);
                Assert.That(State(longFleet).HasFrame, Is.False, "flag off → no plane seeded");
                Assert.That(State(shortFleet).HasFrame, Is.False, "flag off → no plane seeded");

                State(longFleet).Separation_m = 500;
                State(shortFleet).Separation_m = 500;

                CombatEngagement.StepEngagement(longFleet, shortFleet, 5.0);

                Log($"flag off: long-fleet pool={Pool(longFleet):E2} (short fleet's 1 km gun fires at the 500 m scalar gap)");
                Assert.That(Pool(longFleet), Is.GreaterThan(0),
                    "with the plane off, the short gun fires at the 500 m SCALAR gap — the long fleet takes damage " +
                    "(the opposite of the plane-on case, so the flag gates it)");
            }
            finally
            {
                CombatEngagement.EnableClosingRange = prevClose;
                CombatEngagement.ClosingSpeedScale_mps = prevScale;
            }
        }

        // ─── WithinWeaponRange reads the anchor pair-distance, not the ships' 3D separation ──────────────────────

        [Test]
        [Description("Flag ON: WithinWeaponRange measures the 2D ANCHOR pair-distance for two framed fleets. At seed " +
                     "the anchors (~50 km) are within both fleets' 100 km reach → true; move a framed fleet's anchor " +
                     "600 km away and it goes false — while the real 3D FleetSeparation is unchanged (50 km). With the " +
                     "plane OFF the gate reads that 3D separation again → true. So the gate reads the plane when it's on.")]
        public void WithinWeaponRange_PlaneOn_ReadsAnchorPairDistance()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var a = MakeFleet(s, s.Faction, "A");
            var b = MakeFleet(s, reds, "B");
            AddShipAt(s, s.Faction, a, new Vector3(0, 0, 0), range_m: 100_000, evasion: 0);
            AddShipAt(s, reds, b, new Vector3(50_000, 0, 0), range_m: 100_000, evasion: 0);

            bool prevPlane = CombatEngagement.EnableGroupPlane;
            CombatEngagement.EnableGroupPlane = true;   // seeding is gated on THIS flag alone (closing not needed)
            try
            {
                CombatEngagement.StartEngagement(a, b);   // seeds the frozen plane (both framed)
                Assert.That(State(a).HasFrame && State(b).HasFrame, Is.True, "both fleets are framed");

                Assert.That(CombatEngagement.WithinWeaponRange(a, b), Is.True,
                    "at seed the anchors (~50 km) are within both fleets' 100 km reach");

                // Slide the anchors far apart on the plane (the real 3D positions are untouched).
                State(a).Anchor = new Vector2(0, 0);
                State(b).Anchor = new Vector2(600_000, 0);
                Log($"moved anchors 600 km apart; real 3D FleetSeparation still ~50 km");

                Assert.That(CombatEngagement.WithinWeaponRange(a, b), Is.False,
                    "600 km anchor gap exceeds the 100 km reach — the gate read the ANCHORS, not the 50 km 3D distance");

                // Flag off → the gate reads the real 3D separation again (~50 km ≤ 100 km) → back to true.
                CombatEngagement.EnableGroupPlane = false;
                Assert.That(CombatEngagement.WithinWeaponRange(a, b), Is.True,
                    "with the plane off the gate reads the 3D FleetSeparation (~50 km) — byte-identical to pre-S2");
            }
            finally { CombatEngagement.EnableGroupPlane = prevPlane; }
        }

        // ─── Plane ON but CLOSING OFF: SeparationOf stays 0 (no range gating) ────────────────────────────────────

        [Test]
        [Description("Byte-identity guard: turning the plane ON without the closing model must NOT activate range " +
                     "gating. With EnableGroupPlane on but EnableClosingRange OFF, the plane IS seeded (HasFrame true) " +
                     "yet SeparationOf returns 0, so the range gate is a no-op — a 1 km short gun fires across a 50 km " +
                     "plane gap exactly as an un-gated fight would. Proves the closing gate is the master switch.")]
        public void PlaneOn_ClosingOff_NoRangeGating()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var longFleet = MakeFleet(s, s.Faction, "Long");
            var shortFleet = MakeFleet(s, reds, "Short");
            AddShipAt(s, s.Faction, longFleet, new Vector3(0, 0, 0), range_m: 100_000, evasion: 0);
            AddShipAt(s, reds, shortFleet, new Vector3(50_000, 0, 0), range_m: 1_000, evasion: 0);

            bool prevPlane = CombatEngagement.EnableGroupPlane;
            bool prevClose = CombatEngagement.EnableClosingRange;
            CombatEngagement.EnableGroupPlane = true;
            CombatEngagement.EnableClosingRange = false;   // the master switch is OFF → no range gate
            try
            {
                CombatEngagement.StartEngagement(longFleet, shortFleet);
                Assert.That(State(longFleet).HasFrame, Is.True, "the plane IS seeded (seeding is gated on the plane flag alone)");
                double planeGap = GroupPlane.PairDistance(State(longFleet).Anchor, State(shortFleet).Anchor);
                Assert.That(planeGap, Is.GreaterThan(40_000), "the anchors are ~50 km apart");

                CombatEngagement.StepEngagement(longFleet, shortFleet, 5.0);

                Log($"plane on / closing off: long-fleet pool={Pool(longFleet):E2} (short 1 km gun fires despite the ~50 km plane gap)");
                Assert.That(Pool(longFleet), Is.GreaterThan(0),
                    "closing off → SeparationOf 0 → the range gate is a no-op, so the short gun fires regardless of the " +
                    "50 km plane gap (byte-identical to a fight with no range gate at all)");
            }
            finally
            {
                CombatEngagement.EnableGroupPlane = prevPlane;
                CombatEngagement.EnableClosingRange = prevClose;
            }
        }
    }
}
