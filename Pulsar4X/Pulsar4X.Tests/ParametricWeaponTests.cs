using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Weapons;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C / Path B — the GENERIC PARAMETRIC WEAPON, end to end (B1b).
    ///
    /// The weapons design tool (<c>docs/Actual HTMLs Of designers/weaponsderived.html</c>) proposes ONE weapon form —
    /// pick Delivery × Nature + a few numbers, and every gun falls out. This fixture proves the ENGINE half of that
    /// collapse is real and buildable:
    ///   (1) the base-mod <c>parametric-weapon</c> JSON template builds a real <c>ParametricWeaponAtb</c> through the
    ///       live path (template → NCalc → atb via reflection) — the gotcha-10 sensor, the same risk
    ///       <c>RailgunWeaponTests</c> guards; and the default design reproduces a Beam / Energy laser regime.
    ///   (2) ONE form reproduces every gun-type's REGIME: Slug → railgun range, Cloud → flak range, Guided → missile
    ///       range, Beam+Exotic → disruptor range at light-speed, Bolt → mid range with a finite velocity. This is the
    ///       parametric collapse (11 per-type weapons expressible through one component), via the fidelity-proven
    ///       <see cref="Components.Designers.WeaponsDesignModel"/> that <see cref="ParametricWeaponAtb.BuildProfile"/> runs.
    ///
    /// Engine-only → runs in CI. Additive: no ship mounts a parametric weapon, so live combat is byte-identical; this
    /// only proves the new design PATH works.
    /// </summary>
    [TestFixture]
    public class ParametricWeaponTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[parametric] " + m);

        [Test]
        [Description("The base-mod parametric-weapon template builds a ParametricWeaponAtb from JSON, and its default design reproduces a Beam/Energy laser profile.")]
        public void ParametricLaser_BuildsFromJson_BindsAtb_ReproducesBeamEnergyRegime()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;
            Assert.That(designs.ContainsKey("default-design-parametric-laser"), Is.True,
                "the parametric-laser design loads onto the faction — the JSON parametric-weapon template is unlocked + the ComponentDesign registered");

            var design = designs["default-design-parametric-laser"] as ComponentDesign;
            Assert.That(design, Is.Not.Null, "default-design-parametric-laser is a ComponentDesign");

            // gotcha-10: JSON template -> NCalc AtbConstrArgs -> ParametricWeaponAtb constructor binding (via reflection).
            var atb = design.GetAttribute<ParametricWeaponAtb>();
            Assert.That(atb, Is.Not.Null, "the design bound a ParametricWeaponAtb (arg order / AttributeType wired right)");
            Assert.That(atb.Delivery, Is.EqualTo(WeaponDelivery.Beam), "default delivery = Beam");
            Assert.That(atb.Nature, Is.EqualTo(WeaponNature.Energy), "default nature = Energy (a laser)");

            var p = atb.BuildProfile();
            Log($"laser profile: nature={p.Nature} delivery={p.Delivery} vel={p.Velocity:0} range={p.Range_m:0} dps={p.DamagePerSecond:0}");
            Assert.That(p.Nature, Is.EqualTo(WeaponNature.Energy));
            Assert.That(p.Delivery, Is.EqualTo(WeaponDelivery.Beam));
            Assert.That(p.Velocity, Is.GreaterThan(1e8), "a beam is ~light-speed — the delivery forces it (undodgeable)");
            Assert.That(p.Range_m, Is.EqualTo(atb.Range_m).Within(1), "a non-exotic beam writes its OWN reach");
            Assert.That(p.DamagePerSecond, Is.GreaterThan(0), "the dialed damage/sec flows into the profile");
        }

        [Test]
        [Description("ONE parametric form reproduces every gun-type's velocity/range regime — the collapse the weapons HTML draws.")]
        public void ParametricWeapon_ReproducesEveryDeliveryRegime()
        {
            // Slug -> railgun: finite muzzle velocity, mid class range, kinetic.
            var slug = new ParametricWeaponAtb((double)WeaponDelivery.Slug, (double)WeaponNature.Kinetic,
                200000, 5, 0, 50000, 0.05, 0, 0, 0).BuildProfile();
            Assert.That(slug.Delivery, Is.EqualTo(WeaponDelivery.Slug));
            Assert.That(slug.Velocity, Is.EqualTo(50000).Within(1), "a slug keeps its dialed (finite) muzzle velocity — dodgeable");
            Assert.That(slug.Range_m, Is.EqualTo(ShipCombatValueDB.RailgunRange_m).Within(1), "slug takes the railgun class range");

            // Cloud -> flak: short class range, high saturation.
            var cloud = new ParametricWeaponAtb((double)WeaponDelivery.Cloud, (double)WeaponNature.Kinetic,
                30000, 300, 0, 20000, 0.1, 0, 0, 0).BuildProfile();
            Assert.That(cloud.Range_m, Is.EqualTo(ShipCombatValueDB.FlakRange_m).Within(1), "cloud takes the short flak class range");

            // Guided -> missile: long class range.
            var guided = new ParametricWeaponAtb((double)WeaponDelivery.Guided, (double)WeaponNature.Explosive,
                100000, 1, 0, 0, -1, 0, 0, 0).BuildProfile();
            Assert.That(guided.Delivery, Is.EqualTo(WeaponDelivery.Guided));
            Assert.That(guided.Range_m, Is.EqualTo(ShipCombatValueDB.MissileRange_m).Within(1), "guided takes the long missile class range");

            // Beam + Exotic -> ion disruptor: light-speed, its own exotic class range, bypasses shields.
            var ion = new ParametricWeaponAtb((double)WeaponDelivery.Beam, (double)WeaponNature.Exotic,
                150000, 2, 0, 0, -1, 0, 0, 0).BuildProfile();
            Assert.That(ion.Nature, Is.EqualTo(WeaponNature.Exotic));
            Assert.That(ion.Velocity, Is.GreaterThan(1e8), "exotic beam is light-speed too (undodgeable)");
            Assert.That(ion.Range_m, Is.EqualTo(ShipCombatValueDB.DisruptorRange_m).Within(1), "exotic beam takes the disruptor class range");

            // Bolt + Energy -> plasma: finite velocity (dodgeable) but energy nature (bleeds shields), mid range.
            var bolt = new ParametricWeaponAtb((double)WeaponDelivery.Bolt, (double)WeaponNature.Energy,
                100000, 3, 0, 200000, 0.1, 0, 0, 0).BuildProfile();
            Assert.That(bolt.Nature, Is.EqualTo(WeaponNature.Energy));
            Assert.That(bolt.Velocity, Is.EqualTo(200000).Within(1), "a bolt keeps its dialed finite velocity");
            Assert.That(bolt.Range_m, Is.EqualTo(ShipCombatValueDB.RailgunRange_m).Within(1), "bolt reuses the mid range");

            Log("collapse OK: Slug/Cloud/Guided/Beam-Exotic/Bolt each reproduced their weapon-type regime through ONE form");
        }
    }
}
