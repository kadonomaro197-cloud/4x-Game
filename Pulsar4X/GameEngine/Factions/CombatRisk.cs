using Pulsar4X.Engine;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.6 (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II — the Organism engine): the Risk trait meets THE EYES.
    /// A faction reads its own strength (<see cref="FactionRollup.MilitaryStrength"/>) against a fog-limited estimate
    /// of a rival's (<see cref="ThreatAssessment.DetectedStrengthOf"/>, sharpened by intel via
    /// <see cref="IntelAssessment"/>) and decides whether the odds are good enough to commit to a fight. Its
    /// <see cref="PersonalityTrait.Risk"/> sets the bar: a bold faction (Risk 1) engages at PARITY; a cautious one
    /// (Risk 0) demands OVERWHELMING odds; neutral wants a comfortable margin. This is the payoff of the eyes — the
    /// NPC's willingness to fight now scales with what it can SEE of the enemy.
    ///
    /// Pure/read-only — nothing calls it autonomously yet (the Conquer/Defend emitters + the planner will), so live
    /// behaviour is unchanged. It's the decision half of "should I fight"; the act half is later.
    /// </summary>
    public static class CombatRisk
    {
        /// <summary>Strength ratio a maximally CAUTIOUS faction (Risk 0) demands before it will engage — 2× the enemy.</summary>
        public const double CautiousRatio = 2.0;
        /// <summary>Strength ratio a maximally BOLD faction (Risk 1) demands — parity (1×).</summary>
        public const double ParityRatio = 1.0;

        /// <summary>
        /// The own-to-enemy strength ratio this faction demands before committing, from its Risk trait: 2.0 at Risk 0
        /// (needs double the enemy), 1.0 at Risk 1 (fights at parity), 1.5 at neutral. Clamped to [0,1] on the trait.
        /// </summary>
        public static double RequiredStrengthRatio(double riskTrait)
        {
            double r = riskTrait < 0.0 ? 0.0 : (riskTrait > 1.0 ? 1.0 : riskTrait);
            return CautiousRatio + (ParityRatio - CautiousRatio) * r;   // 2.0 → 1.0 as Risk 0 → 1
        }

        /// <summary>
        /// Would a faction of this <paramref name="riskTrait"/> commit, holding <paramref name="ownStrength"/> against
        /// an <paramref name="enemyStrength"/> estimate? True iff own ≥ enemy × <see cref="RequiredStrengthRatio"/>.
        /// A non-positive enemy estimate (nothing detected / no threat) always engages.
        /// </summary>
        public static bool WouldEngage(double ownStrength, double enemyStrength, double riskTrait)
        {
            if (enemyStrength <= 0.0) return true;
            return ownStrength >= enemyStrength * RequiredStrengthRatio(riskTrait);
        }

        /// <summary>
        /// Entity convenience: read this faction's own strength + its fog-limited estimate of the rival (intel-sharpened
        /// when a <paramref name="ledger"/> is supplied) + its Risk trait, and decide. Defensive — a null faction never
        /// engages; a faction with no <see cref="PersonalityDB"/> reads neutral Risk.
        /// </summary>
        public static bool WouldEngage(Entity attackerFaction, int enemyFactionId, InformationLedgerDB ledger = null)
        {
            if (attackerFaction == null) return false;
            double own = FactionRollup.MilitaryStrength(attackerFaction);
            double enemy = ledger != null
                ? IntelAssessment.EstimatedMilitaryStrength(attackerFaction, enemyFactionId, ledger)
                : ThreatAssessment.DetectedStrengthOf(attackerFaction, enemyFactionId);
            double risk = attackerFaction.TryGetDataBlob<PersonalityDB>(out var p)
                ? p.TraitOf(PersonalityTrait.Risk)
                : PersonalityDB.Neutral;
            return WouldEngage(own, enemy, risk);
        }
    }
}
