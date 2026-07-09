using NUnit.Framework;
using Pulsar4X.Galaxy;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the REAL Earth surface map. The Sol homeworld used to render as a random sum-of-sines world (the
    /// same generator every body gets) — "Earth is a joke as far as how the map looks." Now a body named Earth
    /// samples <see cref="EarthTerrainMap"/>, a baked map of our actual planet. These tests assert (a) the baked
    /// table is well-formed (an edit that mis-sizes a row fails CI, not the developer's map), and (b) it really is
    /// Earth — the poles are ice, the mid-Pacific is open ocean, the continents are land, and the water fraction is
    /// Earth-like — the difference between a real map and noise.
    /// </summary>
    [TestFixture]
    public class EarthTerrainMapTests
    {
        // engine sample space: lon 0..1 wraps east from 180°W, lat 0 = north pole → 1 = south pole.
        private static double Lon(double deg) => (deg + 180.0) / 360.0;   // real longitude → 0..1
        private static double Lat(double deg) => (90.0 - deg) / 180.0;    // real latitude  → 0..1

        [Test]
        [Description("The baked table is exactly Rows×Cols — a mis-sized row is a data bug caught here.")]
        public void EarthMap_IsWellFormed()
        {
            Assert.That(EarthTerrainMap.IsWellFormed(), Is.True, "every row must be exactly Cols chars, Rows rows");
        }

        [Test]
        [Description("The poles are ice caps, the open oceans are ocean — the unmistakable Earth landmarks.")]
        public void EarthMap_PolesAreIce_OceansAreOcean()
        {
            Assert.That(EarthTerrainMap.Sample(0.25, 0.0),  Is.EqualTo(RegionFeatureType.Ice), "north pole");
            Assert.That(EarthTerrainMap.Sample(0.25, 0.99), Is.EqualTo(RegionFeatureType.Ice), "south pole (Antarctica)");
            // Mid-Pacific and mid-South-Atlantic are open water.
            Assert.That(EarthTerrainMap.Sample(Lon(-140), Lat(0)),  Is.EqualTo(RegionFeatureType.Ocean), "mid-Pacific");
            Assert.That(EarthTerrainMap.Sample(Lon(-25),  Lat(-30)), Is.EqualTo(RegionFeatureType.Ocean), "south Atlantic");
        }

        [Test]
        [Description("The continents are land, not ocean — central Africa and the Amazon fall on solid ground.")]
        public void EarthMap_ContinentsAreLand()
        {
            var congo  = EarthTerrainMap.Sample(Lon(20),  Lat(0));    // central Africa
            var amazon = EarthTerrainMap.Sample(Lon(-60), Lat(-5));   // South America
            Assert.That(congo,  Is.Not.EqualTo(RegionFeatureType.Ocean), "central Africa should be land");
            Assert.That(amazon, Is.Not.EqualTo(RegionFeatureType.Ocean), "the Amazon should be land");
        }

        [Test]
        [Description("The surface is a realistic MIX — substantial open ocean, substantial land, and ice caps — not a degenerate all-land or all-water field.")]
        public void EarthMap_HasEarthLikeMix()
        {
            int total = 0, ocean = 0, ice = 0, land = 0;
            for (int r = 0; r < 180; r++)
                for (int c = 0; c < 360; c++)
                {
                    var f = EarthTerrainMap.Sample((c + 0.5) / 360.0, (r + 0.5) / 180.0);
                    total++;
                    if (f == RegionFeatureType.Ocean) ocean++;
                    else if (f == RegionFeatureType.Ice) ice++;
                    else land++;
                }
            double oceanFrac = (double)ocean / total, iceFrac = (double)ice / total, landFrac = (double)land / total;
            Assert.That(oceanFrac, Is.InRange(0.30, 0.60), $"open-ocean fraction {oceanFrac:P0}");
            Assert.That(landFrac,  Is.InRange(0.25, 0.55), $"land fraction {landFrac:P0}");
            Assert.That(iceFrac,   Is.GreaterThan(0.05),   $"ice-cap fraction {iceFrac:P0} — the poles must be frozen");
        }
    }
}
