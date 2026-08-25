using System;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// Collects colony tax into the owning faction's Ledger each month (M4, docs/society/MORALE-AND-POPULATION-DESIGN.md).
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

        /// <summary>
        /// TIER 2.5 run-cost — the colony INSTALLATION UPKEEP throttle (the colony echo of StationUpkeep /
        /// GroundForceUpkeep). Default OFF → the engine suite is byte-identical (no colony is billed); NewGameMenu flips
        /// it ON for a menu game (the EnableEmploymentMorale pattern). When on, a colony's installations cost a small
        /// fraction of their build price to KEEP each month — the missing "middle" of the cradle-to-grave chain (a
        /// component costs to build and to lose, but nothing to keep).
        /// </summary>
        public static bool EnableInstallationUpkeep = false;

        /// <summary>Monthly upkeep as a fraction of an installation's build price (credits). 1% keeps the number
        /// sensible against the existing tax income; tunable in one place.</summary>
        public const decimal UpkeepRatePerMonth = 0.01m;

        /// <summary>
        /// The Civic ▸ Commerce door — a colony's MARKETS/EXCHANGES (components carrying <see cref="CommerceAtbDB"/>)
        /// earn local trade revenue each month, booked as <c>TransactionCategory.ColonyCommerce</c> income. Default OFF
        /// → the engine suite is byte-identical (no colony ships a market, and a market-less colony earns 0); NewGameMenu
        /// flips it ON for a menu game (the EnableInstallationUpkeep / EnableEmploymentMorale pattern). A NEW money source
        /// distinct from population TAX and from the standing inter-faction Trade income.
        /// </summary>
        public static bool EnableCommerceIncome = false;

        public void Init(Game game) { }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            CollectTax(entity);
            BillInstallationUpkeep(entity);
            BillCommerceIncome(entity);
        }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var colonies = manager.GetAllEntitiesWithDataBlob<ColonyEconomyDB>();
            foreach (var colony in colonies)
            {
                CollectTax(colony);
                BillInstallationUpkeep(colony);
                BillCommerceIncome(colony);
            }
            return colonies.Count;
        }

        /// <summary>The colony's total monthly commerce income = Σ over installed, enabled components of
        /// (<see cref="CommerceAtbDB.TradeValue"/> × health). Health-scaled like InstallationUpkeep, and 0
        /// (byte-identical) for a colony with no market building.</summary>
        public static decimal CommerceIncome(ComponentInstancesDB comps)
        {
            if (comps == null) return 0m;
            return (decimal)comps.GetTotalCommerce();
        }

        /// <summary>Book this colony's commerce income as monthly INCOME on the owning faction's ledger. Mirrors
        /// CollectTax / BillInstallationUpkeep exactly (defensive: capture-mutated FactionOwnerID → TryGetValue, never a
        /// hard index that would freeze the sim clock; unowned/neutral colony earns for no one).</summary>
        internal static void BillCommerceIncome(Entity colony)
        {
            if (!EnableCommerceIncome) return;
            if (!colony.TryGetDataBlob<ComponentInstancesDB>(out var comps)) return;

            decimal income = CommerceIncome(comps);
            if (income <= 0m) return;

            int factionId = colony.FactionOwnerID;
            if (factionId < 0) return;
            var game = colony.Manager?.Game;
            if (game == null) return;
            if (!game.Factions.TryGetValue(factionId, out var faction)) return;
            if (!faction.TryGetDataBlob<FactionInfoDB>(out var factionInfo)) return;

            factionInfo.Money.AddIncome(
                colony.Manager.StarSysDateTime,
                TransactionCategory.ColonyCommerce,
                $"Commerce at {colony.GetName(factionId)}",
                income);
        }

        /// <summary>The colony's total monthly installation upkeep = Σ over installed, enabled components of
        /// (build price × health × <see cref="UpkeepRatePerMonth"/>). Derived from the existing component
        /// <c>CreditCost</c> — needs NO new dial/atb (no exact-arity save landmine), and is 0 (byte-identical) for a
        /// colony whose installations have no credit cost. Health-scaled like GetTotalJobs.</summary>
        public static decimal InstallationUpkeep(ComponentInstancesDB comps)
        {
            if (comps == null) return 0m;
            decimal total = 0m;
            foreach (var byDesign in comps.GetComponentsByDesigns())
            {
                if (!comps.AllDesigns.TryGetValue(byDesign.Key, out var design)) continue;
                if (design.CreditCost <= 0) continue;
                foreach (var component in byDesign.Value)
                {
                    if (!component.IsEnabled) continue;
                    total += (decimal)design.CreditCost * (decimal)component.HealthPercent * UpkeepRatePerMonth;
                }
            }
            return total;
        }

        /// <summary>Bill this colony's installation upkeep as a monthly EXPENSE on the owning faction's ledger. Mirrors
        /// StationUpkeepProcessor.BillUpkeep (defensive: capture-mutated FactionOwnerID → TryGetValue, never a hard index
        /// that would freeze the sim clock; unowned/neutral colony pays no one).</summary>
        internal static void BillInstallationUpkeep(Entity colony)
        {
            if (!EnableInstallationUpkeep) return;
            if (!colony.TryGetDataBlob<ComponentInstancesDB>(out var comps)) return;

            decimal upkeep = InstallationUpkeep(comps);
            if (upkeep <= 0m) return;

            int factionId = colony.FactionOwnerID;
            if (factionId < 0) return;
            var game = colony.Manager?.Game;
            if (game == null) return;
            if (!game.Factions.TryGetValue(factionId, out var faction)) return;
            if (!faction.TryGetDataBlob<FactionInfoDB>(out var factionInfo)) return;

            factionInfo.Money.AddExpense(
                colony.Manager.StarSysDateTime,
                TransactionCategory.ColonyInstallationUpkeep,
                $"Installation upkeep at {colony.GetName(factionId)}",
                upkeep);
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
