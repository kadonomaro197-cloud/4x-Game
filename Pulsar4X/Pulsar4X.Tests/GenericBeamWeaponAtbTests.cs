using NUnit.Framework;
using Pulsar4X.Weapons;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Weapon-designer SCALE SPAN (beam correctness slice): a beam's pulse energy must span up to a superlaser without
    /// the field type capping it. The whole downstream chain (FireBeamWeapon, ShipCombatValueDB firepower, the
    /// power-draw check) already uses a double; only <see cref="GenericBeamWeaponAtb.Energy"/> was an int, which
    /// silently overflowed (wrapped negative) past ~2.1 GJ — the top of the beam scale. This gauges that a
    /// superlaser-scale design keeps its full energy. Pure unit test (no JSON caps in the way) → runs in CI.
    /// Design: docs/GROUND-UNIT-DESIGNER-DESIGN.md §6a-ii (weapon-designer scale span, task #2).
    /// </summary>
    [TestFixture]
    public class GenericBeamWeaponAtbTests
    {
        [Test]
        [Description("A superlaser-scale pulse energy (> int.MaxValue joules, ~2.1 GJ) survives on the beam attribute — no int wrap. Proves the beam's energy field spans to the top of its purview.")]
        public void BeamPulseEnergy_HoldsSuperlaserScale_WithoutIntOverflow()
        {
            const double superlaserJoules = 5_000_000_000.0;   // 5 GJ — well past int.MaxValue (~2.147 GJ)
            var beam = new GenericBeamWeaponAtb(maxRange: 100000, waveLen: 700, jules: superlaserJoules);

            Assert.That(beam.Energy, Is.EqualTo(superlaserJoules).Within(1.0),
                "the pulse energy survives at superlaser scale — the old int field would have wrapped");
            Assert.That(beam.Energy, Is.GreaterThan(int.MaxValue),
                "and it exceeds what the old int field could hold (the overflow that capped a superlaser's firepower)");

            // and it round-trips through the copy ctor (used when a design is cloned)
            var copy = new GenericBeamWeaponAtb(beam);
            Assert.That(copy.Energy, Is.EqualTo(superlaserJoules).Within(1.0), "the copy keeps the full energy too");
        }
    }
}
