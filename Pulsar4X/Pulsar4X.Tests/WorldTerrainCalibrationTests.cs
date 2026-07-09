using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Galaxy;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauges for the PROCEDURAL terrain generator's calibration — the noise field every NON-authored world uses
    /// (i.e. everything except Earth/Mars). Two bugs were found by rendering the generator's own output:
    ///   (1) wet worlds over-flooded — a 71%-water world came out ~82% ocean (the old linear sea-level guess didn't
    ///       match the bunched-up noise distribution); now sea level is the hydrosphere QUANTILE, so ocean tracks it.
    ///   (2) cold worlds collapsed to a single biome — below −10 °C the classifier only emitted tundra/highlands/ice,
    ///       so Mars/Luna/every outer body was a featureless tundra ball; now a dry cold world reads as barren/desert.
    /// These assert the fixes on the generator directly (via the internal test seam), across several seeds so it's a
    /// property of the generator, not one lucky world. Authored bodies (Earth/Mars) bypass this path entirely.
    /// </summary>
    [TestFixture]
    public class WorldTerrainCalibrationTests
    {
        // Measure the terrain mix a world produces over a fine lon/lat grid.
        private static Dictionary<RegionFeatureType, double> Mix(WorldTerrain w)
        {
            const int C = 144, R = 72;
            var counts = new Dictionary<RegionFeatureType, int>();
            for (int r = 0; r < R; r++)
                for (int c = 0; c < C; c++)
                {
                    var f = w.ClassifyForTest((c + 0.5) / C, (r + 0.5) / R);
                    counts.TryGetValue(f, out var n); counts[f] = n + 1;
                }
            var frac = new Dictionary<RegionFeatureType, double>();
            foreach (var kv in counts) frac[kv.Key] = (double)kv.Value / (C * R);
            return frac;
        }
        private static double Of(Dictionary<RegionFeatureType, double> m, RegionFeatureType t)
            => m.TryGetValue(t, out var v) ? v : 0.0;

        [Test]
        [Description("Ocean coverage tracks the hydrosphere: a 71%-water world comes out ~71% ocean (was ~82%), a 30% world ~30% — across seeds.")]
        public void OceanFraction_TracksHydrosphere()
        {
            foreach (int seed in new[] { 1, 2, 3, 7, 42 })
            {
                var wet = Mix(WorldTerrain.ForTest(0.71, 14.0, true, seed));
                Assert.That(Of(wet, RegionFeatureType.Ocean), Is.InRange(0.63, 0.79),
                    $"seed {seed}: a 71%-water world should be ~71% ocean, not the old ~82%");

                var mid = Mix(WorldTerrain.ForTest(0.30, 14.0, true, seed));
                Assert.That(Of(mid, RegionFeatureType.Ocean), Is.InRange(0.22, 0.38),
                    $"seed {seed}: a 30%-water world should be ~30% ocean");
            }
        }

        [Test]
        [Description("A dry world has no ocean; a waterworld is (almost) all ocean — the endpoints of the quantile mapping.")]
        public void DryWorld_NoOcean_WaterWorld_AllOcean()
        {
            var dry = Mix(WorldTerrain.ForTest(0.0, 14.0, true, 1));
            Assert.That(Of(dry, RegionFeatureType.Ocean), Is.EqualTo(0.0), "a hydrosphere-0 world has no ocean");

            var sea = Mix(WorldTerrain.ForTest(1.0, 14.0, true, 1));
            Assert.That(Of(sea, RegionFeatureType.Ocean), Is.GreaterThan(0.95), "a hydrosphere-1 world is (near) all ocean");
        }

        [Test]
        [Description("A cold DRY world (Mars/Luna-like) is not a featureless tundra ball — it reads as barren/desert with relief, and almost no tundra (the old bug made it ~all tundra).")]
        public void ColdDryWorld_IsNotUniformTundra()
        {
            foreach (int seed in new[] { 1, 2, 3, 7 })
            {
                var m = Mix(WorldTerrain.ForTest(0.0, -63.0, true, seed));
                Assert.That(Of(m, RegionFeatureType.Ocean), Is.EqualTo(0.0), $"seed {seed}: dry world, no ocean");
                Assert.That(Of(m, RegionFeatureType.Tundra), Is.LessThan(0.10),
                    $"seed {seed}: a DRY cold world should be barren/desert, not tundra (the old bug)");
                double rocky = Of(m, RegionFeatureType.Barren) + Of(m, RegionFeatureType.Desert);
                Assert.That(rocky, Is.GreaterThan(0.5), $"seed {seed}: dry cold lowland is rock and dust");
                int distinct = 0;
                foreach (var kv in m) if (kv.Value > 0.01) distinct++;
                Assert.That(distinct, Is.GreaterThanOrEqualTo(2), $"seed {seed}: more than one biome — not a flat ball");
            }
        }
    }
}
