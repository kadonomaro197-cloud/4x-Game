using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar4X.Damage;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Ships;

namespace Pulsar4X.Hazards
{
    /// <summary>
    /// Drives every <see cref="SpaceHazardDB"/> region each tick: grows/fades/expires transient flares, and
    /// applies the PER-TICK effects to ships inside — DAMAGE (each damage effect through the armour-resisted
    /// DamageProcessor path, at its own wavelength) and DRAG (reduced by the ship's MovementDrag resistance).
    /// The query-time effects (sensor jam, warp inhibit) are read at the point of use via <see cref="SpaceHazardTools"/>.
    /// </summary>
    public class SpaceHazardProcessor : IHotloopProcessor
    {
        public void Init(Game game) { }

        public TimeSpan RunFrequency => TimeSpan.FromSeconds(5);
        public TimeSpan FirstRunOffset => TimeSpan.FromSeconds(0);
        public Type GetParameterType => typeof(SpaceHazardDB);

        public void ProcessEntity(Entity entity, int deltaSeconds) => ProcessOne(entity, deltaSeconds);

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var list = new List<SpaceHazardDB>(manager.GetAllDataBlobsOfType<SpaceHazardDB>());
            foreach (var hazDb in list)
            {
                var owner = hazDb.OwningEntity;
                if (owner == null || !owner.IsValid)
                    continue;
                ProcessOne(owner, deltaSeconds);
            }
            return list.Count;
        }

        private static void ProcessOne(Entity hazardEntity, int deltaSeconds)
        {
            var hazDb = hazardEntity.GetDataBlob<SpaceHazardDB>();
            var manager = hazardEntity.Manager;
            if (manager == null)
                return;
            DateTime now = hazardEntity.StarSysDateTime;

            // Transient (solar-flare) lifecycle: grow to peak, fade, then remove from the game.
            if (hazDb.IsTransient)
            {
                if (now >= hazDb.ExpiresAt)
                {
                    hazardEntity.Destroy();
                    return;
                }
                hazDb.Radius_m = FlareRadiusAt(hazDb, now);
            }

            if (hazDb.Effects == null || hazDb.Effects.Count == 0)
                return;

            // Per-tick effects: damage (any IsDamage effect) and drag (a MovementDrag effect). Sensor/warp are query-time.
            var damageEffects = hazDb.Effects.Where(e => e.IsDamage && e.Magnitude > 0).ToList();
            var dragEffect = hazDb.Effects.FirstOrDefault(e => e.Type == HazardEffectType.MovementDrag && e.Magnitude < 1.0);
            if (damageEffects.Count == 0 && dragEffect == null)
                return;

            if (!hazardEntity.TryGetDataBlob<PositionDB>(out var hazPos))
                return;
            Vector3 center = hazPos.AbsolutePosition;
            double radius = hazDb.Radius_m;

            foreach (var shipInfo in manager.GetAllDataBlobsOfType<ShipInfoDB>())
            {
                var ship = shipInfo.OwningEntity;
                if (ship == null || !ship.IsValid)
                    continue;
                if (!ship.TryGetDataBlob<PositionDB>(out var shipPos))
                    continue;
                double dist = (shipPos.AbsolutePosition - center).Length();
                if (dist > radius)
                    continue;

                // DISCOVERY — a ship inside a hazard learns its damage flavours (the knowledge the research loop
                // gates on); on first contact this also fires a notification and unlocks the counter-research.
                HazardDiscovery.RecordAndAnnounce(ship, hazDb, now);

                // DAMAGE — each effect at its own wavelength (so the ship's ARMOUR material is the defence).
                if (damageEffects.Count > 0)
                {
                    double proximity = ProximityIntensity(dist, radius, hazDb.InnerRadius_m);
                    foreach (var e in damageEffects)
                    {
                        double dps = e.ScalesWithProximity ? e.Magnitude * proximity : e.Magnitude;
                        if (dps <= 0)
                            continue;
                        var fragment = new DamageFragment
                        {
                            Energy = Math.Max(dps * deltaSeconds, 1.0),
                            Wavelength = e.Wavelength_nm,
                            // Stamp the keystone flavour so armour rated against this hazard's signature resists it.
                            Signature = e.Signature ?? Pulsar4X.Damage.DamageSignatures.FromWavelength_nm(e.Wavelength_nm),
                            Position = (0, 0),
                            Velocity = new Vector2(1, 0),
                        };
                        DamageProcessor.OnTakingDamage(ship, fragment);
                    }
                }

                // DRAG — only a free-flying ship (NewtonMoveDB), reduced by its MovementDrag resistance component.
                if (dragEffect != null && ship.TryGetDataBlob<NewtonMoveDB>(out var nmdb))
                {
                    double resistance = SpaceHazardTools.ResistanceFraction(ship, HazardEffectType.MovementDrag);
                    double effMult = SpaceHazardTools.ApplyResistance(dragEffect.Magnitude, resistance);
                    if (effMult < 1.0)
                    {
                        double dragThisTick = Math.Pow(effMult, deltaSeconds / 3600.0);
                        nmdb.CurrentVector_ms *= dragThisTick;
                    }
                }
            }
        }

        /// <summary>
        /// A flare's radius over its life: grows from a point to <see cref="SpaceHazardDB.MaxRadius_m"/> at the
        /// halfway peak, then fades back toward nothing. Pure function so a test can check the shape.
        /// </summary>
        /// <summary>
        /// Intensity of a proximity-scaled effect at <paramref name="dist"/> from the hazard centre, 0 at the outer
        /// edge → 1 at the core. Pure + tested. With an <paramref name="innerR"/> (a star's surface) it models real
        /// radiative FLUX (∝ 1/dist²) between the surface and the outer radius, so damage is concentrated tight
        /// against the source — the outer zone is a near-harmless warning band and a normal orbit (well outside the
        /// zone) takes zero; only a genuine close dive (within a few inner-radii) accumulates real damage. Without an
        /// inner radius it falls back to the original LINEAR band (back-compat for hazards that don't set one).
        /// </summary>
        internal static double ProximityIntensity(double dist, double outerR, double innerR)
        {
            if (outerR <= 0) return 1.0;
            if (dist >= outerR) return 0.0;                     // outside the zone (the caller's region check also excludes this)
            if (innerR <= 0)                                    // no inner radius → original linear band
                return Math.Max(0.0, 1.0 - dist / outerR);

            double d = Math.Max(dist, innerR);                  // clamp to the surface — don't divide past it
            double raw = (innerR / d) * (innerR / d);           // inverse-square flux, = 1 at the surface
            double edge = (innerR / outerR) * (innerR / outerR);// flux at the zone edge
            if (edge >= 1.0) return raw >= 1.0 ? 1.0 : 0.0;     // degenerate (inner ≈ outer)
            return Math.Clamp((raw - edge) / (1.0 - edge), 0.0, 1.0); // normalise: 1 at surface → 0 at the edge
        }

        public static double FlareRadiusAt(SpaceHazardDB hazDb, DateTime now)
        {
            if (now <= hazDb.StartedAt)
                return 1.0;
            if (now >= hazDb.ExpiresAt)
                return 0.0;

            double total = (hazDb.ExpiresAt - hazDb.StartedAt).TotalSeconds;
            if (total <= 0)
                return hazDb.MaxRadius_m;

            double t = (now - hazDb.StartedAt).TotalSeconds / total;
            double shape = t < 0.5 ? (t / 0.5) : (1.0 - (t - 0.5) / 0.5);
            return Math.Max(1.0, hazDb.MaxRadius_m * shape);
        }
    }
}
