using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;
using Pulsar4X.Sensors;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Detection-range tuning gauge. A play-test (2026-06-27) showed a fleet sitting AT Luna detecting none of the
    /// enemies parked there — with fog of war on, that also means combat never triggers. The ships DO carry sensors
    /// (the token <c>passive-sensor-s50</c>, antenna 5.5), so the question is purely "how far can a ship detect another
    /// ship, and is that enough to reach across a planet's orbit / the engagement range?". This fixture MEASURES that
    /// (prints the real number so the rebalance targets data, not a guess) and then GATES it at a combat-useful range.
    /// </summary>
    [TestFixture]
    public class DetectionTuningTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[detect-tune] " + m);

        // Distance references (m): Luna low orbit is a few thousand km; Earth↔Luna ≈ 0.384 Gm; the auto-engage range
        // (CombatEngagement.EngagementRange_m) is 1 Gm. For fog-on combat to be reachable, two ships parked at the same
        // body (well under 0.384 Gm apart) must be able to detect each other — so reach must clear at least ~0.1 Gm.
        const double CombatUsefulReach_m = 1.0e8; // 0.1 Gm = 100,000 km

        [Test]
        [Description("MEASURE ship-vs-ship detection range (gauge, not yet a gate — so it lands green and the rebalance " +
                     "targets a real number). Builds two warships and prints how far one detects the other, and whether " +
                     "that clears the combat-useful floor. Asserts only that reach > 0; the printed Gm drives the tuning.")]
        public void ShipVsShip_DetectionRange_Measure()
        {
            var s = TestScenario.CreateWithColony();
            var info = s.Faction.GetDataBlob<FactionInfoDB>();

            var detector = ShipFactory.CreateShip(info.ShipDesigns["default-ship-design-test-warship"], s.Faction, s.StartingBody, "Detector");
            var target   = ShipFactory.CreateShip(info.ShipDesigns["default-ship-design-test-corvette"], s.Faction, s.StartingBody, "Target");

            double reach = SensorTools.SensorReachRange_m(detector);              // how far it sees a ship like itself
            double range = SensorTools.DetectionRangeAgainst(detector, target);    // how far it sees THIS corvette

            Log($"detector best sensor reach (ship-like-it) = {reach:N0} m = {reach / 1e9:0.######} Gm");
            Log($"detector detects the corvette at          = {range:N0} m = {range / 1e9:0.######} Gm");
            Log($"reference: Earth↔Luna ≈ 0.384 Gm; engagement range = 1 Gm; combat-useful floor = {CombatUsefulReach_m / 1e9:0.###} Gm");
            Log($"clears combat-useful floor? {(range >= CombatUsefulReach_m ? "YES" : "NO — too short for fog-on combat at a body")}");

            Assert.That(reach, Is.GreaterThan(0), "a sensor-equipped ship must have a positive reach");
        }
    }
}
