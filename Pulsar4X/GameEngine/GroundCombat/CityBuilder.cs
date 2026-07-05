using System.Collections.Generic;
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

        // ── GLOBAL grid (G4) — the same place/remove + roll-up, addressed on the cylinder by global (Q,R) ─────────────

        /// <summary>Place a building on the GLOBAL hex at (<paramref name="gQ"/>,<paramref name="gR"/>), anchored at fine
        /// tile (<paramref name="tileQ"/>,<paramref name="tileR"/>) — the cylinder-addressed twin of
        /// <see cref="PlaceBuildingOnTile"/>. <b>MULTI-TILE (C-track):</b> the building occupies its design's
        /// <see cref="GroundFootprintAtb.TileFootprint"/> many CONTIGUOUS empty tiles (a spaceport spans more than a
        /// factory), gathered outward from the anchor; a single id is added to the roll-up
        /// (<see cref="GroundHex.InstallationIds"/>) once. Returns false if the hex/anchor can't be resolved, the anchor
        /// is taken, or there isn't enough contiguous empty room for the whole footprint (an all-or-nothing place).</summary>
        public static bool PlaceBuildingOnGlobalTile(Entity body, int gQ, int gR, int tileQ, int tileR, int buildingInstanceId)
        {
            var hex = CityGridFactory.ResolveGlobalHex(body, gQ, gR);
            if (hex == null) return false;
            var grid = CityGridFactory.EnsureCityForGlobalHex(body, gQ, gR);
            var anchor = grid?.TileAt(tileQ, tileR);
            if (anchor == null || anchor.BuildingInstanceId != -1) return false;

            int need = GroundBuildings.FootprintTilesFor(body, buildingInstanceId);   // ≥ 1
            var footprint = GatherContiguousEmptyTiles(grid, anchor, need);
            if (footprint == null) return false;   // not enough contiguous empty room for the whole footprint

            foreach (var t in footprint) t.BuildingInstanceId = buildingInstanceId;
            if (!hex.InstallationIds.Contains(buildingInstanceId)) hex.InstallationIds.Add(buildingInstanceId);   // roll-up (once)
            return true;
        }

        /// <summary>Remove the building occupying the GLOBAL-hex fine tile (<paramref name="tileQ"/>,<paramref name="tileR"/>)
        /// — clears ALL the tiles that building sits on (its whole multi-tile footprint) AND drops its id from the
        /// operational hex's roll-up. Returns false if the hex/tile can't be resolved or the tile is empty.</summary>
        public static bool RemoveBuildingFromGlobalTile(Entity body, int gQ, int gR, int tileQ, int tileR)
        {
            var hex = CityGridFactory.ResolveGlobalHex(body, gQ, gR);
            var tile = hex?.CityGrid?.TileAt(tileQ, tileR);
            if (tile == null || tile.BuildingInstanceId == -1) return false;

            int id = tile.BuildingInstanceId;
            ClearBuildingFromCity(hex, id);    // clears EVERY tile this building occupies
            hex.InstallationIds?.Remove(id);   // roll-up
            return true;
        }

        /// <summary>Clear a specific building from a hex's city grid — empties EVERY fine tile it occupies (its whole
        /// multi-tile footprint), used by the grave rung (a bombed operational hex must empty all its tiles so the
        /// roll-up stays honest). Returns true if any tile was cleared.</summary>
        public static bool ClearBuildingFromCity(GroundHex hex, int buildingInstanceId)
        {
            if (hex?.CityGrid?.Tiles == null) return false;
            bool cleared = false;
            foreach (var t in hex.CityGrid.Tiles)
                if (t.BuildingInstanceId == buildingInstanceId) { t.BuildingInstanceId = -1; cleared = true; }
            return cleared;
        }

        /// <summary>Gather <paramref name="need"/> CONTIGUOUS empty tiles starting at <paramref name="anchor"/> (BFS over
        /// the 6 axial neighbours) — the footprint blob for a multi-tile building. Returns exactly <paramref name="need"/>
        /// tiles, or null if the connected empty region around the anchor is too small. The anchor must already be empty.</summary>
        private static List<CityTile> GatherContiguousEmptyTiles(CityGrid grid, CityTile anchor, int need)
        {
            if (grid == null || anchor == null || need < 1) return null;
            var result = new List<CityTile>();
            var seen = new HashSet<(int, int)> { (anchor.Q, anchor.R) };
            var queue = new Queue<CityTile>();
            queue.Enqueue(anchor);
            int[][] deltas = { new[]{1,0}, new[]{-1,0}, new[]{0,1}, new[]{0,-1}, new[]{1,-1}, new[]{-1,1} };   // axial neighbours
            while (queue.Count > 0 && result.Count < need)
            {
                var t = queue.Dequeue();
                result.Add(t);
                foreach (var d in deltas)
                {
                    var n = grid.TileAt(t.Q + d[0], t.R + d[1]);
                    if (n != null && n.BuildingInstanceId == -1 && seen.Add((n.Q, n.R))) queue.Enqueue(n);
                }
            }
            return result.Count >= need ? result.GetRange(0, need) : null;
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

        /// <summary>Global-grid twin of <see cref="DevelopColonyHex"/> (C-track) — develop the GLOBAL operational hex at
        /// (<paramref name="gQ"/>,<paramref name="gR"/>): ensure its city grid and lay the hex's footprint buildings
        /// (already in <see cref="GroundHex.InstallationIds"/> via <c>GroundBuildings.LocateFootprintsOnGlobalHexes</c>)
        /// onto empty fine tiles (first-fit). Returns how many were newly placed. Idempotent. Defensive.</summary>
        public static int DevelopGlobalHex(Entity body, int gQ, int gR)
        {
            var hex = CityGridFactory.ResolveGlobalHex(body, gQ, gR);
            if (hex == null || hex.InstallationIds == null) return 0;
            var grid = CityGridFactory.EnsureCityForGlobalHex(body, gQ, gR);
            if (grid?.Tiles == null) return 0;

            var onTile = new HashSet<int>();
            foreach (var t in grid.Tiles) if (t.BuildingInstanceId != -1) onTile.Add(t.BuildingInstanceId);

            int placed = 0;
            foreach (var id in new List<int>(hex.InstallationIds))   // copy — PlaceBuildingOnGlobalTile may touch InstallationIds
            {
                if (onTile.Contains(id)) continue;
                var empty = NextEmptyTile(grid);
                if (empty == null) break;   // city is full
                if (!PlaceBuildingOnGlobalTile(body, gQ, gR, empty.Q, empty.R, id)) break;   // footprint-aware; no room → stop
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
