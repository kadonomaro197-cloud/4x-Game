using System;
using System.Collections.Generic;
using Pulsar4X.Engine;

namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// Generates a body's HEX patches (Planet → Region → Hex) — LAZILY. The coarse <see cref="PlanetRegionsDB"/> layer
    /// (4 regions) is built for every major body at galaxy-gen; the fine hexes are built ONLY when a body becomes a
    /// theatre (colonized / garrisoned / the tactical view opened), so a galaxy never carries millions of hexes. Each
    /// region gets a hex DISK whose radius scales with the planet's size (bigger world → more hexes), and each hex is
    /// assigned a terrain drawn from that region's coarse <see cref="Region.Features"/> mix — so the fine map is a
    /// faithful realization of the coarse one. Idempotent, deterministic (system RNG), defensive. Save-safe.
    ///
    /// Design: docs/HEX-GROUND-AND-ORDERS-DESIGN.md.
    /// </summary>
    public static class PlanetHexFactory
    {
        private const double EarthRadiusM = 6.371e6;
        private const int BaseHexRadius = 12;   // Earth → a radius-12 patch per region ≈ 469 hexes ×4 ≈ 1876 total
        private const int MinHexRadius = 2;
        private const int MaxHexRadius = 24;     // bound the cost on giant worlds

        /// <summary>The hex-patch radius for one region of a body this size. Scales linearly with planet radius (so hex
        /// COUNT scales with surface AREA), clamped so a tiny moon still has a usable patch and a giant world doesn't
        /// explode. Pure — unit-testable.</summary>
        public static int HexPatchRadiusFor(double bodyRadiusM)
        {
            if (bodyRadiusM <= 0) return MinHexRadius;
            int r = (int)Math.Round(BaseHexRadius * bodyRadiusM / EarthRadiusM);
            return Math.Max(MinHexRadius, Math.Min(MaxHexRadius, r));
        }

        /// <summary>Number of hexes in a disk of the given radius (3r² + 3r + 1). Pure.</summary>
        public static int HexDiskCount(int radius) => 3 * radius * radius + 3 * radius + 1;

        /// <summary>Generate the hex patches for a body's regions if not already done (idempotent). No-op on a body with
        /// no region layer (gas giant / non-major). Never throws.</summary>
        public static void EnsureHexesForBody(Entity body)
        {
            try
            {
                if (body == null || !body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB) || regionsDB.Regions.Count == 0)
                    return;

                double radiusM = body.TryGetDataBlob<MassVolumeDB>(out var mv) ? mv.RadiusInM : EarthRadiusM;
                int patchRadius = HexPatchRadiusFor(radiusM);
                var system = body.Manager as StarSystem;

                foreach (var region in regionsDB.Regions)
                {
                    if (region.Hexes != null && region.Hexes.Count > 0) continue;   // already generated — don't redo
                    region.Hexes = BuildPatch(region, patchRadius, system);
                }
            }
            catch { /* hex gen is a nicety — never break the game over it */ }
        }

        /// <summary>Build one region's hex disk, terrain drawn from the region's feature mix.</summary>
        private static List<GroundHex> BuildPatch(Region region, int radius, StarSystem system)
        {
            var hexes = new List<GroundHex>();
            for (int q = -radius; q <= radius; q++)
            {
                int rLo = Math.Max(-radius, -q - radius);
                int rHi = Math.Min(radius, -q + radius);
                for (int r = rLo; r <= rHi; r++)
                    hexes.Add(new GroundHex(q, r, PickTerrain(region, system)));
            }
            return hexes;
        }

        /// <summary>Weighted terrain pick from a region's features (coverage = weight). Barren if the region has none.</summary>
        private static RegionFeatureType PickTerrain(Region region, StarSystem system)
        {
            var feats = region.Features;
            if (feats == null || feats.Count == 0) return RegionFeatureType.Barren;

            double total = 0;
            foreach (var f in feats) total += Math.Max(0.0001, f.Coverage);

            double roll = (system != null ? system.RNGNextDouble() : 0.5) * total;
            foreach (var f in feats)
            {
                roll -= Math.Max(0.0001, f.Coverage);
                if (roll <= 0) return f.Type;
            }
            return feats[feats.Count - 1].Type;
        }
    }
}
