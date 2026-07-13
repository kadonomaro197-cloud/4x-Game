using System;
using System.Linq;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;
using Pulsar4X.Events;
using Pulsar4X.Ships;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// Site Engine SE-3c — the player order that LOADS a ground unit into a transport ship's troop bays (the front door
    /// for <see cref="GroundTransport.TryLoadUnit"/>, which was engine-only until now). Issued to the SHIP (which carries
    /// an <see cref="Pulsar4X.Engine.Orders.OrderableDB"/>), so it rides the real player order path
    /// (<c>Game.OrderHandler.HandleOrder</c>) — mirrors <see cref="Pulsar4X.Galaxy.PlaceInstallationOnHexOrder"/>. The
    /// unit is named by its stable <see cref="GroundUnit.UnitId"/> on the body it stands on.
    ///
    /// Additive: nothing issues this yet (a client button is the UI slice), and it only wraps an existing helper, so the
    /// engine is byte-identical.
    /// </summary>
    public class LoadTroopsOrder : EntityCommand
    {
        /// <summary>The body the unit currently stands on.</summary>
        public int BodyEntityId { get; private set; }
        /// <summary>The stable id of the ground unit to load.</summary>
        public int UnitId { get; private set; }

        public override ActionLaneTypes ActionLanes => ActionLaneTypes.InstantOrder;
        public override bool IsBlocking => false;
        public override string Name => "Load Troops";
        public override string Details => "Load a ground unit standing on a body into this ship's troop bays";

        private Entity _entityCommanding;
        internal override Entity EntityCommanding => _entityCommanding;

        public static LoadTroopsOrder CreateCommand(Entity ship, Entity body, int unitId)
        {
            return new LoadTroopsOrder()
            {
                _entityCommanding = ship,
                EntityCommandingGuid = ship.Id,
                RequestingFactionGuid = ship.FactionOwnerID,
                BodyEntityId = body.Id,
                UnitId = unitId,
            };
        }

        public override EntityCommand Clone() => throw new NotImplementedException();

        internal override bool IsFinished() => _isFinished;

        internal override void Execute(DateTime atDateTime)
        {
            var ship = _entityCommanding;
            if (ship?.Manager == null) return;
            if (!ship.Manager.TryGetEntityById(BodyEntityId, out var body)) return;
            if (!body.TryGetDataBlob<GroundForcesDB>(out var forces)) return;

            var unit = forces.Units.FirstOrDefault(u => u.UnitId == UnitId);
            if (unit == null) return;

            if (GroundTransport.TryLoadUnit(ship, body, unit))
            {
                _isFinished = true;
                EventManager.Instance.Publish(
                    Event.Create(EventType.ProductionCompleted, atDateTime,
                        $"{unit.Name} loaded aboard ship #{ship.Id}",
                        RequestingFactionGuid, ship.Manager.ManagerID, ship.Id));
            }
        }

        internal override bool IsValidCommand(Game game)
            => _entityCommanding != null && _entityCommanding.HasDataBlob<ShipInfoDB>();
    }
}
