namespace Pulsar4X.People
{
    /// <summary>
    /// Reads a commander's competence out of their <see cref="BonusesDB"/> as a combat multiplier — the
    /// "a person's skill modifies an outcome" wire (rung 4), the commander-side mirror of how
    /// <c>ResearchProcessor.RefreshPointModifiers</c> folds a scientist's <see cref="BonusesDB"/> into research
    /// output. Each bonus in the requested category contributes a factor of <c>(1 + Value)</c> (so a Value of
    /// 0.15 = +15%); the multiplier is the product across all matching bonuses, and 1.0 (no effect) when there
    /// are none. Pure/deterministic → unit-testable with no game scaffolding.
    ///
    /// Slice 3b wires this into the fleet auto-resolver (a fleet's flagship commander's Firepower/Toughness
    /// multipliers fold into <c>FleetDoctrineDB.FirepowerMult</c>/<c>ToughnessMult</c>, which the resolver
    /// already reads); the same helper serves the Enhancers unit-caliber elites (dossiers ⚙6/⚙10).
    /// </summary>
    public static class CommanderBonuses
    {
        public static double CombatMultiplier(BonusesDB bonuses, BonusCategory category)
        {
            double mult = 1.0;
            if (bonuses == null)
                return mult;

            foreach (var bonus in bonuses.Bonuses)
            {
                if (bonus.Category == category)
                    mult *= 1.0 + bonus.Value;
            }

            return mult;
        }
    }
}
