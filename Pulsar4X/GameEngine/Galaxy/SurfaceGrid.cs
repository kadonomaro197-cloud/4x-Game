using System.Collections.Generic;
using Newtonsoft.Json;

namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// ONE continuous CYLINDER of surface hexes (G-track) — the planet surface as a single grid instead of four
    /// per-region disks. <b>Q = longitude column</b> `[0, Cols)` and **WRAPS** (column `Cols-1` is adjacent to column
    /// `0` — the seam that keeps the Pacific whole); <b>R = latitude row</b> `[0, Rows)`, bounded (poles are the top and
    /// bottom rows). Terrain is continuous AND wrapping by construction (every hex sampled from `WorldTerrain` at its
    /// global lon/lat). A "region" is just a column BAND label over this grid.
    ///
    /// Hexes are stored row-major (`index = R*Cols + Q`) so lookup is O(1). Save-safe (deep-copied; `[JsonProperty]`).
    /// Reuses <see cref="GroundHex"/> (its Q/R now hold GLOBAL coords). Design: docs/GLOBAL-HEX-GRID-DESIGN.md.
    /// </summary>
    public class SurfaceGrid
    {
        [JsonProperty] public int Cols { get; internal set; }
        [JsonProperty] public int Rows { get; internal set; }
        /// <summary>Row-major (index = R*Cols + Q); each hex carries its GLOBAL (Q,R).</summary>
        [JsonProperty] public List<GroundHex> Hexes { get; internal set; } = new List<GroundHex>();

        public SurfaceGrid() { }
        public SurfaceGrid(int cols, int rows) { Cols = cols; Rows = rows; }
        public SurfaceGrid(SurfaceGrid o)
        {
            Cols = o.Cols; Rows = o.Rows;
            Hexes = new List<GroundHex>(o.Hexes?.Count ?? 0);
            if (o.Hexes != null) foreach (var h in o.Hexes) Hexes.Add(new GroundHex(h));
        }

        /// <summary>Wrap a column into `[0, Cols)` (the longitude seam).</summary>
        public int WrapCol(int q) => Cols <= 0 ? 0 : ((q % Cols) + Cols) % Cols;

        /// <summary>The hex at global (q,r): q WRAPS; an out-of-range row returns null. O(1) row-major.</summary>
        public GroundHex HexAt(int q, int r)
        {
            if (Cols <= 0 || r < 0 || r >= Rows) return null;
            int idx = r * Cols + WrapCol(q);
            return (idx >= 0 && idx < Hexes.Count) ? Hexes[idx] : null;
        }
    }
}
