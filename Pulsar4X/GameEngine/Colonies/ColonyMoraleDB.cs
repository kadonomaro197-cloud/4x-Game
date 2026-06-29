using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// Morale for a manned host (colony now; station later — this is the shared "manning" concept of
    /// docs/SPACE-STATIONS-DESIGN.md). Morale is the level-control valve on the population "tank": it sets
    /// whether people arrive (immigration) or leave (emigration), and later scales economic output and gates
    /// recruitment. See docs/MORALE-AND-POPULATION-DESIGN.md.
    ///
    /// M1 scope: morale (0..100, 50 = neutral) is recalculated each population tick from inputs that already
    /// exist — environment CONDITIONS (species ColonyCost) and OVERCROWDING (pop vs capacity) — and drives a
    /// migration rate added to population growth. Jobs/unemployment, power, food, tax, and war come in later
    /// slices.
    ///
    /// DESIGN RULE (government-ready): every weight below is a NAMED COEFFICIENT, never hardcoded inline, so a
    /// future GovernmentDB (capitalist-consent / communist-command / …) can re-skin the whole loop without
    /// touching the processor. A command economy, for example, would convert the emigration valve into an
    /// unrest/revolt response — same inputs, different coefficients.
    /// </summary>
    public class ColonyMoraleDB : BaseDataBlob
    {
        /// <summary>The neutral midpoint. Above it people are content (immigration); below it they leave.</summary>
        public const double Neutral = 50.0;

        // --- Tunable coefficients (a future GovernmentDB swaps these) ---
        /// <summary>Morale points lost per 1.0 of species ColonyCost (how hostile the environment is).</summary>
        public const double ConditionsWeight = 12.0;
        /// <summary>Cap on the conditions penalty so a brutal world can't drive morale straight to zero alone.</summary>
        public const double MaxConditionsPenalty = 40.0;
        /// <summary>Crowding only bites once population passes this fraction of capacity.</summary>
        public const double CrowdingThreshold = 0.85;
        /// <summary>Cap on the overcrowding penalty.</summary>
        public const double MaxCrowdingPenalty = 35.0;
        /// <summary>Morale bonus at full employment (jobs ≥ population).</summary>
        public const double MaxEmploymentBonus = 15.0;
        /// <summary>Morale penalty at total unemployment (no jobs for the population).</summary>
        public const double MaxUnemploymentPenalty = 25.0;
        /// <summary>Cap on the housing-comfort morale bonus.</summary>
        public const double MaxComfortBonus = 20.0;
        /// <summary>Max fraction of population that migrates per month at morale 0 (out) or 100 (in).</summary>
        public const double MaxMigrationRate = 0.05;

        /// <summary>Current morale, 0..100.</summary>
        [JsonProperty] public double Morale { get; internal set; } = Neutral;

        /// <summary>
        /// The breakdown of what set morale this tick (factor name → delta from neutral). This is the GAUGE —
        /// it lets the player (and tests) see WHY morale is where it is, not just the number.
        /// </summary>
        [JsonProperty] public Dictionary<string, double> Factors { get; internal set; } = new ();

        public ColonyMoraleDB() { }

        public ColonyMoraleDB(ColonyMoraleDB other)
        {
            Morale = other.Morale;
            Factors = new Dictionary<string, double>(other.Factors);
        }

        public override object Clone() => new ColonyMoraleDB(this);

        /// <summary>
        /// Pure morale computation from the M1 inputs. <paramref name="worstColonyCost"/> is the harshest
        /// ColonyCost among resident species (0 = a native/hospitable world). <paramref name="crowdingRatio"/>
        /// is population / capacity (0 when there is no finite capacity, e.g. a native world). Fills
        /// <paramref name="factorsOut"/> (if non-null) with the per-factor breakdown. Returns morale clamped 0..100.
        /// </summary>
        /// <summary>M1-compatible overload — no employment/comfort data (neutral). Kept for callers/tests that
        /// only have the conditions + crowding inputs.</summary>
        public static double ComputeMorale(double worstColonyCost, double crowdingRatio, Dictionary<string, double> factorsOut)
            => ComputeMorale(worstColonyCost, crowdingRatio, -1.0, 0.0, factorsOut);

        /// <summary>
        /// Full morale computation (M2). <paramref name="employmentRatio"/> = jobs / population; pass a NEGATIVE
        /// value when no installation declares jobs ("no job data" → neutral, not 100% unemployment).
        /// <paramref name="comfort"/> = summed housing comfort (a morale bonus). Other params as the M1 overload.
        /// </summary>
        public static double ComputeMorale(double worstColonyCost, double crowdingRatio, double employmentRatio, double comfort, Dictionary<string, double> factorsOut)
        {
            factorsOut?.Clear();
            double morale = Neutral;
            factorsOut?.Add("baseline", Neutral);

            double conditions = -Math.Min(MaxConditionsPenalty, Math.Max(0.0, worstColonyCost) * ConditionsWeight);
            morale += conditions;
            factorsOut?.Add("conditions", conditions);

            double crowding = 0.0;
            if (crowdingRatio > CrowdingThreshold)
            {
                double over = (crowdingRatio - CrowdingThreshold) / (1.0 - CrowdingThreshold); // 0 at threshold, 1 at capacity
                crowding = -Math.Min(MaxCrowdingPenalty, over * MaxCrowdingPenalty);
            }
            morale += crowding;
            factorsOut?.Add("crowding", crowding);

            // Employment (two-sided). Negative employmentRatio = no job data → neutral contribution.
            double employment = 0.0;
            if (employmentRatio >= 0.0)
            {
                if (employmentRatio >= 1.0)
                    employment = MaxEmploymentBonus;                                    // full employment
                else
                    employment = -(1.0 - employmentRatio) * MaxUnemploymentPenalty;     // unemployment
            }
            morale += employment;
            factorsOut?.Add("employment", employment);

            // Housing comfort — a capped positive bonus.
            double comfortContribution = Math.Min(MaxComfortBonus, Math.Max(0.0, comfort));
            morale += comfortContribution;
            factorsOut?.Add("comfort", comfortContribution);

            if (morale < 0.0) morale = 0.0;
            if (morale > 100.0) morale = 100.0;
            return morale;
        }

        /// <summary>
        /// Monthly migration as a fraction of population. Positive = immigration (morale above neutral),
        /// negative = emigration (below neutral). At morale 50 it is exactly 0.
        /// </summary>
        public static double MigrationRate(double morale)
        {
            return ((morale - Neutral) / Neutral) * MaxMigrationRate;
        }
    }
}
