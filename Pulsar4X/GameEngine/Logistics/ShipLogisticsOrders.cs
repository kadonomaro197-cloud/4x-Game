using System;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;

namespace Pulsar4X.Logistics;

public class ShipLogisticsOrders : EntityCommand
{
    public override ActionLaneTypes ActionLanes => ActionLaneTypes.IneteractWithSelf;

    public override bool IsBlocking => false;

    public override string Name {get {return _name;}}
    string _name = "Logisitics";

    public override string Details {get {return _details;}}
    string _details = "";

    internal override Entity EntityCommanding { get { return _entityCommanding; } }
    Entity _entityCommanding;
    Entity _factionEntity;

    LogiShipperDB _logiShipperDB;

    internal override bool IsFinished()
    {
        if(_logiShipperDB != null && _entityCommanding.HasDataBlob<LogiShipperDB>())
            _isFinished = false;
        else
            _isFinished = true;
        return _isFinished;
    }

    internal override void Execute(DateTime atDateTime)
    {

    }
    public override void UpdateDetailString()
    {
        _details = _logiShipperDB.CurrentState.ToString();
        switch (_logiShipperDB.CurrentState)
        {
            case LogiShipperDB.States.Bidding:
            {
                _details = "Bidding on " + _logiShipperDB.BiddingTasks.Count + " consignments";
            }
                break;
            case LogiShipperDB.States.MoveToSupply:
            {
                _details = "Traveling to Supply to collect goods";
            }
                break;
            case LogiShipperDB.States.Loading:
            {
                _details = "Loading goods ";
            }
                break;
            case LogiShipperDB.States.MoveToDestination:
            {
                _details = "Moving to Destination";
            }
                break;
            case LogiShipperDB.States.Unloading:
            {
                _details = "Unloading goods";
            }
                break;
            case LogiShipperDB.States.ResuplySelf:
            {
                _details = "Refueling";
            }
                break;
            case LogiShipperDB.States.Waiting:
            {
                _details = "Waiting for suply and/or demand";
            }
                break;

            default:
                break;
        }
        //_logiShipperDB.ActiveCargoTasks[0].
        _logiShipperDB.StateString = _details;
    }

    internal override bool IsValidCommand(Game game)
    {
        if (CommandHelpers.IsCommandValid(game.GlobalManager, RequestingFactionGuid, EntityCommandingGuid, out _factionEntity, out _entityCommanding))
        {
            _logiShipperDB = _entityCommanding.GetDataBlob<LogiShipperDB>();
            return true;

        }
        return false;
    }

    public override EntityCommand Clone()
    {
        // De-crashed (OPERATION BLUEPRINT-TO-STEEL A4): the base-side bidding loop (LogisticsProcessor) drives the
        // real cargo work, so this per-ship order is a DISPLAY SHIM — Execute is intentionally empty and its only
        // construction site (LogiShipperDB) has the HandleOrder call commented out, so it is never issued today. But a
        // throwing Clone would land on the sim thread as a clock-killing [FATAL] if it were ever standing-ordered.
        // Driving the per-ship CurrentState machine (MoveToSupply→Loading→…) in Execute is a parked design decision
        // (campaign log ADJUDICATION QUEUE); the blobs re-resolve in IsValidCommand, so the clone needs only the base
        // command fields + the display strings.
        var command = new ShipLogisticsOrders
        {
            _name = this._name,
            _details = this._details,
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