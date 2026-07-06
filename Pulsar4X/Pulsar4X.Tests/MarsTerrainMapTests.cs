using NUnit.Framework;
using Pulsar4X.Galaxy;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the REAL Mars surface map — the Sol playtest's classic invasion target now reads as Mars, not the
    /// random noise field. These tests assert (a) the baked table is well-formed, and (b) it really is Mars: a DRY
    /// world (no ocean anywhere), ice at both poles, and the volcanic + high-relief cratered character (Tharsis /
    /// Olympus volcanoes, the ancient southern highlands) that make it Mars rather than just "a red world."
    /// </summary>
    [TestFixture]
    public class MarsTerrainMapTests
    {
        private static double Lat(double deg) => (90.0 - deg) / 180.0;

        [Test]
        [Description("The baked table is exactly Rows×Cols — a mis-sized row is a data bug caught here.")]
        public void MarsMap_IsWellFormed()
        {
            Assert.That(MarsTerrainMap.IsWellFormed(), Is.True, "every row must be exactly Cols chars, Rows rows");
        }

        [Test]
        [Description("Mars is a DRY world with ice caps — no ocean anywhere, but frozen poles.")]
        public void MarsMap_IsDry_WithPolarIce()
        {
            Assert.That(MarsTerrainMap.Sample(0.25, 0.0),  Is.EqualTo(RegionFeatureType.Ice), "north polar cap");
            Assert.That(MarsTerrainMap.Sample(0.25, 0.99), Is.EqualTo(RegionFeatureType.Ice), "south polar cap");

            for (int r = 0; r < 180; r++)
                for (int c = 0; c < 360; c++)
                    Assert.That(MarsTerrainMap.Sample((c + 0.5) / 360.0, (r + 0.5) / 180.0),
                        Is.Not.EqualTo(RegionFeatureType.Ocean), "Mars has no oceans");
        }

        [Test]
        [Description("The Martian character is there — volcanoes (Tharsis/Olympus/Elysium), rugged relief, and the cratered southern highlands.")]
        public void MarsMap_HasVolcanoesAndHighlands()
        {
            int total = 0, volcanic = 0, mountains = 0, highlands = 0, ice = 0;
            for (int r = 0; r < 180; r++)
                for (int c = 0; c < 360; c++)
                {
                    var f = MarsTerrainMap.Sample((c + 0.5) / 360.0, (r + 0.5) / 180.0);
                    total++;
                    if (f == RegionFeatureType.Volcanic) volcanic++;
                    else if (f == RegionFeatureType.Mountains) mountains++;
                    else if (f == RegionFeatureType.Highlands) highlands++;
                    else if (f == RegionFeatureType.Ice) ice++;
                }
            Assert.That(volcanic,  Is.GreaterThan(0), "the great volcanoes (Tharsis/Olympus/Elysium)");
            Assert.That(mountains, Is.GreaterThan(0), "rugged relief (Tharsis rise, Valles Marineris walls)");
            Assert.That((double)highlands / total, Is.GreaterThan(0.20), "the ancient cratered southern highlands dominate the south");
            Assert.That((double)ice / total,       Is.GreaterThan(0.05), "polar caps");
        }
    }
}
