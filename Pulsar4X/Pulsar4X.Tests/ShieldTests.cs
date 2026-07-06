using NUnit.Framework;
using Pulsar4X.Combat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// SPACE SHIELD layer, Phase A (docs/WEAPON-TAXONOMY-DESIGN.md §6, developer's call 2026-07-06). The shield is the
    /// "shield" mechanism on the defence axis — a depleting + regenerating energy POOL (option B). This slice lays the
    /// foundation: the <see cref="ShieldAtb"/> component carries the pool, and <see cref="ShipCombatValueDB"/> sums it
    /// (health-scaled). ADDITIVE — an unshielded ship reads a 0 pool, so combat is byte-identical until a shield is
    /// fitted. The resolve depleting/regenerating it ("shields at 40%!") + the base-mod generator are later slices.
    /// Pure → runs in CI.
    /// </summary>
    [TestFixture]
    public class ShieldTests
    {
        [Test]
        [Description("The ShieldAtb carries a depleting/regen pool (Capacity_J + RegenRate_Jps) and clamps negatives; ShipCombatValueDB carries the shield-pool fields defaulting to 0, so every ship today (none has a generator) reads 0 → combat unchanged.")]
        public void ShieldAtb_CarriesPool_AndCombatValueDefaultsToZero()
        {
            var gen = new ShieldAtb(capacity_J: 500000, regenRate_Jps: 10000);
            Assert.That(gen.Capacity_J, Is.EqualTo(500000), "the shield pool size");
            Assert.That(gen.RegenRate_Jps, Is.EqualTo(10000), "the recharge rate");

            var clamped = new ShieldAtb(-1, -1);
            Assert.That(clamped.Capacity_J, Is.EqualTo(0), "negative capacity clamps to 0");
            Assert.That(clamped.RegenRate_Jps, Is.EqualTo(0), "negative regen clamps to 0");

            var unshielded = new ShipCombatValueDB();
            Assert.That(unshielded.ShieldCapacity_J, Is.EqualTo(0), "an unshielded ship has a 0 pool — combat is byte-identical");
            Assert.That(unshielded.ShieldRegen_Jps, Is.EqualTo(0));
        }
    }
}
