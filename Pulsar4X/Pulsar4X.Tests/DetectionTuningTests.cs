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
        [Description("Colony = system-wide EARLY WARNING, by design (decision 2026-06-28). The audit measured the " +
                     "colony's full Passive Scanner (antenna 5000, ~900× the ship sensor) at ~230 Gm — past Venus, " +
                     "across the whole inner system. Detection range is LINEAR in antenna size and the 1e6 scale is " +
                     "ONE global knob, so you can't rein in the colony without dropping the ship sensor back below the " +
                     "combat-useful floor. The DECISION (developer): keep it — a fixed ground megasensor is realistic " +
                     "early warning; the fog that matters (ship-vs-ship in deep space) stays a tight ~0.3 Gm bubble " +
                     "(the OTHER two tests guard that). So this asserts the colony sees FAR and decisively farther " +
                     "than a ship — documenting the intent, NOT gating a cap.")]
        public void ColonyScanner_IsSystemWideEarlyWarning_ByDesign()
        {
            var s = TestScenario.CreateWithColony();
            var info = s.Faction.GetDataBlob<FactionInfoDB>();
            var ship = ShipFactory.CreateShip(info.ShipDesigns["default-ship-design-test-corvette"], s.Faction, s.StartingBody, "Target");
            var picket = ShipFactory.CreateShip(info.ShipDesigns["default-ship-design-test-warship"], s.Faction, s.StartingBody, "Picket");

            double colonyReach = SensorTools.DetectionRangeAgainst(s.Colony, ship);   // colony's full scanner vs a ship
            double shipReach   = SensorTools.DetectionRangeAgainst(picket, ship);     // a warship's sensor vs the same ship
            const double InnerSystemScale_m = 1.0e10; // 10 Gm — the ship "tactical bubble" ceiling

            Log($"colony detects a ship at {colonyReach / 1e9:0.###} Gm (Venus ≈ 60 Gm) — early-warning radar, by design");
            Log($"a warship detects the same ship at {shipReach / 1e9:0.###} Gm — the tactical bubble fog runs on");

            Assert.That(colonyReach, Is.GreaterThan(0), "the colony's scanner must have a positive reach");
            // Intent: the fixed ground array sees WAY past a ship's tactical bubble (system-wide early warning).
            Assert.That(colonyReach, Is.GreaterThan(InnerSystemScale_m),
                "a colony's megasensor is intended system-wide early warning — it should see well past a ship's ~10 Gm bubble");
            Assert.That(colonyReach, Is.GreaterThan(shipReach * 10),
                "the colony array must dwarf a warship's sensor — that asymmetry IS the early-warning design");
        }
    }
}
