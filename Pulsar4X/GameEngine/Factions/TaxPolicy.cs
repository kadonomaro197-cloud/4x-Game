namespace Pulsar4X.Factions
{
    /// <summary>
    /// M2-1d (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II — Authoritarianism → tax-under-unrest): the pure decision
    /// helper an NPC brain uses to set its tax rate when its people are restless. A restless population wants relief
    /// (lower taxes); the faction's <see cref="PersonalityTrait.Authoritarianism"/> decides whether it GIVES that
    /// relief or holds the line and suppresses. A high-Authoritarianism faction keeps taxes high under unrest (rule
    /// by force); a low one cuts to appease (rule by consent); a neutral one splits the difference.
    ///
    /// Pure/stateless — nothing calls it autonomously yet (the Phase-2 Tick will), so live behaviour is unchanged.
    /// It reads the government's own <see cref="GovernmentDB.TaxCeiling"/> as the "no-unrest" baseline, so the
    /// regime dial and the faction's disposition compose rather than fight.
    /// </summary>
    public static class TaxPolicy
    {
        /// <summary>How far full unrest can pull the tax rate down for a maximally-appeasing (Authoritarianism 0)
        /// faction — the whole way to zero. Scaled by (1 − Authoritarianism), so an authoritarian faction gives up
        /// less (or none) of its tax.</summary>
        public const double MaxAppeasement = 1.0;

        /// <summary>
        /// The tax rate (a fraction of income, 0..<paramref name="taxCeiling"/>) an NPC with this
        /// <paramref name="personality"/> sets given current <paramref name="unrest"/> (0..1). At zero unrest the
        /// rate is the ceiling for every personality (byte-identical to today's "tax at the ceiling"); unrest pulls
        /// it down to appease, but Authoritarianism resists that cut — a high-Auth faction holds near the ceiling
        /// (suppress), a low-Auth one cuts hard (appease), neutral is a half cut. A null personality reads neutral
        /// Authoritarianism.
        /// </summary>
        public static double TaxRateUnderUnrest(PersonalityDB personality, double taxCeiling, double unrest)
        {
            double authoritarianism = personality == null ? PersonalityDB.Neutral : personality.TraitOf(PersonalityTrait.Authoritarianism);
            double u = unrest < 0.0 ? 0.0 : (unrest > 1.0 ? 1.0 : unrest);
            double appeasement = u * (1.0 - authoritarianism) * MaxAppeasement;   // 0 for a full authoritarian
            return taxCeiling * (1.0 - appeasement);
        }
    }
}
