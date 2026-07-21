using System;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;
using Pulsar4X.Colonies;   // ColonyMoraleDB + ColonyEconomyDB.MonthlyTaxIncome (the shared tax model)

namespace Pulsar4X.Stations
{
    /// <summary>
    /// A station's monthly NET-OPERATING PASS: it BILLS the station's operating COST (Slice C — the cost curve) and
    /// COLLECTS its operating INCOME (population tax — Operation Earthfall D2.1) to/from its owning faction's Ledger.
    /// It is the station-side parallel to the colony's two processors combined —
    /// <see cref="Pulsar4X.Colonies.ColonyEconomyProcessor"/> (income) plus its own upkeep (spend). Cost rises with the
    /// station's size + function-diversity (see <see cref="StationEconomyDB.OperatingCost"/>), so a sprawling
    /// do-everything station bleeds funds — but a POPULATED station also earns tax, so a productive colony-station
    /// (the Kithrin outpost) is no longer a guaranteed monotonic drain (A6 finding: upkeep with ZERO income = a
    /// structural bankruptcy that emptied the treasury in ~a month). Cost still exceeds income for a big idle
    /// platform (the "expensive as a planet-replacement" pressure); a well-run populated station is roughly solvent.
    ///
    /// Keyed on <see cref="StationEconomyDB"/> (NOT StationInfoDB — StationPopulationProcessor already owns that;
    /// one hotloop per DataBlob type, rule L9 — so the income pass FOLDS INTO this existing processor rather than
    /// standing up a second hotloop on a station blob). Every station carries a StationEconomyDB, so this still
    /// processes all stations. Runs station-side (like ColonyEconomyProcessor) because MasterTimePulse iterates the
    /// systems where stations live. Defensive: never throws (L4), no-ops on a neutral/unowned station.
    /// </summary>
    public class StationUpkeepProcessor : IHotloopProcessor
    {
        public TimeSpan RunFrequency { get; } = TimeSpan.FromDays(30);
        public TimeSpan FirstRunOffset { get; } = TimeSpan.FromDays(30);
        public Type GetParameterType { get; } = typeof(StationEconomyDB);

        public void Init(Game game) { }

        public void ProcessEntity(Entity entity, int deltaSeconds) => NetOperatingPass(entity);

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var stations = manager.GetAllEntitiesWithDataBlob<StationEconomyDB>();
            foreach (var station in stations)
                NetOperatingPass(station);
            return stations.Count;
        }

        /// <summary>Bill the station's upkeep AND collect its income in one monthly pass (the net-operating pass).</summary>
        internal static void NetOperatingPass(Entity station)
        {
            BillUpkeep(station);
            CollectIncome(station);
        }

        internal static void BillUpkeep(Entity station)
        {
            if (!station.TryGetDataBlob<StationEconomyDB>(out var econ)) return;

            decimal cost = StationEconomyDB.OperatingCost(station);
            econ.LastOperatingCost = cost; // cache for the UI readout, even if we don't bill (unowned)
            if (cost <= 0m) return;

            var game = station.Manager?.Game;
            if (game == null) return;

            int factionId = station.FactionOwnerID;
            if (factionId < 0) return; // a neutral / unowned station bills no one

            if (!game.Factions.TryGetValue(factionId, out var faction)) return;
            if (!faction.TryGetDataBlob<FactionInfoDB>(out var factionInfo)) return;

            factionInfo.Money.AddExpense(
                station.Manager.StarSysDateTime,
                TransactionCategory.StationUpkeep,
                $"Upkeep for {station.GetName(factionId)}",
                cost);
        }

        /// <summary>
        /// The INCOME half of the net-operating pass (Operation Earthfall D2.1): collect a POPULATED station's monthly
        /// tax into the owning faction's Ledger — the station echo of <see cref="Pulsar4X.Colonies.ColonyEconomyProcessor.CollectTax"/>.
        /// Uses the SHARED colony tax model (<see cref="StationEconomyDB.MonthlyIncome"/> → <see cref="ColonyEconomyDB.MonthlyTaxIncome"/>):
        /// population × the station's <see cref="StationEconomyDB.TaxRate"/> × a morale multiplier, with the regime's
        /// <c>GovernmentDB.TaxCeiling</c> capping the effective rate (exact parity with the colony processor).
        /// Defensive/no-throw (L4); an UNMANNED station (pop 0) yields 0 → this is inert for every unmanned station, so
        /// the only station that earns is a populated one (new data). Booked under its own
        /// <see cref="TransactionCategory.StationIncome"/> category (the cross-lane REQUEST that asked CORE to add one is
        /// now applied — it previously borrowed <c>ColonyTax</c>, which conflated station income with real colony tax).
        /// </summary>
        internal static void CollectIncome(Entity station)
        {
            if (!station.TryGetDataBlob<StationInfoDB>(out var info)) return;
            if (!station.TryGetDataBlob<StationEconomyDB>(out var econ)) return;

            long population = 0;
            foreach (var kv in info.Population)
                population += kv.Value;
            if (population <= 0) { econ.LastOperatingIncome = 0m; return; } // unmanned → no tax base, no income

            double morale = ColonyMoraleDB.Neutral;
            if (station.TryGetDataBlob<ColonyMoraleDB>(out var moraleDB))
                morale = moraleDB.Morale;

            // Government MODULATOR (parity with ColonyEconomyProcessor): the regime's TaxCeiling caps the billed rate.
            double ceiling = GovernmentTools.OwnerOf(station).TaxCeiling();
            double effectiveTaxRate = econ.TaxRate < ceiling ? econ.TaxRate : ceiling;

            decimal income = ColonyEconomyDB.MonthlyTaxIncome(population, effectiveTaxRate, morale);
            econ.LastOperatingIncome = income; // cache the billed figure for the UI readout
            if (income <= 0m) return;

            var game = station.Manager?.Game;
            if (game == null) return;

            int factionId = station.FactionOwnerID;
            if (factionId < 0) return; // a neutral / unowned station pays tax to no one

            // Defensive TryGetValue (parity with ColonyEconomyProcessor): FactionOwnerID is MUTATED by capture, so it
            // can name a faction absent from the dictionary; a hard index would throw on the sim thread (L4).
            if (!game.Factions.TryGetValue(factionId, out var faction)) return;
            if (!faction.TryGetDataBlob<FactionInfoDB>(out var factionInfo)) return;

            factionInfo.Money.AddIncome(
                station.Manager.StarSysDateTime,
                TransactionCategory.StationIncome, // dedicated category (was borrowing ColonyTax) — LANE-DEV-NOTES request
                $"Station tax from {station.GetName(factionId)} ({effectiveTaxRate:P0})",
                income);
        }
    }
}
