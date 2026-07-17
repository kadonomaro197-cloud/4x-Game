using Pulsar4X.Combat;
using Pulsar4X.Energy;
using Pulsar4X.Engine;
using Pulsar4X.JumpPoints;
using Pulsar4X.Movement;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.8 P-3 — the military REACH, third and last helper (MilitaryReach). The "can my fleet actually GET
    /// THERE?" perception (docs/AI-BRAIN-BUILD-TRACKER.md). <see cref="MilitaryTarget"/> names the enemy world and
    /// <see cref="MilitaryComposition"/> confirms a mass fleet is ready; this replaces the coarse near/far PROXY they
    /// use (<see cref="MilitaryTarget.ReachInSystem"/> / <see cref="MilitaryTarget.ReachDistant"/>) with a real read
    /// off the jump-point network and the fleet's fuel/range. Pure read, no warp/order surface touched.
    ///
    /// Two questions, answered separately:
    ///   • CAN I ROUTE THERE? — is the target in the SAME star system as the fleet (a direct warp — <see
    ///     cref="ReachTier.SameSystem"/>), reachable across ONE KNOWN jump connection (<see cref="ReachTier.OneJump"/>),
    ///     reachable across SEVERAL discovered jumps (<see cref="ReachTier.MultiJump"/> — the <see cref="JumpRouter"/>
    ///     walks the discovered jump graph), or with no discovered route at all (<see cref="ReachTier.Unreachable"/>)?
    ///     A "known" jump is one the faction has DISCOVERED (<see cref="JumpPointDB.IsDiscovered"/>) — an undiscovered
    ///     gate might as well not exist to the AI's planner.
    ///   • DO I HAVE THE RANGE? — does the fleet hold a warp-capable ship with enough STORED energy to spin up a warp
    ///     bubble (the exact gate <see cref="WarpMoveCommand"/> enforces: a <see cref="WarpAbilityDB"/> with a real
    ///     speed AND <see cref="EnergyGenAbilityDB.EnergyStored"/> ≥ <see cref="WarpAbilityDB.BubbleCreationCost"/>)?
    ///     A freshly-built, un-charged fleet reads NOT ready even when the target is right next door.
    ///
    /// So the reach chain closes: 3.4b DECLARES the war → MilitaryTarget SCORES the world → MilitaryComposition MASSES
    /// the fleet → this confirms the fleet can REACH it → ConquerResolver SAILS. When the target is a same-system warp
    /// the resolver sails straight at it; when it's one or more DISCOVERED jumps away the resolver sails the next leg
    /// toward it (one gate per cycle); an unreachable target keeps the resolver massing until a route/range exists.
    ///
    /// BOUNDED v1 (deliberate, labelled): the routing now covers same-system / one-jump / MULTI-jump over the
    /// DISCOVERED jump graph (a breadth-first hop COUNT via <see cref="JumpRouter"/>) — the multi-jump router that used
    /// to be deferred. It is a hop-count route, not a full A* over per-leg travel-time/fuel cost, and the fuel check is
    /// a warp-bubble sufficiency test at the current leg, not a delta-V budget over the whole route. Both IMPROVE on
    /// the binary in-system/distant proxy they replace and are the seam a costed router bolts onto. Defensive/no-throw
    /// — safe to call every monthly cycle.
    /// </summary>
    public static class MilitaryReach
    {
        // --- Reach factors (mirror MilitaryTarget's tunable weights so a scorer can consume ReachFactor directly) ---

        /// <summary>Reach factor for a target in the SAME system (a direct warp — the cheapest strike).</summary>
        public const double SameSystemReach = 1.0;
        /// <summary>Reach factor for a target one KNOWN jump away (reachable but costlier — discounted vs same-system).</summary>
        public const double OneJumpReach = 0.5;
        /// <summary>Reach factor for a target TWO OR MORE known jumps away (reachable via a discovered route, but the
        /// deepest discount — a far campaign the AI weighs against nearer prizes). Discounted further than one jump.</summary>
        public const double MultiJumpReach = 0.25;
        /// <summary>Reach factor for a target with no discovered route (only a path through an undiscovered gate, or no
        /// path at all).</summary>
        public const double UnreachableReach = 0.0;

        /// <summary>How far the target is, in ROUTING terms (not distance): the same system, one known jump, several
        /// known jumps, or beyond any discovered route.</summary>
        public enum ReachTier
        {
            /// <summary>Directly reachable — the target sits in the fleet's own star system (a single warp).</summary>
            SameSystem,
            /// <summary>Reachable across exactly ONE jump connection the faction has discovered.</summary>
            OneJump,
            /// <summary>Reachable across TWO OR MORE discovered jump connections — a multi-jump route the router found
            /// over the known jump graph. Costlier still, but a real strike the fleet can sail one leg at a time.</summary>
            MultiJump,
            /// <summary>No discovered route — the only path runs through an undiscovered gate, or no path exists — so
            /// the AI can't route to it yet.</summary>
            Unreachable,
        }

        /// <summary>The reachability read: the routing tier, the hop count, and whether the fleet has the fuel/range.</summary>
        public readonly struct ReachResult
        {
            public ReachTier Tier { get; init; }
            /// <summary>Jump hops to the target: 0 = same system, 1 = one known jump, -1 = unreachable.</summary>
            public int Hops { get; init; }
            /// <summary>True when the fleet has a warp-capable ship carrying enough stored energy to spin a warp bubble.</summary>
            public bool HasRange { get; init; }

            /// <summary>True when a route exists (same-system or one known jump) — ignores fuel.</summary>
            public bool IsReachable => Tier != ReachTier.Unreachable;
            /// <summary>True when the target is both routable AND the fleet has the range to make the trip — the sail gate.</summary>
            public bool IsReady => IsReachable && HasRange;

            /// <summary>The scoring discount for this tier — a scorer multiplies a prize's value by this (1.0 near, 0.5
            /// one jump, 0.25 several jumps, 0 unreachable), so a reachable smaller world can outweigh a distant bigger
            /// one.</summary>
            public double ReachFactor => Tier switch
            {
                ReachTier.SameSystem => SameSystemReach,
                ReachTier.OneJump => OneJumpReach,
                ReachTier.MultiJump => MultiJumpReach,
                _ => UnreachableReach,
            };

            public static ReachResult None => new ReachResult
            {
                Tier = ReachTier.Unreachable, Hops = -1, HasRange = false,
            };
        }

        /// <summary>
        /// The full read for a FLEET striking a target body: derives the fleet's home system and its fuel/range, then
        /// tiers the route. Faction discovery is taken from the fleet's owner. Returns <see cref="ReachResult.None"/>
        /// for a null/invalid fleet or target.
        /// </summary>
        public static ReachResult Assess(Entity fleet, Entity targetBody)
        {
            if (fleet == null || !fleet.IsValid || targetBody == null || !targetBody.IsValid)
                return ReachResult.None;

            var fromSystem = FleetSystem(fleet);
            var route = JumpRouter.FindRoute(fromSystem, targetBody.Manager, fleet.FactionOwnerID);
            return new ReachResult
            {
                Tier = TierFor(route.Hops),
                Hops = route.Hops,
                HasRange = FleetHasWarpRange(fleet),
            };
        }

        /// <summary>
        /// The routing half only — tier a target from a KNOWN home system (no fleet / no fuel check). Useful to a
        /// scorer that wants the reach discount for a faction asset without a specific fleet in hand.
        /// </summary>
        public static ReachResult AssessRoute(EntityManager fromSystem, Entity targetBody, int factionId)
        {
            if (targetBody == null || !targetBody.IsValid)
                return ReachResult.None;

            var route = JumpRouter.FindRoute(fromSystem, targetBody.Manager, factionId);
            return new ReachResult { Tier = TierFor(route.Hops), Hops = route.Hops, HasRange = false };
        }

        /// <summary>
        /// Tier the route between two star systems over the KNOWN jump graph, via <see cref="JumpRouter"/>: same
        /// system → <see cref="ReachTier.SameSystem"/>; one discovered jump → <see cref="ReachTier.OneJump"/>; two or
        /// more discovered jumps → <see cref="ReachTier.MultiJump"/> (the multi-jump router); no discovered route (only
        /// an undiscovered gate, or none) → <see cref="ReachTier.Unreachable"/>. A null system on either end is
        /// unreachable (defensive).
        /// </summary>
        public static ReachTier TierFromSystem(EntityManager fromSystem, EntityManager targetSystem, int factionId)
            => TierFor(JumpRouter.FindRoute(fromSystem, targetSystem, factionId).Hops);

        /// <summary>True when the fleet holds at least one ship that can actually begin a warp: a real warp drive
        /// (<see cref="WarpAbilityDB.MaxSpeed"/> &gt; 0) AND enough stored energy for the bubble — the exact gate
        /// <see cref="WarpMoveCommand"/> checks. A charged, warp-capable fleet reads ready; a drained or drive-less
        /// fleet reads not ready (keep charging / build the drive).</summary>
        public static bool FleetHasWarpRange(Entity fleet)
        {
            if (fleet == null) return false;
            foreach (var ship in FleetCombat.Ships(fleet))
            {
                if (ship == null || !ship.IsValid) continue;
                if (!ship.TryGetDataBlob<WarpAbilityDB>(out var warp) || !(warp.MaxSpeed > 0)) continue;
                if (!ship.TryGetDataBlob<EnergyGenAbilityDB>(out var power)) continue;

                string eType = warp.EnergyType;
                if (eType == null) continue;
                double stored = power.EnergyStored.TryGetValue(eType, out var es) ? es : 0;
                if (stored >= warp.BubbleCreationCost)
                    return true;
            }
            return false;
        }

        /// <summary>The star system a fleet is in — its own manager, or (defensively) its first ship's manager.</summary>
        private static EntityManager FleetSystem(Entity fleet)
        {
            if (fleet.Manager != null) return fleet.Manager;
            foreach (var ship in FleetCombat.Ships(fleet))
                if (ship != null && ship.Manager != null) return ship.Manager;
            return null;
        }

        /// <summary>Map a router hop count to a reach tier: 0 = same system, 1 = one jump, 2+ = multi-jump, and a
        /// negative "no route" (-1) = unreachable.</summary>
        private static ReachTier TierFor(int hops)
        {
            if (hops < 0) return ReachTier.Unreachable;
            if (hops == 0) return ReachTier.SameSystem;
            if (hops == 1) return ReachTier.OneJump;
            return ReachTier.MultiJump;
        }
    }
}
