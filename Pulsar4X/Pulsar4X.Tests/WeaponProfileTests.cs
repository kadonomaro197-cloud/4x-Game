using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Combat depth — per-weapon flavor profiles (<see cref="WeaponProfile"/>). A ship's combat value now carries
    /// each weapon's damage/velocity/tracking/saturation, not just one firepower number — the breakdown the dodge
    /// model and the weapon triangle read. Engine-only -> runs in CI.
    /// </summary>
    [TestFixture]
    public class WeaponProfileTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[weapon-profile] " + m);

        [Test]
        [Description("A laser warship's combat value carries Beam weapon profiles: ~light-speed, tracks well, has a rate of fire; and the profiles' damage sums exactly to Firepower (backward-compatible).")]
        public void BeamShip_HasLightSpeedBeamProfiles_SummingToFirepower()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            var ship = ShipFactory.CreateShip(designs["default-ship-design-test-warship"], s.Faction, s.StartingBody, "Aegis");
            var cv = ship.GetDataBlob<ShipCombatValueDB>();

            Log($"weapons={cv.Weapons.Count} firepower={cv.Firepower:0}");
            foreach (var w in cv.Weapons)
                Log($"  {w.Class} dps={w.DamagePerSecond:0} vel={w.Velocity:0} track={w.Tracking:0.##} sat={w.Saturation:0.###}");

            Assert.That(cv.Weapons, Is.Not.Empty, "a warship with lasers should have weapon profiles");
            var beams = cv.Weapons.Where(w => w.Class == WeaponClass.Beam).ToList();
            Assert.That(beams, Is.Not.Empty, "the Aegis carries lasers (Beam class)");
            Assert.That(beams[0].Velocity, Is.GreaterThan(1e7), "a beam travels at ~light-speed (you can't dodge light)");
            Assert.That(beams[0].Tracking, Is.GreaterThan(0), "a beam has a base hit chance (tracking)");
            Assert.That(beams[0].Saturation, Is.GreaterThan(0), "a beam has a rate of fire (saturation)");

            double sumDps = cv.Weapons.Sum(w => w.DamagePerSecond);
            Assert.That(sumDps, Is.EqualTo(cv.Firepower).Within(0.01),
                "Firepower must equal the sum of weapon-profile damage — profiles don't change the old number");
        }
    }
}
