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
        /// <summary>Morale penalty at 100% tax rate (scales linearly with the tax rate). M4.</summary>
        public const double MaxTaxPenalty = 30.0;
        /// <summary>Morale penalty at total power shortage (scales with the shortage fraction). M5.</summary>
        public const double MaxPowerShortagePenalty = 30.0;
        /// <summary>Morale penalty at total food shortage — starvation bites harder than a brownout. M5.</summary>
        public const double MaxFoodShortagePenalty = 40.0;
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

        /// <summary>M2-compatible overload — no tax input (taxRate 0).</summary>
        public static double ComputeMorale(double worstColonyCost, double crowdingRatio, double employmentRatio, double comfort, Dictionary<string, double> factorsOut)
            => ComputeMorale(worstColonyCost, crowdingRatio, employmentRatio, comfort, 0.0, factorsOut);

        /// <summary>Positional overload (M4) — power/food shortages default to none. Builds a MoraleInputs.</summary>
        public static double ComputeMorale(double worstColonyCost, double crowdingRatio, double employmentRatio, double comfort, double taxRate, Dictionary<string, double> factorsOut)
            => ComputeMorale(new MoraleInputs
            {
                WorstColonyCost = worstColonyCost,
                CrowdingRatio = crowdingRatio,
                EmploymentRatio = employmentRatio,
                Comfort = comfort,
                TaxRate = taxRate,
                PowerShortage = 0.0,
                FoodShortage = 0.0
            }, factorsOut);

        /// <summary>
        /// The canonical morale computation (M5). All inputs come in a <see cref="MoraleInputs"/> struct so the
        /// parameter list stops growing. Fills <paramref name="factorsOut"/> with the per-factor breakdown (the
        /// gauge) and returns morale clamped 0..100. PowerShortage/FoodShortage are fractions 0 (none)..1
        /// (total) and contribute 0 when there's no shortage — neutral until M5's energy/food wiring feeds real
        /// values. All weights are named coefficients (government-ready).
        /// </summary>
        public static double ComputeMorale(MoraleInputs inp, Dictionary<string, double> factorsOut)
        {
            factorsOut?.Clear();
            double morale = Neutral;
            factorsOut?.Add("baseline", Neutral);

            double conditions = -Math.Min(MaxConditionsPenalty, Math.Max(0.0, inp.WorstColonyCost) * ConditionsWeight);
            morale += conditions;
            factorsOut?.Add("conditions", conditions);

            double crowding = 0.0;
            if (inp.CrowdingRatio > CrowdingThreshold)
            {
                double over = (inp.CrowdingRatio - CrowdingThreshold) / (1.0 - CrowdingThreshold); // 0 at threshold, 1 at capacity
                crowding = -Math.Min(MaxCrowdingPenalty, over * MaxCrowdingPenalty);
            }
            morale += crowding;
            factorsOut?.Add("crowding", crowding);

            // Employment (two-sided). Negative EmploymentRatio = no job data → neutral contribution.
            double employment = 0.0;
            if (inp.EmploymentRatio >= 0.0)
            {
                if (inp.EmploymentRatio >= 1.0)
                    employment = MaxEmploymentBonus;                                        // full employment
                else
                    employment = -(1.0 - inp.EmploymentRatio) * MaxUnemploymentPenalty;     // unemployment
            }
            morale += employment;
            factorsOut?.Add("employment", employment);

            // Housing comfort — a capped positive bonus.
            double comfortContribution = Math.Min(MaxComfortBonus, Math.Max(0.0, inp.Comfort));
            morale += comfortContribution;
            factorsOut?.Add("comfort", comfortContribution);

            // Tax — a linear penalty (heavier tax, unhappier people). A government type sets how hard it bites.
            double tax = -Math.Min(MaxTaxPenalty, Math.Max(0.0, inp.TaxRate) * MaxTaxPenalty);
            morale += tax;
            factorsOut?.Add("tax", tax);

            // Power shortage (M5) — brownouts sour the populace.
            double power = -Math.Min(MaxPowerShortagePenalty, Clamp01(inp.PowerShortage) * MaxPowerShortagePenalty);
            morale += power;
            factorsOut?.Add("power", power);

            // Food shortage (M5) — starvation, the harshest of the everyday inputs.
            double food = -Math.Min(MaxFoodShortagePenalty, Clamp01(inp.FoodShortage) * MaxFoodShortagePenalty);
            morale += food;
            factorsOut?.Add("food", food);

            if (morale < 0.0) morale = 0.0;
            if (morale > 100.0) morale = 100.0;
            return morale;
        }

        private static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);

        /// <summary>
        /// Monthly migration as a fraction of population. Positive = immigration (morale above neutral),
        /// negative = emigration (below neutral). At morale 50 it is exactly 0.
        /// </summary>
        public static double MigrationRate(double morale)
        {
            return ((morale - Neutral) / Neutral) * MaxMigrationRate;
        }
    }

    /// <summary>
    /// All the inputs to <see cref="ColonyMoraleDB.ComputeMorale(MoraleInputs, System.Collections.Generic.Dictionary{string, double})"/>,
    /// gathered into one struct so the morale signature stops growing as new factors are added. Defaults are
    /// the neutral case EXCEPT <see cref="EmploymentRatio"/>, which must be set to a negative sentinel for
    /// "no job data" (the parameterless default 0 would read as total unemployment).
    /// </summary>
    public struct MoraleInputs
    {
        /// <summary>Harshest resident-species ColonyCost (0 = hospitable). Conditions penalty.</summary>
        public double WorstColonyCost;
        /// <summary>Population / capacity (0 = uncrowded). Overcrowding penalty past the threshold.</summary>
        public double CrowdingRatio;
        /// <summary>Jobs / workforce. NEGATIVE = "no job data" → neutral (not unemployment).</summary>
        public double EmploymentRatio;
        /// <summary>Summed housing comfort (a capped morale bonus).</summary>
        public double Comfort;
        /// <summary>Colony tax rate 0..1 (a linear morale penalty).</summary>
        public double TaxRate;
        /// <summary>Power shortage fraction 0 (none)..1 (total). M5.</summary>
        public double PowerShortage;
        /// <summary>Food shortage fraction 0 (none)..1 (total). M5.</summary>
        public double FoodShortage;
    }
}
