using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// RESOLVER MERGE, slice 1 (docs/RESOLVER-MERGE-DESIGN.md §5) — the pinning gauge for the new shared
    /// <see cref="CombatKernel"/>.
    ///
    /// Slice 1 is deliberately ADDITIVE and UNWIRED: the kernel copies the pure salvo math out of the live ship
    /// resolver (<see cref="CombatEngagement"/>) and the live ground matchup (<see cref="GroundDamageMatrix"/>), but
    /// nothing calls it yet, so ship + ground behaviour are byte-identical this slice. This fixture is what makes the
    /// duplication safe until slice 2 collapses it: it (a) PINS the kernel's outputs to hand-computed known values,
    /// and (b) CROSS-CHECKS the kernel against the live functions over a range of inputs — so if a future edit to
    /// either copy diverges, this goes red. Pure math → no colony harness → cheap, deterministic, CI-fast.
    /// </summary>
    [TestFixture]
    public class CombatKernelTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[kernel] " + m);

        private static WeaponProfile Beam(double dps) =>
            new WeaponProfile(dps, 3e8, 1.0, double.PositiveInfinity, 0, WeaponNature.Energy, WeaponDelivery.Beam);
        private static WeaponProfile Slug(double dps) =>
            new WeaponProfile(dps, 1e6, 0.0, 1.0, 0, WeaponNature.Kinetic, WeaponDelivery.Slug);

        [Test]
        [Description("HitFraction pins: a light-speed beam always lands (1.0) regardless of evasion; a finite-velocity slug is dodged (0.75 vs evasion 0.5); saturation floors a perfect dodger at MinLandedFraction.")]
        public void HitFraction_PinsKnownValues()
        {
            // Beam vs a nimble target: velocityTerm≈1, tracking 1 → dodgeChance 0 → lands fully.
            Assert.That(CombatKernel.HitFraction(Beam(100), evasion: 0.95), Is.EqualTo(1.0).Within(1e-9),
                "a light-speed beam can't be dodged");

            // Slug vs evasion 0.5: velocityTerm 0.5, tracking 0 → dodgeChance 0.5*(1-0.5)=0.25 → hit 0.75.
            Assert.That(CombatKernel.HitFraction(Slug(100), evasion: 0.5), Is.EqualTo(0.75).Within(1e-9),
                "a finite-velocity slug is dodged by an evasive target");

            // A perfect dodger vs a genuinely SLOW shot (velocity ≪ VelocityReference, so velocityTerm→~0 and
            // dodgeChance→~1) is floored at MinLandedFraction by the weapon's own saturation floor — enough volume
            // still lands. (A fast 1e6 m/s slug has velocityTerm 0.5, so a perfect dodger only reaches hit 0.5 and
            // the floor never binds — the arithmetic that makes the floor matter needs a slow weapon.)
            var slowShot = new WeaponProfile(100, 1.0, 0.0, 1.0, 0, WeaponNature.Kinetic, WeaponDelivery.Slug);
            double floored = CombatKernel.HitFraction(slowShot, evasion: 1.0);
            Assert.That(floored, Is.EqualTo(CombatKernel.MinLandedFraction).Within(1e-9),
                "enough volume lands even on a perfect dodger");
            Log($"beam=1.0  slug@0.5={CombatKernel.HitFraction(Slug(100), 0.5)}  slowShot@1.0={floored}");
        }

        [Test]
        [Description("ArmourSoak pins the flat-per-source plating: light armour shaves a flat amount off a source; heavy armour is floored at ArmourMinPassFraction so it's never total immunity; 0 armour passes the source untouched.")]
        public void ArmourSoak_PinsKnownValues()
        {
            // defense 10, source 100: after = 100 - 10*1.5 = 85 (above the 10 floor).
            Assert.That(CombatKernel.ArmourSoak(armour: 10, sourceDamage: 100), Is.EqualTo(85).Within(1e-9));
            // defense 100, source 100: after = 100 - 150 = -50 → floored to 100*0.1 = 10.
            Assert.That(CombatKernel.ArmourSoak(armour: 100, sourceDamage: 100), Is.EqualTo(10).Within(1e-9));
            // no armour → full pass.
            Assert.That(CombatKernel.ArmourSoak(armour: 0, sourceDamage: 100), Is.EqualTo(100).Within(1e-9));
        }

        [Test]
        [Description("Penetration is the armour half of the matchup (⚙1 backlog #1): penetration 0 reduces byte-for-byte to the old flat soak; penetration cancels armour point-for-point (a partly-penetrated plate soaks off its REMAINING points); penetration ≥ armour lands in full like an unarmoured target; a negative penetration is clamped (never makes armour stronger).")]
        public void ArmourSoak_Penetration_CancelsArmourPointForPoint()
        {
            // Penetration 0 is byte-identical to the 2-arg flat soak (the byte-identity contract W1a rests on).
            Assert.That(CombatKernel.ArmourSoak(armour: 10, sourceDamage: 100, penetration: 0),
                Is.EqualTo(CombatKernel.ArmourSoak(armour: 10, sourceDamage: 100)).Within(1e-12),
                "penetration 0 == the old flat soak (byte-identical)");

            // Armour 100, penetration 60 → effective armour 40 → after = 100 - 40*1.5 = 40 (above the 10 floor).
            // The SAME weapon vs the same plate with NO penetration is floored at 10 — so penetration is the difference
            // between bouncing (10 lands) and cracking (40 lands).
            Assert.That(CombatKernel.ArmourSoak(armour: 100, sourceDamage: 100, penetration: 60), Is.EqualTo(40).Within(1e-9),
                "penetration cancels armour point-for-point, so the source soaks off the REMAINING plating only");
            Assert.That(CombatKernel.ArmourSoak(armour: 100, sourceDamage: 100, penetration: 0), Is.EqualTo(10).Within(1e-9),
                "the same round with no penetration bounces off heavy plate (floored)");

            // Penetration ≥ armour → the plate is meaningless → full pass (identical to the unarmoured case).
            Assert.That(CombatKernel.ArmourSoak(armour: 40, sourceDamage: 100, penetration: 40), Is.EqualTo(100).Within(1e-9),
                "penetration equal to armour → lands in full, as if unarmoured");
            Assert.That(CombatKernel.ArmourSoak(armour: 40, sourceDamage: 100, penetration: 999), Is.EqualTo(100).Within(1e-9),
                "over-penetration doesn't over-land — it just fully bypasses");

            // A negative penetration can't buff the armour (clamped to 0 → same as the plain soak).
            Assert.That(CombatKernel.ArmourSoak(armour: 10, sourceDamage: 100, penetration: -50),
                Is.EqualTo(CombatKernel.ArmourSoak(armour: 10, sourceDamage: 100)).Within(1e-12),
                "negative penetration is clamped — armour never gets STRONGER");

            Log($"pen0=={CombatKernel.ArmourSoak(10, 100, 0)}  crack(pen60)={CombatKernel.ArmourSoak(100, 100, 60)} vs bounce(pen0)={CombatKernel.ArmourSoak(100, 100, 0)}  fullpass(pen=armour)={CombatKernel.ArmourSoak(40, 100, 40)}");
        }

        [Test]
        [Description("Armour NATURE factor (⚙3 Defense — ablative/composite/reactive plating): natureFactor scales how hard the plating soaks the incoming nature. 1.0 is byte-for-byte the penetration overload (every plain-plated unit); >1 soaks harder (a tuned plate bounces more); <1 soaks less (a poor match lands more); it scales the flat soak only, so penetration still decides the physical breach; clamped at 0.")]
        public void ArmourSoak_NatureFactor_ScalesTheSoak()
        {
            // natureFactor 1.0 is the byte-identity contract this whole slice rests on.
            Assert.That(CombatKernel.ArmourSoak(armour: 10, sourceDamage: 100, penetration: 0, natureFactor: 1.0),
                Is.EqualTo(CombatKernel.ArmourSoak(armour: 10, sourceDamage: 100)).Within(1e-12),
                "natureFactor 1.0 == the old flat soak (byte-identical — a plain plate)");

            // Base: armour 10 → after = 100 - 10*1.5 = 85 lands. A plate TUNED to this nature (×2) soaks twice as hard
            // → after = 100 - 10*1.5*2 = 70 lands. A POOR match (×0.5) soaks half → after = 100 - 10*1.5*0.5 = 92.5.
            Assert.That(CombatKernel.ArmourSoak(10, 100, 0, 1.0), Is.EqualTo(85).Within(1e-9));
            Assert.That(CombatKernel.ArmourSoak(10, 100, 0, 2.0), Is.EqualTo(70).Within(1e-9), "tuned plate soaks harder — less lands");
            Assert.That(CombatKernel.ArmourSoak(10, 100, 0, 0.5), Is.EqualTo(92.5).Within(1e-9), "poor match soaks less — more lands");

            // Nature scales the SOAK, not the breach: penetration ≥ armour still lands in full regardless of nature.
            Assert.That(CombatKernel.ArmourSoak(40, 100, penetration: 40, natureFactor: 5.0), Is.EqualTo(100).Within(1e-9),
                "penetration decides the physical breach first — a fully-penetrated plate can't soak by nature");

            // Clamped at 0 (a nature match can't make armour NEGATIVE / add damage).
            Assert.That(CombatKernel.ArmourSoak(10, 100, 0, -3.0),
                Is.EqualTo(CombatKernel.ArmourSoak(10, 100, 0, 0.0)).Within(1e-12), "negative nature factor clamps to 0 (no soak, not anti-soak)");
            Assert.That(CombatKernel.ArmourSoak(10, 100, 0, 0.0), Is.EqualTo(100).Within(1e-9), "a totally-mismatched plate (factor 0) soaks nothing");

            // The burst overload carries the same factor through the shot split.
            Assert.That(CombatKernel.ArmourSoakBurst(10, 100, shotCount: 1, penetration: 0, natureFactor: 2.0),
                Is.EqualTo(CombatKernel.ArmourSoak(10, 100, 0, 2.0)).Within(1e-12), "one lump == the flat nature soak");

            Log($"plain(×1)={CombatKernel.ArmourSoak(10,100,0,1.0)}  tuned(×2)={CombatKernel.ArmourSoak(10,100,0,2.0)}  poor(×0.5)={CombatKernel.ArmourSoak(10,100,0,0.5)}");
        }

        [Test]
        [Description("PerShotEnergy is the alpha-vs-chip dial (⚙1 backlog #2): BurstShotCount = dps/PerShotEnergy clamped (0 → 1 lump); ArmourSoakBurst splits a source into that many equal shots and soaks each flat, so a swarm of chips (many shots) is mostly bounced by plate while one alpha of EQUAL total punches through. shotCount ≤ 1 is byte-identical to the flat soak, so an un-dialled weapon is unchanged.")]
        public void ArmourSoakBurst_AlphaPunches_ChipBounces()
        {
            // BurstShotCount: a cannon (huge per-shot) fires 1 alpha; a repeater (small per-shot) many; 0 → 1 lump.
            var cannon   = new WeaponProfile(1000, 1e6, 0, 1, 0, WeaponNature.Kinetic, WeaponDelivery.Slug, 0, perShotEnergy: 1000);
            var repeater = new WeaponProfile(1000, 1e6, 0, 1, 0, WeaponNature.Kinetic, WeaponDelivery.Slug, 0, perShotEnergy: 10);
            var undialled = new WeaponProfile(1000, 1e6, 0, 1, 0, WeaponNature.Kinetic, WeaponDelivery.Slug); // PerShotEnergy 0
            Assert.That(CombatKernel.BurstShotCount(cannon), Is.EqualTo(1), "a big-per-shot cannon is one alpha");
            Assert.That(CombatKernel.BurstShotCount(repeater), Is.EqualTo(100), "a small-per-shot repeater is many chips (1000/10)");
            Assert.That(CombatKernel.BurstShotCount(undialled), Is.EqualTo(1), "an un-dialled weapon is a single lump");

            // Equal total damage (1000) vs the same plate: the alpha lands far more than the chip-burst.
            const double armour = 100, total = 1000;
            double alphaLands = CombatKernel.ArmourSoakBurst(armour, total, shotCount: 1);
            double chipLands  = CombatKernel.ArmourSoakBurst(armour, total, shotCount: 100);
            Log($"vs armour {armour}: one {total}-alpha lands {alphaLands}, a 100-chip burst of the same total lands {chipLands}");
            Assert.That(alphaLands, Is.GreaterThan(chipLands), "one big alpha punches plate a swarm of chips bounces off");

            // shotCount ≤ 1 is byte-for-byte the flat soak (an un-dialled weapon is unchanged).
            Assert.That(CombatKernel.ArmourSoakBurst(armour, total, shotCount: 1),
                Is.EqualTo(CombatKernel.ArmourSoak(armour, total)).Within(1e-12), "one lump == the flat soak");

            // Penetration composes: an AP burst cracks more than a plain burst of the same shot count. (The chips are
            // small — 10 each — so the penetration must bring effective armour low enough for a chunk to beat the floor:
            // armour 100, pen 96 → effective 4 → each 10-chunk lands 10-4*1.5=4 instead of the floored 1.)
            double apChip    = CombatKernel.ArmourSoakBurst(armour, total, shotCount: 100, penetration: 96);
            Assert.That(apChip, Is.GreaterThan(chipLands), "penetration still helps a chip-burst crack plate");
        }

        [Test]
        [Description("ResolveShield pins the drain/regen: a charged pool absorbs the soakable damage up to its charge; regen tops it back toward capacity; a 0-capacity (unshielded) pool absorbs nothing.")]
        public void ResolveShield_PinsKnownValues()
        {
            // pool 1000, cap 2000, no regen, salvo 500 fully soakable → absorb 500, pool 500.
            var (absorbed, pool) = CombatKernel.ResolveShield(1000, 2000, regen: 0, salvoDamage: 500, soakFraction: 1.0, dt: 5);
            Assert.That(absorbed, Is.EqualTo(500).Within(1e-9));
            Assert.That(pool, Is.EqualTo(500).Within(1e-9));

            // With regen 100/s over dt 5 = +500, pool 500→1000 (still below cap).
            var (_, regenned) = CombatKernel.ResolveShield(500, 2000, regen: 100, salvoDamage: 0, soakFraction: 0, dt: 5);
            Assert.That(regenned, Is.EqualTo(1000).Within(1e-9), "the pool recharges between volleys");

            // Unshielded (0 capacity) absorbs nothing — the additive no-op.
            var (none, _) = CombatKernel.ResolveShield(0, 0, regen: 0, salvoDamage: 500, soakFraction: 1.0, dt: 5);
            Assert.That(none, Is.EqualTo(0).Within(1e-9), "an unshielded combatant is byte-identical");
        }

        [Test]
        [Description("SoakFractionOf pins the damage-weighted nature matchup: a 50/50 kinetic+exotic damage mix soaks (1.0+0.0)/2 = 0.5.")]
        public void SoakFractionOf_PinsTheNatureMatchup()
        {
            var mix = new List<WeaponProfile>
            {
                new WeaponProfile(100, 1e6, 1, 1, 0, WeaponNature.Kinetic),
                new WeaponProfile(100, 1e6, 1, 1, 0, WeaponNature.Exotic),
            };
            Assert.That(CombatKernel.SoakFractionOf(mix), Is.EqualTo(0.5).Within(1e-9),
                "kinetic fully soakable + exotic fully bypassing → half over the mix");
        }

        [Test]
        [Description("BYTE-IDENTITY tripwire: the kernel's pure math must match the live ship resolver (CombatEngagement) and ground matchup (GroundDamageMatrix) it was extracted from, across a sweep of inputs. If either copy is edited to diverge, this fails — the guard that keeps slice-1 duplication honest until slice 2 removes it.")]
        public void Kernel_MatchesLiveShipAndGroundMath()
        {
            var weapons = new List<WeaponProfile>
            {
                Beam(100), Slug(100),
                new WeaponProfile(50, 5e5, 0.2, 5, 0, WeaponNature.Explosive, WeaponDelivery.Blast),
                new WeaponProfile(75, 2e6, 0.8, 200, 0, WeaponNature.Exotic, WeaponDelivery.Guided),
            };
            double[] evasions = { 0.0, 0.25, 0.5, 0.75, 0.95 };
            double[] separations = { 0.0, 1_000.0, 100_000.0, 5_000_000.0 };

            // HitFraction — vs the internal ship function (reachable via InternalsVisibleTo).
            foreach (var w in weapons)
                foreach (var ev in evasions)
                    foreach (var sep in separations)
                        Assert.That(CombatKernel.HitFraction(w, ev, sep),
                            Is.EqualTo(CombatEngagement.HitFraction(w, ev, sep)).Within(1e-12),
                            $"HitFraction drift @ nat={w.Nature} ev={ev} sep={sep}");

            // SoakFractionOf — vs the internal ship function, over sub-mixes.
            for (int n = 1; n <= weapons.Count; n++)
            {
                var sub = weapons.GetRange(0, n);
                Assert.That(CombatKernel.SoakFractionOf(sub),
                    Is.EqualTo(CombatEngagement.SoakFractionOf(sub)).Within(1e-12), $"SoakFractionOf drift @ n={n}");
            }

            // ResolveShield — vs the internal ship function.
            var (aK, pK) = CombatKernel.ResolveShield(1200, 3000, 250, 900, 0.6, 5);
            var (aE, pE) = CombatEngagement.ResolveShield(1200, 3000, 250, 900, 0.6, 5);
            Assert.That(aK, Is.EqualTo(aE).Within(1e-9), "ResolveShield absorbed drift");
            Assert.That(pK, Is.EqualTo(pE).Within(1e-9), "ResolveShield pool drift");

            // ArmourSoak — vs the live ground function it was promoted from.
            double[] defenses = { 0, 5, 20, 100, 1000 };
            double[] sources = { 0, 10, 100, 1000 };
            foreach (var d in defenses)
                foreach (var src in sources)
                    Assert.That(CombatKernel.ArmourSoak(d, src),
                        Is.EqualTo(GroundDamageMatrix.ArmourSoak(d, src)).Within(1e-12),
                        $"ArmourSoak drift @ def={d} src={src}");

            Log("kernel matches live ship (HitFraction/SoakFractionOf/ResolveShield) + ground (ArmourSoak) math across the sweep");
        }
    }
}
