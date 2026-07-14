using System;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// Collects colony tax into the owning faction's Ledger each month (M4, docs/MORALE-AND-POPULATION-DESIGN.md).
    /// Income scales with population, the player-set tax rate (<see cref="ColonyEconomyDB.TaxRate"/>), and morale
    /// (a happy colony pays more willingly). This is the lever that finally plugs the colony economy into faction
    /// money — until now only research moved funds.
    ///
    /// NOTE: keyed on <see cref="ColonyEconomyDB"/>, NOT ColonyInfoDB — hotloop processors are registered one
    /// per DataBlob type, and PopulationProcessor already owns ColonyInfoDB. Every colony carries a
    /// ColonyEconomyDB, so this still processes all colonies. It runs colony-side (not faction-side) because
    /// MasterTimePulse never iterates the GlobalManager where faction entities live.
    /// </summary>
    public class ColonyEconomyProcessor : IHotloopProcessor
    {
        public TimeSpan RunFrequency { get; } = TimeSpan.FromDays(30);
        public TimeSpan FirstRunOffset { get; } = TimeSpan.FromDays(30);
        public Type GetParameterType { get; } = typeof(ColonyEconomyDB);

        public void Init(Game game) { }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            CollectTax(entity);
        }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var colonies = manager.GetAllEntitiesWithDataBlob<ColonyEconomyDB>();
            foreach (var colony in colonies)
                CollectTax(colony);
            return colonies.Count;
        }

        internal static void CollectTax(Entity colony)
        {
            if (!colony.TryGetDataBlob<ColonyEconomyDB>(out var econ)) return;
            if (!colony.TryGetDataBlob<ColonyInfoDB>(out var colonyInfo)) return;

            long population = 0;
            foreach (var kvp in colonyInfo.Population)
                population += kvp.Value;
            if (population <= 0) return;

            double morale = ColonyMoraleDB.Neutral;
            if (colony.TryGetDataBlob<ColonyMoraleDB>(out var moraleDB))
                morale = moraleDB.Morale;

            // Government MODULATOR (#30): the regime's TaxCeiling caps how hard you can tax (authority raises it).
            // Default Mid ceiling is 0.5; the start tax is 0, so this is inert until a player taxes past the cap.
            double ceiling = GovernmentTools.OwnerOf(colony).TaxCeiling();
            double effectiveTaxRate = econ.TaxRate < ceiling ? econ.TaxRate : ceiling;

            decimal income = ColonyEconomyDB.MonthlyTaxIncome(population, effectiveTaxRate, morale);
            if (income <= 0m) return;

            var game = colony.Manager?.Game;
            if (game == null) return;
            int factionId = colony.FactionOwnerID;
            if (factionId < 0) return; // neutral / unowned colonies pay no tax to anyone

            // Defensive (the mining time-stall class + parity with StationUpkeepProcessor/LegitimacyProcessor, which
            // already TryGetValue here): FactionOwnerID is MUTATED by colony capture, so it can name a faction that
            // isn't in the dictionary. A hard index would throw on the sim thread and freeze the clock.
            if (!game.Factions.TryGetValue(factionId, out var faction)) return;
            if (!faction.TryGetDataBlob<FactionInfoDB>(out var factionInfo)) return;

            factionInfo.Money.AddIncome(
                colony.Manager.StarSysDateTime,
                TransactionCategory.ColonyTax,
                $"Tax from {colony.GetName(factionId)} ({effectiveTaxRate:P0})",
                income);
        }
    }
}
