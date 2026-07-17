using System;
using System.Collections.Generic;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;

namespace Pulsar4X.Industry
{
    /// <summary>
    /// Seeds a body's mineral DEPOSITS onto specific surface HEXES — so "there are resources HERE" is a place you can
    /// scan, see on the map, and build a mine on (the LOCKED PRINCIPLE applied to minerals — every planet, whole game).
    /// Terrain-flavored: deposits cluster where it reads geologically (mountains / volcanic / highlands, then barren /
    /// desert), and NEVER in open ocean or ice. Each deposit hex carries ONE mineral (a legible map — "iron here,
    /// copper there"); a mineral's deposit hexes partition a share of the body's real <see cref="MineralsDB"/> amount.
    /// Deterministic (system RNG), idempotent, defensive.
    ///
    /// v1 is the LOCATED VIEW: the colony still mines the body-wide pool (<c>MineResourcesProcessor</c> unchanged), so
    /// the hex amounts are the spatial picture, not yet the mined source of truth. Per-hex mining (a mine works the
    /// deposit it SITS on, and THAT hex depletes) is the flagged follow-up that promotes these to the source of truth.
    /// All magnitudes below are tunable dials.
    /// </summary>
    public static class HexMinerals
    {
        private const int LandHexesPerDeposit = 150;   // ~1 deposit hex per this many mineable hexes, per mineral
        private const int MinDepositsPerMineral = 3;
        private const int MaxDepositsPerMineral = 20;
        private const double LocatableFraction = 0.6;  // share of a mineral's body amount that is LOCATED on hexes (the
                                                       // rest reads as deep/unlocated reserve) — a flagged dial

        /// <summary>How mineral-rich a terrain reads (geological plausibility, not physics). 0 = no deposits there.</summary>
        private static double TerrainWeight(RegionFeatureType t)
        {
            switch (t)
            {
                case RegionFeatureType.Mountains: return 1.0;
                case RegionFeatureType.Volcanic:  return 1.0;
                case RegionFeatureType.Highlands: return 0.8;
                case RegionFeatureType.Barren:    return 0.7;
                case RegionFeatureType.Desert:    return 0.5;
                case RegionFeatureType.Plains:    return 0.35;
                case RegionFeatureType.Tundra:    return 0.3;
                case RegionFeatureType.Forest:    return 0.3;
                case RegionFeatureType.Jungle:    return 0.3;
                case RegionFeatureType.Wetland:   return 0.25;
                case RegionFeatureType.Coast:     return 0.15;
                default:                          return 0.0;   // Ocean / Ice / GasLayers / Unknown → no deposits
            }
        }

        /// <summary>Seed deposits onto the body's <paramref name="grid"/> hexes. Idempotent (skips if any hex already
        /// holds a deposit). No-op on a body with no grid / no minerals. Deterministic via the system RNG. Never throws.</summary>
        public static void SeedDeposits(Entity body, SurfaceGrid grid)
        {
            try
            {
                if (body == null || grid == null || grid.Hexes == null || grid.Hexes.Count == 0) return;
                if (!body.TryGetDataBlob<MineralsDB>(out var minerals) || minerals.Minerals.Count == 0) return;

                foreach (var h in grid.Hexes) if (h.DepositMineralId >= 0) return;   // already seeded

                var system = body.Manager as StarSystem;

                // Mineable candidate hexes weighted by terrain.
                var candidates = new List<GroundHex>();
                var weights = new List<double>();
                double weightTotal = 0;
                foreach (var h in grid.Hexes)
                {
                    double w = TerrainWeight(h.Terrain);
                    if (w <= 0) continue;
                    candidates.Add(h); weights.Add(w); weightTotal += w;
                }
                if (candidates.Count == 0 || weightTotal <= 0) return;

                int perMineral = Math.Max(MinDepositsPerMineral,
                                 Math.Min(MaxDepositsPerMineral, candidates.Count / LandHexesPerDeposit));

                var used = new HashSet<GroundHex>();
                foreach (var kv in minerals.Minerals)
                {
                    long bodyAmount = kv.Value.Amount.Actual;
                    if (bodyAmount <= 0) continue;
                    long locatable = (long)(bodyAmount * LocatableFraction);
                    if (locatable <= 0) continue;

                    var picks = new List<GroundHex>();
                    int attempts = 0, maxAttempts = perMineral * 12;
                    while (picks.Count < perMineral && attempts++ < maxAttempts)
                    {
                        var h = WeightedPick(candidates, weights, weightTotal, system);
                        if (h == null || used.Contains(h)) continue;
                        used.Add(h);
                        picks.Add(h);
                    }
                    if (picks.Count == 0) continue;

                    long share = Math.Max(1, locatable / picks.Count);
                    foreach (var h in picks)
                    {
                        h.DepositMineralId = kv.Key;
                        h.DepositAmount = share;
                        // Per-faction masked view of the same tonnage — HIDDEN by default (AccessLevel.None), so the
                        // amount no longer leaks to every faction (ground-fog slice 1b). Space survey grants Partial,
                        // a ground scout grants Full. DepositAmount above stays as the omniscient server-truth.
                        h.DepositAssay = new Masked<long>(share, AccessLevel.None);
                    }
                }
            }
            catch { /* deposit seeding is a nicety — never break grid generation over it */ }
        }

        private static GroundHex WeightedPick(List<GroundHex> hexes, List<double> weights, double total, StarSystem system)
        {
            double r = Rnd(system) * total;
            for (int i = 0; i < hexes.Count; i++) { r -= weights[i]; if (r <= 0) return hexes[i]; }
            return hexes.Count > 0 ? hexes[hexes.Count - 1] : null;
        }

        private static double Rnd(StarSystem system) => system != null ? system.RNGNextDouble() : 0.5;
    }
}
