using System;
using System.Collections.Generic;
using Pulsar4X.Galaxy;

namespace Pulsar4X.GroundCombat
{
    /// <summary>One step of a hex march: the hex to move INTO (which region + axial q/r) and how many game-seconds
    /// that step takes (its terrain-weighted cost). A path is a list of these, from the first move to the goal.</summary>
    public readonly struct HexStep
    {
        public readonly int RegionIndex;
        public readonly int Q;
        public readonly int R;
        public readonly double Seconds;
        public HexStep(int regionIndex, int q, int r, double seconds)
        { RegionIndex = regionIndex; Q = q; R = r; Seconds = seconds; }
        public override string ToString() => $"R{RegionIndex}({Q},{R}) {Seconds:0}s";
    }

    /// <summary>
    /// A* pathfinding over a planet's HEXES (Planet → Region → Hex). The graph is the per-region hex patches
    /// (<see cref="Region.Hexes"/>, built by <see cref="PlanetHexFactory"/>) stitched together at BORDER GATES: a
    /// unit at a region's east-edge gate steps into the next region's west-edge gate, wrapping seam-free around the
    /// 4-region ring (the developer's "global"/London→Paris continuity, realised on the merged per-region-patch
    /// foundation — the locked H1 model, not a re-tiled sphere).
    ///
    /// Step cost = the destination hex's base step-time × its <see cref="HexMovement.TerrainCost"/> for the moving
    /// unit's <see cref="MovementDomain"/> — so mountains slow a tank, open water stops it, and air flies straight.
    /// Base step-time is DERIVED from the coarse layer (<see cref="Region.CrossingTimeSeconds"/> ÷ the patch's hex
    /// diameter), so a full march across the fine hexes totals roughly the coarse region's crossing time — the fine
    /// map stays consistent with the strategic one. No new distance constants.
    ///
    /// Pure (operates on a <see cref="PlanetRegionsDB"/>, no entity) → unit-testable with hand-built terrain.
    /// Deterministic. Design: docs/HEX-GROUND-AND-ORDERS-DESIGN.md (H2).
    /// </summary>
    public static class HexPathfinder
    {
        // The 6 axial hex directions (matches Colonies.HexCoordinate's neighbour order).
        private static readonly (int dq, int dr)[] Directions =
        {
            (1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1)
        };

        /// <summary>
        /// Find the cheapest hex path for a unit of <paramref name="domain"/> from a start hex to a goal hex,
        /// crossing region borders as needed. Returns the steps from the first move to the goal (the start hex is
        /// NOT included); an EMPTY list means already there, an invalid start/goal, or no route (e.g. a land unit
        /// asked to reach an ocean hex with no land bridge).
        /// </summary>
        public static List<HexStep> FindPath(PlanetRegionsDB regionsDB,
            int fromRegion, int fromQ, int fromR,
            int toRegion, int toQ, int toR,
            MovementDomain domain)
        {
            var result = new List<HexStep>();
            if (regionsDB?.Regions == null) return result;
            var regions = regionsDB.Regions;
            int n = regions.Count;
            if (fromRegion < 0 || fromRegion >= n || toRegion < 0 || toRegion >= n) return result;
            if (fromRegion == toRegion && fromQ == toQ && fromR == toR) return result; // already there

            // ── Precompute per region: a hex lookup, the base step-time, and the east/west border gates. ──
            var hexTerrain = new Dictionary<(int, int), RegionFeatureType>[n];
            var baseStep = new double[n];
            var eastGate = new (int q, int r)?[n];
            var westGate = new (int q, int r)?[n];
            for (int i = 0; i < n; i++)
            {
                var reg = regions[i];
                var set = new Dictionary<(int, int), RegionFeatureType>();
                int maxRadius = 0;
                (int q, int r) east = default; bool haveEast = false;
                (int q, int r) west = default; bool haveWest = false;
                if (reg.Hexes != null)
                {
                    foreach (var h in reg.Hexes)
                    {
                        set[(h.Q, h.R)] = h.Terrain;
                        int d = (Math.Abs(h.Q) + Math.Abs(h.R) + Math.Abs(h.Q + h.R)) / 2; // hex distance from patch centre
                        if (d > maxRadius) maxRadius = d;
                        // East gate = the hex furthest +Q (the east edge), centre-most on ties (smallest |R|).
                        if (!haveEast || h.Q > east.q || (h.Q == east.q && Math.Abs(h.R) < Math.Abs(east.r)))
                        { east = (h.Q, h.R); haveEast = true; }
                        // West gate = the hex furthest -Q (the west edge), centre-most on ties.
                        if (!haveWest || h.Q < west.q || (h.Q == west.q && Math.Abs(h.R) < Math.Abs(west.r)))
                        { west = (h.Q, h.R); haveWest = true; }
                    }
                }
                hexTerrain[i] = set;
                // Diameter ≈ 2·radius steps to cross the patch → each step ≈ CrossingTime / (2·radius).
                baseStep[i] = (maxRadius > 0 && reg.CrossingTimeSeconds > 0)
                    ? reg.CrossingTimeSeconds / (2.0 * maxRadius)
                    : Math.Max(reg.CrossingTimeSeconds, 0.0);
                eastGate[i] = haveEast ? east : ((int, int)?)null;
                westGate[i] = haveWest ? west : ((int, int)?)null;
            }

            var start = (region: fromRegion, q: fromQ, r: fromR);
            var goal = (region: toRegion, q: toQ, r: toR);
            if (!hexTerrain[start.region].ContainsKey((start.q, start.r))) return result; // start isn't a real hex
            if (!hexTerrain[goal.region].ContainsKey((goal.q, goal.r))) return result;    // goal isn't a real hex

            // ── A* (uniform-cost; zero heuristic keeps it simple and always correct for the ≤~1876-hex graph). ──
            var gScore = new Dictionary<(int, int, int), double> { [(start.region, start.q, start.r)] = 0.0 };
            var cameFrom = new Dictionary<(int, int, int), (int, int, int)>();
            var stepCost = new Dictionary<(int, int, int), double>();
            var closed = new HashSet<(int, int, int)>();
            var open = new PriorityQueue<(int, int, int), double>();
            open.Enqueue((start.region, start.q, start.r), 0.0);

            var goalKey = (goal.region, goal.q, goal.r);
            while (open.Count > 0)
            {
                var cur = open.Dequeue();
                if (cur.Equals(goalKey)) break;
                if (!closed.Add(cur)) continue; // stale queue entry (a cheaper route already expanded this node)
                double gCur = gScore[cur];

                foreach (var (nb, sec) in Neighbours(cur, regions, hexTerrain, baseStep, eastGate, westGate, domain, n))
                {
                    double tentative = gCur + sec;
                    if (!gScore.TryGetValue(nb, out double old) || tentative < old)
                    {
                        gScore[nb] = tentative;
                        cameFrom[nb] = cur;
                        stepCost[nb] = sec;
                        open.Enqueue(nb, tentative);
                    }
                }
            }

            if (!gScore.ContainsKey(goalKey)) return result; // unreachable

            // Reconstruct start→goal, then emit steps in travel order (start excluded).
            var reverse = new List<(int, int, int)>();
            var node = goalKey;
            var startKey = (start.region, start.q, start.r);
            while (!node.Equals(startKey))
            {
                reverse.Add(node);
                if (!cameFrom.TryGetValue(node, out node)) return new List<HexStep>(); // broken chain (shouldn't happen)
            }
            for (int i = reverse.Count - 1; i >= 0; i--)
            {
                var p = reverse[i];
                double sec = stepCost.TryGetValue(p, out double sc) ? sc : 0.0;
                result.Add(new HexStep(p.Item1, p.Item2, p.Item3, sec));
            }
            return result;
        }

        /// <summary>The traversable neighbours of a hex node: its 6 same-region hexes that exist and are passable,
        /// plus — if it's a border gate — the bridge into the adjacent region's opposite gate.</summary>
        private static IEnumerable<((int, int, int) node, double sec)> Neighbours(
            (int region, int q, int r) cur, List<Region> regions,
            Dictionary<(int, int), RegionFeatureType>[] hexTerrain, double[] baseStep,
            (int q, int r)?[] eastGate, (int q, int r)?[] westGate, MovementDomain domain, int n)
        {
            int reg = cur.region;

            // Intra-region: the 6 axial neighbours that actually exist in this patch.
            foreach (var (dq, dr) in Directions)
            {
                var key = (cur.q + dq, cur.r + dr);
                if (hexTerrain[reg].TryGetValue(key, out var terr))
                {
                    double c = baseStep[reg] * HexMovement.TerrainCost(domain, terr);
                    if (!double.IsPositiveInfinity(c))
                        yield return ((reg, key.Item1, key.Item2), c);
                }
            }

            // Inter-region: cross at the patch edge. Neighbors[0] = west, Neighbors[1] = east (PlanetRegionsFactory).
            // A region must never border ITSELF (a self-bridge would wormhole a unit east-gate→its-own-west-gate),
            // so each bridge is guarded with `!= reg` — an isolated region uses a -1 (no-neighbour) entry.
            var neighbors = regions[reg].Neighbors;
            if (neighbors == null) yield break;

            // East gate → the east neighbour's WEST gate.
            if (neighbors.Count >= 2 && eastGate[reg].HasValue
                && cur.q == eastGate[reg].Value.q && cur.r == eastGate[reg].Value.r)
            {
                int east = neighbors[1];
                if (east >= 0 && east < n && east != reg && westGate[east].HasValue)
                {
                    var g = westGate[east].Value;
                    if (hexTerrain[east].TryGetValue((g.q, g.r), out var terr))
                    {
                        double c = baseStep[east] * HexMovement.TerrainCost(domain, terr);
                        if (!double.IsPositiveInfinity(c))
                            yield return ((east, g.q, g.r), c);
                    }
                }
            }

            // West gate → the west neighbour's EAST gate.
            if (neighbors.Count >= 1 && westGate[reg].HasValue
                && cur.q == westGate[reg].Value.q && cur.r == westGate[reg].Value.r)
            {
                int west = neighbors[0];
                if (west >= 0 && west < n && west != reg && eastGate[west].HasValue)
                {
                    var g = eastGate[west].Value;
                    if (hexTerrain[west].TryGetValue((g.q, g.r), out var terr))
                    {
                        double c = baseStep[west] * HexMovement.TerrainCost(domain, terr);
                        if (!double.IsPositiveInfinity(c))
                            yield return ((west, g.q, g.r), c);
                    }
                }
            }
        }
    }
}
