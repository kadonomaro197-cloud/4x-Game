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
    /// Weapons ⚙1 ▸ BALLISTIC — the MUZZLE-VELOCITY dial, cradle-to-grave (S10). The railgun designer's Muzzle
    /// Velocity knob is a real dial that decides who it can HIT: a faster slug spends less time in flight, so it
    /// beats evasion (the dodge model reads <c>velocityTerm = velocity/(velocity + VelocityReference)</c> — the
    /// weapon triangle's "fast slug juks the juker"). But it was FREE — the railgun Mass formula ignored Muzzle
    /// Velocity, so max velocity cost nothing.
    ///
    /// This slice closes both ends: the base-mod <c>default-design-high-velocity-railgun</c> dials Muzzle Velocity
    /// to 200,000 m/s (4× the 50,000 baseline), mounts on the new Culverin cruiser, and:
    ///   (1) COMBAT PAYOFF — its railgun <see cref="WeaponProfile.Velocity"/> reads 200,000 and lands MORE fire on
    ///       an evasive target than a default-railgun Lancer (higher <c>HitFraction</c> at high evasion).
    ///   (2) THE COST — velocity now costs MASS: the railgun Mass formula adds <c>Max(0, MuzzleVelocity-50000)/1000</c>,
    ///       so the high-velocity design weighs exactly +150 more than the default railgun (which, anchored at the
    ///       50,000 baseline, pays nothing → byte-identical). Fast is EARNED, not free.
    ///
    /// Cradle-to-grave: JSON design (Muzzle Velocity override) → NCalc <c>railgunAtbArgs</c> → RailgunWeaponAtb →
    /// ShipCombatValueDB.WeaponProfile → the dodge resolver. Additive / byte-identical (new design + new ship; every
    /// existing railgun uses the 50,000 baseline). Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipHighVelocityRailgunTests
    {
        private const string Culverin = "default-ship-design-test-culverin";
        private const string Lancer = "default-ship-design-test-railgun";
        private static void Log(string m) => TestContext.Progress.WriteLine("[high-velocity-railgun] " + m);

        private static Entity Build(TestScenario s, string designId, string name)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            return ShipFactory.CreateShip(designs[designId], s.Faction, s.StartingBody, name);
        }

        private static WeaponProfile FirstRailgun(Entity ship) =>
            ship.GetDataBlob<ShipCombatValueDB>().Weapons.First(w => w.Class == WeaponClass.Railgun);

        [Test]
        [Description("Combat payoff: the Culverin's high-velocity railgun reads 200,000 m/s (vs the Lancer's 50,000) and lands MORE fire on an evasive (0.9) target — the muzzle-velocity dial beats the dodge, through the real design → RailgunWeaponAtb → WeaponProfile → HitFraction path.")]
        public void TheHighVelocityRailgun_BeatsEvasion_BetterThanTheDefault()
        {
            var s = TestScenario.CreateWithColony();
            var fast = FirstRailgun(Build(s, Culverin, "Culverin"));
            var stock = FirstRailgun(Build(s, Lancer, "Lancer"));
            Log($"Culverin muzzle vel={fast.Velocity:0}, Lancer={stock.Velocity:0}");
            Assert.That(fast.Velocity, Is.EqualTo(200_000).Within(1), "the Muzzle Velocity dial reached the gun");
            Assert.That(stock.Velocity, Is.EqualTo(50_000).Within(1), "the default railgun is untouched (byte-identical)");

            double fastHit = CombatEngagement.HitFraction(fast, 0.9);
            double stockHit = CombatEngagement.HitFraction(stock, 0.9);
            Log($"vs evasion 0.9: high-vel lands {fastHit:0.###}, default lands {stockHit:0.###}");
            Assert.That(fastHit, Is.GreaterThan(stockHit),
                "the faster slug spends less time in flight → it beats the dodge better than the baseline railgun");
        }

        [Test]
        [Description("The cost: velocity is EARNED — the high-velocity railgun weighs exactly the velocity-cost term more than the default (Max(0, 200000-50000)/1000 = 150), and the default railgun (anchored at 50,000 → zero extra) is byte-identical. The premium flows on to crew/research/credits/materials via [Mass].")]
        public void TheMuzzleVelocityDial_CostsMass_DefaultUntouched()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            long stockMass = designs["default-design-railgun-weapon"].MassPerUnit;
            long fastMass = designs["default-design-high-velocity-railgun"].MassPerUnit;
            Log($"default railgun mass = {stockMass}, high-velocity = {fastMass}, delta = {fastMass - stockMass}");
            Assert.That(fastMass, Is.GreaterThan(stockMass),
                "muzzle velocity now costs mass — fast is earned, not free");
            Assert.That(fastMass - stockMass, Is.EqualTo(150),
                "the extra mass is exactly the velocity-cost term (200,000 - 50,000 anchor) / 1000 — the default pays nothing (byte-identical)");
        }
    }
}
