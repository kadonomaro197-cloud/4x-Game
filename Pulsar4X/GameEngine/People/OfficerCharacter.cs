namespace Pulsar4X.People
{
    /// <summary>
    /// Phase-2.7 (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II — the Organism engine): officer character, tenure-blended.
    /// A commander has their own leanings, but a GREEN officer mostly executes the faction's doctrine; only as they
    /// gain tenure does their own character start to override it. This is the pure blend/drift MATH — how much of the
    /// officer vs. the faction shows, and how an officer's leanings drift over a long posting. Nothing reads it yet
    /// (attaching officer traits to <see cref="CommanderDB"/> + wiring a blended trait into a live decision is the
    /// follow-on micro-slice), so live behaviour is unchanged.
    /// </summary>
    public static class OfficerCharacter
    {
        /// <summary>
        /// How strongly this officer's OWN character asserts over the faction's doctrine, from their tenure:
        /// 0 for a green officer (defers to doctrine), rising toward 1 as <paramref name="experience"/> approaches
        /// <paramref name="experienceCap"/> (a seasoned officer runs on their own judgement). Clamped 0..1; a
        /// zero/negative cap reads green (0).
        /// </summary>
        public static double TenureWeight(int experience, int experienceCap)
        {
            if (experienceCap <= 0) return 0.0;
            double w = (double)experience / experienceCap;
            return w < 0.0 ? 0.0 : (w > 1.0 ? 1.0 : w);
        }

        /// <summary>
        /// Blend an officer's trait with the faction's, weighted by tenure: at <paramref name="tenureWeight"/> 0 the
        /// result is the faction's value (a green officer follows doctrine); at 1 it's the officer's own value (a
        /// veteran runs on their character); in between, a linear mix.
        /// </summary>
        public static double Blend(double officerTrait, double factionTrait, double tenureWeight)
        {
            double t = tenureWeight < 0.0 ? 0.0 : (tenureWeight > 1.0 ? 1.0 : tenureWeight);
            return factionTrait + (officerTrait - factionTrait) * t;
        }

        /// <summary>
        /// Drift a trait toward a target by a fraction <paramref name="rate"/> (0 = no drift, 1 = snap to target) —
        /// how an officer's leanings shift over a long posting (e.g. toward their faction's, or hardened by combat).
        /// Clamped rate; the trait itself is clamped 0..1.
        /// </summary>
        public static double Drift(double trait, double towardValue, double rate)
        {
            double r = rate < 0.0 ? 0.0 : (rate > 1.0 ? 1.0 : rate);
            double v = trait + (towardValue - trait) * r;
            return v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
        }
    }
}
