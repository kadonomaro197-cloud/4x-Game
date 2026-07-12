using Pulsar4X.Colonies;
using Pulsar4X.Engine;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.2 (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II — the Organism engine): the NEEDS-LADDER READ. Given a
    /// faction's own gauges (war standing, morale, legitimacy, treasury, rebellion) it settles on the LOWEST unmet
    /// <see cref="NeedTier"/> — the Maslow rung the brain must attend before reaching higher. You don't chase a grand
    /// ambition while a colony starves; you don't grow the economy while the capital rebels.
    ///
    /// The core is the PURE <see cref="AssessTier(bool,double,double,double,double,decimal,bool)"/> over raw numbers
    /// (fully unit-tested at the tier boundaries); the entity convenience gathers those numbers off the built
    /// <see cref="FactionRollup"/> gauges + <see cref="DiplomacyDB"/>. Read-only → byte-identical (nothing writes the
    /// objective from it yet; the transition engine 2.3 + the Tick 2.4 do).
    /// </summary>
    public static class NeedsLadder
    {
        // --- tier thresholds (morale/legitimacy on the 0..100 scale, Neutral 50) ---
        /// <summary>Morale at or below this is an existential collapse (Survive).</summary>
        public const double MoraleCrisis = 20.0;
        /// <summary>Legitimacy at or below this is a regime-threatening loss of the mandate (Survive).</summary>
        public const double LegitimacyCrisis = 20.0;
        /// <summary>Below this, morale is unhealthy enough to demand attention (Stabilize).</summary>
        public const double MoraleHealthy = 45.0;
        /// <summary>Below this, legitimacy needs shoring up (Stabilize).</summary>
        public const double LegitimacyHealthy = 45.0;
        /// <summary>At or above this, morale is thriving — a precondition for Ambition.</summary>
        public const double MoraleThriving = 65.0;
        /// <summary>At or above this, legitimacy is thriving — a precondition for Ambition.</summary>
        public const double LegitimacyThriving = 65.0;
        /// <summary>At war, own strength below enemy strength × this = losing badly (Survive).</summary>
        public const double LosingWarRatio = 0.5;
        /// <summary>Treasury at or above this is the "war chest" a grand ambition needs.</summary>
        public const decimal AmbitionWealth = 100000m;

        /// <summary>
        /// The lowest unmet needs-tier for a faction with these gauges. Climbs from the bottom: an existential threat
        /// (rebellion, collapsed morale/legitimacy, a war being lost) is <see cref="NeedTier.Survive"/>; internal
        /// trouble or any active war is <see cref="NeedTier.Stabilize"/>; a dominant, secure, wealthy faction reaches
        /// <see cref="NeedTier.Ambition"/>; everything else is the healthy <see cref="NeedTier.Thrive"/> default.
        /// </summary>
        public static NeedTier AssessTier(bool atWar, double ownStrength, double enemyStrength,
            double meanMorale, double meanLegitimacy, decimal balance, bool inRebellion)
        {
            // Survive — existential: the house is on fire.
            if (inRebellion
                || meanMorale <= MoraleCrisis
                || meanLegitimacy <= LegitimacyCrisis
                || (atWar && ownStrength < enemyStrength * LosingWarRatio))
                return NeedTier.Survive;

            // Stabilize — internal trouble, or an active war that isn't being lost (still demands the attention).
            if (atWar
                || balance < 0m
                || meanMorale < MoraleHealthy
                || meanLegitimacy < LegitimacyHealthy)
                return NeedTier.Stabilize;

            // Ambition — dominant and secure on every axis: content, legitimate, rich, and militarily ahead.
            if (meanMorale >= MoraleThriving
                && meanLegitimacy >= LegitimacyThriving
                && balance >= AmbitionWealth
                && ownStrength >= enemyStrength)
                return NeedTier.Ambition;

            // Thrive — the healthy default: not in crisis, not yet dominant.
            return NeedTier.Thrive;
        }

        /// <summary>
        /// Entity convenience: gather the gauges off a faction and assess its tier. Own strength + morale/legitimacy/
        /// treasury come from <see cref="FactionRollup"/>; war standing + the strongest at-war rival's strength come
        /// from <see cref="DiplomacyDB"/>; rebellion is any colony flagged <see cref="RebellionDB.IsRebelling"/>.
        /// Defensive: a faction with no <see cref="DiplomacyDB"/> is simply "not at war". (Fog-limited enemy strength
        /// is the 2.6 Risk-trait refinement; this v1 reads true rival strength.)
        /// </summary>
        public static NeedTier AssessTier(Entity factionEntity)
        {
            bool atWar = false;
            double enemyStrength = 0;
            var game = factionEntity?.Manager?.Game;
            if (factionEntity != null && factionEntity.TryGetDataBlob<DiplomacyDB>(out var dip) && game != null)
            {
                foreach (var rel in dip.Relationships.Values)
                {
                    if (!rel.AtWar) continue;
                    atWar = true;
                    if (game.Factions.TryGetValue(rel.OtherFactionId, out var rival))
                    {
                        double s = FactionRollup.MilitaryStrength(rival);
                        if (s > enemyStrength) enemyStrength = s;
                    }
                }
            }

            return AssessTier(
                atWar,
                FactionRollup.MilitaryStrength(factionEntity),
                enemyStrength,
                FactionRollup.MeanMorale(factionEntity),
                FactionRollup.MeanLegitimacy(factionEntity),
                FactionRollup.Balance(factionEntity),
                InRebellion(factionEntity));
        }

        /// <summary>True if any of the faction's colonies is currently in open rebellion.</summary>
        private static bool InRebellion(Entity factionEntity)
        {
            if (factionEntity == null || !factionEntity.TryGetDataBlob<FactionInfoDB>(out var info))
                return false;
            foreach (var colony in info.Colonies)
                if (colony.TryGetDataBlob<RebellionDB>(out var reb) && reb.IsRebelling)
                    return true;
            return false;
        }
    }
}
