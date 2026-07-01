using System;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Energy;
using Pulsar4X.Stations;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// Recomputes each province's POWER &amp; FOOD shortage every month (M5b, docs/MORALE-AND-POPULATION-DESIGN.md).
    /// The live-wiring of <see cref="ColonySustenanceDB"/>: shortage = demand (population × per-capita coefficient)
    /// vs supply (power from an <see cref="EnergyGenAbilityDB"/> if attached; food from the — not-yet-existing —
    /// food cargo good, so 0 for now). <see cref="PopulationProcessor"/> then reads the shortages into morale and a
    /// starvation death term.
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

            // Food: demand = pop × per-capita; supply = the food cargo good (not yet defined → 0). Harmless while
            // food demand is 0; the food good + supply read is the local follow-up.
            double foodDemand = pop * sust.PerCapitaFoodDemand;
            double foodSupply = 0.0;
            sust.FoodShortage = ColonySustenanceDB.Shortage(foodDemand, foodSupply);
        }

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
