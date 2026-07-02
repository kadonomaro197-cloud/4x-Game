using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// A province's POWER &amp; FOOD sustenance gauges (M5b, docs/MORALE-AND-POPULATION-DESIGN.md): the computed
    /// <see cref="PowerShortage"/> / <see cref="FoodShortage"/> (0..1) that feed morale — a brownout sours people,
    /// starvation kills them. <see cref="SustenanceProcessor"/> recomputes them each cycle from demand vs supply;
    /// <see cref="PopulationProcessor"/> reads them into morale and applies a starvation death term.
    ///
    /// Built NEUTRAL-WHEN-ABSENT on purpose: the per-capita demand coefficients DEFAULT TO 0, so a colony has NO
    /// shortage (and no morale hit, no deaths) until the numbers are calibrated on the developer's local build.
    /// This deliberately avoids the "a default deficit tanks every colony" trap the design doc warns about — the
    /// WIRING is here and CI-green; the demand rates + a food-supply cargo good are the local calibration (the food
    /// good doesn't exist yet, so food supply reads 0 — harmless while food demand is 0).
    /// </summary>
    public class ColonySustenanceDB : BaseDataBlob
    {
        /// <summary>Per-capita monthly power demand. DEFAULT 0 = inert (no shortage) until set on the local build.</summary>
        [JsonProperty] public double PerCapitaPowerDemand { get; internal set; } = 0.0;
        /// <summary>Per-capita monthly food demand. DEFAULT 0 = inert until set on the local build.</summary>
        [JsonProperty] public double PerCapitaFoodDemand { get; internal set; } = 0.0;

        /// <summary>Computed each cycle: fraction of power demand unmet (0 = fully powered, 1 = total blackout).</summary>
        [JsonProperty] public double PowerShortage { get; internal set; } = 0.0;
        /// <summary>Computed each cycle: fraction of food demand unmet (0 = fed, 1 = total famine).</summary>
        [JsonProperty] public double FoodShortage { get; internal set; } = 0.0;

        /// <summary>Max fraction of population that starves to death per month at TOTAL food shortage.</summary>
        public const double MaxStarvationDeathRate = 0.10;

        /// <summary>
        /// Set the per-capita power &amp; food demand directly (both are normally calibrated coefficients with
        /// internal setters, so a value can't be poked in from another assembly — e.g. the client). This is the
        /// lever the DevTools "Sustenance levers" panel uses to switch the M5b shortage→morale wiring ON for a
        /// colony during a play-test (the wiring ships neutral/inert — demand defaults to 0). Negatives floor at 0.
        /// </summary>
        public void SetDemand(double perCapitaPower, double perCapitaFood)
        {
            PerCapitaPowerDemand = perCapitaPower < 0.0 ? 0.0 : perCapitaPower;
            PerCapitaFoodDemand = perCapitaFood < 0.0 ? 0.0 : perCapitaFood;
        }

        public ColonySustenanceDB() { }

        public ColonySustenanceDB(ColonySustenanceDB other)
        {
            PerCapitaPowerDemand = other.PerCapitaPowerDemand;
            PerCapitaFoodDemand = other.PerCapitaFoodDemand;
            PowerShortage = other.PowerShortage;
            FoodShortage = other.FoodShortage;
        }

        public override object Clone() => new ColonySustenanceDB(this);

        /// <summary>Shortage fraction from demand vs supply, clamped 0..1. Returns 0 when there's no demand — the
        /// neutral-safe default (no demand configured → never a shortage).</summary>
        public static double Shortage(double demand, double supply)
        {
            if (demand <= 0.0) return 0.0;
            double s = (demand - supply) / demand;
            return s < 0.0 ? 0.0 : (s > 1.0 ? 1.0 : s);
        }

        /// <summary>Fraction of population lost to starvation this month — 0 unless food shortage is real. Scales
        /// linearly with the shortage up to <see cref="MaxStarvationDeathRate"/> at total famine.</summary>
        public static double StarvationDeathRate(double foodShortage)
        {
            double f = foodShortage < 0.0 ? 0.0 : (foodShortage > 1.0 ? 1.0 : foodShortage);
            return f * MaxStarvationDeathRate;
        }
    }
}
