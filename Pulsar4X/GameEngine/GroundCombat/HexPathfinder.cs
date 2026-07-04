using System;
using System.Collections.Generic;
using Pulsar4X.Galaxy;
using Pulsar4X.Colonies;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// The ground layer's fine-grained NAVIGATOR (H2) — A* pathfinding over a region's hex patch so a formation can
    /// march London→Paris hex-by-hex and terrain decides the route. The ground echo of the fleet navigator, one scale
    /// DOWN: where a fleet crosses a system, a unit crosses a region's <see cref="GroundHex"/> disk.
    ///
    /// <para>Two pure, unit-testable pieces:</para>
    /// <list type="bullet">
    /// <item><b>The cost model</b> — <see cref="HexMoveMult"/>: how much terrain slows a march across ONE hex. Moderate
    /// tiers (the developer's call, 2026-07-04): open ×1, cover (forest/wetland) ×1.5, rough (mountains/volcanic) ×2.5 —
    /// a real reason to route around mountains without making them walls. Mirrors the "moderate edge" philosophy the
    /// combat terrain dials (<see cref="GroundTerrain"/>) already use.</item>
    /// <item><b>The pathfinder</b> — <see cref="FindPath"/>: classic A* over the patch, edge cost = the terrain
    /// multiplier of the hex being ENTERED, heuristic = hex distance (admissible: min step cost is ×1). Returns the
    /// ordered steps (excluding the start, including the destination), or an empty list if unreachable.</item>
    /// </list>
    ///
    /// The pathfinder works in pure TERRAIN-COST units; the processor converts a step to game-seconds with
    /// <see cref="PerHexBaseSeconds"/> (derived from the region's existing <see cref="Region.CrossingTimeSeconds"/>
    /// distance datum — not a new magic number). Because the base is a constant within a region, the terrain-cost-optimal
    /// path is also the time-optimal one.
    ///
    /// Design: docs/HEX-GROUND-AND-ORDERS-DESIGN.md (H2). Reuses <see cref="HexCoordinate"/> for neighbour/distance math.
    /// </summary>
    public static class HexPathfinder
    {
        // --- terrain movement-cost tiers (the developer's "Moderate" call, 2026-07-04) ---
        public const double Move_Open  = 1.0;    // plains / desert / barren / coast / tundra — easy going
        public const double Move_Cover = 1.5;    // forest / jungle / wetland — slower
        public const double Move_Rough = 2.5;    // mountains / highlands / volcanic — a real barrier (and water, v1)

        /// <summary>How much this hex's terrain slows a march across it (the cost to ENTER it). Uses the same
        /// open/cover/rough sorting the combat terrain (<see cref="GroundTerrain.Classify"/>) uses, but for movement.
        /// (v1 note: Ocean/Ice are costed as rough — passable but slow — a transparent placeholder; true
        /// impassable-water / amphibious gating is a documented follow-on, see the design doc.)</summary>
        public static double HexMoveMult(RegionFeatureType terrain)
        {
            switch (terrain)
            {
                case RegionFeatureType.Mountains:
                case RegionFeatureType.Highlands:
                case RegionFeatureType.Volcanic:
                case RegionFeatureType.Ocean:      // v1: water is slow-but-passable (placeholder — see design doc)
                case RegionFeatureType.Ice:
                    return Move_Rough;
                case RegionFeatureType.Forest:
                case RegionFeatureType.Jungle:
                case RegionFeatureType.Wetland:
                    return Move_Cover;
                default:   // Plains / Desert / Barren / Coast / Tundra / GasLayers / Unknown
                    return Move_Open;
            }
        }

        /// <summary>
        /// A* from (<paramref name="startQ"/>,<paramref name="startR"/>) to (<paramref name="destQ"/>,<paramref name="destR"/>)
        /// over the region's hex disk. Returns the ordered list of hexes to step onto — EXCLUDING the start, INCLUDING the
        /// destination. Empty if the patch is empty, the destination isn't in the patch, start == dest, or no route exists.
        /// Deterministic (no RNG): ties break by insertion order via the priority queue.
        /// </summary>
        public static List<GroundHex> FindPath(List<GroundHex> hexes, int startQ, int startR, int destQ, int destR)
        {
            var result = new List<GroundHex>();
            if (hexes == null || hexes.Count == 0) return result;
            if (startQ == destQ && startR == destR) return result;   // already there

            // Index the patch by coordinate for O(1) neighbour lookup.
            var byCoord = new Dictionary<(int, int), GroundHex>(hexes.Count);
            foreach (var h in hexes) byCoord[(h.Q, h.R)] = h;

            var startKey = (startQ, startR);
            var destKey = (destQ, destR);
            if (!byCoord.ContainsKey(destKey)) return result;   // can't march to a hex outside the patch

            var dest = new HexCoordinate(destQ, destR);
            var gScore = new Dictionary<(int, int), double> { [startKey] = 0.0 };
            var cameFrom = new Dictionary<(int, int), (int, int)>();
            var open = new PriorityQueue<(int, int), double>();
            open.Enqueue(startKey, new HexCoordinate(startQ, startR).DistanceTo(dest) * Move_Open);

            while (open.Count > 0)
            {
                var current = open.Dequeue();
                if (current == destKey)
                    return Reconstruct(cameFrom, byCoord, startKey, destKey);

                double baseG = gScore[current];
                foreach (var nb in new HexCoordinate(current.Item1, current.Item2).GetNeighbors())
                {
                    var nkey = (nb.Q, nb.R);
                    if (!byCoord.TryGetValue(nkey, out var nhex)) continue;   // off the patch
                    double tentative = baseG + HexMoveMult(nhex.Terrain);     // cost to ENTER the neighbour
                    if (gScore.TryGetValue(nkey, out var known) && tentative >= known) continue;
                    gScore[nkey] = tentative;
                    cameFrom[nkey] = current;
                    open.Enqueue(nkey, tentative + nb.DistanceTo(dest) * Move_Open);
                }
            }
            return result;   // unreachable
        }

        private static List<GroundHex> Reconstruct(Dictionary<(int, int), (int, int)> cameFrom,
            Dictionary<(int, int), GroundHex> byCoord, (int, int) startKey, (int, int) destKey)
        {
            var steps = new List<GroundHex>();
            var cur = destKey;
            while (cur != startKey)
            {
                steps.Add(byCoord[cur]);
                cur = cameFrom[cur];
            }
            steps.Reverse();   // start→dest order
            return steps;
        }

        /// <summary>The disk radius of a hex patch = the largest hex distance from the patch centre (0,0). Pure.</summary>
        public static int PatchRadius(List<GroundHex> hexes)
        {
            if (hexes == null || hexes.Count == 0) return 0;
            var origin = new HexCoordinate(0, 0);
            int max = 0;
            foreach (var h in hexes)
            {
                int d = new HexCoordinate(h.Q, h.R).DistanceTo(origin);
                if (d > max) max = d;
            }
            return max;
        }

        /// <summary>
        /// Game-seconds to cross ONE hex of open ground in this region — derived from the region's end-to-end
        /// <see cref="Region.CrossingTimeSeconds"/> distance datum (NOT a new hardcoded number): the patch is
        /// 2×<see cref="PatchRadius"/> hexes across, so one open hex ≈ crossing-time / diameter. A step's actual time
        /// is this × <see cref="HexMoveMult"/> of the hex entered. Falls back to the whole crossing time if the region
        /// has no hex patch yet.
        /// </summary>
        public static double PerHexBaseSeconds(Region region)
        {
            if (region == null) return 0.0;
            int r = PatchRadius(region.Hexes);
            int diameter = Math.Max(1, 2 * r);
            return region.CrossingTimeSeconds / diameter;
        }
    }
}
