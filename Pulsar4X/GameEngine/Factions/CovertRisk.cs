namespace Pulsar4X.Factions
{
    /// <summary>The graduated result of a covert op — the deniability game (docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md §F).</summary>
    public enum CovertOutcome
    {
        /// <summary>Undetected — you hurt them and nobody knows it was you.</summary>
        Clean,
        /// <summary>They noticed the op but can't prove it was you — suspicion builds.</summary>
        Traced,
        /// <summary>Caught red-handed — a relation hit / casus belli for THEM, and suspicion spikes.</summary>
        Caught,
    }

    /// <summary>
    /// F-C3d (docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md §F, risk model F2+F3): the graduated caught/suspicion model.
    /// A covert op resolves Clean / Traced / Caught from its effective detection risk, and each non-clean outcome
    /// builds a per-rival SUSPICION meter — hurt them quietly, but every "traced" stacks until they're sure. Pure &
    /// DETERMINISTIC: the random roll is passed IN (never Math.Random / a wall clock in engine code), so the same
    /// inputs always resolve the same way (and tests are exact). Byte-identical (nothing runs an op yet).
    /// </summary>
    public static class CovertRisk
    {
        /// <summary>Suspicion added when an op is Traced (they noticed but can't prove it). Flagged balance dial.</summary>
        public const double SuspicionTraced = 8.0;
        /// <summary>Suspicion added when an op is Caught (proof). Flagged balance dial.</summary>
        public const double SuspicionCaught = 30.0;

        /// <summary>
        /// Resolve a covert op's outcome. Effective risk = base × (1 − agent skill) × (1 + target counter-intel), so a
        /// skilled agent lowers it and the target's counter-intel raises it. The <paramref name="roll01"/> (0..1,
        /// higher = unluckier) decides the band: the top (eff/2) of the range is Caught, the next eff/2 is Traced,
        /// the rest Clean. So a low roll under low risk is Clean; a high roll or high risk trends Traced/Caught.
        /// </summary>
        public static CovertOutcome Resolve(double baseDetectionRisk, double agentSkill01, double targetCounterIntel01, double roll01)
        {
            double eff = Clamp01(baseDetectionRisk) * (1.0 - Clamp01(agentSkill01)) * (1.0 + Clamp01(targetCounterIntel01));
            eff = Clamp01(eff);

            double caughtCut = 1.0 - eff / 2.0; // the unluckiest eff/2 of rolls → Caught
            double tracedCut = 1.0 - eff;       // the next band → Traced
            double roll = Clamp01(roll01);

            if (roll >= caughtCut) return CovertOutcome.Caught;
            if (roll >= tracedCut) return CovertOutcome.Traced;
            return CovertOutcome.Clean;
        }

        /// <summary>Per-rival suspicion after an outcome: Clean +0, Traced +small, Caught +large; clamped 0..100.</summary>
        public static double SuspicionAfter(double currentSuspicion, CovertOutcome outcome)
        {
            double add = outcome switch
            {
                CovertOutcome.Traced => SuspicionTraced,
                CovertOutcome.Caught => SuspicionCaught,
                _ => 0.0,
            };
            double value = currentSuspicion + add;
            return value < 0.0 ? 0.0 : (value > 100.0 ? 100.0 : value);
        }

        private static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
    }
}
