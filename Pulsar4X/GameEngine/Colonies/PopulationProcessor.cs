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

        /// <summary>
        /// OPERATION BLUEPRINT-TO-STEEL A1 (civic door / ENGINE-WIRING-BACKLOG TIER 2) — the employment→morale term.
        /// **Default OFF so the ENGINE test suite stays byte-identical** (with it off the employment ratio reads the -1.0
        /// "no job data" neutral sentinel exactly as before, and <c>MoraleTests.StartingColony_HasMorale_NeutralOnHomeworld</c>
        /// stays green); **a real menu-started game turns it ON** in <c>NewGameMenu.CreateGameCore</c>/Quickstart — the same
        /// default-off/menu-on pattern as <c>EnableGroundTacticalAI</c>, <c>LegitimacyProcessor.ReadCurrentMorale</c>, etc.
        /// With it ON, a colony's installed-building jobs (<c>ComponentInstancesDB.GetTotalJobs</c>, sourced from each
        /// building's CrewReq) move morale (+15 full / −25 unemployment) vs a per-capita job DEMAND (see below).
        /// ✅ CALIBRATED 2026-08-13 (developer-authorized): the denominator is <c>population × ColonyMoraleDB.JobsPerCapita</c>
        /// — the SustenanceProcessor food/power shape, which SCALES with population (was jobs÷workforce, which pinned a
        /// billions-pop homeworld to −25). A fully-built homeworld reads near-neutral; an under-built colony reads a
        /// deficit → migration pressure. Turning it ON cascades morale → migration → tax income → legitimacy, so the live
        /// feel is the developer's PC play-test (CI can't run the client). Read by all three morale consumers below +
        /// <c>StationPopulationProcessor</c>.
        /// </summary>
        public static bool EnableEmploymentMorale = false;

        /// <summary>
        /// The MEDICAL civic dial's live consumer (docs/assembler/01-IO-civic.md — the Medical option's "+N health").
        /// When true, a colony's installed HOSPITALS (components carrying <see cref="MedicalAtbDB"/>, summed via
        /// <see cref="Pulsar4X.Extensions.ComponentInstancesDBExtensions.GetTotalMedical"/>) add a positive morale term
        /// (capped by <see cref="ColonyMoraleDB.MaxHealthBonus"/>) — so building hospitals lifts a struggling colony's
        /// morale, the offset a harsh world needs. HEALTH is a NEW morale consumer (not one of the original six inputs),
        /// the sibling of the food-QUALITY bonus. Defaults <b>false</b> so the whole existing suite stays byte-identical
        /// (no colony ships a hospital, and the term adds no factor at 0) — the same default-off/menu-on pattern as
        /// <see cref="EnableEmploymentMorale"/>; <c>NewGameMenu</c> flips it on. Read by both colony morale gatherings
        /// below (GrowPopulation + ComputeCurrentMorale); stations use the positional overload so they're byte-identical
        /// until a station-medical follow-up. Grave rung: bombard the hospital → GetTotalMedical drops → morale falls.
        /// </summary>
        public static bool EnableMedicalMorale = false;

        /// <summary>Master gate for the RECREATION → morale civic term (the Amenity dial). OFF (default) →
        /// AmenityStrength reads 0 → byte-identical; <c>NewGameMenu</c> flips it on with the other civic terms. Grave
        /// rung: bombard the recreation building → GetTotalAmenity drops → morale falls. Mirrors <see cref="EnableMedicalMorale"/>.</summary>
        public static bool EnableAmenityMorale = false;

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

            // --- M1 morale (the population "tank" valve, docs/society/MORALE-AND-POPULATION-DESIGN.md) ---
            // Recompute morale from the inputs that already exist (conditions + overcrowding) and turn it into
            // a migration rate added to growth below. Guarded: a colony without a ColonyMoraleDB (e.g. built by
            // an older path) just skips morale and grows as before.
            double migration = 0.0;

            // M5b: the province's computed power/food shortage (SustenanceProcessor). Neutral (0) by default → no
            // morale hit and no deaths until demand is calibrated on the local build (the default-deficit guard).
            double powerShortage = 0.0, foodShortage = 0.0, starvation = 0.0;
            if (colony.TryGetDataBlob<ColonySustenanceDB>(out var sustenanceDB))
            {
                powerShortage = sustenanceDB.PowerShortage;
                foodShortage = sustenanceDB.FoodShortage;
                starvation = ColonySustenanceDB.StarvationDeathRate(foodShortage);
            }

            if (colony.TryGetDataBlob<ColonyMoraleDB>(out var moraleDB))
            {
                double crowdingRatio = 0.0;
                if (worstColonyCost > 0.0) // only support-capped (hostile) worlds can overcrowd
                {
                    long needs = needsSupport < 1 ? 1 : needsSupport;
                    double capacity = ((double)popSupportValue / needs) / worstColonyCost;
                    crowdingRatio = capacity > 0.0 ? totalPop / capacity : 2.0;
                }

                // M2 employment + the 2026-08-13 CALIBRATION: jobs are measured against a per-capita job DEMAND
                // (pop × ColonyMoraleDB.JobsPerCapita), the SAME shape SustenanceProcessor uses for food/power — the
                // denominator SCALES with population, so a billions-pop homeworld no longer pins to −25 (the category
                // error a raw jobs÷workforce ratio produced). Two-sided; a colony with no installation declaring jobs
                // has "no job data" → neutral employment (sentinel -1), not 100% unemployment. Housing comfort is a bonus.
                // 🔁 KEEP IN SYNC with the identical block in ComputeCurrentMorale below + StationPopulationProcessor.
                long jobs = instancesDB.GetTotalJobs();
                double jobDemand = totalPop * ColonyMoraleDB.JobsPerCapita;
                double employmentRatio = (EnableEmploymentMorale && jobs > 0 && jobDemand > 0) ? jobs / jobDemand : -1.0;
                double comfort = instancesDB.GetHousingComfort();

                // M4: tax is a morale input (read the colony's tax rate; ColonyEconomyProcessor reads morale
                // back to scale income — a one-tick-lagged loop).
                double taxRate = colony.TryGetDataBlob<ColonyEconomyDB>(out var econDB) ? econDB.TaxRate : 0.0;
                // Government MODULATOR (#30): cap the effective tax at the regime's TaxCeiling here too, so the
                // morale penalty and the billed income (ColonyEconomyProcessor) agree. Inert at the Mid default.
                double taxCeiling = Pulsar4X.Factions.GovernmentTools.OwnerOf(colony).TaxCeiling();
                if (taxRate > taxCeiling) taxRate = taxCeiling;

                // M5b: power/food shortage now feed morale (both 0 by default → neutral until calibrated).
                moraleDB.Morale = ColonyMoraleDB.ComputeMorale(new MoraleInputs
                {
                    WorstColonyCost = worstColonyCost,
                    CrowdingRatio = crowdingRatio,
                    EmploymentRatio = employmentRatio,
                    Comfort = comfort,
                    TaxRate = taxRate,
                    PowerShortage = powerShortage,
                    FoodShortage = foodShortage,
                    // M5c: the colony's output-weighted average food quality → a morale bonus above "not starving".
                    FoodQuality = instancesDB.GetAverageFoodQuality(),
                    // Medical civic dial (flag-gated OFF → byte-identical): the colony's installed hospitals lift morale.
                    HealthStrength = EnableMedicalMorale ? instancesDB.GetTotalMedical() : 0.0,
                    // Recreation civic dial (flag-gated OFF → byte-identical): the colony's amenity buildings lift morale.
                    AmenityStrength = EnableAmenityMorale ? instancesDB.GetTotalAmenity() : 0.0
                }, moraleDB.Factors);
                // Government MODULATOR (#30): the regime's MoraleWeight scales how hard public opinion pulls
                // migration (People-end amplifies it, One-Ruler-end damps it). Neutral (×1.0) at the default Mid
                // authority, so this changes nothing until a non-Mid regime is set.
                migration = ColonyMoraleDB.MigrationRate(moraleDB.Morale) * Pulsar4X.Factions.GovernmentTools.OwnerOf(colony).MoraleWeight();
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
                        newPop = (long)(value * (1.0 + growthRate - starvation));
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
                        newPop = (long)(value * (1.0 + growthRate + migration - starvation));
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

        /// <summary>
        /// Compute a host's CURRENT-cycle morale from its live inputs — the SAME morale that <see cref="GrowPopulation"/>
        /// writes to <see cref="ColonyMoraleDB.Morale"/> each population tick, but as a pure READ that never mutates the
        /// blob. <see cref="LegitimacyProcessor"/> calls this (under its <c>ReadCurrentMorale</c> flag) so legitimacy
        /// reads THIS cycle's morale instead of the possibly one-cycle-stale <see cref="ColonyMoraleDB.Morale"/> field —
        /// killing the stale echo regardless of which monthly hotloop fires first (findings/A3-objective-flip.md).
        ///
        /// It feeds the identical canonical math (<see cref="ColonyMoraleDB.ComputeMorale(MoraleInputs, Dictionary{string, double})"/>)
        /// with the identical inputs GrowPopulation gathers — so the value matches. It is left as a SEPARATE reader
        /// (GrowPopulation is untouched) to keep the population sim byte-identical; the two input-gatherings must be
        /// kept in sync (any new morale input added to GrowPopulation's block above must be added here too).
        ///
        /// Defensive (runs from a monthly hotloop — gotcha L4): a host missing the population inputs (e.g. a station
        /// with no ColonyInfoDB) falls back to its stored <see cref="ColonyMoraleDB.Morale"/>, then to the neutral
        /// midpoint; any unexpected read failure returns that same fallback rather than throwing.
        /// </summary>
        internal static double ComputeCurrentMorale(Entity colony)
        {
            // Fallback for a host we can't recompute from population inputs.
            double fallback = colony.TryGetDataBlob<ColonyMoraleDB>(out var moraleBlob) ? moraleBlob.Morale : ColonyMoraleDB.Neutral;

            if (!colony.TryGetDataBlob<ColonyInfoDB>(out var colonyInfoDB)) return fallback;
            if (!colony.TryGetDataBlob<ComponentInstancesDB>(out var instancesDB)) return fallback;

            try
            {
                long popSupportValue = instancesDB.GetPopulationSupportValue(colonyInfoDB.PlanetEntity);

                long needsSupport = 0;
                long totalPop = 0;
                double worstColonyCost = 0.0;
                foreach (var (id, value) in colonyInfoDB.Population)
                {
                    var species = colony.Manager.GetGlobalEntityById(id).GetDataBlob<SpeciesDB>();
                    double cc = species.ColonyCost(colonyInfoDB.PlanetEntity);
                    if (cc > 0.0) needsSupport++;
                    if (cc > worstColonyCost) worstColonyCost = cc;
                    totalPop += value;
                }

                double powerShortage = 0.0, foodShortage = 0.0;
                if (colony.TryGetDataBlob<ColonySustenanceDB>(out var sustenanceDB))
                {
                    powerShortage = sustenanceDB.PowerShortage;
                    foodShortage = sustenanceDB.FoodShortage;
                }

                double crowdingRatio = 0.0;
                if (worstColonyCost > 0.0) // only support-capped (hostile) worlds can overcrowd
                {
                    long needs = needsSupport < 1 ? 1 : needsSupport;
                    double capacity = ((double)popSupportValue / needs) / worstColonyCost;
                    crowdingRatio = capacity > 0.0 ? totalPop / capacity : 2.0;
                }

                // 🔁 KEEP IN SYNC with GrowPopulation above + StationPopulationProcessor — the per-capita job-demand
                // denominator (2026-08-13 calibration). See the comment on the GrowPopulation copy for the rationale.
                long jobs = instancesDB.GetTotalJobs();
                double jobDemand = totalPop * ColonyMoraleDB.JobsPerCapita;
                double employmentRatio = (EnableEmploymentMorale && jobs > 0 && jobDemand > 0) ? jobs / jobDemand : -1.0;
                double comfort = instancesDB.GetHousingComfort();

                double taxRate = colony.TryGetDataBlob<ColonyEconomyDB>(out var econDB) ? econDB.TaxRate : 0.0;
                double taxCeiling = Pulsar4X.Factions.GovernmentTools.OwnerOf(colony).TaxCeiling();
                if (taxRate > taxCeiling) taxRate = taxCeiling;

                return ColonyMoraleDB.ComputeMorale(new MoraleInputs
                {
                    WorstColonyCost = worstColonyCost,
                    CrowdingRatio = crowdingRatio,
                    EmploymentRatio = employmentRatio,
                    Comfort = comfort,
                    TaxRate = taxRate,
                    PowerShortage = powerShortage,
                    FoodShortage = foodShortage,
                    FoodQuality = instancesDB.GetAverageFoodQuality(),
                    // Medical civic dial (flag-gated OFF → byte-identical): keep in sync with GrowPopulation above.
                    HealthStrength = EnableMedicalMorale ? instancesDB.GetTotalMedical() : 0.0,
                    // Recreation civic dial (flag-gated OFF → byte-identical): keep in sync with GrowPopulation above.
                    AmenityStrength = EnableAmenityMorale ? instancesDB.GetTotalAmenity() : 0.0
                }, null);
            }
            catch
            {
                return fallback; // never throw from the monthly hotloop path (L4)
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
