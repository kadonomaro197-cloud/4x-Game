using System;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;

namespace Pulsar4X.Stations
{
    /// <summary>
    /// Bills a station's monthly OPERATING COST to its owning faction's Ledger (Slice C — the cost curve). The
    /// station-side parallel to <see cref="Pulsar4X.Colonies.ColonyEconomyProcessor"/> (which collects tax income):
    /// this SPENDS money, and the amount rises with the station's size + function-diversity (see
    /// <see cref="StationEconomyDB.OperatingCost"/>), so a sprawling do-everything station bleeds funds — the
    /// "expensive as a planet-replacement" pressure that makes cheap-and-focused vs. big-and-costly a real decision.
    ///
    /// Keyed on <see cref="StationEconomyDB"/> (NOT StationInfoDB — StationPopulationProcessor already owns that;
    /// one hotloop per DataBlob type). Every station carries a StationEconomyDB, so this still processes all
    /// stations. Runs station-side (like ColonyEconomyProcessor) because MasterTimePulse iterates the systems where
    /// stations live. Defensive: never throws (L4), no-ops on a neutral/unowned station.
    /// </summary>
    public class StationUpkeepProcessor : IHotloopProcessor
    {
        public TimeSpan RunFrequency { get; } = TimeSpan.FromDays(30);
        public TimeSpan FirstRunOffset { get; } = TimeSpan.FromDays(30);
        public Type GetParameterType { get; } = typeof(StationEconomyDB);

        public void Init(Game game) { }

        public void ProcessEntity(Entity entity, int deltaSeconds) => BillUpkeep(entity);

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var stations = manager.GetAllEntitiesWithDataBlob<StationEconomyDB>();
            foreach (var station in stations)
                BillUpkeep(station);
            return stations.Count;
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
    }
}
