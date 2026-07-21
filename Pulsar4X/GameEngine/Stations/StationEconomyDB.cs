using Newtonsoft.Json;
using System;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Colonies;   // ColonyMoraleDB (morale mult) + ColonyEconomyDB.MonthlyTaxIncome (the shared tax model)

namespace Pulsar4X.Stations
{
    /// <summary>
    /// The station's OPERATING-COST side (Slice C — the cost curve) — the parallel to
    /// <see cref="Pulsar4X.Colonies.ColonyEconomyDB"/> (which is the income/tax side). A station bills a monthly
    /// upkeep to its owning faction that RISES with the station's size AND its function-diversity — the mechanical
    /// half of the design's cost gradient: "cheap while focused, expensive as a planet-replacement"
    /// (docs/SPACE-STATIONS-DESIGN.md). A one-job mining post costs little; a station that mines AND refines AND
    /// researches AND houses people pays the escalating per-function cost. That drain is the DECISION — spread cheap
    /// focused stations vs. pile everything onto one expensive platform.
    ///
    /// It ALSO carries the station's INCOME side (Operation Earthfall D2.1): a per-station <see cref="TaxRate"/> and the
    /// pure <see cref="MonthlyIncome"/> (population tax through the shared colony model), so <see cref="StationUpkeepProcessor"/>
    /// runs a NET-OPERATING pass — a populated station now EARNS as well as costs, ending the A6 structural bankruptcy
    /// (upkeep with zero income).
    ///
    /// This blob exists mainly to be the hotloop KEY for <see cref="StationUpkeepProcessor"/> (one hotloop per
    /// DataBlob type — <see cref="StationPopulationProcessor"/> already owns <see cref="StationInfoDB"/>, exactly the
    /// same reason ColonyEconomyProcessor is keyed on ColonyEconomyDB and not ColonyInfoDB). It also caches the last
    /// billed cost AND income for the UI readout.
    /// </summary>
    public class StationEconomyDB : BaseDataBlob
    {
        // PLACEHOLDER cost-gradient coefficients (Slice C, 2026-07-03) — credits per month. Tune when the cost
        // curve is locked (a currently-open design question). PerFunctionUpkeep is deliberately the STEEP term:
        // it's charged per DISTINCT module design, so accumulating different FUNCTIONS (not just more of the same)
        // is what makes a station expensive — the "expensive as a planet-replacement" end of the gradient.
        public const decimal BaseUpkeep = 10m;           // keeping the lights on, even on a bare platform
        public const decimal PerModuleUpkeep = 5m;       // each installed module (raw size)
        public const decimal PerFunctionUpkeep = 25m;    // each DISTINCT module design (function-diversity — the gradient)
        public const decimal PerPopUpkeepPer1000 = 1m;   // crew / habitat overhead, per 1000 people

        /// <summary>
        /// The station's default per-capita tax rate — the INCOME side of the net-operating pass (Operation Earthfall
        /// D2.1). A station with no strain node (the Kithrin outpost) still needs a tax base or it bleeds upkeep with
        /// zero income (A6 finding: ~6,880/mo billed, income ZERO → structural bankruptcy). This is a MODEST default
        /// (the developer sets balance): the UMF's authored war-strain colony tax is 0.30; a station defaults to half
        /// that. A scenario or a governor can override <see cref="TaxRate"/> per-station later.
        /// </summary>
        // FLAGGED balance value
        public const double DefaultStationTaxRate = 0.15;

        /// <summary>The most recent monthly operating cost billed — for the UI readout. Not gameplay state.</summary>
        [JsonProperty]
        public decimal LastOperatingCost { get; internal set; } = 0m;

        /// <summary>The most recent monthly OPERATING INCOME collected (station tax) — for the UI readout. Not gameplay state.</summary>
        [JsonProperty]
        public decimal LastOperatingIncome { get; internal set; } = 0m;

        /// <summary>
        /// The station's tax rate, 0.0 (none) .. 1.0 (total) — the income lever, parallel to
        /// <see cref="ColonyEconomyDB.TaxRate"/>. Defaults to the modest <see cref="DefaultStationTaxRate"/> (a colony
        /// defaults to UNTAXED, but a station has no strain node to seed a rate, so a nonzero default is what keeps a
        /// populated station solvent instead of a guaranteed drain). The billed rate is capped by the regime's
        /// <c>GovernmentDB.TaxCeiling</c> at collection time (mirrors <see cref="Pulsar4X.Colonies.ColonyEconomyProcessor"/>).
        /// </summary>
        [JsonProperty] public double TaxRate { get; set; } = DefaultStationTaxRate;

        public StationEconomyDB() { }

        public StationEconomyDB(StationEconomyDB other)
        {
            LastOperatingCost = other.LastOperatingCost;
            LastOperatingIncome = other.LastOperatingIncome;
            TaxRate = other.TaxRate;
        }

        public override object Clone()
        {
            return new StationEconomyDB(this);
        }

        /// <summary>
        /// The placeholder operating-cost formula. Pure (reads the station's installed modules + population), so it
        /// can be shown in the UI as a preview and billed by the processor from one source of truth.
        /// </summary>
        public static decimal OperatingCost(Entity station)
        {
            decimal cost = BaseUpkeep;

            if (station.TryGetDataBlob<ComponentInstancesDB>(out var comps))
            {
                cost += PerModuleUpkeep * comps.AllComponents.Count;    // total modules (size)
                cost += PerFunctionUpkeep * comps.AllDesigns.Count;     // distinct designs (function-diversity — the gradient)
            }

            if (station.TryGetDataBlob<StationInfoDB>(out var info))
            {
                long pop = 0;
                foreach (var kv in info.Population)
                    pop += kv.Value;
                cost += PerPopUpkeepPer1000 * (decimal)(pop / 1000.0);
            }

            return cost;
        }

        /// <summary>
        /// The INCOME side of the net-operating pass (Operation Earthfall D2.1): a populated station's monthly tax,
        /// through the EXISTING colony tax model (<see cref="ColonyEconomyDB.MonthlyTaxIncome"/>) — population × the
        /// station <see cref="TaxRate"/> × a morale multiplier (a content station pays more willingly). Pure (reads the
        /// station's population + morale), so it can be shown in the UI as a preview and collected by the processor
        /// from one source of truth. An UNMANNED station (pop 0) yields 0 — no tax base — which is what keeps this
        /// inert for every unmanned station in the test suite. The regime's tax CEILING is applied at billing time by
        /// <see cref="StationUpkeepProcessor"/> (this preview is pre-cap, exactly like <see cref="OperatingCost"/> is
        /// regime-unaware); at the default rate (below the Mid 0.5 ceiling) the two agree.
        /// </summary>
        public static decimal MonthlyIncome(Entity station)
        {
            if (!station.TryGetDataBlob<StationInfoDB>(out var info)) return 0m;

            long pop = 0;
            foreach (var kv in info.Population)
                pop += kv.Value;
            if (pop <= 0) return 0m;   // unmanned → no tax base

            double taxRate = station.TryGetDataBlob<StationEconomyDB>(out var econ) ? econ.TaxRate : DefaultStationTaxRate;

            double morale = ColonyMoraleDB.Neutral;
            if (station.TryGetDataBlob<ColonyMoraleDB>(out var moraleDB))
                morale = moraleDB.Morale;

            return ColonyEconomyDB.MonthlyTaxIncome(pop, taxRate, morale);
        }
    }
}
