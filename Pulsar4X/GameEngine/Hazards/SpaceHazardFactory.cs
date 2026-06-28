using System;
using System.Collections.Generic;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Movement;
using Pulsar4X.Names;
using Pulsar4X.Orbital;

namespace Pulsar4X.Hazards
{
    /// <summary>
    /// Builds hazard entities (the gas cloud and the solar flare). Both are an entity with a
    /// <see cref="SpaceHazardDB"/> (the effect), a <c>PositionDB</c> (where it is — parented to the star so it
    /// stays put as the system updates), a name and a "visible by default" tag so it shows on the map.
    /// </summary>
    public static class SpaceHazardFactory
    {
        /// <summary>
        /// A permanent gas cloud — sensor-degrading, movement-slowing, warp-slowing, slowly corrosive terrain.
        /// Placed in some (non-Sol) systems so deep space has interesting geography to cross or hide in.
        /// </summary>
        public static Entity CreateGasCloud(StarSystem system, Entity star, Vector3 offsetFromStar_m, double radius_m, string name = "Gas Cloud")
        {
            var hazard = new SpaceHazardDB
            {
                HazardType = SpaceHazardType.GasCloud,
                Radius_m = radius_m,
                SensorRangeMultiplier = 0.35,   // sensors badly degraded inside — a place to hide / be ambushed
                MoveSpeedMultiplier = 0.5,      // sub-light thrust slowed, like wading through mud
                WarpSpeedMultiplier = 0.25,     // warp crawls through the dense medium
                DamagePerSecond = 50.0,         // slow corrosive hull damage — uncomfortable to loiter in
                BlindsSensors = false,
                IsTransient = false,
            };

            var pos = new PositionDB(offsetFromStar_m, star);
            var blobs = new List<BaseDataBlob>
            {
                hazard,
                pos,
                new NameDB(name),
                new VisibleByDefaultDB(),
            };

            var entity = Entity.Create();
            entity.FactionOwnerID = Game.NeutralFactionId;
            system.AddEntity(entity, blobs);
            return entity;
        }

        /// <summary>
        /// A transient solar flare hugging the star — blinds sensors in the area and irradiates ships caught in
        /// it, then fades. Created by <c>SolarFlareProcessor</c> on a schedule so the home star has "weather".
        /// </summary>
        public static Entity CreateSolarFlare(StarSystem system, Entity star, DateTime startedAt, TimeSpan duration, double maxRadius_m, string name = "Solar Flare")
        {
            var hazard = new SpaceHazardDB
            {
                HazardType = SpaceHazardType.SolarFlare,
                Radius_m = 1.0,                 // starts as a point, grows toward MaxRadius_m at peak (see SpaceHazardProcessor)
                MaxRadius_m = maxRadius_m,
                SensorRangeMultiplier = 0.0,    // washes sensors out entirely in the area
                MoveSpeedMultiplier = 1.0,      // a flare doesn't slow you — it fries you
                WarpSpeedMultiplier = 1.0,
                DamagePerSecond = 500.0,        // radiation / heat damage
                BlindsSensors = true,
                IsTransient = true,
                StartedAt = startedAt,
                ExpiresAt = startedAt + duration,
            };

            // Centred on the star (relative position zero, parented to the star) so it sits at the star.
            var pos = new PositionDB(Vector3.Zero, star);
            var blobs = new List<BaseDataBlob>
            {
                hazard,
                pos,
                new NameDB(name),
                new VisibleByDefaultDB(),
            };

            var entity = Entity.Create();
            entity.FactionOwnerID = Game.NeutralFactionId;
            system.AddEntity(entity, blobs);
            return entity;
        }
    }
}
