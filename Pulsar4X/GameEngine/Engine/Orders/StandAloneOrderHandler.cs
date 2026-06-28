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

                // This runs SYNCHRONOUSLY from the UI click handler (the Fleet window issues a move order inside its
                // ImGui Begin/End). If an order's Execute/scheduling throws, the exception unwinds through that open
                // window and corrupts the WHOLE frame — one bad order blanked every window (the 2026-06-28 "moved two
                // fleets at once and the UI broke" cascade, root-caused to a warp self-parent throw). Contain + log
                // it loudly instead: the order fails, the game keeps running, and the trace lands in the captured log
                // (game_logs). A logged skip is strictly more visible than the silent engagement-lock return above —
                // and never worse than killing the render.
                try
                {
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
                catch (Exception ex)
                {
                    System.Console.WriteLine("[OrderError] " + entityCommand.GetType().Name + " on entity #"
                        + entityCommand.EntityCommandingGuid + " threw and was skipped (the order failed; the game "
                        + "keeps running). Fix the cause:\n" + ex);
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