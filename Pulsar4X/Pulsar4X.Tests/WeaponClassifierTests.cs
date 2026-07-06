using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Weapon-designer UNIFICATION — the triangle corner (<see cref="WeaponClass"/>) EMERGES from the axes + dials
    /// (docs/WEAPON-TAXONOMY-DESIGN.md, the developer's "the axes are the filing-cabinet path; the type falls out of
    /// the drawer you opened + the dials, not a hand-picked label"). `WeaponProfile.Class` is now a pure computed
    /// read-out — there is no authored type field. Three gauges:
    ///   • the pure classifier hits each corner (beam / railgun / flak / missile) + the disambiguating edges;
    ///   • on every real built base-mod weapon, the computed <see cref="WeaponProfile.Class"/> is the EXPECTED corner;
    ///   • the corner survives fire-mix AGGREGATION (the mix keeps each weapon's real Nature + Delivery).
    /// Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class WeaponClassifierTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[classifier] " + m);

        [Test]
        [Description("The pure classifier derives each triangle corner from Delivery + specs, and the spec checks disambiguate a discrete projectile (a hyper-velocity slug reads as a beam; a pellet-storm slug reads as flak).")]
        public void Classify_DerivesEachCorner_FromDeliveryAndSpecs()
        {
            Assert.That(WeaponClassifier.Classify(WeaponDelivery.Beam, 3e8, 1.0, 1), Is.EqualTo(WeaponClass.Beam), "continuous light-speed → Beam");
            Assert.That(WeaponClassifier.Classify(WeaponDelivery.Slug, 50_000, 0.05, 5), Is.EqualTo(WeaponClass.Railgun), "finite ballistic slug → Railgun");
            Assert.That(WeaponClassifier.Classify(WeaponDelivery.Cloud, 20_000, 0.1, 300), Is.EqualTo(WeaponClass.Flak), "pellet cloud → Flak");
            Assert.That(WeaponClassifier.Classify(WeaponDelivery.Guided, 5_000, 0.9, 1), Is.EqualTo(WeaponClass.Missile), "it tracks → Missile");

            // an EXOTIC ion lance is still delivered as a beam → Beam class (nature is a separate axis, not the class)
            Assert.That(WeaponClassifier.Classify(WeaponDelivery.Beam, 3e8, 1.0, 2), Is.EqualTo(WeaponClass.Beam), "exotic ion beam is still Beam-class by delivery");

            // disambiguation of the discrete-projectile family by spec:
            Assert.That(WeaponClassifier.Classify(WeaponDelivery.Slug, 3e8, 0.05, 1), Is.EqualTo(WeaponClass.Beam), "a hyper-velocity slug behaves like a beam");
            Assert.That(WeaponClassifier.Classify(WeaponDelivery.Slug, 50_000, 0.05, 100), Is.EqualTo(WeaponClass.Flak), "a pellet-storm slug (high saturation) behaves like flak");
        }

        [Test]
        [Description("On every real base-mod weapon, the computed WeaponProfile.Class is the EXPECTED triangle corner — laser→Beam, railgun→Railgun, flak→Flak, ion-disruptor→Beam (an exotic weapon delivered as a beam). The type is emergent from the axes, with no authored field.")]
        public void Class_ComputesToTheExpectedCorner_OnEveryRealBaseModWeapon()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;

            // one ship per corner that actually carries weapons in the base mod → the corner every weapon must compute to
            var expected = new Dictionary<string, WeaponClass>
            {
                { "default-ship-design-test-warship",   WeaponClass.Beam    }, // Aegis  — lasers
                { "default-ship-design-test-railgun",   WeaponClass.Railgun }, // Lancer — railguns
                { "default-ship-design-test-flak",      WeaponClass.Flak    }, // Bulwark — flak
                { "default-ship-design-test-disruptor", WeaponClass.Beam    }, // Ravager — ion disruptors (exotic, beam-delivery)
            };

            int weaponsChecked = 0;
            foreach (var kv in expected)
            {
                Assert.That(designs.ContainsKey(kv.Key), Is.True, $"{kv.Key} loads from the base mod");
                var ship = ShipFactory.CreateShip(designs[kv.Key], s.Faction, s.StartingBody, kv.Key);
                var cv = ship.GetDataBlob<ShipCombatValueDB>();
                Assert.That(cv.Weapons.Count, Is.GreaterThan(0), $"{kv.Key} is armed");

                foreach (var w in cv.Weapons)
                {
                    Assert.That(w.Class, Is.EqualTo(kv.Value),
                        $"{kv.Key}: a {w.Nature}/{w.Delivery} weapon (vel={w.Velocity:0}, sat={w.Saturation:0.##}) must COMPUTE to {kv.Value}");
                    weaponsChecked++;
                }
            }
            Log($"verified the computed corner on {weaponsChecked} real weapon profiles across {expected.Count} ships");
            Assert.That(weaponsChecked, Is.GreaterThanOrEqualTo(expected.Count), "checked at least one weapon per ship");
        }

        [Test]
        [Description("The two axes survive fire-mix AGGREGATION: BuildFireMix buckets by (class, nature, DELIVERY), so a real Vanguard's plasma fire aggregates to a profile that still carries Nature=Energy AND Delivery=Bolt (not defaulted away), so its computed corner (Railgun-class, dodgeable) is preserved through aggregation.")]
        public void Aggregation_PreservesNatureAndDelivery()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Skirmishers");
            var ship = ShipFactory.CreateShip(designs["default-ship-design-test-plasma"], s.Faction, s.StartingBody, "Vanguard");
            ship.FactionOwnerID = s.Faction.Id;
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, fleet, ship));

            var combatShips = CombatEngagement.GetCombatShips(fleet);
            var mix = CombatEngagement.BuildFireMix(combatShips);
            Assert.That(mix.Count, Is.EqualTo(1), "the Vanguard's 3 identical plasma repeaters aggregate to ONE bucket");
            var plasma = mix[0];
            Log($"aggregated: class={plasma.Class} nature={plasma.Nature} delivery={plasma.Delivery}");

            Assert.That(plasma.Nature, Is.EqualTo(WeaponNature.Energy), "aggregation preserves the Energy nature (the shield matchup survives)");
            Assert.That(plasma.Delivery, Is.EqualTo(WeaponDelivery.Bolt), "aggregation preserves the Bolt delivery (it was dropped to the Slug default before this fix)");
            Assert.That(plasma.Class, Is.EqualTo(WeaponClass.Railgun), "so the emergent corner (dodgeable Railgun-class) survives aggregation");
        }
    }
}
