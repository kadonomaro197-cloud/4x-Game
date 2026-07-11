using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Weapons ⚙1 ▸ BALLISTIC — the SIEGE-railgun Muzzle-Velocity dial cost (S14). Completes "muzzle velocity is
    /// EARNED, not free" across ALL ballistic mass-drivers: the ordinary railgun got its velocity cost in S10; the
    /// siege railgun (a spinal mass-driver) was still free. A dedicated free-dial audit of every base-mod template
    /// (mass-chain-resolved, so a dial costed through a derived <c>Mass</c> property is NOT flagged) found the siege
    /// railgun's Muzzle Velocity was one of the last truly-free ADVANTAGE dials — a hypervelocity slug beats evasion
    /// (velocity feeds the dodge model) for zero mass.
    ///
    /// Fix: the <c>siege-railgun</c> Mass formula now adds <c>Max(0, MuzzleVelocity - 50000) / 1000</c> (bigger rails
    /// and capacitors). The base-mod <c>default-design-high-velocity-siege</c> (Muzzle Velocity 250,000 m/s, 5× the
    /// 50,000 baseline) weighs exactly +200 more than the stock siege railgun; anchored at the 50,000 baseline → the
    /// stock siege design (and the Bombard that mounts it) is byte-identical. Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipHighVelocitySiegeTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[siege-velocity-cost] " + m);

        [Test]
        [Description("The siege-railgun Muzzle Velocity dial now costs mass: the hypervelocity siege design weighs exactly the velocity-cost term more than the stock siege railgun (Max(0, 250000-50000)/1000 = 200), and the stock design (anchored at the 50,000 baseline → zero extra) is byte-identical.")]
        public void TheSiegeMuzzleVelocityDial_CostsMass_StockUntouched()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            long stockMass = designs["default-design-siege-railgun"].MassPerUnit;
            long fastMass = designs["default-design-high-velocity-siege"].MassPerUnit;
            Log($"stock siege mass = {stockMass}, hypervelocity siege = {fastMass}, delta = {fastMass - stockMass}");
            Assert.That(fastMass, Is.GreaterThan(stockMass),
                "hypervelocity now costs mass — a faster siege slug is earned, not free");
            Assert.That(fastMass - stockMass, Is.EqualTo(200),
                "the extra mass is exactly the velocity-cost term (250,000 - 50,000 anchor) / 1000 — the stock siege pays nothing (byte-identical)");
        }
    }
}
