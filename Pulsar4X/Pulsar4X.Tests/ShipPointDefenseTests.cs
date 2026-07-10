using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Weapons pilot W6 (missiles as resolvable targets + point-defense) — SLICE W6a, the byte-identical foundation.
    /// A missile is a GUIDED projectile — a thing you can SHOOT DOWN on its way in, unlike a beam or a slug. W6a adds the
    /// pieces WITHOUT wiring the intercept into the resolver: the <see cref="PointDefenseAtb"/> component (the
    /// missile-killer mount), <see cref="ShipCombatValueDB.PointDefense_Jps"/> (sum of installed PD), and the pure
    /// intercept math (<see cref="CombatEngagement.PointDefenseInterceptFraction"/> — a saturating curve, capped so a big
    /// enough swarm always leaks through).
    ///
    /// The invariant this pins: with NO PD a ship reads 0 intercept rating → the intercept step is skipped → combat is
    /// byte-identical (every current ship, until the W6c base-mod PD mount). W6b wires the per-salvo intercept of incoming
    /// guided fire; W6c adds the buildable base-mod PD mount + a missile ship + a PD ship. Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class ShipPointDefenseTests
    {
        private const string BeamShip = "default-ship-design-test-warship"; // Aegis — 4 lasers, no PD
        private static void Log(string m) => TestContext.Progress.WriteLine("[ship-pd] " + m);

        [Test]
        [Description("PointDefenseAtb (the missile-killer mount) holds its J/s intercept rating and clamps a negative to 0; clone preserves it.")]
        public void PointDefenseAtb_PinsTheFoundation()
        {
            var pd = new PointDefenseAtb(250_000);
            Assert.That(pd.InterceptRating_Jps, Is.EqualTo(250_000), "the PD mount holds its intercept rating");
            Assert.That(new PointDefenseAtb(-10).InterceptRating_Jps, Is.EqualTo(0), "a negative rating clamps to 0");
            Assert.That(((PointDefenseAtb)pd.Clone()).InterceptRating_Jps, Is.EqualTo(250_000), "clone preserves the rating");
        }

        [Test]
        [Description("W6a additive/byte-identical: a real base-mod warship with NO point-defense reads PointDefense_Jps == 0 (so its intercept step is skipped and the resolve is untouched), exactly as an unshielded/magazine-less ship reads 0.")]
        public void ARealShip_WithNoPointDefense_ReadsZeroIntercept()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(BeamShip), Is.True, "the Aegis warship loads onto the faction");

            var ship = ShipFactory.CreateShip(designs[BeamShip], s.Faction, s.StartingBody, "Aegis");
            var cv = ship.GetDataBlob<ShipCombatValueDB>();
            Log($"Aegis (no PD): firepower={cv.Firepower:0}, pointDefense={cv.PointDefense_Jps:0} J/s");

            Assert.That(cv.Firepower, Is.GreaterThan(0), "the Aegis carries its lasers");
            Assert.That(cv.PointDefense_Jps, Is.EqualTo(0),
                "no PD mount → 0 intercept rating → the intercept step stays disabled → combat byte-identical until the W6c PD mount");
        }

        [Test]
        [Description("W6b pure helper: MissileFireDamage sums ONLY the guided (interceptable/missile) fire — a beam or a slug contributes nothing, since PD can't shoot those down.")]
        public void MissileFireDamage_SumsOnlyGuidedFire()
        {
            var mix = new List<WeaponProfile>
            {
                new WeaponProfile(1000, 5_000, 0.9, 1.0, 0, WeaponNature.Explosive, WeaponDelivery.Guided),  // a missile — interceptable
                new WeaponProfile(2000, 3e8,   0.95, 0.5, 0, WeaponNature.Energy,    WeaponDelivery.Beam),   // a beam — NOT interceptable
                new WeaponProfile(3000, 2e6,   0.1,  1.0, 0, WeaponNature.Kinetic,   WeaponDelivery.Slug),   // a slug — NOT interceptable
            };
            Assert.That(CombatEngagement.MissileFireDamage(mix), Is.EqualTo(1000), "only the guided missile fire counts as interceptable");
            Assert.That(CombatEngagement.IsInterceptable(WeaponDelivery.Guided), Is.True, "guided fire is interceptable");
            Assert.That(CombatEngagement.IsInterceptable(WeaponDelivery.Beam), Is.False, "a beam is not interceptable");
            Assert.That(CombatEngagement.IsInterceptable(WeaponDelivery.Slug), Is.False, "a slug is not interceptable");
        }

        [Test]
        [Description("W6b pure helper: PointDefenseInterceptFraction is a saturating curve — 0 with no PD or no missiles; rises toward the cap as PD out-masses the salvo; equal PD-vs-salvo intercepts half; a swarm big enough to out-mass the PD leaks most through; never exceeds the cap.")]
        public void PointDefenseInterceptFraction_SaturatesAndCaps()
        {
            // No PD, or no missiles → nothing intercepted (byte-identical).
            Assert.That(CombatEngagement.PointDefenseInterceptFraction(0, 1000), Is.EqualTo(0), "no PD → intercepts nothing");
            Assert.That(CombatEngagement.PointDefenseInterceptFraction(1000, 0), Is.EqualTo(0), "no missiles → intercepts nothing");

            // Equal PD and salvo → half intercepted: 1000/(1000+1000) = 0.5.
            Assert.That(CombatEngagement.PointDefenseInterceptFraction(1000, 1000), Is.EqualTo(0.5).Within(1e-9),
                "PD equal to the incoming missile fire intercepts half of it");

            // PD ≫ salvo → most stopped (but capped): 10000/(10000+1000) ≈ 0.909.
            double heavy = CombatEngagement.PointDefenseInterceptFraction(10_000, 1000);
            Assert.That(heavy, Is.GreaterThan(0.5).And.LessThanOrEqualTo(CombatEngagement.PointDefenseMaxIntercept),
                "a strong screen vs a light salvo stops most of it, capped below full immunity");

            // Swarm ≫ PD → saturates, most leaks through: 1000/(1000+50000) ≈ 0.0196.
            double swarmed = CombatEngagement.PointDefenseInterceptFraction(1000, 50_000);
            Assert.That(swarmed, Is.LessThan(0.05), "a swarm that out-masses the PD saturates it — most missiles leak through");

            // The cap holds even for absurd PD.
            Assert.That(CombatEngagement.PointDefenseInterceptFraction(1e12, 1000), Is.EqualTo(CombatEngagement.PointDefenseMaxIntercept).Within(1e-9),
                "nothing is ever fully immune — the intercept fraction can't exceed the cap");
        }
    }
}
