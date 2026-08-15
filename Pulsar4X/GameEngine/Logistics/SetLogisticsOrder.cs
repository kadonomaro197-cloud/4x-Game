using System;
using System.Collections.Generic;
using Pulsar4X.Engine.Orders;
using Pulsar4X.Engine;
using Pulsar4X.Storage;

namespace Pulsar4X.Logistics;

public class SetLogisticsOrder : EntityCommand
{
    public enum OrderTypes
    {
        RemoveLogiBaseDB,
        SetBaseItems,
        AddLogiShipDB,
        RemoveLogiShipDB,
        SetShipTypeAmounts,
        SetDesiredLevels
    }

    //public SetLogisticsOrder Order;
    private OrderTypes _type;
    public override ActionLaneTypes ActionLanes { get; } = ActionLaneTypes.InstantOrder;

    public override bool IsBlocking { get; } = false;

    public override string Name { get; } = "Set TradeObject";

    public override string Details { get; } = "Set Logi";



    internal override Entity EntityCommanding { get { return _entityCommanding; } }
    Entity _entityCommanding;
    Entity _factionEntity;

    private Dictionary<ICargoable,(int count, int demandSupplyWeight)> _baseChanges;
    private Dictionary<ICargoable,(int minVal, int maxVal)> _desiredChanges;
    private Changes _shipChanges;

    internal override bool IsValidCommand(Game game)
    {

        if (CommandHelpers.IsCommandValid(game.GlobalManager, RequestingFactionGuid, EntityCommandingGuid, out _factionEntity, out _entityCommanding))
        {
            if (_type == OrderTypes.SetBaseItems && _entityCommanding.TryGetDataBlob<LogiBaseDB>(out LogiBaseDB? lbdb))
            {
                if(lbdb.ListedItems.Count >= _baseChanges.Count)
                    return true;
                return false;
            }

            // A SetDesiredLevels order is only valid on a trade hub (a colony OR a station carrying a LogiBaseDB) —
            // Execute reads that blob, so guard here rather than throwing on execution.
            if (_type == OrderTypes.SetDesiredLevels)
                return _entityCommanding.HasDataBlob<LogiBaseDB>();

            return true;


        }
        return false;
    }

    public static void CreateCommand(Entity entity, OrderTypes ordertype )
    {
        SetLogisticsOrder cmd = new SetLogisticsOrder()
        {
            EntityCommandingGuid = entity.Id,
            RequestingFactionGuid = entity.FactionOwnerID,
            _type = ordertype
        };

        entity.Manager.Game.OrderHandler.HandleOrder(cmd);
    }

    public static void CreateCommand_SetBaseItems(Entity entity, Dictionary<ICargoable,(int count, int demandSupplyWeight)> changes )
    {
        SetLogisticsOrder cmd = new SetLogisticsOrder()
        {
            EntityCommandingGuid = entity.Id,
            RequestingFactionGuid = entity.FactionOwnerID,
            _type = OrderTypes.SetBaseItems,
            _baseChanges = changes
        };

        entity.Manager.Game.OrderHandler.HandleOrder(cmd);
    }

    /// <summary>
    /// Set a trade hub's per-item stockpile MIN/MAX targets (<see cref="LogiBaseDB.DesiredLevels"/>) — the write path
    /// for the "Set Stockpile Min/Max Target" order. The READER is already live: <c>LogisticsCycle.UpdateListings</c>
    /// prices each item's shortfall/surplus off these levels every hour, so this is the missing half of that lever.
    /// A <c>(0,0)</c> pair REMOVES the item's target (the hub stops caring about it). The write is deferred into
    /// <see cref="Execute"/> (the sim thread) ON PURPOSE — <c>DesiredLevels</c> is iterated by the base processor, so a
    /// UI-thread mutation mid-iteration would throw "collection was modified" (unlike a harmless torn scalar write).
    /// The AI sets the same targets through this same order (one verb, both seats). Valid on a colony OR a station.
    /// </summary>
    public static void CreateCommand_SetDesiredLevels(Entity entity, Dictionary<ICargoable,(int minVal, int maxVal)> changes )
    {
        SetLogisticsOrder cmd = new SetLogisticsOrder()
        {
            EntityCommandingGuid = entity.Id,
            RequestingFactionGuid = entity.FactionOwnerID,
            _type = OrderTypes.SetDesiredLevels,
            _desiredChanges = changes
        };

        entity.Manager.Game.OrderHandler.HandleOrder(cmd);
    }

    public class Changes//maybe should be a struct, but would need to not use a dictionary and need to check mutability.
    {
        public Dictionary<string, double> VolumeAmounts;
        public int MaxMass;

        public Changes()
        {
            VolumeAmounts = new Dictionary<string, double>();
            MaxMass = 0;
        }
    }

    public static void CreateCommand_SetShipTypeAmounts(Entity entity, Changes changes )
    {

        SetLogisticsOrder cmd = new SetLogisticsOrder();
        cmd.EntityCommandingGuid = entity.Id;
        cmd.RequestingFactionGuid = entity.FactionOwnerID;
        cmd._type = OrderTypes.SetShipTypeAmounts;
        cmd._shipChanges = changes;

        entity.Manager.Game.OrderHandler.HandleOrder(cmd);

        entity.Manager.Game.ProcessorManager.GetProcessor<LogiShipperDB>().ProcessEntity(entity, 0);
        entity.Manager.Game.ProcessorManager.GetProcessor<LogiBaseDB>().ProcessManager(entity.Manager, 0);
        cmd.UpdateDetailString();
    }

    internal override void Execute(DateTime atDateTime)
    {
        if (!IsRunning)
        {
            IsRunning = true;
            switch (_type)
            {
                case OrderTypes.SetBaseItems:
                {

                    var db = EntityCommanding.GetDataBlob<LogiBaseDB>();
                    foreach (var item in _baseChanges)
                    {
                        if (item.Value.count == 0)
                            db.ListedItems.Remove(item.Key);
                        else
                            db.ListedItems[item.Key] = item.Value;
                    }
                    //NOTE: possibly some conflict here.
                    //we might need to consider what to do if a ship is already contracted to grab stuff,
                    //and then we change this and remove the items before the ship has collected them.

                    break;
                }


                case OrderTypes.SetDesiredLevels:
                {
                    var db = EntityCommanding.GetDataBlob<LogiBaseDB>();
                    foreach (var item in _desiredChanges)
                    {
                        if (item.Value.minVal == 0 && item.Value.maxVal == 0)
                            db.DesiredLevels.Remove(item.Key);
                        else
                            db.DesiredLevels[item.Key] = item.Value;
                    }
                    break;
                }

                case OrderTypes.AddLogiShipDB:
                {
                    var db = new LogiShipperDB();
                    EntityCommanding.SetDataBlob(db);
                    break;
                }
                case OrderTypes.RemoveLogiShipDB:
                {
                    EntityCommanding.RemoveDataBlob<LogiShipperDB>();
                    break;
                }
                case OrderTypes.SetShipTypeAmounts:
                {
                    var db = EntityCommanding.GetDataBlob<LogiShipperDB>();
                    foreach (var item in _shipChanges.VolumeAmounts)
                    {
                        db.TradeSpace[item.Key] = item.Value;
                    }

                    db.MaxTradeMass = _shipChanges.MaxMass;

                    break;
                }

            }
        }
    }

    internal override bool IsFinished()
    {
        return _isFinished = true;
    }

    public override EntityCommand Clone()
    {
        throw new NotImplementedException();
    }
}