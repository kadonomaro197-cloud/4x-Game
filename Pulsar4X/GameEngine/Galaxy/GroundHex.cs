using System.Collections.Generic;
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

        /// <summary>The MINERAL deposit ON this hex (-1 = none) — the "there are resources HERE" the map flags post-scan,
        /// so you build a mine on the actual deposit (the LOCKED PRINCIPLE applied to minerals; docs/RESOURCES-AND-
        /// MATERIALS-DESIGN.md). Seeded terrain-flavored by <c>Industry.HexMinerals</c> when the body's surface grid is
        /// generated (post-survey). v1: one mineral per deposit hex, for a legible map. <see cref="DepositAmount"/> is
        /// the accessible tonnes located here — a share of the body's real deposit (mining stays colony-wide in v1;
        /// per-hex mining that draws THIS deposit is the follow-up). Save-safe (copied below).</summary>
        [JsonProperty] public int DepositMineralId { get; internal set; } = -1;
        /// <summary>Accessible amount of <see cref="DepositMineralId"/> located on this hex (0 if none).</summary>
        [JsonProperty] public long DepositAmount { get; internal set; }

        /// <summary>Instance ids of the FOOTPRINT buildings that occupy THIS operational hex (War-map layer, W1) — the
        /// finer-than-region location of a strategic base (a fort / spaceport / HQ). This is the "ship icon" the war
        /// map fights over: capturing the hex captures what's on it, bombing it damages what's on it. Only buildings
        /// whose design carries a <c>GroundCombat.GroundFootprintAtb</c> land here (a solar panel doesn't); the region
        /// keeps its full <c>Region.InstallationIds</c> for the economy + fortification. Save-safe (deep-copied).
        /// Design: docs/GROUND-CITY-AND-WARMAP-DESIGN.md.</summary>
        [JsonProperty] public List<int> InstallationIds { get; internal set; } = new List<int>();

        /// <summary>The FINE city grid you zoom into (C-track) — null until this operational hex is DEVELOPED by a
        /// colony (lazy, so an undeveloped hex costs nothing). Its tiles' buildings roll up to
        /// <see cref="InstallationIds"/>. Deep-copied below. Design: docs/GROUND-CITY-AND-WARMAP-DESIGN.md.</summary>
        [JsonProperty] public CityGrid CityGrid { get; internal set; }

        public GroundHex() { }
        public GroundHex(int q, int r, RegionFeatureType terrain) { Q = q; R = r; Terrain = terrain; }
        public GroundHex(GroundHex o)
        {
            Q = o.Q; R = o.R; Terrain = o.Terrain; OwnerFactionID = o.OwnerFactionID;
            DepositMineralId = o.DepositMineralId; DepositAmount = o.DepositAmount;
            InstallationIds = o.InstallationIds != null ? new List<int>(o.InstallationIds) : new List<int>();
            CityGrid = o.CityGrid != null ? new CityGrid(o.CityGrid) : null;
        }
    }
}
