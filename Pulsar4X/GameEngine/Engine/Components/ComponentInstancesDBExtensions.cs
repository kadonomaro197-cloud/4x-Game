using System.Collections.Generic;
using System.Linq;
using Pulsar4X.Datablobs;
using Pulsar4X.Components;
using Pulsar4X.Colonies;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Movement;

namespace Pulsar4X.Extensions
{
    public static class ComponentInstancesDBExtensions
    {
        /// <summary>
        /// Total jobs (worker slots) an entity's installed components provide — the EMPLOYMENT the morale model reads
        /// against the workforce. Per the civic-door design ("jobs are published from every industry building's CrewReq",
        /// civicderived.html:207), a component's operating-CREW requirement IS its employment — an emergent colony total,
        /// no new data. A component may OVERRIDE that with an explicit <see cref="EmploymentAtbDB.Jobs"/> (e.g. a habitat
        /// that employs beyond its raw operating crew), which keeps that attribute live rather than dead. Health-scaled
        /// (a bomb-damaged factory employs fewer). Only enabled components count.
        ///
        /// This is ONE producer read by (eventually) TWO consumers — the ±40 employment→morale term
        /// (<see cref="PopulationProcessor"/>) and the future workforce→production staffing throttle (ENGINE-WIRING-BACKLOG
        /// TIER 2.6) — so this number stays TRUTHFUL. The behaviour flag that turns the morale term on lives at the morale
        /// consumers (<c>PopulationProcessor.EnableEmploymentMorale</c>), not here, so the staffing reader gets the real
        /// figure. Before A2/employment-wiring (2026-08-13) no template declared jobs, so this summed to 0 forever.
        /// See docs/society/MORALE-AND-POPULATION-DESIGN.md + docs/assembler/ENGINE-WIRING-BACKLOG-2026-08-06.md TIER 2.
        /// </summary>
        public static long GetTotalJobs(this ComponentInstancesDB componentInstances)
        {
            long jobs = 0;
            foreach (var byDesign in componentInstances.GetComponentsByDesigns())
            {
                if (!componentInstances.AllDesigns.TryGetValue(byDesign.Key, out var design)) continue;
                // Jobs = an explicit EmploymentAtbDB.Jobs override if the component declares one, else the building's
                // operating-crew requirement (the civic-door default). A component with neither declares no jobs.
                long perComponent = design.TryGetAttribute<EmploymentAtbDB>(out var emp) ? emp.Jobs : design.CrewReq;
                if (perComponent <= 0) continue;
                foreach (var component in byDesign.Value.Where(c => c.IsEnabled))
                    jobs += (long)(perComponent * component.HealthPercent);
            }
            return jobs;
        }

        /// <summary>
        /// Total housing comfort (a morale bonus) from installed components carrying <see cref="HousingAtbDB"/>,
        /// scaled by component health. The quality-of-life "tier" layer above bare life-support capacity.
        /// </summary>
        public static double GetHousingComfort(this ComponentInstancesDB componentInstances)
        {
            double comfort = 0.0;
            foreach (var design in componentInstances.GetDesignsByType(typeof(HousingAtbDB)))
            {
                double perComponent = design.GetAttribute<HousingAtbDB>().Comfort;
                foreach (var component in componentInstances.GetComponentsBySpecificDesign(design.UniqueID).Where(c => c.IsEnabled))
                    comfort += perComponent * component.HealthPercent;
            }
            return comfort;
        }

        /// <summary>
        /// Total food produced per day by installed components carrying <see cref="Pulsar4X.Colonies.FoodProductionAtbDB"/>,
        /// scaled by component health (a bomb-damaged farm makes less). This is the food SUPPLY the SustenanceProcessor
        /// weighs against demand. Zero when no installation makes food (M5c).
        /// </summary>
        public static double GetTotalFoodOutput(this ComponentInstancesDB componentInstances)
        {
            double food = 0.0;
            foreach (var design in componentInstances.GetDesignsByType(typeof(Pulsar4X.Colonies.FoodProductionAtbDB)))
            {
                double perComponent = design.GetAttribute<Pulsar4X.Colonies.FoodProductionAtbDB>().FoodOutput;
                foreach (var component in componentInstances.GetComponentsBySpecificDesign(design.UniqueID).Where(c => c.IsEnabled))
                    food += perComponent * component.HealthPercent;
            }
            return food;
        }

        /// <summary>
        /// Total SECURITY (order strength) provided by installed components carrying
        /// <see cref="Pulsar4X.Colonies.SecurityAtbDB"/>, scaled by component health (a bomb-damaged precinct provides
        /// less order). Fed into the legitimacy security term (LegitimacyProcessor, behind
        /// <see cref="Pulsar4X.Colonies.LegitimacyProcessor.EnableSecurityLegitimacy"/>). Zero when no installation
        /// provides security — so it's neutral until a colony builds a precinct (the grave rung: bombard it, this drops).
        /// </summary>
        public static double GetTotalSecurity(this ComponentInstancesDB componentInstances)
        {
            double security = 0.0;
            foreach (var design in componentInstances.GetDesignsByType(typeof(Pulsar4X.Colonies.SecurityAtbDB)))
            {
                double perComponent = design.GetAttribute<Pulsar4X.Colonies.SecurityAtbDB>().SecurityRating;
                foreach (var component in componentInstances.GetComponentsBySpecificDesign(design.UniqueID).Where(c => c.IsEnabled))
                    security += perComponent * component.HealthPercent;
            }
            return security;
        }

        /// <summary>
        /// Total MEDICAL (health care) provided by installed components carrying
        /// <see cref="Pulsar4X.Colonies.MedicalAtbDB"/>, scaled by component health (a bomb-damaged hospital cares for
        /// fewer). Fed into the morale health term (PopulationProcessor, behind
        /// <see cref="Pulsar4X.Colonies.PopulationProcessor.EnableMedicalMorale"/>). Zero when no installation provides
        /// care — so it's neutral until a colony builds a hospital (the grave rung: bombard it, this drops).
        /// </summary>
        public static double GetTotalMedical(this ComponentInstancesDB componentInstances)
        {
            double medical = 0.0;
            foreach (var design in componentInstances.GetDesignsByType(typeof(Pulsar4X.Colonies.MedicalAtbDB)))
            {
                double perComponent = design.GetAttribute<Pulsar4X.Colonies.MedicalAtbDB>().HealthRating;
                foreach (var component in componentInstances.GetComponentsBySpecificDesign(design.UniqueID).Where(c => c.IsEnabled))
                    medical += perComponent * component.HealthPercent;
            }
            return medical;
        }

        /// <summary>
        /// The total AMENITY (recreation / leisure strength) the colony's installed recreation buildings provide, from
        /// <see cref="Pulsar4X.Colonies.AmenityAtbDB"/>, scaled by component health (a bomb-damaged arena entertains
        /// fewer). Fed into the morale amenity term (PopulationProcessor, behind
        /// <see cref="Pulsar4X.Colonies.PopulationProcessor.EnableAmenityMorale"/>). Zero when no installation provides
        /// leisure — so it's neutral until a colony builds one (the grave rung: bombard it, this drops). Mirrors
        /// <see cref="GetTotalMedical"/>.
        /// </summary>
        public static double GetTotalAmenity(this ComponentInstancesDB componentInstances)
        {
            double amenity = 0.0;
            foreach (var design in componentInstances.GetDesignsByType(typeof(Pulsar4X.Colonies.AmenityAtbDB)))
            {
                double perComponent = design.GetAttribute<Pulsar4X.Colonies.AmenityAtbDB>().AmenityRating;
                foreach (var component in componentInstances.GetComponentsBySpecificDesign(design.UniqueID).Where(c => c.IsEnabled))
                    amenity += perComponent * component.HealthPercent;
            }
            return amenity;
        }

        /// <summary>
        /// The total COMMERCE (monthly trade revenue, credits) the colony's installed market buildings generate, from
        /// <see cref="Pulsar4X.Colonies.CommerceAtbDB"/>, scaled by component health (a bomb-damaged exchange trades
        /// less). Booked as income by <see cref="Pulsar4X.Colonies.ColonyEconomyProcessor"/> (behind
        /// <see cref="Pulsar4X.Colonies.ColonyEconomyProcessor.EnableCommerceIncome"/>). Zero when no installation
        /// provides commerce — so it's neutral until a colony builds a market (the grave rung: bombard it, this drops).
        /// Mirrors <see cref="GetTotalAmenity"/>.
        /// </summary>
        public static double GetTotalCommerce(this ComponentInstancesDB componentInstances)
        {
            double commerce = 0.0;
            foreach (var design in componentInstances.GetDesignsByType(typeof(Pulsar4X.Colonies.CommerceAtbDB)))
            {
                double perComponent = design.GetAttribute<Pulsar4X.Colonies.CommerceAtbDB>().TradeValue;
                foreach (var component in componentInstances.GetComponentsBySpecificDesign(design.UniqueID).Where(c => c.IsEnabled))
                    commerce += perComponent * component.HealthPercent;
            }
            return commerce;
        }

        /// <summary>
        /// The colony's average food QUALITY — the OUTPUT-WEIGHTED mean quality across installed food components (so a
        /// tiny gourmet dome doesn't outweigh the bulk farms that actually feed everyone). Health-scaled like the output.
        /// Returns 0 when there is no food production (the caller reads that as "no quality bonus"). M5c.
        /// </summary>
        public static double GetAverageFoodQuality(this ComponentInstancesDB componentInstances)
        {
            double weightedQuality = 0.0, totalOutput = 0.0;
            foreach (var design in componentInstances.GetDesignsByType(typeof(Pulsar4X.Colonies.FoodProductionAtbDB)))
            {
                var atb = design.GetAttribute<Pulsar4X.Colonies.FoodProductionAtbDB>();
                foreach (var component in componentInstances.GetComponentsBySpecificDesign(design.UniqueID).Where(c => c.IsEnabled))
                {
                    double output = atb.FoodOutput * component.HealthPercent;
                    weightedQuality += atb.FoodQuality * output;
                    totalOutput += output;
                }
            }
            return totalOutput > 0.0 ? weightedQuality / totalOutput : 0.0;
        }

        public static long GetPopulationSupportValue(this ComponentInstancesDB componentInstances, Entity bodyEntity)
        {
            var infrustructureDesigns = componentInstances.GetDesignsByType(typeof(PopulationSupportAtbDB));

            double bodyGravityMps2 = 0;
            if (bodyEntity.TryGetDataBlob<SystemBodyInfoDB>(out var bodyInfo))
                bodyGravityMps2 = bodyInfo.Gravity;

            double bodyPressureAtm = 0;
            if (bodyEntity.TryGetDataBlob<AtmosphereDB>(out var atmosphere))
                bodyPressureAtm = atmosphere.Pressure;

            long popSupportValue = 0;
            foreach (var design in infrustructureDesigns)
            {
                if (design.TryGetAttribute<GravityToleranceAtb>(out var gravTol)
                    && !gravTol.SupportsBodyGravity(bodyGravityMps2))
                    continue;

                if (design.TryGetAttribute<PressureToleranceAtb>(out var pressTol)
                    && !pressTol.SupportsBodyPressure(bodyPressureAtm))
                    continue;

                var componentCapacity = design.GetAttribute<PopulationSupportAtbDB>().PopulationCapacity;
                foreach (var component in componentInstances.GetComponentsBySpecificDesign(design.UniqueID).Where(c => c.IsEnabled))
                {
                    popSupportValue += (long)(componentCapacity * component.HealthPercent);
                }
            }

            return popSupportValue;
        }

        public static long GetTotalDryMass(this ComponentInstancesDB componentInstances)
        {
            long totalTonnage = 0;

            foreach (KeyValuePair<string, List<ComponentInstance>> instance in componentInstances.GetComponentsByDesigns())
            {
                var componentTonnage = componentInstances.AllDesigns[instance.Key].MassPerUnit;
                instance.Value.ForEach(x => totalTonnage += componentTonnage);
            }

            return totalTonnage;
        }

        public static double GetTotalVolume(this ComponentInstancesDB componentInstances)
        {
            double totalVolume = 0;

            foreach (KeyValuePair<string, List<ComponentInstance>> instance in componentInstances.GetComponentsByDesigns())
            {
                var componentVolume = componentInstances.AllDesigns[instance.Key].VolumePerUnit;
                instance.Value.ForEach(x => totalVolume += componentVolume);
            }

            return totalVolume;
        }

        public static int GetTotalEnginePower(this ComponentInstancesDB instancesDB, out Dictionary<string, double> totalFuelUsage)
        {
            int totalEnginePower = 0;
            totalFuelUsage = new Dictionary<string, double>();
            var designs = instancesDB.GetDesignsByType(typeof(WarpDriveAtb));

            //TODO: this is how fuel was calculated, currently power use is static, but will revisit this.

            foreach (var design in designs)
            {
                var warpAtb = design.GetAttribute<WarpDriveAtb>();
                foreach (var instanceInfo in instancesDB.GetComponentsBySpecificDesign(design.UniqueID))
                {
                    var warpAtb2 = (WarpDriveAtb)instanceInfo.Design.AttributesByType[typeof(WarpDriveAtb)];
                    //var fuelUsage = (ResourceConsumptionAtbDB)instanceInfo.Design.AttributesByType[typeof(ResourceConsumptionAtbDB)];
                    if (instanceInfo.IsEnabled)
                    {
                        totalEnginePower += (int)(warpAtb.WarpPower * instanceInfo.HealthPercent);
                        //foreach (var item in fuelUsage.MaxUsage)
                        //{
                        //    totalFuelUsage.SafeValueAdd(item.Key, item.Value);
                        //}
                    }
                }
            }

            return totalEnginePower;
        }
    }
}
