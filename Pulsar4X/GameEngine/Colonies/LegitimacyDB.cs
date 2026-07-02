using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// The regime's health bar for ONE province — legitimacy (0–100), the INTERNAL-politics counterpart to a
    /// colony's morale (docs/GOVERNMENT-AND-POLITICS-DESIGN.md "Legitimacy — the regime's health bar, LOCAL not
    /// empire-wide"). The load-bearing locked decision: legitimacy is tracked **per system/province, NOT as one
    /// empire-wide number** — so the whole empire can never rebel at once. You lose *provinces*, one at a time, and
    /// can fight to hold or retake them; only the capital falling (or enough provinces at once) topples the central
    /// regime. A lone station can hold its own legitimacy and break away — the fragile frontier node.
    ///
    /// Legitimacy is **DERIVED each cycle, not a parallel resource** (the same principle as morale): it is computed
    /// from the local hosts' morale (the v1 driver — a content province is a loyal one) + the demand track-record +
    /// war outcomes affecting it + governor competence + distance/connectivity to the capital. Below
    /// <see cref="CollapseThreshold"/> the province enters the REBELLION state (the grave rung, #38) — a process
    /// you can fight, not an instant loss.
    ///
    /// SUBSTRATE slice: this is the blob + the pure derivation. It is NOT yet attached to a province entity or
    /// recomputed by a processor, and government-type modulation of the weights is a flagged follow-up (the inputs
    /// are here; weighting by regime is the next refinement — same "ship it government-ready" path morale took).
    /// So adding this changes no behavior. All weights are NAMED COEFFICIENTS (government-ready).
    /// </summary>
    public class LegitimacyDB : BaseDataBlob
    {
        /// <summary>The neutral midpoint (a province neither loyal nor rebellious).</summary>
        public const double Neutral = 50.0;

        // --- Tunable coefficients (a future GovernmentDB re-weights these per regime) ---
        /// <summary>Cap on how far unmet demands erode legitimacy (a bad demand track-record).</summary>
        public const double MaxDemandPenalty = 25.0;
        /// <summary>How far a recent war outcome swings legitimacy (win → +, loss → −).</summary>
        public const double MaxWarSwing = 20.0;
        /// <summary>Cap on the bonus a capable governor gives for holding a restless province (the delegation layer).</summary>
        public const double MaxGovernorBonus = 15.0;
        /// <summary>Cap on the penalty for a far, poorly-connected province (harder to hold — ties to logistics/gates).</summary>
        public const double MaxDistancePenalty = 20.0;
        /// <summary>Below this, the province is collapsing → it enters the REBELLION state (#38, the grave rung).</summary>
        public const double CollapseThreshold = 20.0;

        /// <summary>Current legitimacy, 0..100.</summary>
        [JsonProperty] public double Legitimacy { get; internal set; } = Neutral;

        /// <summary>The per-factor breakdown of what set legitimacy this cycle — the GAUGE (why, not just the number).</summary>
        [JsonProperty] public Dictionary<string, double> Factors { get; internal set; } = new();

        public LegitimacyDB() { }

        public LegitimacyDB(LegitimacyDB other)
        {
            Legitimacy = other.Legitimacy;
            Factors = new Dictionary<string, double>(other.Factors);
        }

        public override object Clone() => new LegitimacyDB(this);

        /// <summary>
        /// The canonical legitimacy computation. The local hosts' <see cref="LegitimacyInputs.AverageMorale"/> is
        /// the v1 baseline (legitimacy tracks contentment); the other inputs adjust it and each contributes 0 when
        /// its "no data" sentinel is passed, so an unwired input is neutral. Fills <paramref name="factorsOut"/>
        /// (if non-null) with the breakdown and returns legitimacy clamped 0..100.
        /// </summary>
        public static double ComputeLegitimacy(LegitimacyInputs inp, Dictionary<string, double> factorsOut)
        {
            factorsOut?.Clear();

            // Baseline: legitimacy tracks the province's contentment (its hosts' average morale, 0..100).
            double legitimacy = Clamp0100(inp.AverageMorale);
            factorsOut?.Add("morale", legitimacy);

            // Demand track-record — unmet demands ERODE (satisfaction 1 = no erosion, 0 = full penalty). Negative
            // sentinel = no demand data → neutral.
            double demand = 0.0;
            if (inp.DemandSatisfaction >= 0.0)
                demand = -(1.0 - Clamp01(inp.DemandSatisfaction)) * MaxDemandPenalty;
            legitimacy += demand;
            factorsOut?.Add("demands", demand);

            // War outcome — a recent win props up the regime, a loss saps it. 0 = no recent war.
            double war = Clamp(inp.WarOutcome, -1.0, 1.0) * MaxWarSwing;
            legitimacy += war;
            factorsOut?.Add("war", war);

            // Governor competence — a capable governor holds a restless province (a bonus). Negative = no governor.
            double governor = 0.0;
            if (inp.GovernorCompetence >= 0.0)
                governor = Clamp01(inp.GovernorCompetence) * MaxGovernorBonus;
            legitimacy += governor;
            factorsOut?.Add("governor", governor);

            // Connectivity to the capital — a far, poorly-connected province is harder to hold. Negative = unknown.
            double distance = 0.0;
            if (inp.Connectivity >= 0.0)
                distance = -(1.0 - Clamp01(inp.Connectivity)) * MaxDistancePenalty;
            legitimacy += distance;
            factorsOut?.Add("connectivity", distance);

            legitimacy = Clamp0100(legitimacy);
            return legitimacy;
        }

        /// <summary>True if legitimacy has fallen into the collapse band — the province enters the rebellion
        /// process (#38). This is the trigger the rebellion-state wiring will read.</summary>
        public static bool IsCollapsing(double legitimacy) => legitimacy < CollapseThreshold;

        private static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
        private static double Clamp0100(double v) => v < 0.0 ? 0.0 : (v > 100.0 ? 100.0 : v);
        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
    }

    /// <summary>
    /// Inputs to <see cref="LegitimacyDB.ComputeLegitimacy"/>, gathered in a struct so the signature stops growing.
    /// Defaults are the neutral case: <see cref="AverageMorale"/> at the midpoint, and every OTHER input at its
    /// "no data" sentinel (negative for the one-sided inputs, 0 for war) so an unwired input contributes nothing.
    /// </summary>
    public struct LegitimacyInputs
    {
        /// <summary>The province's hosts' average morale (0..100) — the v1 legitimacy baseline.</summary>
        public double AverageMorale;
        /// <summary>Fraction of demands met, 0..1 (1 = no erosion). NEGATIVE = "no demand data" → neutral.</summary>
        public double DemandSatisfaction;
        /// <summary>Recent war outcome, −1 (a costly loss) .. +1 (a decisive win). 0 = no recent war.</summary>
        public double WarOutcome;
        /// <summary>Seated governor's competence 0..1 (a hold-the-province bonus). NEGATIVE = no governor → neutral.</summary>
        public double GovernorCompetence;
        /// <summary>Connectivity to the capital 0..1 (1 = well-connected). NEGATIVE = unknown → neutral.</summary>
        public double Connectivity;

        /// <summary>The neutral-everything input except a supplied morale — the common v1 call (morale-only driver).</summary>
        public static LegitimacyInputs FromMorale(double averageMorale) => new LegitimacyInputs
        {
            AverageMorale = averageMorale,
            DemandSatisfaction = -1.0,
            WarOutcome = 0.0,
            GovernorCompetence = -1.0,
            Connectivity = -1.0
        };
    }
}
