using System;
using System.Collections.Generic;

namespace Pulsar4X.People
{
    /// <summary>
    /// The commander-competence <b>read</b> (<see cref="CombatMultiplier"/>) and <b>generate</b>
    /// (<see cref="RollCombatCompetence"/>) helpers — the two ends of the rung-4 "a person's skill modifies an
    /// outcome" loop, the commander-side mirror of how <c>ResearchProcessor</c> folds a scientist's
    /// <see cref="BonusesDB"/> into research output. Pure/deterministic → unit-testable with no game scaffolding.
    ///
    /// The loop end to end: an academy graduate's <c>ExperienceCap</c> → <see cref="RollCombatCompetence"/>
    /// writes Firepower/Toughness bonuses onto their <see cref="BonusesDB"/> (slice 3c) → seat the officer as a
    /// fleet's flagship → <see cref="CombatMultiplier"/> folds those bonuses into the fleet auto-resolver's
    /// firepower/toughness (slice 3b). The same helpers serve the Enhancers unit-caliber elites (dossiers ⚙6/⚙10).
    /// </summary>
    public static class CommanderBonuses
    {
        /// <summary>The combat bonus a maximum-potential graduate (ExperienceCap 200) contributes — modest by
        /// design (a tiebreaker on top of doctrine + composition, never a replacement). Tunable balance dial.</summary>
        public const double MaxCombatCompetenceBonus = 0.15;

        /// <summary>The RESEARCH bonus a maximum-potential scientist (ExperienceCap 200) contributes to their
        /// specialty tech category — the research-side twin of <see cref="MaxCombatCompetenceBonus"/>. Modest,
        /// tunable balance dial. (F-A2, docs/AI-BRAIN-BUILD-TRACKER.md — competence is now EARNABLE for scientists,
        /// not just for combat officers.)</summary>
        public const double MaxResearchCompetenceBonus = 0.15;

        /// <summary>
        /// Read a commander's competence in a category as a combat multiplier: each matching bonus contributes a
        /// factor of <c>(1 + Value)</c> (so a Value of 0.15 = +15%); the product across all matching bonuses, and
        /// 1.0 (no effect) when there are none or the DB is null.
        /// </summary>
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

        /// <summary>
        /// Generate a graduate's combat competence as Firepower + Toughness bonuses scaled by their
        /// <paramref name="experienceCap"/> (0–200, mean ~100 from the academy roll, shifted by training length).
        /// Deterministic in the cap: a cap of 200 gives the full <see cref="MaxCombatCompetenceBonus"/>, 100 gives
        /// half, ≤0 gives nothing. These are exactly what <see cref="CombatMultiplier"/> folds when the officer is
        /// a fleet's flagship — closing the rung-4 loop. (Firepower == Toughness for now; an aggressive-vs-defensive
        /// split is a later flavor pass.)
        /// </summary>
        public static List<Bonus> RollCombatCompetence(int experienceCap)
        {
            var result = new List<Bonus>();
            if (experienceCap <= 0)
                return result;

            double frac = Math.Min(experienceCap, 200) / 200.0 * MaxCombatCompetenceBonus;
            if (frac <= 0)
                return result;

            result.Add(new Bonus("Combat Leadership", frac, BonusType.Perentage, BonusCategory.Firepower));
            result.Add(new Bonus("Combat Leadership", frac, BonusType.Perentage, BonusCategory.Toughness));
            return result;
        }

        /// <summary>
        /// F-A2 (docs/AI-BRAIN-BUILD-TRACKER.md, Movement I): generate a scientist's RESEARCH competence in a tech
        /// category — the research-side twin of <see cref="RollCombatCompetence"/>, and the missing GENERATOR the
        /// live reader already expects. A scientist entity ships with an EMPTY <see cref="BonusesDB"/>, so
        /// <c>ResearchProcessor.RefreshPointModifiers</c> (the read) has nothing to fold; this rolls the value it
        /// reads. The bonus is shaped exactly for that reader: <b>FilterId = the tech category</b> (what
        /// RefreshPointModifiers matches a scientist bonus on) and <b>Type = Percentage</b> (folded as a % increase
        /// to that category's points/day). Scaled by <paramref name="experienceCap"/> the same way combat is: cap
        /// 200 → the full <see cref="MaxResearchCompetenceBonus"/>, 100 → half, ≤0 → none. Deterministic/pure.
        ///
        /// This only MAKES competence available — it changes no default path (a scientist still ships empty unless a
        /// caller stamps a rolled bonus), so it is byte-identical until a consumer (the field-scientist career, an
        /// academy) wires it. That deliberate wiring is a later slice; this is the foundation it stands on.
        /// </summary>
        public static List<Bonus> RollResearchCompetence(int experienceCap, string techCategoryId)
        {
            var result = new List<Bonus>();
            if (experienceCap <= 0 || string.IsNullOrEmpty(techCategoryId))
                return result;

            double frac = Math.Min(experienceCap, 200) / 200.0 * MaxResearchCompetenceBonus;
            if (frac <= 0)
                return result;

            result.Add(new Bonus("Research Aptitude", frac, BonusType.Perentage, BonusCategory.ResearchPoints, techCategoryId));
            return result;
        }
    }
}
