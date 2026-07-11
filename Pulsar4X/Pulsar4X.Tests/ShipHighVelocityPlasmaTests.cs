using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;
using Pulsar4X.Weapons;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Weapons ⚙1 ▸ EXOTIC/ENERGY-BOLT — the plasma BOLT-VELOCITY dial, cradle-to-grave (S11). A plasma repeater's
    /// bolts are ENERGY (a shield only half-soaks them) but travel at a FINITE velocity, so — like a railgun slug — a
    /// nimble ship can dodge them. Bolt Velocity decides how well they beat that dodge (the same
    /// <c>velocityTerm = velocity/(velocity + VelocityReference)</c> the resolver reads). But it was FREE — the
    /// plasma Mass formula ignored Bolt Velocity, so max velocity cost nothing.
    ///
    /// This slice closes both ends: the base-mod <c>default-design-high-velocity-plasma</c> dials Bolt Velocity to
    /// 600,000 m/s (3× the 200,000 baseline) on the new Tempest Plasma Lancer, and:
    ///   (1) COMBAT PAYOFF — its plasma <see cref="WeaponProfile.Velocity"/> reads 600,000 and lands MORE fire on an
    ///       evasive target than a default-plasma Vanguard (higher <c>HitFraction</c> at high evasion).
    ///   (2) THE COST — Bolt Velocity now costs MASS: the plasma Mass formula adds
    ///       <c>Max(0, Bolt Velocity - 200000) / 10000</c>, so the high-velocity design weighs exactly +40 more than
    ///       the default (which, anchored at 200,000, pays nothing → byte-identical). Fast is EARNED, not free.
    ///
    /// Cradle-to-grave: JSON design (Bolt Velocity override) → NCalc <c>plasmaAtbArgs</c> → PlasmaBoltWeaponAtb →
    /// ShipCombatValueDB.WeaponProfile → the dodge resolver. Additive / byte-identical (new design + new ship; every
    /// existing plasma uses the 200,000 baseline). Engine-only → CI. The third of the free-velocity-dial trio
    /// (laser Range S9, railgun Muzzle Velocity S10, plasma Bolt Velocity S11).
    /// </summary>
    [TestFixture]
    public class ShipHighVelocityPlasmaTests
    {
        private const string Tempest = "default-ship-design-test-tempest";
        private const string Vanguard = "default-ship-design-test-plasma";
        private static void Log(string m) => TestContext.Progress.WriteLine("[high-velocity-plasma] " + m);

        private static Entity Build(TestScenario s, string designId, string name)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            return ShipFactory.CreateShip(designs[designId], s.Faction, s.StartingBody, name);
        }

        private static WeaponProfile FirstPlasma(Entity ship) =>
            ship.GetDataBlob<ShipCombatValueDB>().Weapons.First(w => w.Delivery == WeaponDelivery.Bolt);

        [Test]
        [Description("Combat payoff: the Tempest's high-velocity plasma reads 600,000 m/s (vs the Vanguard's 200,000) and lands MORE fire on an evasive (0.9) target — the bolt-velocity dial beats the dodge, through the real design → PlasmaBoltWeaponAtb → WeaponProfile → HitFraction path.")]
        public void TheHighVelocityPlasma_BeatsEvasion_BetterThanTheDefault()
        {
            var s = TestScenario.CreateWithColony();
            var fast = FirstPlasma(Build(s, Tempest, "Tempest"));
            var stock = FirstPlasma(Build(s, Vanguard, "Vanguard"));
            Log($"Tempest bolt vel={fast.Velocity:0}, Vanguard={stock.Velocity:0}");
            Assert.That(fast.Velocity, Is.EqualTo(600_000).Within(1), "the Bolt Velocity dial reached the gun");
            Assert.That(stock.Velocity, Is.EqualTo(200_000).Within(1), "the default plasma is untouched (byte-identical)");

            double fastHit = CombatEngagement.HitFraction(fast, 0.9);
            double stockHit = CombatEngagement.HitFraction(stock, 0.9);
            Log($"vs evasion 0.9: high-vel lands {fastHit:0.###}, default lands {stockHit:0.###}");
            Assert.That(fastHit, Is.GreaterThan(stockHit),
                "the faster bolt spends less time in flight → it beats the dodge better than the baseline plasma");
        }

        [Test]
        [Description("The cost: bolt velocity is EARNED — the high-velocity plasma weighs exactly the velocity-cost term more than the default (Max(0, 600000-200000)/10000 = 40), and the default plasma (anchored at 200,000 → zero extra) is byte-identical.")]
        public void TheBoltVelocityDial_CostsMass_DefaultUntouched()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            long stockMass = designs["default-design-plasma-repeater"].MassPerUnit;
            long fastMass = designs["default-design-high-velocity-plasma"].MassPerUnit;
            Log($"default plasma mass = {stockMass}, high-velocity = {fastMass}, delta = {fastMass - stockMass}");
            Assert.That(fastMass, Is.GreaterThan(stockMass),
                "bolt velocity now costs mass — fast is earned, not free");
            Assert.That(fastMass - stockMass, Is.EqualTo(40),
                "the extra mass is exactly the velocity-cost term (600,000 - 200,000 anchor) / 10000 — the default pays nothing (byte-identical)");
        }
    }
}
