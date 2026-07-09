using NUnit.Framework;
using Pulsar4X.Galaxy;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the REAL Luna (Moon) surface map — the Sol playtest's Moon reads as the Moon, not the noise field.
    /// Asserts (a) the baked table is well-formed, and (b) it is really Luna: an airless DRY world (no ocean), ice at
    /// the poles, bright cratered HIGHLANDS dominating the surface, and the dark near-side MARIA present (the "seas"
    /// that make the Moon's face). The specific mare positions aren't asserted (that's art); the character is.
    /// </summary>
    [TestFixture]
    public class LunaTerrainMapTests
    {
        [Test]
        [Description("The baked table is exactly Rows×Cols — a mis-sized row is a data bug caught here.")]
        public void LunaMap_IsWellFormed()
        {
            Assert.That(LunaTerrainMap.IsWellFormed(), Is.True, "every row must be exactly Cols chars, Rows rows");
        }

        [Test]
        [Description("Luna is airless and dry with polar ice — no ocean anywhere, frozen poles.")]
        public void LunaMap_IsDry_WithPolarIce()
        {
            Assert.That(LunaTerrainMap.Sample(0.5, 0.0),  Is.EqualTo(RegionFeatureType.Ice), "north polar ice");
            Assert.That(LunaTerrainMap.Sample(0.5, 0.99), Is.EqualTo(RegionFeatureType.Ice), "south polar ice");
            for (int r = 0; r < 180; r++)
                for (int c = 0; c < 360; c++)
                    Assert.That(LunaTerrainMap.Sample((c + 0.5) / 360.0, (r + 0.5) / 180.0),
                        Is.Not.EqualTo(RegionFeatureType.Ocean), "the Moon has no oceans");
        }

        [Test]
        [Description("Bright cratered highlands dominate, with the dark near-side maria (the 'seas') present — the Moon's face.")]
        public void LunaMap_HighlandsDominate_WithMaria()
        {
            int total = 0, highlands = 0, maria = 0;
            for (int r = 0; r < 180; r++)
                for (int c = 0; c < 360; c++)
                {
                    var f = LunaTerrainMap.Sample((c + 0.5) / 360.0, (r + 0.5) / 180.0);
                    total++;
                    if (f == RegionFeatureType.Highlands) highlands++;
                    else if (f == RegionFeatureType.Barren) maria++;   // the dark basaltic maria/basins
                }
            Assert.That((double)highlands / total, Is.GreaterThan(0.60), "cratered highlands dominate the lunar surface");
            Assert.That((double)maria / total, Is.InRange(0.03, 0.30), "the dark maria are present but don't dominate");
        }
    }
}
