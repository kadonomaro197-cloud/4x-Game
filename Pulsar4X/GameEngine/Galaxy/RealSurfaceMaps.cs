using System;

namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// The registry of REAL, authored surface maps — the worlds we have an actual map of (Earth, Mars, …), so the Sol
    /// playtest bodies render as themselves instead of the random noise field every other world gets. <see cref="WorldTerrain"/>
    /// asks this for a sampler keyed on the body's name; a non-null answer means "this world has a real map, use it."
    /// Adding a world is one line here plus its baked map class (see <see cref="EarthTerrainMap"/> / <see cref="MarsTerrainMap"/>).
    ///
    /// Shared sampling convention for every baked map: a fixed grid of chars, <b>row 0 = North pole</b> (top),
    /// <b>column 0 = 180°W</b> (the wrap seam), columns run EAST — matching the client's top-down draw and the engine's
    /// lat=0→north / lon-wraps sample space. <see cref="CharToFeature"/> is the ONE shared letter→terrain decoder so the
    /// maps can't drift apart on what a letter means.
    /// </summary>
    public static class RealSurfaceMaps
    {
        /// <summary>The sampler for a body with this default name, or null if it's just a generated world. Gated by the
        /// caller to real (non-gas) bodies, so a coincidentally-named gas giant can't trip a surface map.</summary>
        public static Func<double, double, RegionFeatureType> SamplerForName(string defaultName)
        {
            switch (defaultName)
            {
                case "Earth": return EarthTerrainMap.Sample;
                case "Mars":  return MarsTerrainMap.Sample;
                case "Luna":  return LunaTerrainMap.Sample;
                default:      return null;
            }
        }

        /// <summary>The shared letter→terrain decoder for every baked map. Anything unrecognized reads as Plains (a safe,
        /// passable default) so a typo in a map can never produce impassable garbage.</summary>
        public static RegionFeatureType CharToFeature(char ch)
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
                case 'v': return RegionFeatureType.Volcanic;
                case 't': return RegionFeatureType.Tundra;
                case 'i': return RegionFeatureType.Ice;
                default:  return RegionFeatureType.Plains;
            }
        }
    }
}
