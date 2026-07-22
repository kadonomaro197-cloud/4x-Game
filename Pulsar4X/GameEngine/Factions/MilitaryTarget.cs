using System.Collections.Generic;
using Pulsar4X.Colonies;
using Pulsar4X.Engine;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.8 P-3 — the deferred military REACH (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md +
    /// AI-BRAIN-BUILD-TRACKER.md). The SEEING half of turning "an NPC built a war fleet" into "an NPC sails that
    /// fleet at the RIGHT enemy world." <see cref="ConquerResolver"/> named three deferred helpers — MilitaryTarget /
    /// MilitaryComposition / MilitaryReach — and this is MilitaryTarget.
    ///
    /// <see cref="BestEnemyTarget"/> is the developer's "a calculation of the best option given the circumstances"
    /// (not "attack the nearest"): it is the FIRST real UTILITY decision in the brain — every candidate enemy world
    /// gets a numeric SCORE from the current situation, and the AI picks the highest. Here the score is
    /// <c>value × reach</c>: a big prize is worth more, but a prize you can't easily get to is discounted — so a
    /// smaller REACHABLE world can rightly beat a bigger DISTANT one. Weights are named + tunable, and the factor list
    /// is meant to GROW (weakness/garrison, true jump-route distance) as those reads land — that growth is exactly how
    /// the hardcoded menu becomes a brain "aware of all its options."
    ///
    /// Pure read, no side effects, no warp/order surface touched (the movement-order emission that USES the target is
    /// a separate, small slice against the landmine-dense warp path). Candidates are colonies owned by a faction this
    /// one holds an <see cref="RelationshipState.AtWar"/> latch toward — the latch a Phase-3.4b coalition sets. So the
    /// chain composes: 3.4b DECLARES the war → this SCORES + names the target world → the reach slice SAILS the fleet.
    /// No war / no rival colony → an invalid target (nothing to hit — the caller falls back to massing more fleet).
    /// Defensive/no-throw — safe to call every monthly cycle.
    /// </summary>
    public static class MilitaryTarget
    {
        // --- Utility weights (provisional, tunable — the first knobs of the scoring brain) ---------------------------

        /// <summary>Weight on a target's raw VALUE (population = the size of the prize).</summary>
        public const double ValueWeight = 1.0;
        /// <summary>Reach factor for a world in a system where we ALREADY have a colony (we can stage the strike now).</summary>
        public const double ReachInSystem = 1.0;
        /// <summary>Reach factor for a world we'd have to JUMP to reach (discounted — true jump-route cost is the
        /// MilitaryReach slice; this is a coarse "near vs far" v1 proxy).</summary>
        public const double ReachDistant = 0.35;
        /// <summary>Audit M3 — weight on the fog-honest EASIEST-LANDING discount (the "weakness/garrison" term this
        /// scoring was designed to grow into). A target's score is divided by (1 + this × detected-defender-strength),
        /// so a scouted, heavily-garrisoned world is a less attractive landing than a softer one. An un-scouted world
        /// reads 0 detected defence → factor 1.0 → score unchanged (byte-identical). FLAGGED balance value.</summary>
        public const double EasyLandingWeight = 0.002;  // FLAGGED balance value

        /// <summary>A scored strike candidate — the enemy colony, the BODY to sail at, and its utility score.</summary>
        public readonly struct ScoredTarget
        {
            public Entity Colony { get; init; }
            public Entity ColonyBody { get; init; }
            public double Score { get; init; }

            /// <summary>True when this names a real world worth striking (a valid body with a positive score).</summary>
            public bool IsValid => ColonyBody != null && ColonyBody.IsValid && Score > 0;

            public static ScoredTarget None => new ScoredTarget
            {
                Colony = Entity.InvalidEntity, ColonyBody = Entity.InvalidEntity, Score = 0,
            };
        }

        /// <summary>
        /// Score every enemy world we're at war with and return the BEST (highest <c>value × reach</c>). Returns
        /// <see cref="ScoredTarget.None"/> when there is no war on record or no at-war rival owns a valid colony.
        /// The "calculation given the circumstances": a reachable prize outweighs a distant one of equal size, so the
        /// AI doesn't blindly march at the nearest — it weighs the payoff.
        /// </summary>
        /// <summary>Audit M3 — the pure EASIEST-LANDING discount: a score multiplier in (0,1] from the fog-honest
        /// detected defender strength. 0 detected → 1.0 (unchanged, byte-identical); more detected defence → a smaller
        /// factor, never 0 (a scored target stays valid). Extracted pure so the discount is CI-testable without a sim.</summary>
        public static double LandingEaseFactor(double detectedDefence)
            => 1.0 / (1.0 + EasyLandingWeight * System.Math.Max(0.0, detectedDefence));

        public static ScoredTarget BestEnemyTarget(Entity faction)
        {
            var best = ScoredTarget.None;
            foreach (var (colony, body, info) in EnemyColonies(faction))
            {
                double reach = InSameSystemAsFactionAsset(faction, body) ? ReachInSystem : ReachDistant;
                // Audit M3 — fog-honest EASIEST-LANDING factor. GroundThreat.DetectedDefenderStrength counts ONLY the
                // enemy ground strength this faction has actually DETECTED on the target world (an un-scouted world
                // reads 0 → landingEase 1.0 → score unchanged, byte-identical); a scouted, heavily-garrisoned world is
                // discounted so the AI prefers the softer landing. Bounded (0,1], never zeroes the score. This is the
                // second consumer of the "one read, two consumers" GroundThreat design (the brain is the first).
                double detectedDefence = Pulsar4X.GroundCombat.GroundThreat.DetectedDefenderStrength(body, faction.Id);
                double score = ColonyValue(info) * ValueWeight * reach * LandingEaseFactor(detectedDefence);
                if (score > best.Score)
                    best = new ScoredTarget { Colony = colony, ColonyBody = body, Score = score };
            }
            return best;
        }

        /// <summary>
        /// The simple v1 helper (unchanged): name the FIRST at-war rival's colony body — a candidate source and the
        /// byte-identical fallback. Prefer <see cref="BestEnemyTarget"/> for the real decision. Returns
        /// <see cref="Entity.InvalidEntity"/> when there is no war on record or no at-war rival owns a valid colony.
        /// </summary>
        public static Entity NearestEnemyColonyBody(Entity faction)
        {
            foreach (var (_, body, _) in EnemyColonies(faction))
                return body;
            return Entity.InvalidEntity;
        }

        /// <summary>Enumerate the colonies owned by every faction <paramref name="faction"/> holds an AtWar latch
        /// toward — the candidate strike set. Only a STORED, AtWar relationship counts. Defensive/no-throw.</summary>
        private static IEnumerable<(Entity colony, Entity body, ColonyInfoDB info)> EnemyColonies(Entity faction)
        {
            if (faction == null || faction.Manager?.Game == null) yield break;
            if (!faction.TryGetDataBlob<DiplomacyDB>(out var dip)) yield break;
            var game = faction.Manager.Game;

            foreach (var rel in dip.Relationships.Values)
            {
                if (!rel.AtWar) continue;                                        // strike only a faction we're at war with
                int enemyId = rel.OtherFactionId;
                if (enemyId == faction.Id || enemyId == Game.NeutralFactionId) continue;
                if (!game.Factions.TryGetValue(enemyId, out var enemy) || enemy == null) continue;
                if (!enemy.TryGetDataBlob<FactionInfoDB>(out var enemyInfo)) continue;

                foreach (var colony in enemyInfo.Colonies)
                {
                    if (colony == null || !colony.IsValid) continue;
                    if (!colony.TryGetDataBlob<ColonyInfoDB>(out var colonyInfo)) continue;
                    var body = colonyInfo.PlanetEntity;
                    if (body != null && body.IsValid)
                        yield return (colony, body, colonyInfo);
                }
            }
        }

        /// <summary>A colony's prize VALUE — its total population (floored at 1 so a zero-pop colony still ranks as a
        /// minimal prize rather than scoring 0). Industry/strategic value are later factors.</summary>
        private static double ColonyValue(ColonyInfoDB info)
        {
            double pop = 0;
            if (info.Population != null)
                foreach (var count in info.Population.Values)
                    pop += count;
            return pop < 1 ? 1 : pop;
        }

        /// <summary>True if the faction already holds a colony in the SAME star system as the target body — a coarse
        /// "we can stage the strike from here now" reach proxy (true multi-jump routing is the MilitaryReach slice).</summary>
        private static bool InSameSystemAsFactionAsset(Entity faction, Entity targetBody)
        {
            var targetMgr = targetBody?.Manager;
            if (targetMgr == null) return false;
            if (!faction.TryGetDataBlob<FactionInfoDB>(out var info)) return false;

            foreach (var colony in info.Colonies)
            {
                if (colony == null || !colony.IsValid) continue;
                if (!colony.TryGetDataBlob<ColonyInfoDB>(out var colonyInfo)) continue;
                var body = colonyInfo.PlanetEntity;
                if (body != null && body.IsValid && body.Manager == targetMgr)
                    return true;
            }
            return false;
        }
    }
}
