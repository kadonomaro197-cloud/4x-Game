using NUnit.Framework;
using Pulsar4X.Combat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The weapon designer's TWO independent axes (docs/WEAPON-TAXONOMY-DESIGN.md, developer's call 2026-07-06). A
    /// <see cref="WeaponProfile"/> now carries <b>Nature</b> (what meets the defence: shields/armour) AND <b>Delivery</b>
    /// (what meets the dodge: velocity/pattern), separately from the transitional fused <see cref="WeaponClass"/>. This
    /// is the foundation the taxonomy needs so a blaster (Energy nature + a slow, DODGEABLE Bolt) — which the old fused
    /// class couldn't express — becomes buildable. Additive: WeaponClass + all combat behaviour are unchanged (this slice
    /// only records the axes; making the triangle EMERGE from them is a later slice). Pure → runs in CI.
    /// </summary>
    [TestFixture]
    public class WeaponAxesTests
    {
        [Test]
        [Description("Nature and Delivery are INDEPENDENT axes: a phaser and a blaster share the same Energy nature but have opposite deliveries (undodgeable Beam vs dodgeable Bolt) — the distinction the fused WeaponClass could not make. Both survive the copy ctor.")]
        public void WeaponProfile_CarriesNatureAndDeliveryAxes_Independently()
        {
            // a phaser: energy nature, beam delivery (~c → undodgeable)
            var phaser = new WeaponProfile(WeaponClass.Beam, damagePerSecond: 100, velocity: 3e8, tracking: 0.9, saturation: 1, range_m: 5000,
                nature: WeaponNature.Energy, delivery: WeaponDelivery.Beam);
            Assert.That(phaser.Nature, Is.EqualTo(WeaponNature.Energy));
            Assert.That(phaser.Delivery, Is.EqualTo(WeaponDelivery.Beam));

            // the build the OLD model couldn't express: a blaster — ENERGY nature but a slow, DODGEABLE Bolt
            var blaster = new WeaponProfile(WeaponClass.Beam, damagePerSecond: 40, velocity: 500, tracking: 0.1, saturation: 2, range_m: 2000,
                nature: WeaponNature.Energy, delivery: WeaponDelivery.Bolt);
            Assert.That(blaster.Nature, Is.EqualTo(phaser.Nature), "same nature as the phaser (energy — bleeds through shields)");
            Assert.That(blaster.Delivery, Is.Not.EqualTo(phaser.Delivery), "but a DIFFERENT delivery (dodgeable bolt) — the two axes are independent");

            // the copy ctor (used when a design is cloned) preserves both axes
            var copy = new WeaponProfile(blaster);
            Assert.That(copy.Nature, Is.EqualTo(WeaponNature.Energy));
            Assert.That(copy.Delivery, Is.EqualTo(WeaponDelivery.Bolt));
        }

        [Test]
        [Description("The old fused-class default is preserved for callers that don't specify the axes (backward-compatible): a WeaponProfile built without Nature/Delivery reads the Kinetic/Slug default, so nothing that pre-dates the axes changes behaviour.")]
        public void WeaponProfile_DefaultsToKineticSlug_WhenAxesUnspecified()
        {
            var legacy = new WeaponProfile(WeaponClass.Railgun, damagePerSecond: 50, velocity: 5e4, tracking: 0.05, saturation: 5);
            Assert.That(legacy.Nature, Is.EqualTo(WeaponNature.Kinetic));
            Assert.That(legacy.Delivery, Is.EqualTo(WeaponDelivery.Slug));
        }
    }
}
