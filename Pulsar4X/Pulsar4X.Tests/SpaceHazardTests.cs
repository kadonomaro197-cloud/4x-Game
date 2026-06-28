using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Hazards;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauges the space-hazard system (gas cloud + solar flare) — the "region of space that affects ships inside
    /// it" feature. Engine-only, so CI verifies the logic (the on-map visuals are client-side and CI-blind).
    /// </summary>
    [TestFixture]
    public class SpaceHazardTests
    {
        private static Entity FirstStar(TestScenario s)
            => s.StartingSystem.GetAllDataBlobsOfType<StarInfoDB>().First().OwningEntity;

        [Test]
        [Description("A gas cloud applies its effects to a point INSIDE its radius and nothing outside.")]
        public void GasCloud_AffectsInsideOnly()
        {
            var s = TestScenario.CreateWithColony();
            var star = FirstStar(s);
            var starPos = star.GetDataBlob<PositionDB>().AbsolutePosition;

            var offset = new Vector3(Distance.AuToMt(5), 0, 0);
            double radius = Distance.AuToMt(1.0);
            SpaceHazardFactory.CreateGasCloud(s.StartingSystem, star, offset, radius);

            var center = starPos + offset; // the cloud sits at star + offset

            var inside = SpaceHazardTools.CombinedAt(s.StartingSystem, center);
            Assert.IsTrue(inside.InAnyHazard, "Point at the cloud centre should be inside it.");
            Assert.Less(inside.SensorRangeMultiplier, 1.0, "Sensors should be cut inside the cloud.");
            Assert.Less(inside.MoveSpeedMultiplier, 1.0, "Movement should be slowed inside the cloud.");
            Assert.Less(inside.WarpSpeedMultiplier, 1.0, "Warp should be slowed inside the cloud.");
            Assert.Greater(inside.DamagePerSecond, 0.0, "The cloud should damage over time.");

            var outside = SpaceHazardTools.CombinedAt(s.StartingSystem, center + new Vector3(Distance.AuToMt(5), 0, 0));
            Assert.IsFalse(outside.InAnyHazard, "A point well outside the cloud should be unaffected.");
            Assert.AreEqual(1.0, outside.SensorRangeMultiplier, 1e-9);
            Assert.AreEqual(1.0, outside.MoveSpeedMultiplier, 1e-9);
        }

        [Test]
        [Description("A solar flare's radius grows from a point to its max at the halfway peak, then fades.")]
        public void FlareRadius_GrowsToPeakThenFades()
        {
            var haz = new SpaceHazardDB
            {
                IsTransient = true,
                MaxRadius_m = 1000.0,
                StartedAt = new DateTime(2050, 1, 1, 0, 0, 0),
                ExpiresAt = new DateTime(2050, 1, 1, 12, 0, 0),
            };

            double rStart = SpaceHazardProcessor.FlareRadiusAt(haz, haz.StartedAt);
            double rQuarter = SpaceHazardProcessor.FlareRadiusAt(haz, haz.StartedAt + TimeSpan.FromHours(3));
            double rPeak = SpaceHazardProcessor.FlareRadiusAt(haz, haz.StartedAt + TimeSpan.FromHours(6));
            double rThreeQ = SpaceHazardProcessor.FlareRadiusAt(haz, haz.StartedAt + TimeSpan.FromHours(9));
            double rEnd = SpaceHazardProcessor.FlareRadiusAt(haz, haz.ExpiresAt);

            Assert.AreEqual(1000.0, rPeak, 1.0, "Flare should reach its max radius at the halfway peak.");
            Assert.Greater(rPeak, rQuarter, "It should be growing before the peak.");
            Assert.Greater(rPeak, rThreeQ, "It should be fading after the peak.");
            Assert.Greater(rQuarter, rStart, "Start should be near a point.");
            Assert.LessOrEqual(rEnd, 1.0001, "It should be gone by the end.");
        }

        [Test]
        [Description("Placement: the home star is given recurring-flare weather on load.")]
        public void HomeStar_HasFlareWeather()
        {
            var s = TestScenario.CreateWithColony();
            bool hasFlareSource = s.StartingSystem.GetAllDataBlobsOfType<StarFlareSourceDB>().Any();
            Assert.IsTrue(hasFlareSource, "The home star should carry a StarFlareSourceDB (recurring solar flares).");
        }

        [Test]
        [Description("A solar flare blinds sensors in its area, and is removed from the game once it expires.")]
        public void Flare_BlindsThenExpires()
        {
            var s = TestScenario.CreateWithColony();
            var star = FirstStar(s);
            DateTime now = star.StarSysDateTime;

            // An already-expired flare (started 2h ago, lasted 1h) — so processing it should remove it.
            var flare = SpaceHazardFactory.CreateSolarFlare(
                s.StartingSystem, star, now - TimeSpan.FromHours(2), TimeSpan.FromHours(1), Distance.AuToMt(0.1));

            var starPos = star.GetDataBlob<PositionDB>().AbsolutePosition;
            var atStar = SpaceHazardTools.CombinedAt(s.StartingSystem, starPos);
            Assert.IsTrue(atStar.BlindsSensors, "A flare at the star should blind sensors there.");
            Assert.AreEqual(0.0, atStar.SensorRangeMultiplier, 1e-9, "Blinding means zero effective sensor range.");

            // Processing past its expiry removes it.
            new SpaceHazardProcessor().ProcessEntity(flare, 5);
            Assert.IsFalse(flare.IsValid, "An expired flare should be destroyed when processed.");
        }

        [Test]
        [Description("Every star gets a permanent corona danger zone (proximity-scaled heat damage); it bites near the star and not far out.")]
        public void StarCorona_DamagesNearTheStar()
        {
            var s = TestScenario.CreateWithColony();
            var star = FirstStar(s);
            var starPos = star.GetDataBlob<PositionDB>().AbsolutePosition;

            var coronas = s.StartingSystem.GetAllDataBlobsOfType<SpaceHazardDB>()
                .Where(h => h.HazardType == SpaceHazardType.StarCorona).ToList();
            Assert.IsNotEmpty(coronas, "The home star should have a corona danger zone.");
            Assert.IsTrue(coronas[0].DamageScalesWithProximity, "Corona damage should scale with proximity.");
            Assert.Greater(coronas[0].DamagePerSecond, 0.0);

            var atStar = SpaceHazardTools.CombinedAt(s.StartingSystem, starPos);
            Assert.IsTrue(atStar.InAnyHazard, "Right at the star you should be inside the corona.");
            Assert.Greater(atStar.DamagePerSecond, 0.0, "The corona should be flagged as damaging.");

            // 10 AU out is far beyond the corona — no corona there (and Sol has no other load-time hazards).
            var farOut = SpaceHazardTools.CombinedAt(s.StartingSystem, starPos + new Vector3(Distance.AuToMt(10), 0, 0));
            Assert.IsFalse(farOut.InAnyHazard, "Far from the star you should be clear of the corona.");
        }
    }
}
