using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Combat depth P3 — the RAILGUN / slug-thrower weapon type, end to end through the REAL data path. This is
    /// the sensor for the gotcha-10 risk: the live game builds weapons from JSON (template → NCalc → Atb via
    /// reflection), which `dotnet test` normally skips. This fixture builds the base-mod railgun cruiser the same
    /// way `TestScenario` loads the colony, so CI exercises the JSON `railgun-weapon` template → `RailgunWeaponAtb`
    /// constructor binding → `ShipCombatValueDB` read. If the template args/constructor drift, this (and
    /// `BaseModIntegrityTests`) go red in CI instead of crashing the developer's New Game.
    ///
    /// The payoff it proves: a real built railgun rates as FINITE-velocity, ballistic (near-zero tracking) kinetic
    /// fire — so the dodge model lets a nimble ship juke it while a sluggish one eats it (the opposite of a beam).
    /// Engine-only -> runs in CI.
    /// </summary>
    [TestFixture]
    public class RailgunWeaponTests
    {
        private const string RailgunShip = "default-ship-design-test-railgun";
        private static void Log(string m) => TestContext.Progress.WriteLine("[railgun] " + m);

        [Test]
        [Description("The base-mod railgun cruiser builds from JSON and its real railgun components rate as finite-velocity, ballistic (low-tracking) kinetic fire — dodgeable, unlike a beam.")]
        public void RailgunDesign_BuildsRealComponent_AndRatesAsDodgeableKineticFire()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(RailgunShip), Is.True,
                "the railgun cruiser design loads onto the faction — the JSON railgun-weapon template + component design wired up");

            // Build it the real way (this instantiates the RailgunWeaponAtb from JSON via reflection).
            var ship = ShipFactory.CreateShip(designs[RailgunShip], s.Faction, s.StartingBody, "Lancer");
            var cv = ship.GetDataBlob<ShipCombatValueDB>();

            var railguns = cv.Weapons.Where(w => w.Class == WeaponClass.Railgun).ToList();
            var first = railguns.FirstOrDefault();
            Log($"firepower={cv.Firepower:0}; weapons={cv.Weapons.Count} railgunProfiles={railguns.Count}; " +
                $"first: vel={first?.Velocity:0} trk={first?.Tracking:0.###} sat={first?.Saturation:0.###} dps={first?.DamagePerSecond:0}");

            Assert.That(cv.Firepower, Is.GreaterThan(0), "the railgun cruiser is armed (railgun firepower flows into the combat value)");
            Assert.That(railguns.Count, Is.GreaterThanOrEqualTo(1),
                "at least one railgun component produced a Railgun weapon profile — JSON template -> Atb -> combat value is wired");
            Assert.That(cv.Weapons.All(w => w.Class == WeaponClass.Railgun), Is.True,
                "every weapon on the railgun cruiser is a railgun (no stray/misclassified profile)");

            // The flavor stats are read straight from the design (health-independent), so they're exact.
            Assert.That(first.Velocity, Is.EqualTo(50_000).Within(1), "muzzle velocity read from the design");
            Assert.That(first.Velocity, Is.LessThan(CombatEngagement.VelocityReference_mps),
                "and it is FINITE/slow vs the dodge reference — a railgun shot CAN be dodged (a beam can't)");
            Assert.That(first.Tracking, Is.EqualTo(0.05).Within(0.001), "ballistic: near-zero tracking (no guidance)");
            Assert.That(first.Saturation, Is.EqualTo(5).Within(0.001), "saturation = rounds/sec from the design");
            Assert.That(first.DamagePerSecond, Is.GreaterThan(0), "energy-per-shot × rounds/sec gives real damage/sec");

            // The payoff, on the REAL built weapon: its fire lands on a sluggish hull but is dodged by a nimble one.
            double vsSluggish = CombatEngagement.HitFraction(first, 0.0);
            double vsNimble = CombatEngagement.HitFraction(first, 0.9);
            Log($"hit fraction: vsSluggish(ev=0)={vsSluggish:0.###} vsNimble(ev=0.9)={vsNimble:0.###}");
            Assert.That(vsSluggish, Is.GreaterThan(0.9), "a railgun lands fully on a target that can't dodge");
            Assert.That(vsNimble, Is.LessThan(vsSluggish), "but a nimble ship dodges much of the slug fire — the railgun's weakness");
        }
    }
}
