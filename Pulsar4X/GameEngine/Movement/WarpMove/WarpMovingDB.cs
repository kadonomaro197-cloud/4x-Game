using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Pulsar4X.Orbital;
using Pulsar4X.Engine;
using Pulsar4X.Orbits;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Movement
{


    /// <summary>
    /// This datablob gets added to an entity when that entity is doing non-newtonion translation type movement.
    /// It gets removed from the entity once the entity has finished the translation.
    /// </summary>
    public class WarpMovingDB : BaseDataBlob
    {

        #region InWarpData
        [JsonProperty]
        public bool HasStarted { get; internal set; } = false;

        [JsonProperty]
        public DateTime LastProcessDateTime = new DateTime();
        [JsonProperty]
        public float Heading_Radians { get; internal set; }

        [JsonProperty]
        public Vector3 CurrentNonNewtonionVectorMS { get; internal set; }

        /// <summary>The speed (m/s) this ship ACTUALLY warps at — the fleet cap (slowest unit) when moving as a
        /// fleet, else the ship's own MaxSpeed. The processor (<see cref="WarpMoveProcessor.StartNonNewtTranslation"/>)
        /// MUST read this, not <c>WarpAbilityDB.MaxSpeed</c>, or the cap is dropped and a fast ship races ahead even
        /// on a fleet move (the "Aegis lagged / others arrived early" bug). 0 (old save) → fall back to MaxSpeed.</summary>
        [JsonProperty]
        public double WarpSpeed_mps { get; internal set; }

        [JsonProperty]
        internal Vector2 _position;
        [JsonProperty]
        internal Entity _parentEnitity;

        [JsonProperty]
        internal bool IsAtTarget { get; set; }

        #endregion


        #region StartPointData

        [JsonProperty]
        public DateTime EntryDateTime { get; internal set; }
        [JsonProperty]
        public Vector3 SavedNewtonionVector { get; internal set; }

        [JsonProperty]
        public Vector3 EntryPointAbsolute { get; internal set; }

        #endregion

        #region EndPointData

        [JsonProperty]
        public DateTime PredictedExitTime { get; internal set; }

        [JsonProperty]
        public Vector3 ExitPointAbsolute { get; internal set; }

        [JsonProperty]
        public Vector3 ExitPointrelative { get; internal set; }
        [JsonProperty]
        public KeplerElements EndpointTargetOrbit { get; private set; }

        [JsonProperty]
        internal Entity? TargetEntity;


        [JsonProperty] 
        internal PositionDB TargetPositionDB;
        public PositionDB GetTargetPosDB
        {
            get { return TargetPositionDB; }
        }

        #endregion


        public WarpMovingDB()
        {
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="T:Pulsar4X.ECSLib.TranslateMoveDB"/> class.
        /// Use this one to move to a specific postion vector.
        /// </summary>
        /// <param name="targetPosition_m">Target position in Meters.</param>
        public WarpMovingDB(Entity thisEntity, Vector3 targetPosition_m)
        {
            ExitPointAbsolute = targetPosition_m;

            var startState = MoveMath.GetAbsoluteState(thisEntity);

            ExitPointAbsolute = targetPosition_m;
            EntryPointAbsolute = startState.pos;
            EntryDateTime = thisEntity.Manager.ManagerSubpulses.StarSysDateTime;
            ExitPointrelative = Vector3.Zero;
            //PredictedExitTime = targetIntercept.atDateTime;
            SavedNewtonionVector = MoveMath.GetRelativeState(thisEntity).Velocity; //TODO: this needs to check GameSettings.UseRelativeVelocity
            TargetEntity = null;
            WarpSpeed_mps = thisEntity.GetDataBlob<WarpAbilityDB>().MaxSpeed; // no fleet cap on a direct-position warp

            Heading_Radians = (float)Vector3.AngleBetween(startState.pos, ExitPointAbsolute);

            Heading_Radians = (float)Math.Atan2(targetPosition_m.Y, targetPosition_m.X);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="targetPositiondb"></param>
        /// <param name="offsetPosition">normaly you want to move to a position next to the entity, this is
        /// a position relative to the entity you're wanting to move to</param>
        public WarpMovingDB(Entity thisEntity, Entity targetEntity, Vector3 offsetPosition, KeplerElements endpointTargetOrbit, double speedCap_m = 0)
        {
            EntryDateTime = thisEntity.Manager.ManagerSubpulses.StarSysDateTime;
            // speedCap_m (> 0) makes this ship warp at the FLEET's slowest speed, so a fleet arrives together. The
            // ETA (PredictedExitTime) bakes in here, so the cap only needs to reach GetInterceptPosition.
            var targetIntercept = WarpMath.GetInterceptPosition(thisEntity, targetEntity, EntryDateTime, offsetPosition, speedCap_m);
            // The actual travel speed must match the speed the ETA above was computed at — the fleet cap when set,
            // else the ship's own MaxSpeed. The processor reads WarpSpeed_mps, so the cap reaches the real velocity.
            WarpSpeed_mps = speedCap_m > 0 ? speedCap_m : thisEntity.GetDataBlob<WarpAbilityDB>().MaxSpeed;

            var startState = MoveMath.GetAbsoluteState(thisEntity);
            ExitPointAbsolute = targetIntercept.position + offsetPosition;
            EntryPointAbsolute = startState.pos;
            ExitPointrelative = offsetPosition;
            PredictedExitTime = targetIntercept.etiDateTime;
            EndpointTargetOrbit = endpointTargetOrbit;
            SavedNewtonionVector = MoveMath.GetRelativeState(thisEntity).Velocity; //TODO: this needs to check GameSettings.UseRelativeVelocity
            TargetEntity = targetEntity;
            TargetPositionDB = targetEntity.GetDataBlob<PositionDB>();
            Heading_Radians = (float)Vector3.AngleBetween(startState.pos, ExitPointAbsolute);
        }

        public WarpMovingDB(WarpMovingDB db)
        {
            LastProcessDateTime = db.LastProcessDateTime;
            SavedNewtonionVector = db.SavedNewtonionVector;
            EntryPointAbsolute = db.EntryPointAbsolute;
            ExitPointAbsolute = db.ExitPointAbsolute;
            CurrentNonNewtonionVectorMS = db.CurrentNonNewtonionVectorMS;
            EndpointTargetOrbit = db.EndpointTargetOrbit;
            IsAtTarget = db.IsAtTarget;
            TargetEntity = db.TargetEntity;
            WarpSpeed_mps = db.WarpSpeed_mps;

            HasStarted = db.HasStarted;
            TargetPositionDB = db.TargetPositionDB;

        }
        // JSON deserialization callback.
        [OnDeserialized]
        private void Deserialized(StreamingContext context)
        {
            
        }

        public override object Clone()
        {
            return new WarpMovingDB(this);
        }

        internal override void OnSetToEntity()
        {
            if (OwningEntity.HasDataBlob<OrbitDB>())
            {
                OwningEntity.RemoveDataBlob<OrbitDB>();
            }
            if (OwningEntity.HasDataBlob<OrbitUpdateOftenDB>())
            {
                OwningEntity.RemoveDataBlob<OrbitUpdateOftenDB>();
            }
            if (OwningEntity.HasDataBlob<NewtonMoveDB>())
            {
                OwningEntity.RemoveDataBlob<NewtonMoveDB>();
            }
            if (OwningEntity.HasDataBlob<NewtonSimpleMoveDB>())
            {
                OwningEntity.RemoveDataBlob<NewtonSimpleMoveDB>();
            }

        }
    }
}
