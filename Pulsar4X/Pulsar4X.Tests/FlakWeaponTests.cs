using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Combat depth P4 — the FLAK / point-defense weapon type, end to end through the REAL data path (same
    /// gotcha-10 sensor as <see cref="RailgunWeaponTests"/>: builds the base-mod flak escort from JSON so CI
    /// exercises the template → NCalc → Atb reflection binding, not just the blueprint load).
    ///
    /// The payoff it proves: flak's strength is SATURATION (rounds/sec × pellets/shot), not per-hit punch — so its
    /// high saturation FLOORS the dodge, landing heavily even on a nimble target that juke a railgun. Flak is the
    /// fighter/missile killer; a single slow slug is not. Engine-only -> runs in CI.
    /// </summary>
    [TestFixture]
    public class FlakWeaponTests
    {
        private const string FlakShip = "default-ship-design-test-flak";
        private static void Log(string m) => TestContext.Progress.WriteLine("[flak] " + m);

        [Test]
        [Description("The base-mod flak escort builds from JSON and its real flak guns rate as HIGH-saturation fire that floors the hit fraction — it catches a nimble target a low-saturation slug would miss.")]
        public void FlakDesign_BuildsRealComponent_AndItsSaturationFloorsTheDodge()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(FlakShip), Is.True,
                "the flak escort design loads onto the faction — the JSON flak-weapon template + component design wired up");

            var ship = ShipFactory.CreateShip(designs[FlakShip], s.Faction, s.StartingBody, "Bulwark");
            var cv = ship.GetDataBlob<ShipCombatValueDB>();

            var flaks = cv.Weapons.Where(w => w.Class == WeaponClass.Flak).ToList();
            var first = flaks.FirstOrDefault();
            Log($"firepower={cv.Firepower:0}; weapons={cv.Weapons.Count} flakProfiles={flaks.Count}; " +
                $"first: vel={first?.Velocity:0} trk={first?.Tracking:0.###} sat={first?.Saturation:0} dps={first?.DamagePerSecond:0}");

            Assert.That(cv.Firepower, Is.GreaterThan(0), "the flak escort is armed");
            Assert.That(flaks.Count, Is.GreaterThanOrEqualTo(1),
                "at least one flak component produced a Flak weapon profile — JSON template -> Atb -> combat value is wired");
            Assert.That(cv.Weapons.All(w => w.Class == WeaponClass.Flak), Is.True, "every weapon on the escort is flak");

            // Saturation = rounds/sec (10) × pellets/shot (30) = 300 — HIGH, read straight from the design.
            Assert.That(first.Velocity, Is.EqualTo(20_000).Within(1), "moderate muzzle velocity from the design");
            Assert.That(first.Tracking, Is.EqualTo(0.1).Within(0.001), "medium tracking — the spread helps");
            Assert.That(first.Saturation, Is.EqualTo(10.0 * 30.0).Within(0.001), "saturation = rounds/sec × pellets/shot");
            Assert.That(first.DamagePerSecond, Is.GreaterThan(0), "damage/pellet × saturation gives real damage/sec");

            // The payoff: flak's saturation FLOORS the hit fraction, so it lands heavily even on a high-evasion
            // target — where a low-saturation ballistic slug (same nimble target) gets dodged.
            double flakVsNimble = CombatEngagement.HitFraction(first, 0.9);
            var slug = new WeaponProfile(WeaponClass.Railgun, 1000, 50_000, 0.05, 5); // low-saturation ballistic
            double slugVsNimble = CombatEngagement.HitFraction(slug, 0.9);
            Log($"vs nimble (ev=0.9): flak={flakVsNimble:0.###} slug={slugVsNimble:0.###}");

            Assert.That(flakVsNimble, Is.GreaterThan(0.5),
                "flak fills the sky — its saturation keeps the landed fraction high even on a hard dodger");
            Assert.That(flakVsNimble, Is.GreaterThan(slugVsNimble),
                "and far higher than a single slow slug against the same nimble target — flak is the fighter/missile killer");
        }
    }
}
