namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// The REAL Luna (the Moon) surface — a baked, hand-authored equirectangular map, so the Sol playtest's Moon reads
    /// as the Moon: the dark near-side MARIA that make its "face" (Imbrium, Serenitatis, Tranquillitatis, Crisium,
    /// Oceanus Procellarum, the southern seas), the bright cratered HIGHLANDS that dominate (esp. the far side), the huge
    /// South Pole-Aitken basin, and small polar ICE caps (the permanently-shadowed crater ice confirmed at the poles).
    /// Same convention + shared decoder as <see cref="EarthTerrainMap"/> (see <see cref="RealSurfaceMaps"/>): row 0 =
    /// north, column 0 = the 180 deg meridian (far-side seam), so the NEAR side (the face) sits centred at column 36.
    /// An airless DRY world — no ocean/atmosphere letters; the palette is highlands / barren (maria+basins) / mountains
    /// (crater rims) / ice. Generator: scratchpad/gen_luna.py. DATA, not physics. Tune by editing the strings (keep each
    /// row exactly <see cref="Cols"/> chars — <see cref="IsWellFormed"/> is the test's guard).
    /// </summary>
    public static class LunaTerrainMap
    {
        public const int Cols = 72;
        public const int Rows = 36;

        // One coherent, recognizable Moon. See the class summary for the row/column convention.
        private static readonly string[] Map =
        {
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhbbbbbbbbbhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhbbbbbhhhhhhhhhhmmhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhmmhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhbbbbbhhhhhhhhhhhhmmhhhhhhhhhhhhhhhhmmhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhbbhhbbbbbbhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhmmhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhbbbhhbbbbbbhhhbbbhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhbbbbhhbbbbbhhhbbbhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhbbbbbhhhhhhhhhhbbbhhhhhhbhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhbbbbbhhhhhhhhhhhhhhbhhhbbbhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhbbbbbhhhhhhhhhhhhbbbbhhhbbhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhbbbbhhhhhhbbbhhhbbbbhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhbbbhhhhhhhbbbhhhhbbbhhbhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhbhhhhhhhhhhhhhhhhhhhbbbhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhmmhhhhhhhhhhhbbbbbhhhhhhhhhhhhbbbhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhmmhhhhhhhhhhbbbbbbhhhhhhhhhbbhhbhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhbbbbbbbhhhhhhhhbbhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhmmhhhhhbbbbbhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhmmhhhhhhbbhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "bbbbhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhbbbbbb",
            "bbbbbbhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhbbbbbbbb",
            "bbbbbbbhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhbbbbbbbbb",
            "bbbbbbbhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhbbbbbbbbb",
            "bbbbbbbhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhbbbbbbbbb",
            "bbbbbbbhhhhhhhhhhhhhhhhhhhhhhhmmmmhhhhhhhhhhhhhhhhhhhhhhhhhhhhhbbbbbbbbb",
            "bbbbbbhhhhhhhhhhhhhhhhhhhhhhhhmmmmhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhbbbbbbbb",
            "bbbbhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhbbbbbb",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii",
        };

        /// <summary>The terrain of real Luna at a fractional position: <paramref name="lon"/> 0..1 wraps eastward from
        /// the 180 deg meridian, <paramref name="lat"/> 0 = north pole -> 1 = south pole. Wraps in longitude, clamps at the poles.</summary>
        public static RegionFeatureType Sample(double lon, double lat)
        {
            double lonFrac = lon - System.Math.Floor(lon);
            int col = (int)(lonFrac * Cols);
            if (col < 0) col = 0; else if (col >= Cols) col = Cols - 1;

            int row = (int)(lat * Rows);
            if (row < 0) row = 0; else if (row >= Rows) row = Rows - 1;

            return RealSurfaceMaps.CharToFeature(Map[row][col]);
        }

        /// <summary>Guard the baked table's shape — every row is exactly <see cref="Cols"/> chars, <see cref="Rows"/> rows.</summary>
        public static bool IsWellFormed()
        {
            if (Map.Length != Rows) return false;
            foreach (var line in Map)
                if (line == null || line.Length != Cols) return false;
            return true;
        }
    }
}
