using System;
using System.Linq;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;
using Pulsar4X.Events;
using Pulsar4X.Ships;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// Site Engine SE-3c — the player order that LANDS a ground unit from a transport ship onto a target body's region
    /// (the front door for <see cref="GroundTransport.TryLandUnit"/>). Issued to the SHIP; the unit is named by its
    /// stable <see cref="GroundUnit.UnitId"/> among the ship's loaded units. Landing is gated (inside the helper) on the
    /// ship being at the body AND holding the orbit (no foreign ship present) — you win the space over a world before
    /// you put boots on it. Mirrors <see cref="Pulsar4X.Galaxy.PlaceInstallationOnHexOrder"/>.
    ///
    /// Additive: nothing issues this yet (a client button is the UI slice), and it only wraps an existing helper, so the
    /// engine is byte-identical.
    /// </summary>
    public class LandTroopsOrder : EntityCommand
    {
        /// <summary>The body to land on.</summary>
        public int TargetBodyEntityId { get; private set; }
        /// <summary>The stable id of the loaded unit to land.</summary>
        public int UnitId { get; private set; }
        /// <summary>The region on the target body to land into.</summary>
        public int RegionIndex { get; private set; }

        public override ActionLaneTypes ActionLanes => ActionLaneTypes.InstantOrder;
        public override bool IsBlocking => false;
        public override string Name => "Land Troops";
        public override string Details => "Land a ground unit from this ship onto a target body's region";

        private Entity _entityCommanding;
        internal override Entity EntityCommanding => _entityCommanding;

        public static LandTroopsOrder CreateCommand(Entity ship, Entity targetBody, int unitId, int regionIndex)
        {
            return new LandTroopsOrder()
            {
                _entityCommanding = ship,
                EntityCommandingGuid = ship.Id,
                RequestingFactionGuid = ship.FactionOwnerID,
                TargetBodyEntityId = targetBody.Id,
                UnitId = unitId,
                RegionIndex = regionIndex,
            };
        }

        public override EntityCommand Clone() => throw new NotImplementedException();

        internal override bool IsFinished() => _isFinished;

        internal override void Execute(DateTime atDateTime)
        {
            var ship = _entityCommanding;
            if (ship?.Manager == null) return;
            if (!ship.Manager.TryGetEntityById(TargetBodyEntityId, out var body)) return;
            if (!ship.TryGetDataBlob<GroundTransportDB>(out var transport)) return;

            var unit = transport.LoadedUnits.FirstOrDefault(u => u.UnitId == UnitId);
            if (unit == null) return;

            if (GroundTransport.TryLandUnit(ship, body, unit, RegionIndex))
            {
                _isFinished = true;
                EventManager.Instance.Publish(
                    Event.Create(EventType.ProductionCompleted, atDateTime,
                        $"{unit.Name} landed on region {RegionIndex}",
                        RequestingFactionGuid, ship.Manager.ManagerID, ship.Id));
            }
        }

        internal override bool IsValidCommand(Game game)
            => _entityCommanding != null && _entityCommanding.HasDataBlob<ShipInfoDB>();
    }
}
