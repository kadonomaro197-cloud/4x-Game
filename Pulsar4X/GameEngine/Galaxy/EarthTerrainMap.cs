namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// The REAL Earth surface — a baked, hand-authored equirectangular map of our planet's geography, so the Sol
    /// homeworld renders as recognizable Earth (the Americas, the Andes, the Sahara, the Himalaya, Australia, both
    /// ice caps) instead of the random sum-of-sines world the procedural <see cref="WorldTerrain"/> rolls for every
    /// other body. This is Earth's ANSWER to that generator: when a body is our Earth, <see cref="WorldTerrain"/>
    /// samples THIS table instead of the noise field, at the same (lon,lat) sample points, so nothing downstream
    /// (the cylinder grid, the region disks, the hex terrain, the client map) changes shape — only the terrain that
    /// comes out.
    ///
    /// Layout (the developer-facing datum): a fixed grid of <see cref="Cols"/>×<see cref="Rows"/> cells.
    ///   • ROW 0 is the NORTH pole (top of the surface map), the last row is the SOUTH pole — matching the client's
    ///     top-down draw and the engine's lat=0→north convention.
    ///   • COLUMN 0 is the 180°W meridian (the mid-Pacific seam, so the wrap falls on open ocean and no continent is
    ///     cut in half); columns run EAST. Column 36 is the prime meridian (0°, Greenwich).
    /// Each cell is one terrain letter (see <see cref="CharToFeature"/>). The map was authored from real
    /// continent/biome positions (generator: scratchpad/gen_earth.py) — a coarse but honest Earth. It is DATA, not
    /// physics: the resolution is deliberately low (it's sampled onto a hex grid a fraction of this size), and the
    /// biomes are the readable-at-a-glance kind (ocean / coast / forest / plains / desert / jungle / mountains /
    /// tundra / ice), not a climate model. Tune by editing the strings (keep every row exactly <see cref="Cols"/>
    /// characters — <see cref="IsWellFormed"/> is the guard the test asserts).
    /// </summary>
    public static class EarthTerrainMap
    {
        public const int Cols = 72;
        public const int Rows = 36;

        // One coherent, recognizable Earth. See the class summary for the row/column convention.
        private static readonly string[] Map =
        {
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii",
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii",
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiitttttttttttttttttttttttt",
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiitttttttttttttttttttttttt",
            "c.cfffffffffffffffffffffciiiiiii....cffffffffffftttttttttttttttttttttttt",
            "c.cfffffffffffffffffffffciiiiiii..cccfffffffffffffffffffffffffffffffffff",
            "c..cccccfffmmfffffffffffc........cffffffffffffffffffffffffffffffffffffff",
            "c......cfffmmfffffffffffc........cffffffffccffffffffffffffffffffffffffff",
            "........cccmmfffffffffcc.........cfffmmfffccppppddddddddddppcccccccccccc",
            "..........cmmfffffffffc..........cpmmpppppppppppddddddddddppcffffc......",
            "..........cmmfffffffffc..........cppppppppppddddddmmmmddddffcffffc......",
            "..........cmmfffffffffc.........cdddddddddddddddccmmmmccffffccccc.......",
            "...........cccpppppccc..........cdddddddddddddddccppppjjjjppc...........",
            ".............cpppppcc...........cdddddddddddddddccppppjjjjppc...........",
            ".............cppppjjjc..........cpppppppcccccccc.cppppcjjjccjc..........",
            "..............ccccjjjccccc......cpppppppcc.......cppppcjjjccjc..........",
            ".................cjjjjjjjjc......cjjjjjjjjc.......cccccjjjccjccc........",
            "..................ccmmjjjjc......cjjjjjjjjc...........cjjjjjjjjjc.......",
            "...................cmmjjjjcc.....cjjjjjjjjcc..........cjjjjjjjjjc.......",
            "...................cmmjjjjjjc.....ccccppppppc.........cjjjjjjjjjcc......",
            "...................cmmjjjjjjc........cppppppc..........ccccpppppppc.....",
            "...................cmmjjjjjjc........cppppppc.............cpppppppc.....",
            "...................cmmcpppppc.........cddddc...............cddddddc.....",
            "...................cmmcpppppc.........cddddc...............cddddddc.....",
            "...................cmmcpppppc.........cddddc................cccppppc.ccc",
            "c..................cmmpppccc...........cccc...................cppppccfff",
            "c...................cppppc.....................................cccc.cfff",
            "....................cppppc...........................................ccc",
            "....................cppppc..............................................",
            ".....................cccc...............................................",
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii",
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii",
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii",
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii",
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii",
            "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii",
        };

        /// <summary>Map one authored letter to a <see cref="RegionFeatureType"/>. Anything unrecognized reads as Plains
        /// (a safe, passable default) so a typo in the map can never produce impassable garbage.</summary>
        private static RegionFeatureType CharToFeature(char ch)
        {
            switch (ch)
            {
                case '.': return RegionFeatureType.Ocean;
                case 'c': return RegionFeatureType.Coast;
                case 'w': return RegionFeatureType.Wetland;
                case 'f': return RegionFeatureType.Forest;
                case 'j': return RegionFeatureType.Jungle;
                case 'p': return RegionFeatureType.Plains;
                case 'd': return RegionFeatureType.Desert;
                case 'b': return RegionFeatureType.Barren;
                case 'h': return RegionFeatureType.Highlands;
                case 'm': return RegionFeatureType.Mountains;
                case 't': return RegionFeatureType.Tundra;
                case 'i': return RegionFeatureType.Ice;
                default:  return RegionFeatureType.Plains;
            }
        }

        /// <summary>
        /// The terrain of real Earth at a fractional position: <paramref name="lon"/> 0..1 wraps eastward from 180°W,
        /// <paramref name="lat"/> 0 = north pole → 1 = south pole (the same sample space <see cref="WorldTerrain"/>
        /// feeds every body). Wraps in longitude and clamps at the poles, so any input is safe.
        /// </summary>
        public static RegionFeatureType Sample(double lon, double lat)
        {
            // longitude wraps; latitude clamps to the pole rows.
            double lonFrac = lon - System.Math.Floor(lon);         // → [0,1)
            int col = (int)(lonFrac * Cols);
            if (col < 0) col = 0; else if (col >= Cols) col = Cols - 1;

            int row = (int)(lat * Rows);
            if (row < 0) row = 0; else if (row >= Rows) row = Rows - 1;

            return CharToFeature(Map[row][col]);
        }

        /// <summary>Guard the baked table's shape — every row is exactly <see cref="Cols"/> chars and there are
        /// <see cref="Rows"/> rows. The test asserts this so an edit that mis-sizes a row is caught in CI, not on the
        /// developer's map.</summary>
        public static bool IsWellFormed()
        {
            if (Map.Length != Rows) return false;
            foreach (var line in Map)
                if (line == null || line.Length != Cols) return false;
            return true;
        }
    }
}
