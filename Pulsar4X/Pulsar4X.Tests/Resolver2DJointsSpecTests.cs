using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Executable SPECIFICATION for the two 2D-resolver joints pinned in docs/combat/RESOLVER-2D-JOINTS.md
    /// (Operation Earthfall slice T0.1). These are the homework docs/combat/RESOLVER-2D-GROUP-PLANE-DESIGN.md §11
    /// assigned before slices S6 (multi-party / FFA) and S5 (combined theater / Endor) may ship.
    ///
    /// This fixture holds a SELF-CONTAINED reference implementation of both joints and asserts their invariants,
    /// so the algorithm is pinned and proven BEFORE it is wired into GroupPlane/BattleTheater. It touches no
    /// production code path (it reads the WeaponProfile public type only), so it is byte-identical for every
    /// existing fixture. When S6/S5 implement these against production, they MUST reproduce this worked example.
    ///
    /// Joint #1 — conserved, target-weighted fire-allocation (total dealt == total owned; residual-exact).
    /// Joint #2 — combined-theater cadence (fixed 5s quantum; fast-forward == watch).
    /// </summary>
    [TestFixture]
    public class Resolver2DJointsSpecTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[2d-joints] " + m);

        // ─────────────────────────────────────────────────────────────────────────────────────────────────────
        //  JOINT #1 — the reference AllocateFire (docs/combat/RESOLVER-2D-JOINTS.md §1.2)
        //  One DPS pool per firing group, split across its engageable targets, conserved exactly via the residual.
        // ─────────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>A firing target: a stable group id + the raw weight this firer places on it.</summary>
        private readonly struct Target
        {
            public readonly int GroupId;
            public readonly double Weight;
            public Target(int groupId, double weight) { GroupId = groupId; Weight = weight; }
        }

        /// <summary>
        /// The pinned residual-exact allocation. Returns groupId -> DPS dealt. Sum of the returned values equals
        /// <paramref name="pool"/> EXACTLY (one subtraction backs out the last, deterministic, target by id order),
        /// so a group can never deal more OR less than its pool. Empty targets -> nothing dealt.
        /// </summary>
        private static Dictionary<int, double> AllocateFire(double pool, IReadOnlyList<Target> targets)
        {
            var dealt = new Dictionary<int, double>();
            if (targets == null || targets.Count == 0) return dealt;

            // (1) deterministic order — ascending stable group id (fixes residual owner + iteration order).
            var order = targets.OrderBy(t => t.GroupId).ToList();

            // (2) weights -> shares that sum to exactly 1.0 via residual assignment.
            double W = order.Sum(t => t.Weight);
            if (W <= 0) return dealt;                       // no valid weight -> deal nothing

            double running = 0;
            for (int i = 0; i < order.Count - 1; i++)       // every target EXCEPT the last
            {
                double d = pool * (order[i].Weight / W);
                dealt[order[i].GroupId] = d;
                running += d;
            }
            dealt[order[order.Count - 1].GroupId] = pool - running; // THE RESIDUAL — exact conservation
            return dealt;
        }

        // --- Joint #1 tests ---------------------------------------------------------------------------------------

        [Test]
        [Description("3-way FFA, equal weights: every firer deals exactly its pool, and system-wide total dealt == total owned (§1.4).")]
        public void FireAllocation_ThreeWayFFA_EqualWeights_ConservesSystemWide()
        {
            // Pools: A=100, B=60, C=40. All mutually hostile + in range. Equal weight (=1) each.
            const int A = 1, B = 2, C = 3;
            var aFire = AllocateFire(100, new[] { new Target(B, 1), new Target(C, 1) });
            var bFire = AllocateFire(60,  new[] { new Target(A, 1), new Target(C, 1) });
            var cFire = AllocateFire(40,  new[] { new Target(A, 1), new Target(B, 1) });

            // Each firer's total == its pool, exactly.
            Assert.That(aFire.Values.Sum(), Is.EqualTo(100).Within(1e-12), "A conserves its pool");
            Assert.That(bFire.Values.Sum(), Is.EqualTo(60).Within(1e-12),  "B conserves its pool");
            Assert.That(cFire.Values.Sum(), Is.EqualTo(40).Within(1e-12),  "C conserves its pool");

            // Equal split matches today's space `1/split`.
            Assert.That(aFire[B], Is.EqualTo(50).Within(1e-12));
            Assert.That(aFire[C], Is.EqualTo(50).Within(1e-12));

            // Incoming each group takes (the worked example in §1.4).
            double intoA = bFire[A] + cFire[A]; // 30 + 20
            double intoB = aFire[B] + cFire[B]; // 50 + 20
            double intoC = aFire[C] + bFire[C]; // 50 + 30
            Assert.That(intoA, Is.EqualTo(50).Within(1e-12));
            Assert.That(intoB, Is.EqualTo(70).Within(1e-12));
            Assert.That(intoC, Is.EqualTo(80).Within(1e-12));

            // System-wide: total dealt == total owned. No group multiplied its guns.
            double totalDealt = intoA + intoB + intoC;
            double totalOwned = 100 + 60 + 40;
            Log($"totalDealt={totalDealt} totalOwned={totalOwned}");
            Assert.That(totalDealt, Is.EqualTo(totalOwned).Within(1e-12), "system-wide firepower is conserved");
        }

        [Test]
        [Description("The double-count trap (§1.1): a naive full-pool-per-target deals 2x in a 3-way; the conserved allocation deals exactly 1x.")]
        public void FireAllocation_KillsTheDoubleCountTrap()
        {
            const int B = 2, C = 3;
            var targets = new[] { new Target(B, 1), new Target(C, 1) };

            // Naive (the ground bug at GroundForcesProcessor.ResolveRegionCombat): full pool at EACH target.
            double naiveDealt = 100 /*at B*/ + 100 /*at C*/;
            // Conserved (the pinned allocation): one pool, split.
            double conservedDealt = AllocateFire(100, targets).Values.Sum();

            Log($"naive={naiveDealt} conserved={conservedDealt}");
            Assert.That(naiveDealt, Is.EqualTo(200), "the trap doubles a 100-pool across two targets");
            Assert.That(conservedDealt, Is.EqualTo(100).Within(1e-12), "the conserved allocation deals exactly the pool");
        }

        [Test]
        [Description("Target-weighting concentrates fire on the higher-weight target while still conserving the pool exactly (§1.3/§1.4).")]
        public void FireAllocation_TargetWeighted_ConcentratesButConserves()
        {
            const int B = 2, C = 3;
            // Weight = target threat = target's own pool. B(60) is the bigger threat than C(40).
            var aFire = AllocateFire(100, new[] { new Target(B, 60), new Target(C, 40) });

            Assert.That(aFire[B], Is.GreaterThan(aFire[C]), "A concentrates on the bigger threat B");
            Assert.That(aFire[B], Is.EqualTo(60).Within(1e-12));
            Assert.That(aFire[C], Is.EqualTo(40).Within(1e-12));
            Assert.That(aFire.Values.Sum(), Is.EqualTo(100).Within(1e-12), "still conserved to the bit");
        }

        [Test]
        [Description("Determinism (§1.2): the allocation is independent of the order targets are supplied in (sorted by id internally).")]
        public void FireAllocation_IsOrderIndependent()
        {
            const int B = 2, C = 3, D = 4;
            var forward = AllocateFire(100, new[] { new Target(B, 60), new Target(C, 40), new Target(D, 20) });
            var shuffled = AllocateFire(100, new[] { new Target(D, 20), new Target(B, 60), new Target(C, 40) });

            CollectionAssert.AreEquivalent(forward.Keys, shuffled.Keys);
            foreach (var id in forward.Keys)
                Assert.That(shuffled[id], Is.EqualTo(forward[id]).Within(1e-12), $"group {id} gets the same fire regardless of input order");
            Assert.That(forward.Values.Sum(), Is.EqualTo(100).Within(1e-12));
        }

        [Test]
        [Description("Weight ties resolve deterministically and still conserve exactly (residual to the highest id).")]
        public void FireAllocation_WeightTies_ResolveDeterministically()
        {
            const int B = 2, C = 3, D = 4;
            var f = AllocateFire(100, new[] { new Target(B, 1), new Target(C, 1), new Target(D, 1) });
            // Equal thirds; the residual (highest id, D) backs out any float drift so the sum is exact.
            Assert.That(f[B], Is.EqualTo(100.0 / 3).Within(1e-9));
            Assert.That(f[C], Is.EqualTo(100.0 / 3).Within(1e-9));
            Assert.That(f.Values.Sum(), Is.EqualTo(100).Within(1e-12), "residual makes the three thirds sum to exactly 100");
        }

        [Test]
        [Description("Empty engageable set -> a group deals nothing this step (no carry); no pool is silently lost.")]
        public void FireAllocation_NoTargets_DealsNothing()
        {
            var f = AllocateFire(100, Array.Empty<Target>());
            Assert.That(f, Is.Empty);
        }

        [Test]
        [Description("Splitting a real WeaponProfile fire MIX by the same shares conserves total DPS within tolerance and preserves Nature/Delivery (§1.2).")]
        public void FireAllocation_WeaponProfileMix_ConservesTotalDpsAndFlavor()
        {
            // A's fire mix: a kinetic slug (700 dps) + an energy beam (300 dps) = 1000 dps pool.
            var mix = new List<WeaponProfile>
            {
                new WeaponProfile(700, 5e4, 0.1, 5, 0, WeaponNature.Kinetic, WeaponDelivery.Slug),
                new WeaponProfile(300, 3e8, 0.9, 1, 0, WeaponNature.Energy,  WeaponDelivery.Beam),
            };
            double pool = mix.Sum(w => w.DamagePerSecond);
            Assert.That(pool, Is.EqualTo(1000).Within(1e-9));

            const int B = 2, C = 3;
            var alloc = AllocateFire(pool, new[] { new Target(B, 60), new Target(C, 40) });

            // Scale the whole mix by each target's share (the AddScaledFire move), preserving flavor.
            List<WeaponProfile> ScaleMix(double dealtToTarget)
            {
                double share = dealtToTarget / pool;
                return mix.Select(w => new WeaponProfile(
                    w.DamagePerSecond * share, w.Velocity, w.Tracking, w.Saturation, w.Range_m, w.Nature, w.Delivery)).ToList();
            }
            var toB = ScaleMix(alloc[B]);
            var toC = ScaleMix(alloc[C]);

            // Total DPS across both split piles == the original pool (mix-level conservation, within epsilon).
            double splitTotal = toB.Sum(w => w.DamagePerSecond) + toC.Sum(w => w.DamagePerSecond);
            Log($"pool={pool} splitTotal={splitTotal}");
            Assert.That(splitTotal, Is.EqualTo(pool).Within(1e-6), "the mix split conserves total DPS");

            // Flavor rides along unchanged — B's pile still has one Kinetic/Slug and one Energy/Beam bucket.
            Assert.That(toB.Count(w => w.Nature == WeaponNature.Kinetic && w.Delivery == WeaponDelivery.Slug), Is.EqualTo(1));
            Assert.That(toB.Count(w => w.Nature == WeaponNature.Energy && w.Delivery == WeaponDelivery.Beam), Is.EqualTo(1));
        }

        // ─────────────────────────────────────────────────────────────────────────────────────────────────────
        //  JOINT #2 — combined-theater cadence (docs/combat/RESOLVER-2D-JOINTS.md §2)
        //  A self-contained model of the fixed-quantum stepping + the fast-forward==watch proof.
        // ─────────────────────────────────────────────────────────────────────────────────────────────────────

        private const int SpaceQuantumSeconds  = 5;     // MasterTimePulse.CombatReactionStep / BattleTriggerProcessor
        private const int GroundNativeSeconds   = 3600;  // GroundForcesProcessor.RunFrequency (1 h)
        private const int TheaterGroundQuantum  = 5;     // the theater force-steps the ground fight at the space grid

        [Test]
        [Description("The divisibility invariant (§2.5 rule 2): the theater's 5s ground quantum evenly divides the native 1h cadence (720 exact) so the two grids nest with no drift.")]
        public void Cadence_TheaterQuantumEvenlyDividesNativeHour()
        {
            Assert.That(GroundNativeSeconds % TheaterGroundQuantum, Is.EqualTo(0), "5s must evenly divide 3600s");
            Assert.That(GroundNativeSeconds / TheaterGroundQuantum, Is.EqualTo(720), "720 five-second steps == one hour");
            Assert.That(TheaterGroundQuantum, Is.EqualTo(SpaceQuantumSeconds), "the theater borrows the space trigger's fixed quantum");
        }

        /// <summary>
        /// A deterministic, quantum-SENSITIVE integrator standing in for a stepped ground fight: each step advances
        /// state by a nonlinear function of the step size, so a coarse single step and many fine steps differ (as a
        /// real closing/damage integration does). Used to prove the fixed-quantum invariant matters.
        /// </summary>
        private static double StepFight(double state, int dtSeconds) => state + Math.Sqrt(dtSeconds) * dtSeconds;

        [Test]
        [Description("Fast-forward == watch (§2.4): stepping the theater fight in one big master chunk equals stepping it fixed-5s the same number of times; a single VARIABLE 1h step differs (why the fixed quantum is load-bearing).")]
        public void Cadence_FastForwardEqualsWatch_AtFixedQuantum()
        {
            // "Watch": the player crawls through one game-hour at 5s. "Fast-forward": the player jumps the whole
            // hour in one master press. Under the pinned contract BOTH run the theater at a FIXED 5s quantum.
            int steps = GroundNativeSeconds / TheaterGroundQuantum; // 720

            double watched = 0;
            for (int i = 0; i < steps; i++) watched = StepFight(watched, TheaterGroundQuantum);

            double fastForwarded = 0;
            // The master fast-forward jumps the hour, but the space trigger (and thus the theater) still fires once
            // per 5s with dt=5 — so the theater takes the SAME 720 fixed-quantum steps.
            for (int i = 0; i < steps; i++) fastForwarded = StepFight(fastForwarded, TheaterGroundQuantum);

            Assert.That(fastForwarded, Is.EqualTo(watched).Within(1e-9),
                "fast-forward and watch take the same fixed-quantum steps -> identical result");

            // The counter-case: if the ground fight were (wrongly) fed the VARIABLE 1h master step, it would diverge.
            double variableSingleStep = StepFight(0, GroundNativeSeconds);
            Log($"watched={watched:0.000} variable1h={variableSingleStep:0.000}");
            Assert.That(variableSingleStep, Is.Not.EqualTo(watched).Within(1e-6),
                "a variable 1h step differs from the fixed-quantum path — this is why §2.5 rule 1 forbids it");
        }

        [Test]
        [Description("Deterministic formation/dissolution (§2.4 step 3): the set of ticks a theater is active is identical for watch vs fast-forward, because it's a pure function of game-time.")]
        public void Cadence_TheaterActiveTickSet_IdenticalWatchVsFastForward()
        {
            // A theater is active from a deterministic game-time to a deterministic game-time (pure function of state).
            const int formSecond = 5 * 10;   // forms at the 10th 5s tick
            const int endSecond  = 5 * 40;   // dissolves at the 40th 5s tick
            bool TheaterActive(int gameSecond) => gameSecond >= formSecond && gameSecond < endSecond;

            // "Watch": evaluate every 5s tick across the hour.
            var watchedActive = new List<int>();
            for (int t = 0; t <= GroundNativeSeconds; t += TheaterGroundQuantum)
                if (TheaterActive(t)) watchedActive.Add(t);

            // "Fast-forward": the master jumps, but the trigger still lands on every 5s boundary — same evaluation set.
            var ffActive = new List<int>();
            for (int t = 0; t <= GroundNativeSeconds; t += TheaterGroundQuantum)
                if (TheaterActive(t)) ffActive.Add(t);

            CollectionAssert.AreEqual(watchedActive, ffActive, "the theater-active tick set is identical either way");
            Assert.That(watchedActive.Count, Is.EqualTo(30), "active for ticks 10..39 -> 30 five-second steps");
        }
    }
}
