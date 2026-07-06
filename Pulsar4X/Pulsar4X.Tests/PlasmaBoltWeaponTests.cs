using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The TWO-AXIS payoff, on real data — the Plasma Repeater (a bolt-thrower) through the REAL data path. It is the
    /// weapon the fused single-axis <see cref="WeaponClass"/> enum could NOT express and the split Nature × Delivery
    /// axes can: a plasma bolt is <b>ENERGY in nature</b> (a shield only half-soaks it — it bleeds through, like a beam)
    /// yet <b>dodgeable in delivery</b> (a finite-velocity bolt — a nimble ship jukes it, like a railgun slug).
    ///
    /// The crisp proof here: the plasma bolt and a kinetic railgun are the SAME dodge-class (both compute to Railgun —
    /// both finite-velocity, both juke-able) but behave DIFFERENTLY against a shield (the energy bolt bleeds through, the
    /// kinetic slug is fully soaked). Same Delivery-class, different Nature — the two axes are independent. Builds the
    /// base-mod <c>default-ship-design-test-plasma</c> (Vanguard) the way the live game does (JSON <c>plasma-repeater</c>
    /// template → <c>PlasmaBoltWeaponAtb</c> via reflection → <see cref="ShipCombatValueDB"/>), so CI catches a template/
    /// ctor drift instead of the developer's New Game crashing (gotcha #10). Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class PlasmaBoltWeaponTests
    {
        private const string PlasmaShip = "default-ship-design-test-plasma"; // Vanguard — 3 × plasma repeater
        private static void Log(string m) => TestContext.Progress.WriteLine("[plasma] " + m);

        [Test]
        [Description("The base-mod Plasma Repeater builds from JSON as a finite-velocity, dodgeable (Railgun-class) ENERGY bolt: a nimble ship jukes it like a railgun, but a shield only half-soaks it like a beam — where a same-class kinetic railgun is fully soaked. Same dodge-class, different nature: the two axes are independent.")]
        public void PlasmaDesign_BuildsRealComponent_DodgeableLikeARailgun_ButEnergyVsShields()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(PlasmaShip), Is.True,
                "the Vanguard plasma skirmisher loads onto the faction — the JSON plasma-repeater template + component + ship wired up");

            // Build it the real way (instantiates PlasmaBoltWeaponAtb from JSON via reflection).
            var ship = ShipFactory.CreateShip(designs[PlasmaShip], s.Faction, s.StartingBody, "Vanguard");
            var cv = ship.GetDataBlob<ShipCombatValueDB>();
            var plasma = cv.Weapons.FirstOrDefault(w => w.Delivery == WeaponDelivery.Bolt);
            Log($"Vanguard: firepower={cv.Firepower:0}; plasma profile: nature={plasma?.Nature} delivery={plasma?.Delivery} vel={plasma?.Velocity:0} class={plasma?.Class} computed={plasma?.ComputedClass}");

            Assert.That(cv.Firepower, Is.GreaterThan(0), "the Vanguard is armed (plasma firepower flows into the combat value)");
            Assert.That(plasma, Is.Not.Null, "a Plasma Repeater produced a Bolt-delivery profile — JSON template → PlasmaBoltWeaponAtb → combat value is wired");
            Assert.That(plasma.Nature, Is.EqualTo(WeaponNature.Energy), "plasma is ENERGY in nature");
            Assert.That(plasma.Delivery, Is.EqualTo(WeaponDelivery.Bolt), "...delivered as a discrete dodgeable bolt");
            Assert.That(plasma.Velocity, Is.LessThan(WeaponClassifier.BeamVelocityThreshold_mps),
                "finite velocity → dodgeable (a beam is not)");
            Assert.That(plasma.ComputedClass, Is.EqualTo(WeaponClass.Railgun),
                "in the dodge model it reads as Railgun-class (finite-velocity, dodgeable) ...");
            Assert.That(plasma.ComputedClass, Is.EqualTo(plasma.Class), "...and the computed class matches the authored one (the unification invariant)");

            // DODGEABLE like a railgun: a nimble ship evades much of it (a beam it could not).
            double vsSluggish = CombatEngagement.HitFraction(plasma, 0.0);
            double vsNimble = CombatEngagement.HitFraction(plasma, 0.9);
            Log($"hit fraction: vsSluggish={vsSluggish:0.###}, vsNimble={vsNimble:0.###}");
            Assert.That(vsNimble, Is.LessThan(vsSluggish), "a nimble ship dodges much of the finite-velocity bolt");
            Assert.That(vsNimble, Is.LessThan(0.9), "...meaningfully — it is not an undodgeable beam");

            // ENERGY vs shields: a shield only HALF-soaks the plasma (it bleeds through), where a same-dodge-class
            // KINETIC railgun is FULLY soaked. That difference is the whole point of splitting Nature from Delivery.
            double plasmaSoak = CombatEngagement.SoakFractionOf(new List<WeaponProfile> { plasma });
            var kineticRailgun = new WeaponProfile(WeaponClass.Railgun, 1, 50_000, 0.05, 5, 0, WeaponNature.Kinetic, WeaponDelivery.Slug);
            double kineticSoak = CombatEngagement.SoakFractionOf(new List<WeaponProfile> { kineticRailgun });
            Log($"shield soak fraction: plasma(energy)={plasmaSoak:0.##}, railgun(kinetic)={kineticSoak:0.##}");

            Assert.That(plasmaSoak, Is.EqualTo(CombatEngagement.ShieldSoakVsEnergy),
                "a shield only half-soaks the energy plasma bolt (it bleeds through, like a beam)");
            Assert.That(kineticSoak, Is.EqualTo(CombatEngagement.ShieldSoakVsKinetic),
                "...where the same-dodge-class kinetic railgun is fully soaked");
            Assert.That(plasmaSoak, Is.LessThan(kineticSoak),
                "same dodge-class, but the energy bolt beats a shield better than the kinetic slug — the two axes are independent (the two-axis payoff)");
        }
    }
}
