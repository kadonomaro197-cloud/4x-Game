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
        /// Total jobs (worker slots) provided by installed components carrying <see cref="EmploymentAtbDB"/>,
        /// scaled by component health. Zero when no installation declares jobs (M2 treats that as "no job data"
        /// → neutral employment, not 100% unemployment). See docs/MORALE-AND-POPULATION-DESIGN.md.
        /// </summary>
        public static long GetTotalJobs(this ComponentInstancesDB componentInstances)
        {
            long jobs = 0;
            foreach (var design in componentInstances.GetDesignsByType(typeof(EmploymentAtbDB)))
            {
                int perComponent = design.GetAttribute<EmploymentAtbDB>().Jobs;
                foreach (var component in componentInstances.GetComponentsBySpecificDesign(design.UniqueID).Where(c => c.IsEnabled))
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
