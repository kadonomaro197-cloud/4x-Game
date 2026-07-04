using Newtonsoft.Json;

namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// One FINE tile of a developed operational hex's <see cref="CityGrid"/> — the "compartment" in the
    /// damage-control-diagram analogy (docs/GROUND-CITY-AND-WARMAP-DESIGN.md). This is the finest zoom: a single
    /// building sits here 1:1. A save-safe data object like <see cref="GroundHex"/>/<see cref="RegionFeature"/>:
    /// axial coords as plain ints so it serializes cleanly.
    ///
    /// The building is a <c>ComponentInstance</c> on the colony (the same economy object the colony screen and the
    /// W-track operational hex use) — this tile just records WHICH fine tile it occupies. <see cref="BuildingInstanceId"/>
    /// = -1 means the tile is empty.
    /// </summary>
    public class CityTile
    {
        /// <summary>Axial coordinate Q within the city patch (origin = patch centre).</summary>
        [JsonProperty] public int Q { get; internal set; }
        /// <summary>Axial coordinate R within the city patch.</summary>
        [JsonProperty] public int R { get; internal set; }
        /// <summary>This fine tile's terrain (v1: inherited from the operational hex; C2 adds a fine field).</summary>
        [JsonProperty] public RegionFeatureType Terrain { get; internal set; }
        /// <summary>The building instance id on this tile (-1 = empty). One building per fine tile (1:1 placement).</summary>
        [JsonProperty] public int BuildingInstanceId { get; internal set; } = -1;

        public CityTile() { }
        public CityTile(int q, int r, RegionFeatureType terrain) { Q = q; R = r; Terrain = terrain; }
        public CityTile(CityTile o) { Q = o.Q; R = o.R; Terrain = o.Terrain; BuildingInstanceId = o.BuildingInstanceId; }
    }
}
