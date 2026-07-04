using System;
using Pulsar4X.Engine;

namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// Generates a body's ONE continuous cylinder hex grid (G-track) — the replacement for <see cref="PlanetHexFactory"/>'s
    /// per-region disks. One wrapping grid: columns are longitude, rows are latitude, terrain sampled from the global
    /// field so it's continuous across every column and seam-wrapping by construction. Dimensions scale with planet
    /// size and stay divisible by the region count so region column-BANDS are clean. Lazy, idempotent, deterministic
    /// (system RNG via <see cref="WorldTerrain"/>), defensive (never throws). Design: docs/GLOBAL-HEX-GRID-DESIGN.md.
    /// </summary>
    public static class PlanetGridFactory
    {
        private const double EarthRadiusM = 6.371e6;

        /// <summary>Columns per region BAND. Region N owns columns `[N*ColumnsPerRegion, (N+1)*ColumnsPerRegion)`; total
        /// columns = regionCount × this, so the equator is ~the old 4×(2R+1) hex width. Pure.</summary>
        public static int ColumnsPerRegion(double bodyRadiusM) => 2 * PlanetHexFactory.HexPatchRadiusFor(bodyRadiusM) + 1;

        /// <summary>Which region band a global column belongs to (0..regionCount-1). Pure.</summary>
        public static int RegionOfColumn(int q, int cols, int regionCount)
        {
            if (cols <= 0 || regionCount <= 0) return 0;
            int qq = ((q % cols) + cols) % cols;
            return Math.Min(regionCount - 1, qq * regionCount / cols);
        }

        /// <summary>The centre column of a region band (where units muster) — the global twin of the disk's (0,0).</summary>
        public static int BandCentreColumn(int region, int cols, int regionCount)
        {
            if (cols <= 0 || regionCount <= 0) return 0;
            int w = cols / regionCount;
            return region * w + w / 2;
        }

        /// <summary>Ensure the body has its cylinder grid, building it if absent (idempotent). No-op on a body with no
        /// region layer. Never throws. Terrain is the same coherent world (V2) sampled at each hex's GLOBAL lon/lat.</summary>
        public static SurfaceGrid EnsureGridForBody(Entity body)
        {
            try
            {
                if (body == null || !body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB) || regionsDB.Regions.Count == 0)
                    return null;
                if (regionsDB.SurfaceGrid != null && regionsDB.SurfaceGrid.Hexes != null && regionsDB.SurfaceGrid.Hexes.Count > 0)
                    return regionsDB.SurfaceGrid;   // idempotent

                double radiusM = body.TryGetDataBlob<MassVolumeDB>(out var mv) ? mv.RadiusInM : EarthRadiusM;
                int regionCount = regionsDB.Regions.Count;
                int colsPerRegion = ColumnsPerRegion(radiusM);
                int cols = regionCount * colsPerRegion;         // divisible by regionCount → clean bands
                int rows = colsPerRegion;                       // ~square cells pole-to-pole
                var system = body.Manager as StarSystem;
                var world = WorldTerrain.ForBody(body, system, regionCount);

                var grid = new SurfaceGrid(cols, rows);
                double lonDiv = cols;
                double latDiv = Math.Max(1, rows - 1);
                for (int r = 0; r < rows; r++)
                    for (int q = 0; q < cols; q++)
                    {
                        double lon = q / lonDiv;                // 0..1, wraps
                        double lat = r / latDiv;                // 0 pole → 1 pole
                        grid.Hexes.Add(new GroundHex(q, r, world.TerrainForLonLat(lon, lat)));
                    }
                regionsDB.SurfaceGrid = grid;
                return grid;
            }
            catch { return null; }   // grid gen is a nicety — never break the game over it
        }
    }
}
