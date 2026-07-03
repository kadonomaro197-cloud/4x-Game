using Newtonsoft.Json;
using System;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

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
    /// This blob exists mainly to be the hotloop KEY for <see cref="StationUpkeepProcessor"/> (one hotloop per
    /// DataBlob type — <see cref="StationPopulationProcessor"/> already owns <see cref="StationInfoDB"/>, exactly the
    /// same reason ColonyEconomyProcessor is keyed on ColonyEconomyDB and not ColonyInfoDB). It also caches the last
    /// billed figure for the UI readout.
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

        /// <summary>The most recent monthly operating cost billed — for the UI readout. Not gameplay state.</summary>
        [JsonProperty]
        public decimal LastOperatingCost { get; internal set; } = 0m;

        public StationEconomyDB() { }

        public StationEconomyDB(StationEconomyDB other)
        {
            LastOperatingCost = other.LastOperatingCost;
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
    }
}
