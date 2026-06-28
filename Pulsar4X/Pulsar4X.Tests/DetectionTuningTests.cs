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
        [Description("Measure + GATE ship-vs-ship detection range. Builds two warships and prints how far one detects " +
                     "the other (best sensor vs the target's real signature). Asserts it clears the combat-useful floor " +
                     "so a fleet at a body can see hostiles there and fog-on combat can trigger. Pre-rebalance this was " +
                     "~0.0003 Gm (292 km); DetectionSensitivityScale lifts it to ~0.29 Gm.")]
        public void ShipVsShip_DetectionRange_ReachesCombatRange()
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

            Assert.That(reach, Is.GreaterThan(0), "a sensor-equipped ship must have a positive reach");
            Assert.That(range, Is.GreaterThanOrEqualTo(CombatUsefulReach_m),
                $"ship-vs-ship detection ({range / 1e9:0.####} Gm) must clear the combat-useful floor " +
                $"({CombatUsefulReach_m / 1e9:0.###} Gm) so a fleet at a body sees hostiles there and fog-on combat can trigger");
        }

        [Test]
        [Description("Fog of war is preserved: the rebalance must NOT make a ship see the whole system. A ship's reach " +
                     "(~0.29 Gm) must stay well below inner-system scale (Venus is ~0.4 AU ≈ 60 Gm away), so distant " +
                     "fleets are still hidden until they close — detection is a tactical bubble, not omniscience.")]
        public void ShipDetection_StaysWellBelowSystemScale_FogPreserved()
        {
            var s = TestScenario.CreateWithColony();
            var info = s.Faction.GetDataBlob<FactionInfoDB>();
            var detector = ShipFactory.CreateShip(info.ShipDesigns["default-ship-design-test-warship"], s.Faction, s.StartingBody, "Detector");

            double reach = SensorTools.SensorReachRange_m(detector);
            const double InnerSystemScale_m = 1.0e10; // 10 Gm — an order of magnitude under Venus's ~60 Gm distance
            Log($"reach {reach / 1e9:0.###} Gm vs fog ceiling {InnerSystemScale_m / 1e9:0.#} Gm");
            Assert.That(reach, Is.LessThan(InnerSystemScale_m),
                "a ship sensor must not reach across the inner system, or fog of war is gone");
        }

        [Test]
        [Description("THE UNVERIFIED CASE (audit, 2026-06-27): the 1e6 detection scale was only checked on the WEAK " +
                     "ship sensor (antenna 5.5). The COLONY carries the full Passive Scanner (antenna 5000), ~900× " +
                     "bigger — does that make a colony see the whole system and gut fog? Measure how far the colony " +
                     "detects a ship, and print whether it stays under the fog ceiling (Venus ~60 Gm). Asserts only " +
                     "that it's positive; the printed Gm decides whether a detection-range CAP is needed.")]
        public void ColonyScanner_DetectionRange_Measure()
        {
            var s = TestScenario.CreateWithColony();
            var info = s.Faction.GetDataBlob<FactionInfoDB>();
            var ship = ShipFactory.CreateShip(info.ShipDesigns["default-ship-design-test-corvette"], s.Faction, s.StartingBody, "Target");

            double colonyReach = SensorTools.DetectionRangeAgainst(s.Colony, ship);   // colony's full scanner vs a ship
            const double Venus_m = 6.0e10;
            Log($"colony detects a ship at {colonyReach:N0} m = {colonyReach / 1e9:0.###} Gm (Venus ≈ 60 Gm)");
            Log(colonyReach < 1.0e10 ? "FOG OK — colony reach < 10 Gm (tactical warning, not omniscient)"
                : colonyReach < Venus_m ? "FOG MARGINAL — colony sees a good chunk of the inner system but not across it"
                : "FOG BROKEN — colony sees PAST Venus; the 1e6 scale needs a detection cap for strong sensors");

            Assert.That(colonyReach, Is.GreaterThan(0), "the colony's scanner must have a positive reach");
        }
    }
}
