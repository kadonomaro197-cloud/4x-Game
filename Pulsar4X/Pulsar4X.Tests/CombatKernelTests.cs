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

            // A perfect dodger still eats the saturation floor (MinLandedFraction) from a low-saturation slug.
            double floored = CombatKernel.HitFraction(Slug(100), evasion: 1.0);
            Assert.That(floored, Is.EqualTo(CombatKernel.MinLandedFraction).Within(1e-9),
                "enough volume lands even on a perfect dodger");
            Log($"beam=1.0  slug@0.5={CombatKernel.HitFraction(Slug(100), 0.5)}  slug@1.0={floored}");
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
