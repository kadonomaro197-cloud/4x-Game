using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Colonies;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// The CITY sub-grid builder (C1) — places buildings on the FINE tiles of a developed operational hex and keeps
    /// the ROLL-UP INVARIANT: the set of building ids on a hex's <see cref="CityGrid"/> tiles stays ==
    /// <see cref="GroundHex.InstallationIds"/> (the operational footprint W-track captures/bombs). So the two zooms are
    /// the same physical buildings — placement here and capture/bombard there can never disagree.
    /// Design: docs/GROUND-CITY-AND-WARMAP-DESIGN.md. Defensive throughout.
    /// </summary>
    public static class CityBuilder
    {
        /// <summary>Place a building on a fine city tile (1:1 — fails if the tile is already occupied). Generates the
        /// hex's city grid on demand, sets the tile, and ADDS the id to the operational hex's roll-up
        /// (<see cref="GroundHex.InstallationIds"/>). Returns false if the hex/tile can't be resolved or the tile is taken.</summary>
        public static bool PlaceBuildingOnTile(Entity body, int regionIndex, int hexQ, int hexR, int tileQ, int tileR, int buildingInstanceId)
        {
            var hex = CityGridFactory.ResolveHex(body, regionIndex, hexQ, hexR);
            if (hex == null) return false;
            var grid = CityGridFactory.EnsureCityForHex(body, regionIndex, hexQ, hexR);
            var tile = grid?.TileAt(tileQ, tileR);
            if (tile == null || tile.BuildingInstanceId != -1) return false;

            tile.BuildingInstanceId = buildingInstanceId;
            if (!hex.InstallationIds.Contains(buildingInstanceId)) hex.InstallationIds.Add(buildingInstanceId);   // roll-up
            return true;
        }

        /// <summary>Remove the building on a fine tile — clears the tile AND drops its id from the operational hex's
        /// roll-up. Returns false if the hex/tile can't be resolved or the tile is empty.</summary>
        public static bool RemoveBuildingFromTile(Entity body, int regionIndex, int hexQ, int hexR, int tileQ, int tileR)
        {
            var hex = CityGridFactory.ResolveHex(body, regionIndex, hexQ, hexR);
            var tile = hex?.CityGrid?.TileAt(tileQ, tileR);
            if (tile == null || tile.BuildingInstanceId == -1) return false;

            int id = tile.BuildingInstanceId;
            tile.BuildingInstanceId = -1;
            hex.InstallationIds?.Remove(id);   // roll-up
            return true;
        }

        /// <summary>Clear a specific building from a hex's city grid (used by the grave rung — a bombed operational hex
        /// must also empty the fine tile it sat on, so the roll-up stays honest). Returns true if a tile was cleared.</summary>
        public static bool ClearBuildingFromCity(GroundHex hex, int buildingInstanceId)
        {
            if (hex?.CityGrid?.Tiles == null) return false;
            foreach (var t in hex.CityGrid.Tiles)
                if (t.BuildingInstanceId == buildingInstanceId) { t.BuildingInstanceId = -1; return true; }
            return false;
        }

        /// <summary>DEVELOP a colony's capital hex: generate its city grid and lay the operational hex's existing
        /// footprint buildings (already in <see cref="GroundHex.InstallationIds"/> from W-track's locate) onto empty
        /// fine tiles (first-fit) — the migration from the coarse "it's here" to the fine "it's on THIS tile". Returns
        /// how many buildings were newly placed on a tile. Defensive: no colony / no region layer / no hexes → 0.</summary>
        public static int DevelopColonyHex(Entity colony)
        {
            if (colony == null || !colony.TryGetDataBlob<ColonyInfoDB>(out var ci)) return 0;
            var body = ci.PlanetEntity;
            if (body == null || !body.IsValid) return 0;

            const int capitalRegion = 0, centreQ = 0, centreR = 0;
            var hex = CityGridFactory.ResolveHex(body, capitalRegion, centreQ, centreR);
            if (hex == null) return 0;
            var grid = CityGridFactory.EnsureCityForHex(body, capitalRegion, centreQ, centreR);
            if (grid?.Tiles == null || hex.InstallationIds == null) return 0;

            // Which ids are already on a tile (idempotency)?
            var onTile = new System.Collections.Generic.HashSet<int>();
            foreach (var t in grid.Tiles) if (t.BuildingInstanceId != -1) onTile.Add(t.BuildingInstanceId);

            int placed = 0;
            foreach (var id in hex.InstallationIds)
            {
                if (onTile.Contains(id)) continue;
                var empty = NextEmptyTile(grid);
                if (empty == null) break;   // city is full
                empty.BuildingInstanceId = id;
                onTile.Add(id);
                placed++;
            }
            return placed;
        }

        private static CityTile NextEmptyTile(CityGrid grid)
        {
            foreach (var t in grid.Tiles) if (t.BuildingInstanceId == -1) return t;
            return null;
        }
    }
}
