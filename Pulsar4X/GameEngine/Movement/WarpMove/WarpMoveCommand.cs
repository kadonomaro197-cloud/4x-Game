using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Pulsar4X.Orbital;
using Pulsar4X.Extensions;
using Pulsar4X.Colonies;
using Pulsar4X.Energy;
using Pulsar4X.Fleets;
using Pulsar4X.Names;
using Pulsar4X.Orbits;
using Pulsar4X.Ships;
using Pulsar4X.Galaxy;
using Pulsar4X.Engine.Orders;
using Pulsar4X.Engine;

namespace Pulsar4X.Movement
{
    public class WarpMoveCommand : EntityCommand
    {

        public override string Name
        {
            get
            {
                if(_targetEntity == null || _entityCommanding == null)
                    return "Warp Move";

                return "Warp Move to " + _targetEntity.GetName(_entityCommanding.FactionOwnerID);
            }
        }

        public override string Details
        {
            get
            {
                string targetName = _targetEntity.GetDataBlob<NameDB>().GetName(_factionEntity);
                return "Warp to + " + Stringify.Distance(EndpointRelitivePosition.Length()) + " from " + targetName;
            }
        }

        public override ActionLaneTypes ActionLanes => ActionLaneTypes.Movement;
        public override bool IsBlocking => true;

        [JsonProperty]
        public int TargetEntityGuid { get; set; }

        private Entity _targetEntity;


        [JsonIgnore]
        Entity _factionEntity;
        WarpMovingDB _warpingDB;
        bool _warpBlockLogged;     // so a "can't warp" reason is logged ONCE, not every tick
        bool _permanentlyBlocked;  // no drive / no reactor — clear the order instead of retrying forever


        Entity _entityCommanding;
        internal override Entity EntityCommanding { get { return _entityCommanding; } }

        public DateTime TransitStartDateTime;
        public Vector3 EndpointRelitivePosition { get; set; }
        public Vector3 EndpointTargetExpendDeltaV;
        /// <summary>Warp speed cap in m/s (0 = the ship's own MaxSpeed). A fleet move sets this to the SLOWEST
        /// unit's speed so every ship arrives together instead of scattering. Persisted so a reloaded mid-warp
        /// order keeps the capped ETA.</summary>
        [JsonProperty]
        public double SpeedCap_m { get; set; }
        /// <summary>
        /// the orbit we want to be in at the target.
        /// </summary>
        public KeplerElements EndpointTargetOrbit;

        public static WarpMoveCommand CreateCommand(
            Entity orderEntity,
            Entity targetEntity,
            DateTime transitStartDatetime,
            Vector3 endpointRelativePos = new Vector3())
        {
            var datetimeArrive = WarpMath.GetInterceptPosition(orderEntity, targetEntity, transitStartDatetime, endpointRelativePos);

            var cmd = new WarpMoveCommand()
            {
                RequestingFactionGuid = orderEntity.FactionOwnerID,
                EntityCommandingGuid = orderEntity.Id,
                CreatedDate = orderEntity.Manager.ManagerSubpulses.StarSysDateTime,
                TargetEntityGuid = targetEntity.Id,
                EndpointRelitivePosition = endpointRelativePos,
                TransitStartDateTime = transitStartDatetime,
            };
            if (targetEntity.GetDataBlob<PositionDB>().MoveType != PositionDB.MoveTypes.None)
            {
                var sgp = GeneralMath.StandardGravitationalParameter(targetEntity.GetDataBlob<MassVolumeDB>().MassTotal + orderEntity.GetDataBlob<MassVolumeDB>().MassTotal);
                cmd.EndpointTargetOrbit = OrbitMath.KeplerCircularFromPosition(sgp, endpointRelativePos, datetimeArrive.Item2);;
            }
            orderEntity.Manager.Game.OrderHandler.HandleOrder(cmd);
            return cmd;
        }

        public static WarpMoveCommand CreateCommand(
            Entity orderEntity,
            Entity targetEntity,
            DateTime transitStartDatetime,
            KeplerElements insertonTargetOrbit,
            Vector3 exitPointRelative)
        {
            var targetOffsetPos_m = exitPointRelative;
            var datetimeArrive = WarpMath.GetInterceptPosition(orderEntity, targetEntity, transitStartDatetime, targetOffsetPos_m);

            var cmd = new WarpMoveCommand()
            {
                RequestingFactionGuid = orderEntity.FactionOwnerID,
                EntityCommandingGuid = orderEntity.Id,
                CreatedDate = orderEntity.Manager.ManagerSubpulses.StarSysDateTime,
                TargetEntityGuid = targetEntity.Id,
                EndpointRelitivePosition = targetOffsetPos_m,
                EndpointTargetOrbit = insertonTargetOrbit,
                TransitStartDateTime = transitStartDatetime,
            };
            if (targetEntity.GetDataBlob<PositionDB>().MoveType != PositionDB.MoveTypes.None)
            {
                var sgp = GeneralMath.StandardGravitationalParameter(targetEntity.GetDataBlob<MassVolumeDB>().MassTotal + orderEntity.GetDataBlob<MassVolumeDB>().MassTotal);
                cmd.EndpointTargetOrbit = OrbitMath.KeplerCircularFromPosition(sgp, targetOffsetPos_m, datetimeArrive.Item2);;
            }

            orderEntity.Manager.Game.OrderHandler.HandleOrder(cmd);


            return cmd;
        }

        /// <summary>
        /// Creates a warp order with an attempted simplenewt circular orbit post warp.
        /// DOES NOT QUEUE THE COMMAND. Game.OrderHandler.HandleOrder(cmd) should be called
        /// </summary>
        /// <param name="orderEntity"></param>
        /// <param name="targetEntity"></param>
        /// <param name="transitStartDatetime"></param>
        /// <returns></returns>
        public static WarpMoveCommand CreateCommandEZ(
            Entity orderEntity,
            Entity targetEntity,
            DateTime transitStartDatetime,
            double speedCap_m = 0)
        {
            //if target is a colony, just make the target the parent planet.
            if(targetEntity.TryGetDataBlob<ColonyInfoDB>(out ColonyInfoDB info))
                targetEntity = info.PlanetEntity;

            (Vector3 pos, Vector3 vel) departureState;
            if(orderEntity.Manager.Game.Settings.UseRelativeVelocity)
            {
                departureState = MoveMath.GetRelativeFutureState(orderEntity, transitStartDatetime);
            }
            else
                departureState = MoveMath.GetAbsoluteState(orderEntity, transitStartDatetime);

            var cmd = new WarpMoveCommand()
            {
                RequestingFactionGuid = orderEntity.FactionOwnerID,
                EntityCommandingGuid = orderEntity.Id,
                CreatedDate = orderEntity.Manager.ManagerSubpulses.StarSysDateTime,
                TargetEntityGuid = targetEntity.Id,
                TransitStartDateTime = transitStartDatetime,
                SpeedCap_m = speedCap_m,

            };

            switch (targetEntity.GetDataBlob<PositionDB>().MoveType) //if the targetEntity's movetype is this:
            {
                case PositionDB.MoveTypes.None: //this means it's a grav anomaly, jump point
                {
                    break;
                }
                case PositionDB.MoveTypes.Orbit:
                {
                    var sgp = OrbitMath.SGP(targetEntity, orderEntity);
                    var lowOrbitRadius = OrbitMath.LowOrbitRadius(targetEntity);
                    var perpVec = Vector3.Normalise(new Vector3(departureState.vel.Y * -1, departureState.vel.X, 0));
                    var lowOrbitPos = perpVec * lowOrbitRadius;
                    (Vector3 pos, DateTime eti) targetIntercept  = WarpMath.GetInterceptPosition(orderEntity, targetEntity, transitStartDatetime, lowOrbitPos, speedCap_m);
                    var lowOrbit = OrbitMath.KeplerCircularFromPosition(sgp, lowOrbitPos, targetIntercept.eti);
                    var lowOrbitState = OrbitMath.GetStateVectors(lowOrbit, targetIntercept.eti);
                    var targetEntityOrbitDb = targetEntity.GetDataBlob<OrbitDB>();
                    Vector3 insertionVector = OrbitProcessor.GetOrbitalInsertionVector(departureState.vel, targetEntityOrbitDb, targetIntercept.eti);
                    var deltaV = insertionVector - (Vector3)lowOrbitState.velocity;

                    cmd.EndpointRelitivePosition = lowOrbitPos;
                    cmd.EndpointTargetOrbit = lowOrbit;
                    cmd.EndpointTargetExpendDeltaV = deltaV;
                    break;
                }
                case PositionDB.MoveTypes.NewtonSimple:
                {
                    //recursive call here, if the target we're trying to go to is manuvering somewhere,
                    //then just target that targets target...
                    //TODO we should check if the target is another empire, in such case we probilby shouldn't know the target?
                    //but maybe we can guess it. idk.
                    var wp = targetEntity.GetDataBlob<WarpMovingDB>();
                    cmd = CreateCommandEZ(orderEntity, wp.TargetEntity, transitStartDatetime);
                    break;
                }
                case PositionDB.MoveTypes.NewtonComplex:
                {
                    //recursive call here, if the target we're trying to go to is manuvering somewhere,
                    //then just target that targets target...
                    //TODO we should check if the target is another empire, in such case we probilby shouldn't know the target?
                    //but maybe we can guess it. idk.
                    var wp = targetEntity.GetDataBlob<WarpMovingDB>();
                    cmd = CreateCommandEZ(orderEntity, wp.TargetEntity, transitStartDatetime);
                    break;
                }
                case PositionDB.MoveTypes.Warp:
                {
                    //recursive call here, if the target we're trying to go to is warping somewhere,
                    //then just target that targets target...
                    //TODO we should check if the target is another empire, in such case we probilby shouldn't know the target?
                    //but maybe we can guess it. idk.
                    var wp = targetEntity.GetDataBlob<WarpMovingDB>();
                    cmd = CreateCommandEZ(orderEntity, wp.TargetEntity, transitStartDatetime);
                    break;
                }
                default:
                    throw new NotImplementedException();
            }

            //orderEntity.Manager.Game.OrderHandler.HandleOrder(cmd);


            return cmd;
        }

        internal override bool IsValidCommand(Game game)
        {
            if (CommandHelpers.IsCommandValid(game.GlobalManager, RequestingFactionGuid, EntityCommandingGuid, out _factionEntity, out _entityCommanding))
            {
                if (game.GlobalManager.TryGetGlobalEntityById(TargetEntityGuid, out _targetEntity))
                {
                    return true;
                }
            }
            return false;
        }

        internal override void Execute(DateTime atDateTime)
        {
            if (!IsRunning)
            {
                // Visibility: these blockers used to fail SILENTLY (the old "FIXME: alert the player?") — the ship
                // just sat there with no message, exactly the "I gave a move order and it didn't move" symptom. Now
                // each one says WHY in the [WARP] log (once, not every tick).
                if (!_entityCommanding.TryGetDataBlob<WarpAbilityDB>(out var warpDB))
                {
                    if (!_warpBlockLogged) { WarpMoveProcessor.WarpBlocked(_entityCommanding, "no warp drive on this design"); _warpBlockLogged = true; }
                    _permanentlyBlocked = true;   // never going to warp; let the order clear
                    return;
                }
                if (!_entityCommanding.TryGetDataBlob<EnergyGenAbilityDB>(out var powerDB))
                {
                    if (!_warpBlockLogged) { WarpMoveProcessor.WarpBlocked(_entityCommanding, "no reactor / power supply to run the warp drive"); _warpBlockLogged = true; }
                    _permanentlyBlocked = true;
                    return;
                }
                string eType = warpDB.EnergyType;
                double estored = powerDB.EnergyStored.TryGetValue(eType, out var es) ? es : 0;
                double creationCost = warpDB.BubbleCreationCost;

                if (creationCost > estored)
                {
                    // Temporary — keep the order pending so it warps the moment the reactor charges. Reason logged once.
                    if (!_warpBlockLogged)
                    {
                        WarpMoveProcessor.WarpBlocked(_entityCommanding, "not enough energy for the warp bubble yet — needs "
                            + creationCost.ToString("0") + " " + eType + ", reactor has " + estored.ToString("0")
                            + " stored (advance time to let it charge, or check reactor fuel)");
                        _warpBlockLogged = true;
                    }
                    return;
                }

                _warpingDB = new WarpMovingDB(_entityCommanding, _targetEntity, EndpointRelitivePosition, EndpointTargetOrbit, SpeedCap_m);

                //if we're already in a warp moving state,
                //then we should carry over the SavedNewtonionVector.
                //this will happen in the case of serveying grav anomalies.
                if (_entityCommanding.TryGetDataBlob<WarpMovingDB>(out var warpMovingDB))
                {
                    _warpingDB.SavedNewtonionVector = warpMovingDB.SavedNewtonionVector;
                }

                EntityCommanding.SetDataBlob(_warpingDB);

                WarpMoveProcessor.StartNonNewtTranslation(EntityCommanding);
                IsRunning = true;

                //debug code:
                double distance = (_warpingDB.EntryPointAbsolute - _warpingDB.ExitPointAbsolute).Length();
                double time = distance / _entityCommanding.GetDataBlob<WarpAbilityDB>().MaxSpeed;
                //Assert.AreEqual((_warpingDB.PredictedExitTime - _warpingDB.EntryDateTime).TotalSeconds, time, 1.0e-10);

            }
        }

        internal override bool IsFinished()
        {
            if (_permanentlyBlocked) return _isFinished = true;   // no drive / no reactor — clear, don't hang the move lane
            if(_warpingDB != null)
                _isFinished = _warpingDB.IsAtTarget;
            else
                _isFinished = false;
            return _isFinished;
        }

        public override EntityCommand Clone()
        {
            throw new NotImplementedException();
        }
    }

    public class WarpFleetTowardsTargetOrder : EntityCommand
    {
        public override ActionLaneTypes ActionLanes => ActionLaneTypes.Movement;

        public override bool IsBlocking => true;

        public override string Name => "Move Fleet Towards Target";

        public override string Details => "";

        private Entity _entityCommanding;

        internal override Entity EntityCommanding => _entityCommanding;

        public Entity Target { get; set; }

        List<EntityCommand> _shipCommands = new List<EntityCommand>();

        public override EntityCommand Clone()
        {
            throw new NotImplementedException();
        }

        internal override bool IsFinished()
        {
            if(!IsRunning)
                _isFinished = false;
            else
            {
                foreach (var command in _shipCommands)
                {
                    if (!command.IsFinished())
                        return _isFinished = false;
                }
                _isFinished = true;
            }
            return _isFinished;
        }

        internal override void Execute(DateTime atDateTime)
        {
            if(IsRunning) return;
            if(!_entityCommanding.TryGetDataBlob<FleetDB>(out var fleetDB)) return;
            // Get all the ships we need to add the movement command to — RECURSIVE, so a fleet organised into role
            // sub-fleets still moves as ONE (its nested ships come along, not just the direct children). Identical to
            // the old direct-children walk for a flat fleet.
            var ships = FleetTools.AllShipsRecursive(_entityCommanding);

            // Fleet moves as ONE: cap every ship to the SLOWEST unit's warp speed so they arrive together (same
            // as MoveToSystemBodyOrder). 0 = no warp-capable ship → per-ship speed (cap ignored when <= 0).
            double fleetWarpFloor = 0;
            foreach(var ship in ships)
            {
                if(!ship.TryGetDataBlob<WarpAbilityDB>(out var wab)) continue;
                if(wab.MaxSpeed > 0 && (fleetWarpFloor == 0 || wab.MaxSpeed < fleetWarpFloor))
                    fleetWarpFloor = wab.MaxSpeed;
            }

            foreach(var ship in ships)
            {
                //don't give move order if ship is already at location.
                var shipParent = ship.GetDataBlob<PositionDB>().Parent;
                if(shipParent == Target)
                    continue;
                if (Target.TryGetDataBlob<ColonyInfoDB>(out var colonyDB) && colonyDB.PlanetEntity == shipParent)
                    continue;
                if(!ship.HasDataBlob<WarpAbilityDB>()) continue;

                var shipCommand = WarpMoveCommand.CreateCommandEZ(ship, Target, atDateTime, fleetWarpFloor);

                _shipCommands.Add(shipCommand);
                ship.Manager.Game.OrderHandler.HandleOrder(shipCommand);
            }
            IsRunning = true;
        }

        public static WarpFleetTowardsTargetOrder CreateCommand(Entity fleet, Entity target)
        {
            var order = new WarpFleetTowardsTargetOrder()
            {
                RequestingFactionGuid = fleet.FactionOwnerID,
                EntityCommandingGuid = fleet.Id,
                _entityCommanding = fleet,
                Target = target,
            };

            return order;
        }

        internal override bool IsValidCommand(Game game)
        {
            return true;
        }
    }
}
