using Pulsar4X.Combat;
using Pulsar4X.Engine;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.8 P-3 — the military REACH, second helper (MilitaryComposition). The "do I have a strike group ready
    /// to sail?" perception (docs/AI-BRAIN-BUILD-TRACKER.md). <see cref="MilitaryTarget"/> names the enemy world; this
    /// decides whether the faction has MASSED enough armed hulls to go on the offensive — the developer's "move as a
    /// mass fleet" (don't trickle ships one at a time). Pure read, no warp/order surface.
    ///
    /// So the reach chain composes: 3.4b DECLARES the war → MilitaryTarget SCORES the target world → this confirms a
    /// mass fleet is READY → ConquerResolver SAILS it. Until the threshold is met, the resolver keeps massing.
    /// </summary>
    public static class MilitaryComposition
    {
        /// <summary>How many armed ships (a real <see cref="ShipCombatValueDB"/> with Firepower &gt; 0) make a fleet a
        /// "strike group" ready to sail offensively — the developer's "mass fleet" gate. v1 flat; a real value scales
        /// with the target's defences (a later slice). FLAGGED tunable.</summary>
        public const int StrikeGroupMinWarships = 3;

        /// <summary>Count the armed ships in a fleet (a built ship rates Firepower &gt; 0 iff it mounts a weapon).</summary>
        public static int WarshipCount(Entity fleet)
        {
            if (fleet == null) return 0;
            int n = 0;
            foreach (var ship in FleetCombat.Ships(fleet))
                if (ship != null && ship.IsValid
                    && ship.TryGetDataBlob<ShipCombatValueDB>(out var cv) && cv.Firepower > 0)
                    n++;
            return n;
        }

        /// <summary>The faction's owned fleet with the MOST armed ships, IF it meets the strike threshold; else
        /// <see cref="Entity.InvalidEntity"/> (keep massing — no group is big enough yet). Reads the fleets the
        /// <see cref="FactionState"/> snapshot already gathered.</summary>
        public static Entity ReadyStrikeFleet(FactionState state)
        {
            if (state == null) return Entity.InvalidEntity;
            Entity best = Entity.InvalidEntity;
            int bestCount = StrikeGroupMinWarships - 1;   // strictly-greater: first fleet at/above threshold wins, then the max
            foreach (var fleet in state.OwnedFleets())
            {
                int c = WarshipCount(fleet);
                if (c > bestCount) { bestCount = c; best = fleet; }
            }
            return best;
        }
    }
}
