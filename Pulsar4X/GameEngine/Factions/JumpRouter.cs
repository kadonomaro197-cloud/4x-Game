using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.JumpPoints;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.8 P-3 — the MULTI-JUMP STRIKE ROUTER. The deferred piece <see cref="MilitaryReach"/> flagged: "a
    /// multi-jump route reads Unreachable (deferred router)." This IS that router. It answers, for the war brain,
    /// "which jump gate does my fleet head for FIRST to get from where it is to the enemy world's system — and how
    /// many gates away is it?"
    ///
    /// Think of the galaxy as islands (star systems) joined by bridges (jump points). A faction can only cross a
    /// bridge it has DISCOVERED (surveyed) — an undiscovered gate might as well not exist to the planner, exactly
    /// like <see cref="MilitaryReach"/>'s one-hop read. This walks that bridge network breadth-first (nearest island
    /// first) from the fleet's current system to the target's system and reports the shortest route: the first bridge
    /// to take and the total number of crossings.
    ///
    /// Deliberately least-commitment: it returns only the NEXT gate to head for, not the whole path — the resolver
    /// sails one leg per monthly cycle (mirroring the resolver's one-step-per-cycle style), then re-routes from the
    /// new system next cycle. Pure read, no warp/order surface touched, deterministic (breadth-first order over the
    /// entity lists; no RNG, no wall-clock). Defensive/no-throw — a null system or no route yields an explicit
    /// "no route" result, safe to call every cycle.
    /// </summary>
    public static class JumpRouter
    {
        /// <summary>The result of a route search: whether a route exists, how many jump crossings it takes, and the
        /// FIRST jump-gate entity (in the fleet's CURRENT system) the fleet should head toward to start the trip.</summary>
        public readonly struct JumpRoute
        {
            /// <summary>True when a discovered route to the target system exists (or it's the same system).</summary>
            public bool Found { get; init; }
            /// <summary>Jump crossings to the target system: 0 = same system, N = N gates, -1 = no route.</summary>
            public int Hops { get; init; }
            /// <summary>The gate in the fleet's CURRENT system to head for first; <see cref="Entity.InvalidEntity"/>
            /// when same-system or no route (nothing to sail to).</summary>
            public Entity FirstGate { get; init; }

            /// <summary>No discovered route to the target — keep massing / the target is unreachable.</summary>
            public static JumpRoute NoRoute => new JumpRoute { Found = false, Hops = -1, FirstGate = Entity.InvalidEntity };
            /// <summary>The target is already in the fleet's own system — a direct warp, no gate to cross.</summary>
            public static JumpRoute Here => new JumpRoute { Found = true, Hops = 0, FirstGate = Entity.InvalidEntity };
        }

        /// <summary>
        /// Breadth-first search over the DISCOVERED jump-point graph from <paramref name="fromSystem"/> to
        /// <paramref name="targetSystem"/>. Returns the shortest route (fewest crossings) as a <see cref="JumpRoute"/>:
        /// same system → 0 hops; a chain of discovered gates → the first gate + the hop count; nothing reachable →
        /// <see cref="JumpRoute.NoRoute"/>. A null system on either end is no route (defensive). Only traverses gates
        /// that are LINKED (<see cref="JumpPointDB.DestinationId"/> &gt; 0) AND the faction has DISCOVERED — the same
        /// "known route" rule <see cref="MilitaryReach"/> uses (an empty discovery set counts as known so older/test
        /// data isn't silently blackholed).
        /// </summary>
        public static JumpRoute FindRoute(EntityManager fromSystem, EntityManager targetSystem, int factionId)
        {
            if (fromSystem == null || targetSystem == null) return JumpRoute.NoRoute;
            if (ReferenceEquals(fromSystem, targetSystem)) return JumpRoute.Here;

            // Systems already reached (by ManagerID string) so the walk never loops or re-expands an island.
            var visited = new HashSet<string> { fromSystem.ManagerID };

            // The frontier remembers, for each reached-but-not-yet-expanded system, the FIRST gate taken OUT of the
            // start system — that's the leg the resolver actually emits this cycle.
            var frontier = new Queue<(EntityManager system, Entity firstGate, int dist)>();

            foreach (var (gate, farSystem) in DiscoveredNeighbors(fromSystem, factionId))
            {
                if (farSystem == null) continue;
                if (ReferenceEquals(farSystem, targetSystem))
                    return new JumpRoute { Found = true, Hops = 1, FirstGate = gate };   // one hop — the seed gate IS the route
                if (visited.Add(farSystem.ManagerID))
                    frontier.Enqueue((farSystem, gate, 1));
            }

            while (frontier.Count > 0)
            {
                var (system, firstGate, dist) = frontier.Dequeue();
                foreach (var (gate, farSystem) in DiscoveredNeighbors(system, factionId))
                {
                    if (farSystem == null) continue;
                    if (ReferenceEquals(farSystem, targetSystem))
                        return new JumpRoute { Found = true, Hops = dist + 1, FirstGate = firstGate };
                    if (visited.Add(farSystem.ManagerID))
                        frontier.Enqueue((farSystem, firstGate, dist + 1));
                }
            }

            return JumpRoute.NoRoute;
        }

        /// <summary>
        /// Convenience: just the FIRST gate the fleet should head toward to reach <paramref name="targetSystem"/>, or
        /// <see cref="Entity.InvalidEntity"/> when same-system or no discovered route. What the resolver emits its
        /// next leg toward.
        /// </summary>
        public static Entity NextGateToward(EntityManager fromSystem, EntityManager targetSystem, int factionId)
            => FindRoute(fromSystem, targetSystem, factionId).FirstGate;

        /// <summary>Yield each DISCOVERED, LINKED jump gate in <paramref name="system"/> paired with the star system
        /// its far end lands in. <see cref="JumpPointDB.DestinationId"/> names the paired gate ENTITY in the other
        /// system (JPFactory links them); its <see cref="Entity.Manager"/> is that destination system. A gate that is
        /// unlinked, undiscovered, or whose far end can't be resolved is skipped.</summary>
        private static IEnumerable<(Entity gate, EntityManager farSystem)> DiscoveredNeighbors(EntityManager system, int factionId)
        {
            foreach (var gate in system.GetAllEntitiesWithDataBlob<JumpPointDB>())
            {
                if (!gate.TryGetDataBlob<JumpPointDB>(out var jpDB)) continue;
                if (jpDB.DestinationId <= 0) continue;                 // unlinked gate — leads nowhere
                if (!KnownToFaction(jpDB, factionId)) continue;        // an undiscovered gate isn't a route to the AI
                if (!system.TryGetGlobalEntityById(jpDB.DestinationId, out var farGate)) continue;
                if (farGate == null) continue;
                var farSystem = farGate.Manager;
                if (farSystem == null) continue;                        // the paired gate isn't in a live system
                yield return (gate, farSystem);
            }
        }

        /// <summary>Whether the faction has DISCOVERED this gate — an empty discovery set (test/older data) counts as
        /// known so the route read never silently blackholes a linked gate that predates per-faction discovery. Mirrors
        /// <see cref="MilitaryReach"/>'s identical private rule (one source of truth for "a known route").</summary>
        private static bool KnownToFaction(JumpPointDB jpDB, int factionId)
            => jpDB.IsDiscovered == null || jpDB.IsDiscovered.Count == 0 || jpDB.IsDiscovered.Contains(factionId);
    }
}
