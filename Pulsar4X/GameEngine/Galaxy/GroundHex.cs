using Newtonsoft.Json;

namespace Pulsar4X.Galaxy
{
    /// <summary>
    /// One HEX of a planet's surface — the fine-grained tile a <see cref="Region"/> is made of (Planet → Region → Hex).
    /// Terrain (and, later, hazards + local ownership) live HERE, so a formation can move London→Paris hex-by-hex and
    /// terrain decides the ground. A save-safe data object (like <see cref="RegionFeature"/>): axial coords stored as
    /// plain ints (<see cref="Q"/>/<see cref="R"/>) so it serializes cleanly — the fix the old city-builder
    /// <c>ColonyHexMapDB</c> lacked. Reuse <c>Colonies.HexCoordinate</c> for neighbour/distance MATH (movement, H2).
    ///
    /// Generated LAZILY per body (<see cref="PlanetHexFactory"/>) — only worlds that become a theatre carry hexes, so a
    /// galaxy isn't bloated with millions. Design: docs/HEX-GROUND-AND-ORDERS-DESIGN.md.
    /// </summary>
    public class GroundHex
    {
        /// <summary>Axial coordinate Q within the region's hex patch (origin = patch centre).</summary>
        [JsonProperty] public int Q { get; internal set; }
        /// <summary>Axial coordinate R within the region's hex patch.</summary>
        [JsonProperty] public int R { get; internal set; }
        /// <summary>This hex's terrain — the fine realization of the region's coarse feature mix.</summary>
        [JsonProperty] public RegionFeatureType Terrain { get; internal set; }
        /// <summary>Which faction holds this hex on the ground (-1 = uncontested). Ground combat flips it (H3).</summary>
        [JsonProperty] public int OwnerFactionID { get; internal set; } = -1;

        public GroundHex() { }
        public GroundHex(int q, int r, RegionFeatureType terrain) { Q = q; R = r; Terrain = terrain; }
        public GroundHex(GroundHex o) { Q = o.Q; R = o.R; Terrain = o.Terrain; OwnerFactionID = o.OwnerFactionID; }
    }
}
