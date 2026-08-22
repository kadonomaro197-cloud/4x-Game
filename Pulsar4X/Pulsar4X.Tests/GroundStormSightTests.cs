using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;
using Pulsar4X.Components;
using Pulsar4X.Hazards;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// D-PLANFN-A gauge — storms dim ground SIGHT (<see cref="GroundStormSight"/> + the two <c>GroundSensors</c> wires).
    /// A unit standing in a SensorJam storm sees LESS far — the ground echo of the space <c>SensorRangeMultiplier</c>.
    /// Mirrors <c>GroundSensorsTests.RadarUnit_RevealsGroundWithinReach</c> (build a radar-carrying unit) + the
    /// storm-setup idiom from <c>GroundForcesTests.Environment_DamagesAUnitStandingInIt</c> (add a RegionEnvironment).
    ///
    /// Four gauges: (1) a 0.4 storm cuts radar reach to 40% (flag on); (2) flag OFF → the storm is ignored
    /// (byte-identical); (3) flag on but no storm → reach unchanged (multiplier 1.0); (4) a blackout storm blinds the
    /// per-tick reveal too — it reveals fewer regions than clear weather (proving the RevealFromUnits wire, not only
    /// the RadarReachHexes accessor).
    /// </summary>
    [TestFixture]
    public class GroundStormSightTests
    {
        [TearDown]
        public void ResetFlag() => GroundStormSight.EnableStormSight = false;   // never leak the flag into a sibling fixture

        /// <summary>Raise a radar-carrying unit in region 0, exactly as GroundSensorsTests builds one.</summary>
        private static (Entity body, GroundUnit unit) RaiseRadarUnit(TestScenario s)
        {
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            var radar = new ComponentDesign { UniqueID = "storm-test-radar", Name = "Radar" };
            radar.AttributesByType[typeof(GroundSensorAtb)] = new GroundSensorAtb(1_000_000_000);   // huge reach
            faction.IndustryDesigns["storm-test-radar"] = radar;

            var design = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "storm-test-scout", "Storm Scout",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)> { (radar, 1) });

            var unit = GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);
            Assert.That(unit.BackingEntityId, Is.GreaterThanOrEqualTo(0), "the radar unit has a backing entity carrying the radar");
            Assert.That(unit.RegionIndex, Is.EqualTo(0));
            return (body, unit);
        }

        /// <summary>Add a SensorJam storm of the given magnitude to a region (creating the environment blob on demand),
        /// the GroundForcesTests.Environment_DamagesAUnitStandingInIt idiom.</summary>
        private static void AddStorm(Entity body, int regionIndex, double magnitude)
        {
            if (!body.TryGetDataBlob<PlanetEnvironmentsDB>(out var env))
            {
                env = new PlanetEnvironmentsDB();
                body.SetDataBlob(env);
            }
            env.Environments.Add(new RegionEnvironment(regionIndex, "Test Dust Storm", HazardEffectType.SensorJam, magnitude));
        }

        [Test]
        public void Storm_ReducesRadarReach_WhenFlagOn()
        {
            var s = TestScenario.CreateWithColony();
            var (body, unit) = RaiseRadarUnit(s);
            double clean = GroundSensors.RadarReachHexes(body, unit);
            Assert.That(clean, Is.GreaterThan(0), "a radar unit has a positive reach in clear weather");

            AddStorm(body, 0, 0.4);   // SensorJam magnitude IS the sight multiplier (reduce to 40%)
            GroundStormSight.EnableStormSight = true;
            double stormy = GroundSensors.RadarReachHexes(body, unit);

            Assert.That(stormy, Is.EqualTo(clean * 0.4).Within(clean * 1e-6),
                "a 0.4 SensorJam storm cuts radar reach to 40%");
        }

        [Test]
        public void Storm_ByteIdentical_WhenFlagOff()
        {
            var s = TestScenario.CreateWithColony();
            var (body, unit) = RaiseRadarUnit(s);
            double clean = GroundSensors.RadarReachHexes(body, unit);

            AddStorm(body, 0, 0.4);
            GroundStormSight.EnableStormSight = false;   // the guard the byte-identity guarantee rests on
            double stormyOff = GroundSensors.RadarReachHexes(body, unit);

            Assert.That(stormyOff, Is.EqualTo(clean), "flag off → the storm has no effect (byte-identical)");
        }

        [Test]
        public void NoStorm_FlagOn_ByteIdentical()
        {
            var s = TestScenario.CreateWithColony();
            var (body, unit) = RaiseRadarUnit(s);
            double clean = GroundSensors.RadarReachHexes(body, unit);

            GroundStormSight.EnableStormSight = true;    // flag on, but no SensorJam storm on the region
            double stillClean = GroundSensors.RadarReachHexes(body, unit);

            Assert.That(stillClean, Is.EqualTo(clean), "no storm → multiplier 1.0 → reach unchanged");
        }

        [Test]
        public void Blackout_RevealsFewerRegions_WhenFlagOn()
        {
            var s = TestScenario.CreateWithColony();
            var (body, unit) = RaiseRadarUnit(s);
            var regions = body.GetDataBlob<PlanetRegionsDB>();

            // Clean baseline (flag off): the huge radar reveals into neighbouring regions.
            foreach (var r in regions.Regions) r.Surveyed = false;
            GroundStormSight.EnableStormSight = false;
            int cleanRevealed = GroundSensors.RevealFromUnits(body);
            Assert.That(cleanRevealed, Is.GreaterThanOrEqualTo(3),
                "a long-range radar reveals several regions in clear weather");

            // Re-fog, then a total-blackout storm (magnitude 0 = fully blind) with the flag ON → reveals only its own region.
            foreach (var r in regions.Regions) r.Surveyed = false;
            AddStorm(body, 0, 0.0);
            GroundStormSight.EnableStormSight = true;
            int stormRevealed = GroundSensors.RevealFromUnits(body);

            Assert.That(regions.Regions[unit.RegionIndex].Surveyed, Is.True, "the unit still knows the ground it stands on");
            Assert.That(stormRevealed, Is.LessThan(cleanRevealed),
                "a blackout storm blinds the radar's reveal — fewer regions than in clear weather (the RevealFromUnits wire bites)");
        }
    }
}
