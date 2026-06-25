using Pulsar4X.Datablobs;
using Pulsar4X.Interfaces;
using Pulsar4X.Engine;
using Pulsar4X.Combat;
using System;

namespace Pulsar4X.Engine.Orders
{
    internal class StandAloneOrderHandler : IOrderHandler
    {
        internal StandAloneOrderHandler(Game game)
        {
            Game = game;
        }

        public Game Game { get; private set; }

        public void HandleOrder(EntityCommand entityCommand)
        {
            if (entityCommand.IsValidCommand(Game))
            {
                // Engagement lock (Combat spine step 11): a fleet that is locked in a battle (it carries a
                // FleetCombatStateDB) cannot take regular orders — only orders that opt in via
                // IsAllowedDuringEngagement still apply. Doctrine changes use a direct FleetDoctrine call (not an
                // order), so they remain available while the fleet fights. The refusal is silent at the engine
                // level; the UI reads the FleetCombatStateDB to show the fleet as locked.
                if (IsEngagementLocked(entityCommand))
                    return;

                if (entityCommand.UseActionLanes)
                {
                    if (entityCommand.ActionOnDate > entityCommand.EntityCommanding.StarSysDateTime)
                    {
                        entityCommand.EntityCommanding.Manager.ManagerSubpulses.AddEntityInterupt(entityCommand.ActionOnDate, nameof(OrderableProcessor), entityCommand.EntityCommanding);
                    }
                    var orderableDB = entityCommand.EntityCommanding.GetDataBlob<OrderableDB>();

                    if(orderableDB == null) throw new NullReferenceException("orderableDB cannot be null");
                    if(orderableDB.OwningEntity == null) throw new NullReferenceException("orderableDB.OwningEntity cannot be null");

                    orderableDB.ActionList.Add(entityCommand);
                    Game.ProcessorManager.GetInstanceProcessor(nameof(OrderableProcessor)).ProcessEntity(orderableDB.OwningEntity, Game.TimePulse.GameGlobalDateTime);
                }
                else
                {
                    if(entityCommand.EntityCommanding.StarSysDateTime >= entityCommand.ActionOnDate)
                        entityCommand.Execute(entityCommand.EntityCommanding.StarSysDateTime);
                    else
                    {
                        entityCommand.EntityCommanding.Manager.ManagerSubpulses.AddEntityInterupt(entityCommand.ActionOnDate, nameof(OrderableProcessor), entityCommand.EntityCommanding);
                    }
                }
            }
        }

        /// <summary>
        /// The engagement lock: true when the order targets a fleet that is currently in combat (has a
        /// <see cref="FleetCombatStateDB"/>) and the order is not one that is explicitly allowed during an
        /// engagement. Keeps players (and the AI) from re-tasking a fleet mid-battle — only doctrine changes
        /// (a direct call, not an order) get through.
        /// </summary>
        private static bool IsEngagementLocked(EntityCommand entityCommand)
        {
            var commanding = entityCommand.EntityCommanding;
            return commanding != null
                && commanding.IsValid
                && commanding.HasDataBlob<FleetCombatStateDB>()
                && !entityCommand.IsAllowedDuringEngagement;
        }
    }
}