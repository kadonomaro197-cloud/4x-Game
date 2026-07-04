using System.Collections.Generic;
using Newtonsoft.Json;

namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// The FINE hex grid of ONE developed operational hex — the "city" you zoom into (the compartment diagram to the
    /// operational hex's ship icon; docs/GROUND-CITY-AND-WARMAP-DESIGN.md). Hangs off <see cref="GroundHex.CityGrid"/>,
    /// null until the hex is DEVELOPED (so an undeveloped hex costs nothing), generated lazily by
    /// <see cref="CityGridFactory"/> — the <see cref="PlanetHexFactory"/> pattern one zoom down. Save-safe (deep-copied
    /// by <see cref="GroundHex"/>'s clone; <c>[JsonProperty]</c>) — the persistence the old <c>ColonyHexMapDB</c> lacked.
    ///
    /// The buildings on these tiles ARE the operational hex's footprint: the set of <see cref="CityTile.BuildingInstanceId"/>
    /// on this grid is kept == <see cref="GroundHex.InstallationIds"/> (the roll-up invariant, maintained by
    /// <c>GroundCombat.CityBuilder</c>), so capture/bombard on the operational hex and placement here can never disagree.
    /// </summary>
    public class CityGrid
    {
        /// <summary>Which operational hex this is the city of (region index + that hex's axial coords).</summary>
        [JsonProperty] public int RegionIndex { get; internal set; }
        [JsonProperty] public int HexQ { get; internal set; }
        [JsonProperty] public int HexR { get; internal set; }
        /// <summary>The fine-patch radius (how many rings of city tiles).</summary>
        [JsonProperty] public int Radius { get; internal set; }
        /// <summary>The fine tiles (a hex disk of <see cref="Radius"/>).</summary>
        [JsonProperty] public List<CityTile> Tiles { get; internal set; } = new List<CityTile>();

        public CityGrid() { }
        public CityGrid(int regionIndex, int hexQ, int hexR, int radius)
        {
            RegionIndex = regionIndex; HexQ = hexQ; HexR = hexR; Radius = radius;
        }
        public CityGrid(CityGrid o)
        {
            RegionIndex = o.RegionIndex; HexQ = o.HexQ; HexR = o.HexR; Radius = o.Radius;
            Tiles = new List<CityTile>(o.Tiles?.Count ?? 0);
            if (o.Tiles != null) foreach (var t in o.Tiles) Tiles.Add(new CityTile(t));
        }

        /// <summary>The tile at (q,r), or null.</summary>
        public CityTile TileAt(int q, int r)
        {
            if (Tiles == null) return null;
            foreach (var t in Tiles) if (t.Q == q && t.R == r) return t;
            return null;
        }
    }
}
