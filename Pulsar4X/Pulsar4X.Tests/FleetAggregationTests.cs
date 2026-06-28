using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Movement;
using Pulsar4X.Sensors;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Roots A & B of the closing-fight model (docs/FLEET-COMBAT-CLOSING-DESIGN.md) — the data the whole tree reads.
    /// Root A: a weapon carries its RANGE into the combat profile (beams real, the rest rangeless-for-now). Root B:
    /// fleet capability AGGREGATION — speed/Δv floor (slowest ship), firepower-vs-range curve (longest gun that
    /// reaches), sensor envelope (best sensor, parallel = max not sum). Pure read-models; no behaviour change.
    /// </summary>
    [TestFixture]
    public class FleetAggregationTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[fleet-agg] " + m);

        private static Entity Build(TestScenario s, string designId, string name)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(designId), Is.True, $"base mod must define {designId}");
            return ShipFactory.CreateShip(designs[designId], s.Faction, s.StartingBody, name);
        }

        // ─── Root A — weapon range on the combat profile ───────────────────────────────────────────────────────

        [Test]
        [Description("A beam weapon carries its design MaxRange into its WeaponProfile.Range_m; railguns carry a finite " +
                     "class-default MID range (RailgunRange_m, 2026-06-28 — was 0/unbounded). Range does NOT change " +
                     "Firepower — the old strength number is identical (the field is additive data the closing model " +
                     "reads, not a stat change).")]
        public void WeaponProfile_CarriesDesignRange_FirepowerUnchanged()
        {
            var s = TestScenario.CreateWithColony();

            var aegis = Build(s, "default-ship-design-test-warship", "Aegis");   // 4 beam lasers
            var cv = aegis.GetDataBlob<ShipCombatValueDB>();
            var beams = cv.Weapons.Where(w => w.Class == WeaponClass.Beam).ToArray();

            Log($"Aegis: {beams.Length} beam profiles, Range_m = {(beams.Length > 0 ? beams[0].Range_m : 0):N0}");
            Assert.That(beams.Length, Is.GreaterThan(0), "the Aegis carries beam weapons");
            foreach (var w in beams)
                Assert.That(w.Range_m, Is.GreaterThan(0), "a beam profile carries its design MaxRange (a real, finite range)");

            // Identity: Firepower is still the sum of the weapon DPS — adding Range_m changed no strength number.
            Assert.That(cv.Weapons.Sum(w => w.DamagePerSecond), Is.EqualTo(cv.Firepower).Within(1e-6).Percent,
                "Firepower must still equal the summed weapon DPS — range is additive data, not a stat change");

            // Railguns now carry a finite class-default MID range (was 0/unbounded — the "firing outside detection
            // range" fix, 2026-06-28). A per-design field is the next step.
            var lancer = Build(s, "default-ship-design-test-railgun", "Lancer");
            var rg = lancer.GetDataBlob<ShipCombatValueDB>().Weapons.Where(w => w.Class == WeaponClass.Railgun).ToArray();
            Assert.That(rg.Length, Is.GreaterThan(0), "the Lancer carries railguns");
            foreach (var w in rg)
                Assert.That(w.Range_m, Is.EqualTo(ShipCombatValueDB.RailgunRange_m).Within(1),
                    "railguns carry the finite class-default mid range (no longer rangeless)");
        }

        // ─── Root B — fleet capability aggregation ─────────────────────────────────────────────────────────────

        [Test]
        [Description("The firepower-vs-range curve: at range 0 every weapon counts; past a weapon's finite range its " +
                     "firepower drops out. The identity tested holds regardless of which weapons are finite — the drop " +
                     "at a given gap is exactly the firepower of the weapons whose range is shorter than that gap. The " +
                     "shape the closing resolve sums each step against the current gap.")]
        public void FirepowerAtRange_DropsFiniteRangeWeapons_AsTheGapGrows()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Task Force");

            // Aegis = beams; Capital = railguns. Both are finite-range now (railgun range added 2026-06-28), so at a
            // 1 Gm gap BOTH drop out — the identity below (drop == finite-range firepower at that gap) still holds.
            foreach (var (id, name) in new[] { ("default-ship-design-test-warship", "Aegis"),
                                               ("default-ship-design-test-capital", "Leviathan") })
                s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, fleet, Build(s, id, name)));

            double total = FleetCombat.FirepowerAtRange(fleet, 0);                  // point blank — everything fires
            double beyond = FleetCombat.FirepowerAtRange(fleet, 1e9);               // 1M km — past every finite range
            double finiteRangeFp = FleetCombat.Ships(fleet)
                .SelectMany(sh => sh.GetDataBlob<ShipCombatValueDB>().Weapons)
                .Where(w => w.Range_m > 0).Sum(w => w.DamagePerSecond);             // the beams that drop out

            Log($"firepower: total(0m)={total:N0}  beyond-beam(1Gm)={beyond:N0}  finite-range(beams)={finiteRangeFp:N0}");
            Assert.That(total, Is.GreaterThan(0), "a fleet at point blank has firepower");
            Assert.That(finiteRangeFp, Is.GreaterThan(0), "the test needs finite-range (beam) weapons to be meaningful");
            Assert.That(beyond, Is.LessThan(total), "past the beams' range, fleet firepower drops");
            Assert.That(beyond, Is.EqualTo(total - finiteRangeFp).Within(0.001).Percent,
                "the drop is exactly the firepower of weapons whose finite range is under the gap; only truly unbounded firepower (if any) remains");
        }

        [Test]
        [Description("The fleet moves as one: speed/Δv FLOOR = the SLOWEST/shortest-legged ship. Sensor envelope = the " +
                     "BEST sensor (parallel = max, not sum). And the ship walk recurses to the real ships only.")]
        public void Floors_TakeTheSlowest_SensorReach_TakesTheBest()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Mixed Fleet");

            // Three distinct hulls → distinct speeds (heavy warship/capital vs. a light fast fighter); the capital
            // carries the sensor.
            foreach (var (id, name) in new[] { ("default-ship-design-test-warship", "Aegis"),
                                               ("default-ship-design-test-capital", "Leviathan"),
                                               ("default-ship-design-test-fighter", "Wasp") })
                s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, fleet, Build(s, id, name)));

            var ships = FleetCombat.Ships(fleet);
            Assert.That(ships.Count, Is.EqualTo(3), "the ship walk returns the three ships (no sub-fleet nodes)");

            // Speed floor = slowest ship's warp speed. (Assert the ships actually differ so 'min' isn't trivially 'max'.)
            double minSpeed = ships.Min(sh => sh.TryGetDataBlob<WarpAbilityDB>(out var w) ? w.MaxSpeed : 0);
            double maxSpeed = ships.Max(sh => sh.TryGetDataBlob<WarpAbilityDB>(out var w) ? w.MaxSpeed : 0);
            Log($"warp speed: floor={FleetCombat.WarpSpeedFloor(fleet):N1}  min={minSpeed:N1}  max={maxSpeed:N1}");
            Assert.That(maxSpeed, Is.GreaterThan(minSpeed), "ships must differ in speed for the floor test to mean anything");
            Assert.That(FleetCombat.WarpSpeedFloor(fleet), Is.EqualTo(minSpeed).Within(1e-6), "speed floor = the slowest ship");

            // Δv floor = min ship Δv (structural — values depend on fuel, but the floor must select the minimum).
            double minDv = ships.Min(sh => sh.TryGetDataBlob<NewtonThrustAbilityDB>(out var nt) ? nt.DeltaV : 0);
            Assert.That(FleetCombat.DeltaVFloor(fleet), Is.EqualTo(minDv).Within(1e-6), "Δv floor = the shortest-legged ship");

            // Sensor reach = the BEST sensor (max, parallel) — the capital's, not a sum.
            double maxReach = ships.Max(sh => SensorTools.SelfDetectionRange_m(sh));
            Assert.That(maxReach, Is.GreaterThan(0), "at least one ship (the capital) must sense for this to be meaningful");
            Assert.That(FleetCombat.SensorReach(fleet), Is.EqualTo(maxReach).Within(1e-6), "sensor reach = the best sensor, not the sum");
        }
    }
}
