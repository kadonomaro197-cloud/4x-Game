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
    /// Weapon-designer UNIFICATION, step 1 — the triangle corner (<see cref="WeaponClass"/>) is DERIVABLE from the
    /// Delivery axis + specs, not just authored (docs/WEAPON-TAXONOMY-DESIGN.md, the developer's "one designer, the
    /// triangle EMERGES"). Two gauges:
    ///   • the pure classifier hits each corner (beam / railgun / flak / missile) + the disambiguating edges;
    ///   • THE INVARIANT, on real built ships: every base-mod weapon's <see cref="WeaponProfile.ComputedClass"/> equals
    ///     its AUTHORED <see cref="WeaponProfile.Class"/>. That agreement is the green light for a later slice to drop
    ///     the authored field and make the class a pure read-out — if this ever diverges, the unification isn't safe yet.
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
        [Description("THE INVARIANT: on every real base-mod weapon (laser / railgun / flak / ion disruptor), the class COMPUTED from the axes equals the AUTHORED class — so the authored field is fully recoverable and can later become a pure read-out.")]
        public void ComputedClass_MatchesAuthoredClass_OnEveryRealBaseModWeapon()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;

            // one ship per corner that actually carries weapons in the base mod
            string[] shipIds =
            {
                "default-ship-design-test-warship",  // Aegis  — lasers (Beam)
                "default-ship-design-test-railgun",  // Lancer — railguns (Railgun)
                "default-ship-design-test-flak",     // Bulwark — flak (Flak)
                "default-ship-design-test-disruptor" // Ravager — ion disruptors (Exotic, Beam-class)
            };

            int weaponsChecked = 0;
            foreach (var id in shipIds)
            {
                Assert.That(designs.ContainsKey(id), Is.True, $"{id} loads from the base mod");
                var ship = ShipFactory.CreateShip(designs[id], s.Faction, s.StartingBody, id);
                var cv = ship.GetDataBlob<ShipCombatValueDB>();
                Assert.That(cv.Weapons.Count, Is.GreaterThan(0), $"{id} is armed");

                foreach (var w in cv.Weapons)
                {
                    Assert.That(w.ComputedClass, Is.EqualTo(w.Class),
                        $"{id}: a {w.Nature}/{w.Delivery} weapon authored as {w.Class} must COMPUTE to {w.Class} (vel={w.Velocity:0}, sat={w.Saturation:0.##})");
                    weaponsChecked++;
                }
            }
            Log($"verified computed==authored class on {weaponsChecked} real weapon profiles across {shipIds.Length} ships");
            Assert.That(weaponsChecked, Is.GreaterThanOrEqualTo(shipIds.Length), "checked at least one weapon per ship");
        }

        [Test]
        [Description("The two axes survive fire-mix AGGREGATION: BuildFireMix now buckets by (class, nature, DELIVERY), so a real Vanguard's plasma fire aggregates to a profile that still carries Nature=Energy AND Delivery=Bolt (not defaulted away) — the prerequisite for later making Class emergent everywhere (a missile would otherwise aggregate to the default Slug delivery and misclassify).")]
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
            Log($"aggregated: class={plasma.Class} nature={plasma.Nature} delivery={plasma.Delivery} computed={plasma.ComputedClass}");

            Assert.That(plasma.Nature, Is.EqualTo(WeaponNature.Energy), "aggregation preserves the Energy nature (the shield matchup survives)");
            Assert.That(plasma.Delivery, Is.EqualTo(WeaponDelivery.Bolt), "aggregation preserves the Bolt delivery (it was dropped to the Slug default before this fix)");
            Assert.That(plasma.ComputedClass, Is.EqualTo(plasma.Class), "so ComputedClass==Class survives aggregation");
        }
    }
}
