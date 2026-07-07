using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;

namespace Pulsar4X.Industry
{
    /// <summary>
    /// Gives a body's mineral DEPOSITS a PLACE on the surface — distributes them across the body's REGIONS so a deposit
    /// is something you can SEE on the planet view, not a body-global abstract. This is the LOCKED PRINCIPLE
    /// (docs/GROUND-COMBAT-MAP-DESIGN.md — "if you can see it, it has a place; abstract-only is a bug") applied to
    /// minerals, the same treatment installations got.
    ///
    /// **One truth, two views (not parallel bookkeeping):** a region holds a SHARE (0..1) of each mineral; its located
    /// amount = share × the body's LIVE <see cref="MineralsDB"/> amount (<see cref="LocatedAmount"/>). So the map is a
    /// spatial VIEW of the same real deposit the colony mines — and a region's shown amount shrinks as the body pool
    /// depletes. **v1 keeps mining colony-wide** (off the body pool); making a mine work only ITS region's share (so
    /// where you place a mine matters) is the flagged follow-up that promotes these shares to the source of truth.
    ///
    /// Shares are terrain-weighted (mountains/volcanic rich, ocean/ice barren) with per-mineral jitter so different
    /// regions are rich in different things, and normalized to sum ~1 per mineral. Deterministic (system RNG),
    /// idempotent, defensive.
    /// </summary>
    public static class RegionMinerals
    {
        // Terrain prospectivity — how mineral-rich a terrain "reads" (geological plausibility, not physics). Tunable dials.
        private static double TerrainWeight(RegionFeatureType t)
        {
            switch (t)
            {
                case RegionFeatureType.Mountains: return 1.0;
                case RegionFeatureType.Volcanic:  return 1.0;
                case RegionFeatureType.Highlands: return 0.8;
                case RegionFeatureType.Barren:    return 0.7;
                case RegionFeatureType.Desert:    return 0.5;
                case RegionFeatureType.Plains:    return 0.4;
                case RegionFeatureType.Tundra:    return 0.3;
                case RegionFeatureType.Forest:    return 0.3;
                case RegionFeatureType.Jungle:    return 0.3;
                case RegionFeatureType.Wetland:   return 0.3;
                case RegionFeatureType.Coast:     return 0.2;
                case RegionFeatureType.Ocean:     return 0.05;   // hard to mine open water
                case RegionFeatureType.Ice:       return 0.05;
                case RegionFeatureType.GasLayers: return 0.0;
                default:                          return 0.2;
            }
        }

        /// <summary>How mineral-rich a region reads overall — its coverage-weighted terrain prospectivity.</summary>
        private static double Prospectivity(Region region)
        {
            if (region?.Features == null || region.Features.Count == 0) return 0.3;   // featureless → modest
            double w = 0, cov = 0;
            foreach (var f in region.Features) { w += f.Coverage * TerrainWeight(f.Type); cov += f.Coverage; }
            return cov > 0 ? Math.Max(0.02, w / cov) : 0.3;
        }

        /// <summary>Fill every region's <see cref="Region.MineralConcentration"/> from the body's minerals — terrain-
        /// weighted + per-mineral jitter, normalized to sum ~1 per mineral. Idempotent (skips if already distributed).
        /// Defensive/no-op on a body with no region layer or no minerals. Deterministic via the system RNG.</summary>
        public static void Distribute(Entity body)
        {
            try
            {
                if (body == null || !body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB) || regionsDB.Regions.Count == 0) return;
                if (!body.TryGetDataBlob<MineralsDB>(out var minerals) || minerals.Minerals.Count == 0) return;

                // Idempotent — if any region already carries shares, it's been distributed.
                foreach (var r in regionsDB.Regions)
                    if (r.MineralConcentration != null && r.MineralConcentration.Count > 0) return;

                var system = body.Manager as StarSystem;
                var regions = regionsDB.Regions;
                var pros = new double[regions.Count];
                for (int i = 0; i < regions.Count; i++) pros[i] = Prospectivity(regions[i]);

                foreach (var mineralId in minerals.Minerals.Keys)
                {
                    var weights = new double[regions.Count];
                    double total = 0;
                    for (int i = 0; i < regions.Count; i++)
                    {
                        double jitter = 0.4 + 1.2 * Rnd(system);       // per-(region,mineral) variety
                        weights[i] = Math.Max(0.0, pros[i] * jitter);
                        total += weights[i];
                    }
                    if (total <= 0) { for (int i = 0; i < regions.Count; i++) weights[i] = 1.0; total = regions.Count; }
                    for (int i = 0; i < regions.Count; i++)
                        regions[i].MineralConcentration[mineralId] = weights[i] / total;
                }
            }
            catch { /* located-deposit distribution is a nicety — never break system gen over it */ }
        }

        /// <summary>The LOCATED amount of one mineral in one region = its share × the body's LIVE deposit amount. 0 if
        /// there's no such deposit or the region holds no share. The read the planet map + any per-region logic uses.</summary>
        public static long LocatedAmount(Region region, MineralsDB bodyMinerals, int mineralId)
        {
            if (region == null || bodyMinerals == null || region.MineralConcentration == null) return 0;
            if (!region.MineralConcentration.TryGetValue(mineralId, out var frac)) return 0;
            if (!bodyMinerals.Minerals.TryGetValue(mineralId, out var deposit)) return 0;
            return (long)(frac * deposit.Amount.Actual);
        }

        private static double Rnd(StarSystem system) => system != null ? system.RNGNextDouble() : 0.5;
    }
}
