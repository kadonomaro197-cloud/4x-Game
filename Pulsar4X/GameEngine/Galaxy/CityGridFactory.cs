using System;
using System.Collections.Generic;
using Pulsar4X.Engine;

namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// Generates the FINE city grid of a developed operational hex — LAZILY. The <see cref="PlanetHexFactory"/> pattern
    /// one zoom DOWN: where that builds a region's operational hex disk when a world becomes a theatre, this builds a
    /// single operational hex's fine tile disk when a colony DEVELOPS that hex. Only developed hexes carry a grid, so a
    /// planet isn't bloated with fine tiles for wilderness it never builds on. Idempotent, deterministic, defensive
    /// (never throws — it runs near the ground hotloop / at colony creation). Save-safe (the grid rides
    /// <see cref="GroundHex"/>'s clone). Design: docs/GROUND-CITY-AND-WARMAP-DESIGN.md.
    /// </summary>
    public static class CityGridFactory
    {
        /// <summary>Fine-patch radius — a developed hex's city is ~127 tiles (3r²+3r+1 at r=6). A STRUCTURAL grid-size
        /// constant (like <see cref="PlanetHexFactory"/>'s BaseHexRadius), in the design-approved 5–8 band, not a
        /// gameplay dial. Tunable.</summary>
        public const int CityPatchRadius = 6;

        /// <summary>Number of tiles in a city of the given radius (3r²+3r+1). Pure.</summary>
        public static int CityTileCount(int radius) => 3 * radius * radius + 3 * radius + 1;

        /// <summary>Ensure the operational hex at (<paramref name="regionIndex"/>, <paramref name="hexQ"/>,
        /// <paramref name="hexR"/>) has a fine city grid, building one if absent (idempotent). Returns the grid, or null
        /// if the hex can't be resolved. Never throws.</summary>
        public static CityGrid EnsureCityForHex(Entity body, int regionIndex, int hexQ, int hexR)
        {
            try
            {
                var hex = ResolveHex(body, regionIndex, hexQ, hexR);
                if (hex == null) return null;
                if (hex.CityGrid != null && hex.CityGrid.Tiles != null && hex.CityGrid.Tiles.Count > 0)
                    return hex.CityGrid;   // idempotent

                hex.CityGrid = BuildGrid(regionIndex, hexQ, hexR, CityPatchRadius, hex.Terrain);
                return hex.CityGrid;
            }
            catch { return null; }   // city gen is a nicety — never break the game over it
        }

        /// <summary>Resolve the <see cref="GroundHex"/> at (region, q, r) on a body, or null. Never throws.</summary>
        public static GroundHex ResolveHex(Entity body, int regionIndex, int hexQ, int hexR)
        {
            if (body == null || !body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB)) return null;
            if (regionIndex < 0 || regionIndex >= regionsDB.Regions.Count) return null;
            var region = regionsDB.Regions[regionIndex];
            if (region?.Hexes == null) return null;
            foreach (var h in region.Hexes) if (h.Q == hexQ && h.R == hexR) return h;
            return null;
        }

        // ── GLOBAL grid (G4) — the same "city under a hex" idea addressed on the ONE continuous cylinder ────────────
        // A hex is found by its GLOBAL (Q,R) on regionsDB.SurfaceGrid instead of region+local. The disk path above stays
        // until G6 retires it. Design: docs/GLOBAL-HEX-GRID-DESIGN.md (G4).

        /// <summary>Resolve the <see cref="GroundHex"/> at GLOBAL (<paramref name="gQ"/>,<paramref name="gR"/>) on the
        /// body's cylinder grid (generating the grid on demand), or null. Q wraps at the seam; an out-of-range row → null.
        /// Never throws.</summary>
        public static GroundHex ResolveGlobalHex(Entity body, int gQ, int gR)
        {
            if (body == null || !body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB)) return null;
            var grid = regionsDB.SurfaceGrid;
            if (grid == null || grid.Hexes == null || grid.Hexes.Count == 0)
                grid = PlanetGridFactory.EnsureGridForBody(body);
            return grid?.HexAt(gQ, gR);
        }

        /// <summary>Ensure the GLOBAL hex at (<paramref name="gQ"/>,<paramref name="gR"/>) has a fine city grid, building
        /// one if absent (idempotent). The city's region label is the column BAND the hex falls in. Returns the grid, or
        /// null if the hex can't be resolved. Never throws.</summary>
        public static CityGrid EnsureCityForGlobalHex(Entity body, int gQ, int gR)
        {
            try
            {
                var hex = ResolveGlobalHex(body, gQ, gR);
                if (hex == null) return null;
                if (hex.CityGrid != null && hex.CityGrid.Tiles != null && hex.CityGrid.Tiles.Count > 0)
                    return hex.CityGrid;   // idempotent

                body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB);
                int cols = regionsDB?.SurfaceGrid?.Cols ?? 0;
                int rc = regionsDB?.Regions?.Count ?? 0;
                int band = PlanetGridFactory.RegionOfColumn(hex.Q, cols, rc);
                hex.CityGrid = BuildGrid(band, hex.Q, hex.R, CityPatchRadius, hex.Terrain);
                return hex.CityGrid;
            }
            catch { return null; }   // city gen is a nicety — never break the game over it
        }

        /// <summary>Build one operational hex's fine tile disk. v1 terrain = the operational hex's terrain (a fine
        /// per-tile field is C2), so the whole developed patch is buildable ground.</summary>
        private static CityGrid BuildGrid(int regionIndex, int hexQ, int hexR, int radius, RegionFeatureType baseTerrain)
        {
            var grid = new CityGrid(regionIndex, hexQ, hexR, radius);
            for (int q = -radius; q <= radius; q++)
            {
                int rLo = Math.Max(-radius, -q - radius);
                int rHi = Math.Min(radius, -q + radius);
                for (int r = rLo; r <= rHi; r++)
                    grid.Tiles.Add(new CityTile(q, r, baseTerrain));
            }
            return grid;
        }
    }
}
