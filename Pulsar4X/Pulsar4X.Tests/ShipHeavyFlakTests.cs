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
    /// Weapons ⚙1 ▸ BALLISTIC/FLAK — the DAMAGE-PER-PELLET dial, cradle-to-grave (S12). Flak's identity is SATURATION
    /// (rounds × pellets, which already costs mass); Damage Per Pellet is how hard each pellet HITS. It was FREE — the
    /// flak Mass formula ignored Damage Per Pellet — an inconsistency: a railgun's damage dial (Kinetic Energy Per
    /// Shot) DOES cost mass, so flak's should too. A player could dial pellet damage to the ceiling for nothing.
    ///
    /// This slice closes both ends: the base-mod <c>default-design-heavy-flak</c> dials Damage Per Pellet to 5,000 J
    /// (5× the 1,000 baseline) on the new Redoubt Heavy Flak Escort, and:
    ///   (1) FIREPOWER PAYOFF — its flak <see cref="WeaponProfile.DamagePerSecond"/> (= damage/pellet × saturation)
    ///       is higher than a default-flak Bulwark's, at the same saturation.
    ///   (2) THE COST — pellet damage now costs MASS: the flak Mass formula adds
    ///       <c>Max(0, Damage Per Pellet - 1000) / 100</c>, so the heavy design weighs exactly +40 more than the
    ///       default (which, anchored at 1,000, pays nothing → byte-identical). Harder-hitting flak is EARNED.
    ///
    /// Cradle-to-grave: JSON design (Damage Per Pellet override) → NCalc flakAtbArgs → FlakWeaponAtb →
    /// ShipCombatValueDB.WeaponProfile → the resolver. Additive / byte-identical (new design + new ship; every
    /// existing flak uses the 1,000 baseline). Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipHeavyFlakTests
    {
        private const string Redoubt = "default-ship-design-test-redoubt";
        private const string Bulwark = "default-ship-design-test-flak";
        private static void Log(string m) => TestContext.Progress.WriteLine("[heavy-flak] " + m);

        private static Entity Build(TestScenario s, string designId, string name)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            return ShipFactory.CreateShip(designs[designId], s.Faction, s.StartingBody, name);
        }

        private static WeaponProfile FirstFlak(Entity ship) =>
            ship.GetDataBlob<ShipCombatValueDB>().Weapons.First(w => w.Class == WeaponClass.Flak);

        [Test]
        [Description("Firepower payoff: the Redoubt's heavy flak hits harder per pellet (Damage Per Pellet 5,000 vs the Bulwark's 1,000) so its flak WeaponProfile carries more damage/sec at the same saturation — the dial reached the gun through the real design → FlakWeaponAtb → WeaponProfile path.")]
        public void TheHeavyFlak_HitsHarder_ThanTheDefault()
        {
            var s = TestScenario.CreateWithColony();
            var heavy = FirstFlak(Build(s, Redoubt, "Redoubt"));
            var stock = FirstFlak(Build(s, Bulwark, "Bulwark"));
            Log($"Redoubt flak dps={heavy.DamagePerSecond:0} (sat {heavy.Saturation:0}), Bulwark dps={stock.DamagePerSecond:0} (sat {stock.Saturation:0})");
            Assert.That(heavy.Saturation, Is.EqualTo(stock.Saturation).Within(0.001),
                "same saturation (rounds × pellets) — only the per-pellet damage differs");
            Assert.That(heavy.DamagePerSecond, Is.GreaterThan(stock.DamagePerSecond),
                "the Damage Per Pellet dial reached the gun — heavier flak deals more damage/sec");
        }

        [Test]
        [Description("The cost: harder-hitting flak is EARNED — the heavy flak weighs exactly the damage-cost term more than the default (Max(0, 5000-1000)/100 = 40), and the default flak (anchored at 1,000 → zero extra) is byte-identical. This brings flak in line with the railgun, whose damage dial already costs mass.")]
        public void TheDamagePerPelletDial_CostsMass_DefaultUntouched()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            long stockMass = designs["default-design-flak-weapon"].MassPerUnit;
            long heavyMass = designs["default-design-heavy-flak"].MassPerUnit;
            Log($"default flak mass = {stockMass}, heavy flak = {heavyMass}, delta = {heavyMass - stockMass}");
            Assert.That(heavyMass, Is.GreaterThan(stockMass),
                "pellet damage now costs mass — harder-hitting flak is earned, not free");
            Assert.That(heavyMass - stockMass, Is.EqualTo(40),
                "the extra mass is exactly the damage-cost term (5,000 - 1,000 anchor) / 100 — the default pays nothing (byte-identical)");
        }
    }
}
