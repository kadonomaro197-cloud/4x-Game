using System;
using System.Collections.Generic;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;
using Pulsar4X.Industry;
using Pulsar4X.Sensors;

namespace Pulsar4X.Engine
{
    //This processor takes an entity and recalcs for each of the datablobs in that entity.
    //as an ability is added to the game, it's recalc processor should be linked here.
    internal static class ReCalcProcessor
    {
        [ThreadStatic]
        private static Entity CurrentEntity;
        internal static Dictionary<Type, Delegate> TypeProcessorMap = new Dictionary<Type, Delegate>
            {
                // Every colony carries a ComponentInstancesDB, so use it to re-sum infrastructure
                // capacity whenever an installation is added or removed. RecalcCapacity ignores
                // non-colony entities (e.g. ships).
                { typeof(ComponentInstancesDB), new Action<ComponentInstancesDB>(processor => { InfrastructureProcessor.RecalcCapacity(CurrentEntity); }) },

                // GRAVE RUNG (detection × damage): rebuild a ship's sensor-receiver cache from its CURRENT
                // components whenever abilities are recalced — which the damage system does after destroying a
                // component. Shoot a ship's sensors off and SetInstances leaves its InstanceStates empty, so it
                // stops scanning and goes blind. (SetInstances rebuilds from live components, so it's idempotent
                // on install/creation recalcs too.)
                { typeof(SensorAbilityDB), new Action<SensorAbilityDB>(processor => { SensorTools.SetInstances(CurrentEntity); }) },

                // { typeof(ShipInfoDB), new Action<ShipInfoDB>(processor => {ShipAndColonyInfoProcessor.ReCalculateShipTonnaageAndHTK(CurrentEntity); }) },
                // { typeof(MiningDB), new Action<MiningDB>(processor => { MineResourcesProcessor.CalcMaxRate(CurrentEntity);}) },
                // { typeof(ColonyLifeSupportDB), new Action<ColonyLifeSupportDB>(processor => {PopulationProcessor.ReCalcMaxPopulation(CurrentEntity); }) },
                //{ typeof(CargoStorageDB), new Action<CargoStorageDB>(processor => {StorageSpaceProcessor.ReCalcCapacity(CurrentEntity); }) },

            };



        internal static void ReCalcAbilities(Entity entity)
        {

            //lock (CurrentEntity)
            //{
                CurrentEntity = entity;
                foreach (var datablob in entity.Manager.GetAllDataBlobsForEntity(entity.Id))
                {
                    var t = datablob.GetType();
                    if (TypeProcessorMap.ContainsKey(t))
                        TypeProcessorMap[t].DynamicInvoke(datablob); // invoke appropriate delegate
                }
            //}
        }
    }



}