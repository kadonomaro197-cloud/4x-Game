using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Events;
using Pulsar4X.Extensions;
using Pulsar4X.Interfaces;
using Pulsar4X.People;


namespace Pulsar4X.Colonies
{
    public class PopulationProcessor : IHotloopProcessor
    {
        public TimeSpan RunFrequency { get; } = TimeSpan.FromDays(30);
        public TimeSpan FirstRunOffset { get; } = TimeSpan.FromDays(30);
        public Type GetParameterType { get; } = typeof(ColonyInfoDB);

        internal void GrowPopulation(Entity colony)
        {
            // Get current population
            var colonyInfoDB = colony.GetDataBlob<ColonyInfoDB>();
            var currentPopulation = colonyInfoDB.Population;
            var instancesDB = colony.GetDataBlob<ComponentInstancesDB>();
            long popSupportValue = instancesDB.GetPopulationSupportValue(colonyInfoDB.PlanetEntity);

            long needsSupport = 0;
            long totalPop = 0;
            double worstColonyCost = 0.0;
            foreach (var (id, value) in currentPopulation)
            {

                var species = colony.Manager.GetGlobalEntityById(id).GetDataBlob<SpeciesDB>();
                double cc = species.ColonyCost(colonyInfoDB.PlanetEntity);
                // count the number of different population groups that need infrastructure support
                if (cc > 0.0)
                    needsSupport++;
                if (cc > worstColonyCost)
                    worstColonyCost = cc;
                totalPop += value;
            }

            // --- M1 morale (the population "tank" valve, docs/MORALE-AND-POPULATION-DESIGN.md) ---
            // Recompute morale from the inputs that already exist (conditions + overcrowding) and turn it into
            // a migration rate added to growth below. Guarded: a colony without a ColonyMoraleDB (e.g. built by
            // an older path) just skips morale and grows as before.
            double migration = 0.0;
            if (colony.TryGetDataBlob<ColonyMoraleDB>(out var moraleDB))
            {
                double crowdingRatio = 0.0;
                if (worstColonyCost > 0.0) // only support-capped (hostile) worlds can overcrowd
                {
                    long needs = needsSupport < 1 ? 1 : needsSupport;
                    double capacity = ((double)popSupportValue / needs) / worstColonyCost;
                    crowdingRatio = capacity > 0.0 ? totalPop / capacity : 2.0;
                }

                // M2 employment + M3 fix: jobs are measured against the WORKFORCE (the drawable fraction of
                // population), not raw headcount — a 500M homeworld isn't "employed" by a handful of
                // installations. Two-sided; a colony with no installation declaring jobs has "no job data"
                // → neutral employment (sentinel -1), not 100% unemployment. Housing comfort is a bonus.
                long jobs = instancesDB.GetTotalJobs();
                long workforce = ColonyManpowerDB.Workforce(totalPop);
                double employmentRatio = (jobs > 0 && workforce > 0) ? (double)jobs / workforce : -1.0;
                double comfort = instancesDB.GetHousingComfort();

                moraleDB.Morale = ColonyMoraleDB.ComputeMorale(worstColonyCost, crowdingRatio, employmentRatio, comfort, moraleDB.Factors);
                migration = ColonyMoraleDB.MigrationRate(moraleDB.Morale);
            }

            // find colony cost, divide the population support value by it
            foreach (var (id, value) in currentPopulation.ToArray())
            {
                var species = colony.Manager.GetGlobalEntityById(id).GetDataBlob<SpeciesDB>();
                double colonyCost = species.ColonyCost(colony.GetDataBlob<ColonyInfoDB>().PlanetEntity);
                long maxPopulation;
                double growthRate;
                long newPop;

                if (colonyCost > 0.0)
                {
                    maxPopulation = (long)((double)(popSupportValue / needsSupport) / colonyCost) ;
                    if (currentPopulation[id] > maxPopulation) // People will start dying
                    {
                        long excessPopulation = currentPopulation[id] - maxPopulation;
                        // @todo: figure out better formula
                        growthRate = -50.0;
                        newPop = (long)(value * (1.0 + growthRate));
                        if (newPop < 0)
                            newPop = 0;
                        UpdatePopulation(colonyInfoDB, currentPopulation, id, newPop);
                    }
                    else
                    {
                        // Colony Growth Rate = 20 / (CurrentPop ^ (1 / 3))
                        // Capped at 10% before modifiers for planetary and sector governors, also affected by radiation
                        growthRate = (20.0 / (Math.Pow(value, (1.0 / 3.0))));
                        if (growthRate > 10.0)
                            growthRate = 10.0;
                        // external factor: morale-driven migration (M1)
                        newPop = (long)(value * (1.0 + growthRate + migration));
                        if (newPop > maxPopulation)
                            newPop = maxPopulation;
                        if (newPop < 0)
                            newPop = 0;
                        UpdatePopulation(colonyInfoDB, currentPopulation, id, newPop);
                    }
                }
                else
                {
                    // Colony Growth Rate = 20 / (CurrentPop ^ (1 / 3))
                    // Capped at 10% before modifiers for planetary and sector governors, also affected by radiation
                    growthRate = (20.0 / (Math.Pow(value, (1.0 / 3.0))));
                    if (growthRate > 10.0)
                        growthRate = 10.0;
                    // external factor: morale-driven migration (M1)
                    newPop = (long)(value * (1.0 + growthRate + migration));
                    if (newPop < 0)
                        newPop = 0;
                    UpdatePopulation(colonyInfoDB, currentPopulation, id, newPop);
                }
            }
        }

        internal void ReCalcMaxPopulation(Entity colonyEntity)
        {
            var infrastructure = new List<Entity>();
            var instancesDB = colonyEntity.GetDataBlob<ComponentInstancesDB>();

            //List<KeyValuePair<Entity, PrIwObsList<Entity>>> infrastructureEntities = instancesDB.ComponentsByDesign.GetInternalDictionary().Where(item => item.Key.HasDataBlob<PopulationSupportAtbDB>()).ToList();

            long totalMaxPop = instancesDB.GetPopulationSupportValue(colonyEntity.GetDataBlob<ColonyInfoDB>().PlanetEntity);

            colonyEntity.GetDataBlob<ColonyLifeSupportDB>().MaxPopulation = totalMaxPop;
        }

        private void UpdatePopulation(ColonyInfoDB colony ,Dictionary<int, long> population, int id, long newPopulation)
        {
            population[id] = newPopulation;
            
            EventManager.Instance.Publish(
                Event.Create(
                    EventType.PopulationChanged,
                    colony.OwningEntity.StarSysDateTime,
                    $"{colony.OwningEntity.GetName(colony.OwningEntity.FactionOwnerID)} population is now {newPopulation}",
                    colony.OwningEntity.FactionOwnerID,
                    colony.OwningEntity.Manager.ManagerID,
                    colony.OwningEntity.Id
                    ));
        }

        public void Init(Game game)
        {
        }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            GrowPopulation(entity);
        }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var colonies = manager.GetAllDataBlobsOfType<ColonyInfoDB>();

            foreach (var colony in colonies)
            {
                if(colony.OwningEntity != null)
                    GrowPopulation(colony.OwningEntity);
            }

            return colonies.Count;
        }

    }
}
