using System.Collections.Generic;
using Pulsar4X.Engine;

namespace Pulsar4X.Combat
{
    /// <summary>How an auto-resolved battle ended.</summary>
    public enum BattleOutcome
    {
        SideAVictory,
        SideBVictory,
        MutualDestruction,
        Stalemate
    }

    /// <summary>Tuning knobs for a single auto-resolved battle.</summary>
    public class AutoResolveConfig
    {
        /// <summary>Game-seconds of fire each salvo round represents. Bigger = coarser, faster-resolving rounds.</summary>
        public double RoundSeconds { get; set; } = 5.0;

        /// <summary>Backstop so a near-frozen fight (tiny firepower vs huge toughness) can't loop forever.
        /// Hitting it ends the battle as a <see cref="BattleOutcome.Stalemate"/>.</summary>
        public int MaxRounds { get; set; } = 2000;
    }

    /// <summary>The outcome of an auto-resolved battle. Reports the casualties; it does NOT destroy them —
    /// the caller (the battle trigger) destroys the entities in <see cref="DestroyedA"/>/<see cref="DestroyedB"/>.</summary>
    public class AutoResolveResult
    {
        public BattleOutcome Outcome { get; internal set; }
        public int RoundsElapsed { get; internal set; }
        public double InitialStrengthA { get; internal set; }
        public double InitialStrengthB { get; internal set; }
        public List<Entity> DestroyedA { get; } = new List<Entity>();
        public List<Entity> DestroyedB { get; } = new List<Entity>();
        public int SurvivorsA { get; internal set; }
        public int SurvivorsB { get; internal set; }
    }

    /// <summary>
    /// The salvo-exchange resolver — the heart of the auto-resolve combat engine (docs/COMBAT-DESIGN.md).
    ///
    /// Each round both sides pour their total <see cref="ShipCombatValueDB.Firepower"/> x round-time (joules)
    /// into the other side's damage pool; the pool removes WHOLE ships (combatants before utility hulls), with
    /// leftover damage carrying to the next round so a weaker fleet still grinds kills over time. Repeats until
    /// one side is gone, both are gone, or a frozen fight hits the round cap.
    ///
    /// This is PURE math + reporting: no per-pixel damage sim, no entity destruction, no RNG (v1 is
    /// deterministic — variance is a later knob). The caller destroys the reported casualties. Cost is
    /// O(rounds x ships), microseconds for normal fleets — that's what lets thousands of battles resolve.
    /// </summary>
    public static class AutoResolve
    {
        private class Unit
        {
            public readonly Entity Ship;
            public readonly double Firepower;
            public readonly double Toughness;
            public readonly double RoleWeight;

            public Unit(Entity ship, ShipCombatValueDB cv)
            {
                Ship = ship;
                Firepower = cv.Firepower;
                Toughness = cv.Toughness;
                RoleWeight = cv.RoleWeight;
            }
        }

        /// <summary>
        /// Resolve a battle between two sides (each a flat list of ship entities). Returns the outcome and the
        /// casualty lists. Does not mutate the input lists and does not destroy entities.
        /// </summary>
        public static AutoResolveResult Resolve(IList<Entity> sideA, IList<Entity> sideB, AutoResolveConfig config = null)
        {
            config ??= new AutoResolveConfig();

            var a = ToUnits(sideA);
            var b = ToUnits(sideB);

            // Combatants absorb casualties before utility/transport hulls (highest RoleWeight first).
            a.Sort((x, y) => y.RoleWeight.CompareTo(x.RoleWeight));
            b.Sort((x, y) => y.RoleWeight.CompareTo(x.RoleWeight));

            var result = new AutoResolveResult
            {
                InitialStrengthA = TotalFirepower(a),
                InitialStrengthB = TotalFirepower(b),
            };

            double poolA = 0, poolB = 0;
            int round = 0;
            while (a.Count > 0 && b.Count > 0 && round < config.MaxRounds)
            {
                double strA = TotalFirepower(a);
                double strB = TotalFirepower(b);

                // Neither side can hurt the other — frozen fight, stop now (Stalemate).
                if (strA <= 0 && strB <= 0)
                    break;

                // Each side pours strength x round-time (joules) into the other's damage pool.
                poolB += strA * config.RoundSeconds;
                poolA += strB * config.RoundSeconds;

                ApplyCasualties(b, ref poolB, result.DestroyedB);
                ApplyCasualties(a, ref poolA, result.DestroyedA);
                round++;
            }

            result.RoundsElapsed = round;
            result.SurvivorsA = a.Count;
            result.SurvivorsB = b.Count;
            result.Outcome = DetermineOutcome(a.Count, b.Count);
            return result;
        }

        private static List<Unit> ToUnits(IList<Entity> side)
        {
            var list = new List<Unit>(side.Count);
            foreach (var e in side)
            {
                if (e == null || !e.IsValid) continue;
                var cv = e.TryGetDataBlob<ShipCombatValueDB>(out var found) ? found : ShipCombatValueDB.Calculate(e);
                list.Add(new Unit(e, cv));
            }
            return list;
        }

        private static double TotalFirepower(List<Unit> units)
        {
            double sum = 0;
            foreach (var u in units) sum += u.Firepower;
            return sum;
        }

        // Whole-or-destroyed: the accumulated damage pool removes whole ships (lead first); leftover damage
        // carries in the pool to the next round, so weaker fleets still grind kills over time.
        private static void ApplyCasualties(List<Unit> side, ref double pool, List<Entity> destroyed)
        {
            while (side.Count > 0 && pool >= side[0].Toughness)
            {
                pool -= side[0].Toughness;
                destroyed.Add(side[0].Ship);
                side.RemoveAt(0);
            }
        }

        private static BattleOutcome DetermineOutcome(int aCount, int bCount)
        {
            if (aCount == 0 && bCount == 0) return BattleOutcome.MutualDestruction;
            if (bCount == 0) return BattleOutcome.SideAVictory;
            if (aCount == 0) return BattleOutcome.SideBVictory;
            return BattleOutcome.Stalemate;
        }
    }
}
