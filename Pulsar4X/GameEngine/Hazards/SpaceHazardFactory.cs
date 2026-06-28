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
    /// Builds hazard entities. Each is an entity with a <see cref="SpaceHazardDB"/> (a radius + a list of typed
    /// effects), a <c>PositionDB</c> (parented to the star so it stays put), a name and a "visible by default"
    /// tag. The named flavours below are convenience constructors; hazards can also be authored as data — see
    /// <see cref="CreateFromEffects"/> (used by the JSON system-blueprint loader).
    /// </summary>
    public static class SpaceHazardFactory
    {
        /// <summary>The general builder every other one funnels through: place a hazard of a given radius and
        /// effect list at <paramref name="offsetFromStar_m"/> relative to <paramref name="star"/>.</summary>
        public static Entity CreateFromEffects(StarSystem system, Entity star, Vector3 offsetFromStar_m,
            double radius_m, SpaceHazardType type, List<HazardEffect> effects, string name,
            bool isTransient = false, DateTime startedAt = default, DateTime expiresAt = default, double maxRadius_m = 0,
            double innerRadius_m = 0)
        {
            var hazard = new SpaceHazardDB
            {
                HazardType = type,
                Radius_m = radius_m,
                InnerRadius_m = innerRadius_m,
                Effects = effects ?? new List<HazardEffect>(),
                IsTransient = isTransient,
                StartedAt = startedAt,
                ExpiresAt = expiresAt,
                MaxRadius_m = maxRadius_m,
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
        /// A permanent gas cloud — sensor-degrading, movement-slowing, warp-slowing, slowly corrosive terrain.
        /// (Heat damage is infrared, so heat-reflective armour resists it; the sensor/drag/warp effects are
        /// resisted by hazard-resistance components.)
        /// </summary>
        public static Entity CreateGasCloud(StarSystem system, Entity star, Vector3 offsetFromStar_m, double radius_m, string name = "Gas Cloud")
        {
            var effects = new List<HazardEffect>
            {
                new HazardEffect(HazardEffectType.HeatDamage, 50.0, 8000.0),   // slow corrosive/thermal hull damage
                new HazardEffect(HazardEffectType.SensorJam, 0.35),            // sensors cut to 35% — hide / get ambushed
                new HazardEffect(HazardEffectType.MovementDrag, 0.5),          // sub-light thrust slowed
                new HazardEffect(HazardEffectType.WarpInhibit, 0.25),          // warp crawls through it
            };
            return CreateFromEffects(system, star, offsetFromStar_m, radius_m, SpaceHazardType.GasCloud, effects, name);
        }

        /// <summary>
        /// A permanent "corona" danger zone hugging a star: heat damage that is max at the star and fades to zero
        /// at the edge of the zone (dive toward the sun and it climbs). Resisted by heat-reflective armour.
        /// </summary>
        public static Entity CreateStarCorona(StarSystem system, Entity star, string name = "Solar Corona")
        {
            double starRadius_m = star.GetDataBlob<MassVolumeDB>().RadiusInM;
            double radius_m = Math.Max(Distance.AuToMt(0.12), starRadius_m * 25.0);

            var effects = new List<HazardEffect>
            {
                new HazardEffect(HazardEffectType.HeatDamage, 800.0, 10000.0, scalesWithProximity: true),
            };
            // Inner radius = the star's surface, so corona heat follows real radiative flux (∝ 1/dist²): the danger
            // is concentrated within a few stellar radii. The outer zone is a warning band and every normal orbit
            // (Mercury at 0.39 AU is well outside the ~0.12 AU zone) takes ~zero — only a genuine close dive cooks.
            return CreateFromEffects(system, star, Vector3.Zero, radius_m, SpaceHazardType.StarCorona, effects, name,
                innerRadius_m: starRadius_m);
        }

        /// <summary>
        /// A transient solar flare hugging the star — blinds sensors in the area and irradiates ships caught in
        /// it (UV/ionising radiation, resisted by radiation shielding), then fades.
        /// </summary>
        public static Entity CreateSolarFlare(StarSystem system, Entity star, DateTime startedAt, TimeSpan duration, double maxRadius_m, string name = "Solar Flare")
        {
            var effects = new List<HazardEffect>
            {
                new HazardEffect(HazardEffectType.RadiationDamage, 500.0, 150.0), // UV/ionising
                new HazardEffect(HazardEffectType.SensorJam, 0.0),               // 0 = fully blinds the area
            };
            return CreateFromEffects(system, star, Vector3.Zero, 1.0, SpaceHazardType.SolarFlare, effects, name,
                isTransient: true, startedAt: startedAt, expiresAt: startedAt + duration, maxRadius_m: maxRadius_m);
        }
    }
}
