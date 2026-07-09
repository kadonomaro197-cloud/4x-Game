namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// The REAL Mars surface — a baked, hand-authored equirectangular map of the actual planet, so the Sol playtest's
    /// classic invasion target reads as Mars (the Tharsis volcanoes + Olympus Mons, the Valles Marineris canyon, the
    /// Hellas and Argyre impact basins, the Elysium volcanic province, the smooth northern lowlands vs. the ancient
    /// cratered southern highlands, and the small polar caps) instead of the random noise field. Same sampling
    /// convention and shared decoder as <see cref="EarthTerrainMap"/> (see <see cref="RealSurfaceMaps"/>): row 0 =
    /// north, column 0 = 180°W, columns run east. Mars is a DRY world — no ocean letters; the palette is desert /
    /// barren / highlands / mountains / volcanic / tundra / ice. Generator: scratchpad/gen_mars.py. It is DATA, not
    /// physics — coarse and readable, not a geology model. Tune by editing the strings (keep each row exactly
    /// <see cref="Cols"/> chars — <see cref="IsWellFormed"/> is the test's guard).
    /// </summary>
    public static class MarsTerrainMap
    {
        public const int Cols = 72;
        public const int Rows = 36;

        // One coherent, recognizable Mars. See the class summary for the row/column convention.
        private static readonly string[] Map =
        {
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii",
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii",
            "tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt",
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
            "dddddddddvvddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
            "dddddddddvdddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
            "ddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddvmmmmddd",
            "dddddddddmmmmmmmmdddddddddddddddddddddddddddddddddddddddddddddddvmmmmddd",
            "bbbbbbbbbmmmmmmmmbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbmmmmmbbb",
            "bbbbbbbbbmmmmmmmmbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            "bbbbbbbbbmmmmvvmmbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            "hhhhhhhhhmmmmvvmmhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhmmmmvvmmhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhmmmmmmmmmmmmmmmmmmmmmhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhmmmmmmmmmmmmmmmmmmmmmhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhmmmmmmmmhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhbbbbbbbbhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhbbbbbbbbhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhbbbbbbhhhhhhhhhhhhhhhhbbbbbbbbhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhbbbbbbhhhhhhhhhhhhhhhhbbbbbbbbhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhbbbbbbhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhbbbbbbhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            "tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt",
            "tttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttttt",
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii",
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii",
        };

        /// <summary>The terrain of real Mars at a fractional position: <paramref name="lon"/> 0..1 wraps eastward from
        /// 180°W, <paramref name="lat"/> 0 = north pole → 1 = south pole. Wraps in longitude, clamps at the poles.</summary>
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
