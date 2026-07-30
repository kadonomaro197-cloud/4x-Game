using System;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Energy;
using Pulsar4X.Stations;
using Pulsar4X.Extensions;   // GetTotalFoodOutput (food supply) extension on ComponentInstancesDB

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// Recomputes each province's POWER &amp; FOOD shortage every month (M5b, docs/society/MORALE-AND-POPULATION-DESIGN.md).
    /// The live-wiring of <see cref="ColonySustenanceDB"/>: shortage = demand (population × per-capita coefficient)
    /// vs supply (power from an <see cref="EnergyGenAbilityDB"/> if attached; food from installed farms <b>plus
    /// SHIPPED food drawn from the host's cargo</b>). <see cref="PopulationProcessor"/> then reads the shortages into
    /// morale and a starvation death term.
    ///
    /// <para>⚠ <b>This comment used to read "food from the — not-yet-existing — food cargo good, so 0 for now."</b>
    /// That sentence was true for a long time and it was the whole gap: food was an installation OUTPUT read off
    /// installed components, so it was grown and eaten in the same place and <b>could never be shipped</b> — a colony
    /// that could not farm could never be supplied by one that could, which removes the most classic reason a supply
    /// line exists. The <c>food</c> good now exists (<c>materials.json</c>, riding <c>perishable-storage</c>) and
    /// <see cref="DrawImportedFood"/> consumes it. <b>The apology in this comment was the specification.</b></para>
    ///
    /// NEUTRAL-WHEN-ABSENT: the per-capita demand coefficients default to 0, so every shortage computes to 0 until
    /// the numbers are set on the local build — no colony is starved or browned-out on New Game. Keyed on its own
    /// blob (<see cref="ColonySustenanceDB"/>) per the one-hotloop-per-blob rule. Host-agnostic (colonies +
    /// stations). Defensive — never throws (a throwing hotloop crashes the game loop).
    /// </summary>
    public class SustenanceProcessor : IHotloopProcessor
    {
        public TimeSpan RunFrequency { get; } = TimeSpan.FromDays(30);
        public TimeSpan FirstRunOffset { get; } = TimeSpan.FromDays(30);
        public Type GetParameterType { get; } = typeof(ColonySustenanceDB);

        public void Init(Game game) { }

        public void ProcessEntity(Entity entity, int deltaSeconds) => Recalc(entity);

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var provinces = manager.GetAllEntitiesWithDataBlob<ColonySustenanceDB>();
            foreach (var province in provinces)
                Recalc(province);
            return provinces.Count;
        }

        /// <summary>Recompute one province's power/food shortage from demand vs supply. No-ops safely if the blob
        /// is missing; inert (0 shortage) while the demand coefficients are at their 0 default.</summary>
        internal static void Recalc(Entity province)
        {
            if (!province.TryGetDataBlob<ColonySustenanceDB>(out var sust)) return;

            long pop = PopulationOf(province);

            // Power: demand = pop × per-capita; supply = the host's own generation (0 if it has no reactor/solar).
            double powerDemand = pop * sust.PerCapitaPowerDemand;
            double powerSupply = province.TryGetDataBlob<EnergyGenAbilityDB>(out var egen) ? egen.TotalOutputMax : 0.0;
            sust.PowerShortage = ColonySustenanceDB.Shortage(powerDemand, powerSupply);

            // Food (M5c): demand = pop × per-capita; supply = the host's installed FOOD PRODUCTION components
            // (agri-domes / hydroponics carrying FoodProductionAtbDB), health-scaled. Was hardcoded 0 — which made ANY
            // food demand an unwinnable 100% shortage. Now a colony that builds enough food output ends the shortage.
            double foodDemand = pop * sust.PerCapitaFoodDemand;
            double foodSupply = province.TryGetDataBlob<Pulsar4X.Datablobs.ComponentInstancesDB>(out var comps)
                ? comps.GetTotalFoodOutput() : 0.0;

            // …and SHIPPED food now counts, which is what this doc-comment used to apologise for ("food from the —
            // not-yet-existing — food cargo good, so 0 for now"). The `food` good exists as of 2026-07-30, so a colony
            // that cannot farm can be SUPPLIED by one that can — the reason a supply line exists at all.
            foodSupply += DrawImportedFood(province, foodDemand, foodSupply);

            sust.FoodShortage = ColonySustenanceDB.Shortage(foodDemand, foodSupply);
        }

        /// <summary>The refined good a stockpile is drawn from. Matches <c>materials.json</c>.</summary>
        internal const string FoodGoodID = "food";

        /// <summary>
        /// Draw stored food to cover whatever the local farms cannot, and <b>consume it</b> — returning the extra supply
        /// as a PER-DAY rate so it is dimensionally the same thing as <see cref="Pulsar4X.Colonies.FoodProductionAtbDB"/>
        /// output and as <c>pop × PerCapitaFoodDemand</c>.
        ///
        /// <para><b>The units matter here and they have bitten this campaign before</b> (the reactor `Lifetime` bug was
        /// exactly this shape): demand and farm output are <b>per day</b>, a stockpile is a <b>quantity</b>. This
        /// processor runs <b>monthly</b>, so one call may draw up to 30 days of shortfall, and the amount drawn is
        /// converted back to a daily rate before it is returned. Adding a raw stockpile to a rate would have made a
        /// single crate of rations look like an infinite farm.</para>
        ///
        /// <para><b>It DEPLETES.</b> A supply that is read but never consumed is free food — the "pretty" failure this
        /// designer campaign exists to remove — so the drawn amount is removed from the host's cargo.</para>
        ///
        /// <para><b>Byte-identical on a stock game, for a structural reason:</b> <c>PerCapitaFoodDemand</c> defaults to
        /// <c>0</c>, so <c>foodDemand</c> is 0, so the shortfall is 0 and nothing is ever drawn or consumed until the
        /// demand coefficients are set. Also a clean no-op for a host with no cargo hold, no food good in the mod, or
        /// an empty larder. Never throws — a throwing hotloop kills the game clock (landmine L4).</para>
        /// </summary>
        private static double DrawImportedFood(Entity province, double foodDemand, double localSupply)
        {
            double shortfallPerDay = foodDemand - localSupply;
            if (shortfallPerDay <= 0) return 0.0;                       // farms cover it — nothing to import
            if (!province.TryGetDataBlob<Pulsar4X.Storage.CargoStorageDB>(out var hold)) return 0.0;

            try
            {
                // ⚠ Entity.GetFactionOwner is `Manager.Game.Factions[FactionOwnerID]` — an UNGUARDED dictionary index,
                // so it throws for an unowned or detached entity. This is a hotloop and a throw here kills the game
                // clock (landmine L4), which is why the whole lookup sits inside a catch rather than a null check.
                var owner = province.GetFactionOwner;
                if (owner == null || !owner.TryGetDataBlob<Pulsar4X.Factions.FactionInfoDB>(out var fi)) return 0.0;
                var food = fi.Data?.CargoGoods?.GetAny(FoodGoodID);
                if (food == null) return 0.0;                           // a mod without the food good: unchanged

                const double days = RunFrequencyDays;
                long stored = hold.GetUnitsStored(food, false);
                if (stored <= 0) return 0.0;

                // Draw at most this month's shortfall, and at most what is actually in the larder.
                long draw = (long)Math.Min(stored, Math.Ceiling(shortfallPerDay * days));
                if (draw <= 0) return 0.0;

                // Consume it. int is the API's unit type; a larger draw is clamped rather than overflowed.
                int drawInt = draw > int.MaxValue ? int.MaxValue : (int)draw;
                Pulsar4X.Storage.CargoTransferProcessor.RemoveCargoItems(province, food, drawInt);

                return drawInt / days;                                  // back to a per-day rate
            }
            catch
            {
                return 0.0;   // no import this month rather than a dead clock
            }
        }

        /// <summary>Days between runs — kept beside <see cref="RunFrequency"/> so the two cannot drift apart.</summary>
        private const double RunFrequencyDays = 30.0;

        private static long PopulationOf(Entity province)
        {
            long pop = 0;
            if (province.TryGetDataBlob<ColonyInfoDB>(out var ci))
                foreach (var kvp in ci.Population) pop += kvp.Value;
            else if (province.TryGetDataBlob<StationInfoDB>(out var si))
                foreach (var kvp in si.Population) pop += kvp.Value;
            return pop;
        }
    }
}
