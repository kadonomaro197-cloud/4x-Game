using System;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;

namespace Pulsar4X.Fleets
{
    public class RefuelAction : EntityCommand
    {
        public override string Name => "Refuel";
        public override string Details => "Refuel the fleet, must be at a location where supplies are availablle.";
         public override ActionLaneTypes ActionLanes { get; } = ActionLaneTypes.IneteractWithSelf | ActionLaneTypes.InteractWithEntitySameFleet;

        public override bool IsBlocking => true;

        private Entity _entityCommanding;
        internal override Entity EntityCommanding
        {
            get { return _entityCommanding; }
        }

        internal override bool IsFinished()
        {
            return _isFinished;
        }

        internal override void Execute(DateTime atDateTime)
        {
            // BEHAVIOR PARKED (OPERATION BLUEPRINT-TO-STEEL A4 — campaign log ADJUDICATION QUEUE): the real refuel
            // transfer needs a developer decision — WHAT counts as a supply source (a co-located friendly
            // colony/station? a fleet tender with a CargoTransferAtb?) and whether the fleet must be within transfer
            // RANGE (CargoTransferOrder does no proximity check). Once settled, the proven routine to call is
            // CargoTransferOrder.CreateRefuelFleetCommand(supply, fleet).
            //
            // DE-WEDGED (this slice): complete immediately so this can never JAM the fleet's blocking order lane. The
            // old body was empty AND IsFinished never flipped true, so once a standing "refuel when low" order fired
            // this onto the ActionList it stayed there forever (IsBlocking + never-finished), and FleetOrderProcessor
            // won't add further standing actions while ActionList.Count > 0 — the fleet's order lane was stuck for good.
            _isFinished = true;
        }

        internal override bool IsValidCommand(Game game)
        {
            // De-fanged: resolve the commanding fleet if we can (mirrors CargoTransferOrder), else stay valid as a
            // no-op so the order completes and clears rather than erroring on the sim thread.
            CommandHelpers.IsCommandValid(game.GlobalManager, RequestingFactionGuid, EntityCommandingGuid, out _, out _entityCommanding);
            return true;
        }

        public RefuelAction() { }
        public RefuelAction(Entity commandingEntity)
        {
            _entityCommanding = commandingEntity;
        }

        public override EntityCommand Clone()
        {
            var command = new RefuelAction(EntityCommanding)
            {
                UseActionLanes = this.UseActionLanes,
                RequestingFactionGuid = this.RequestingFactionGuid,
                EntityCommandingGuid = this.EntityCommandingGuid,
                CreatedDate = this.CreatedDate,
                ActionOnDate = this.ActionOnDate,
                ActionedOnDate = this.ActionedOnDate,
                IsRunning = this.IsRunning
            };

            return command;
        }
    }
}