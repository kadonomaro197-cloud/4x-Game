using Newtonsoft.Json;
using System;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// The colony's economic lever: a TAX RATE the player sets (M4, docs/MORALE-AND-POPULATION-DESIGN.md).
    /// Higher tax = more money for the faction, but lower morale — and a happy colony tolerates more tax before
    /// it bites, so there's a moving "happy-medium" equilibrium. The tax also feeds the morale loop: it's read
    /// by PopulationProcessor as a morale input, while ColonyEconomyProcessor reads morale to scale the income
    /// (a one-tick-lagged loop — each processor reads the other's state on its own tick, so it settles instead
    /// of oscillating).
    ///
    /// All coefficients are NAMED CONSTANTS (government-ready): a future GovernmentDB sets the tax ceiling and
    /// how hard tax bites morale per regime (democracy = low ceiling, morale-sensitive; dictatorship = extract
    /// anyway). See docs/GOVERNMENT-AND-POLITICS-DESIGN.md.
    /// </summary>
    public class ColonyEconomyDB : BaseDataBlob
    {
        /// <summary>Credits produced per person per month at full (neutral-morale) productivity, before tax.</summary>
        public const double PerCapitaTaxBase = 0.01;

        /// <summary>
        /// Player-set tax rate, 0.0 (none) .. 1.0 (total). The lever. Defaults to 0 — a new colony is UNTAXED
        /// until the player (or a governor) sets a rate, so founding a colony never silently dents its morale.
        /// </summary>
        [JsonProperty] public double TaxRate { get; set; } = 0.0;

        public ColonyEconomyDB() { }

        public ColonyEconomyDB(ColonyEconomyDB other)
        {
            TaxRate = other.TaxRate;
        }

        public override object Clone() => new ColonyEconomyDB(this);

        /// <summary>
        /// Pure: monthly tax income from a colony, scaled by morale (a happy colony pays more willingly). At
        /// neutral morale the multiplier is 1.0; it rises toward 2.0 at max morale and falls to 0 at zero.
        /// </summary>
        public static decimal MonthlyTaxIncome(long population, double taxRate, double morale)
        {
            if (population <= 0 || taxRate <= 0.0) return 0m;
            double moraleMult = morale / ColonyMoraleDB.Neutral; // 1.0 at neutral (50)
            if (moraleMult < 0.0) moraleMult = 0.0;
            double income = population * PerCapitaTaxBase * taxRate * moraleMult;
            return (decimal)income;
        }
    }
}
