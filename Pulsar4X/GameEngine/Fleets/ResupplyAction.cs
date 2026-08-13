using System;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;

namespace Pulsar4X.Fleets;
public class ResupplyAction : EntityCommand
{
    public override string Name => "Resupply";
    public override string Details => "Resupply the fleet, must be at a location where supplies are availablle.";
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
        // BEHAVIOR PARKED (OPERATION BLUEPRINT-TO-STEEL A4 — campaign log ADJUDICATION QUEUE): "resupply" is
        // under-specified — is it reloading missile/ordnance MAGAZINES (ShipMagazineAtb/OrdnancePayloadAtb items),
        // or an Aurora-style Maintenance-Supply-Point resource that DOES NOT EXIST in this engine? There is no
        // CreateResupplyFleetCommand helper yet. Needs a developer ruling before it can act.
        //
        // DE-WEDGED (this slice): complete immediately so this can never JAM the fleet's blocking order lane (same
        // fix + reason as RefuelAction — an empty Execute + never-finishing IsFinished stuck the lane forever).
        _isFinished = true;
    }

    internal override bool IsValidCommand(Game game)
    {
        CommandHelpers.IsCommandValid(game.GlobalManager, RequestingFactionGuid, EntityCommandingGuid, out _, out _entityCommanding);
        return true;
    }

    public ResupplyAction() { }
    public ResupplyAction(Entity commandingEntity)
    {
        _entityCommanding = commandingEntity;
    }

    public override EntityCommand Clone()
    {
        var command = new ResupplyAction(EntityCommanding)
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