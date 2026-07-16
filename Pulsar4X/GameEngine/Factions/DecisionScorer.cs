using System.Collections.Generic;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// The cross-domain FEATURES an option can carry — the "axes" every decision is measured on, so a warship, a treaty,
    /// a research project, and a covert op can all be scored on ONE scale. An option declares how much of each feature it
    /// offers; <see cref="DecisionScorer"/> weights those by the faction's <see cref="PersonalityDB"/>, so the SAME
    /// option scores differently for a warlike vs a mercantile faction. Adding a feature is a one-line edit here + in
    /// <see cref="PersonalityWeights"/>.
    /// </summary>
    public enum DecisionFeature
    {
        /// <summary>Solving a problem by force (a warship, an invasion, a strike).</summary>
        MilitarySolve,
        /// <summary>Wealth / production (a refinery, a mine, a trade deal).</summary>
        EconGain,
        /// <summary>Research / knowledge (a lab, a survey, a tech).</summary>
        TechGain,
        /// <summary>New territory (a colony, a claim, an outpost).</summary>
        ExpansionGain,
        /// <summary>How much the option GAMBLES — weighted + by a bold faction, − by a cautious one.</summary>
        RiskLevel,
        /// <summary>Civilian harm / terror (bombardment, purges).</summary>
        Ruthless,
        /// <summary>Spying / feints / fighting dark.</summary>
        Covert,
        /// <summary>Deals / aid / alliance.</summary>
        Cooperative,
        /// <summary>Ideological purity / no compromise.</summary>
        Doctrinaire,
    }

    /// <summary>An option the <see cref="DecisionScorer"/> can rank: it exposes how much of each <see cref="DecisionFeature"/> it offers.</summary>
    public interface IScoredOption
    {
        IReadOnlyDictionary<DecisionFeature, double> Features { get; }
    }

    /// <summary>
    /// The ONE place that encodes "which trait cares about which feature" (docs/ai/AI-PERSONALITY-IMPLEMENTATION-SPEC.md
    /// §3). A one-line edit changes a feature's personality driver. Every weight is a pure read of the faction's 0..1
    /// traits (neutral 0.5), so a fresh all-neutral faction weights every feature the same middling amount — a distinct
    /// identity only emerges once the dials are authored. A null personality reads neutral.
    /// </summary>
    public static class PersonalityWeights
    {
        private static readonly PersonalityDB Neutral = new PersonalityDB();

        public static double Of(PersonalityDB p, DecisionFeature f)
        {
            if (p == null) p = Neutral;
            double T(PersonalityTrait t) => p.TraitOf(t);
            switch (f)
            {
                case DecisionFeature.MilitarySolve: return T(PersonalityTrait.Aggression);
                case DecisionFeature.EconGain:      return T(PersonalityTrait.Ambition) * 0.5 + (1 - T(PersonalityTrait.Aggression)) * 0.5;
                case DecisionFeature.TechGain:      return T(PersonalityTrait.Curiosity) * 0.6 + T(PersonalityTrait.Ambition) * 0.4;
                case DecisionFeature.ExpansionGain: return T(PersonalityTrait.Ambition);
                case DecisionFeature.RiskLevel:     return (T(PersonalityTrait.Risk) - 0.5) * 2.0;   // >0.5 seeks risk, <0.5 penalizes
                case DecisionFeature.Ruthless:      return T(PersonalityTrait.Ruthlessness);
                case DecisionFeature.Covert:        return T(PersonalityTrait.Guile);
                case DecisionFeature.Cooperative:   return T(PersonalityTrait.Altruism) * 0.5 + (1 - T(PersonalityTrait.Xenophobia)) * 0.5;
                case DecisionFeature.Doctrinaire:   return T(PersonalityTrait.Zealotry);
                default: return 0.0;
            }
        }
    }

    /// <summary>
    /// Phase A-2a — the shared cross-domain SCORER (the developer's Q1: "best not first"). Score any set of options by a
    /// faction's personality and PICK THE BEST, instead of taking the first viable one. Pure/stateless (matches
    /// docs/ai/AI-PERSONALITY-IMPLEMENTATION-SPEC.md §3). Additive + UNWIRED here (no caller yet) → byte-identical; A-2b
    /// wires <see cref="PickBest{T}"/> into a real build/objective choice. The whole point: the SAME <see cref="Score"/>
    /// makes distinct factions — a warlike faction picks the warship where a mercantile one picks the refinery, from the
    /// dials alone (the fingerprint gauge, <c>DecisionScorerTests</c>).
    /// </summary>
    public static class DecisionScorer
    {
        /// <summary>An option's utility to this faction = Σ (how much of each feature it offers × how much this faction's
        /// personality values that feature). A null personality reads neutral; a null/empty option scores 0.</summary>
        public static double Score(IScoredOption option, PersonalityDB personality)
        {
            if (option?.Features == null) return 0.0;
            double sum = 0.0;
            foreach (var kv in option.Features)
                sum += kv.Value * PersonalityWeights.Of(personality, kv.Key);
            return sum;
        }

        /// <summary>The highest-scoring option for this faction (the "best not first" pick), or <c>default</c> if the
        /// set is empty. Deterministic (a stable single pass — ties keep the first-seen best).</summary>
        public static T PickBest<T>(IEnumerable<T> options, PersonalityDB personality) where T : IScoredOption
        {
            T best = default;
            double bestScore = double.NegativeInfinity;
            foreach (var o in options)
            {
                double s = Score(o, personality);
                if (s > bestScore) { bestScore = s; best = o; }
            }
            return best;
        }
    }
}
