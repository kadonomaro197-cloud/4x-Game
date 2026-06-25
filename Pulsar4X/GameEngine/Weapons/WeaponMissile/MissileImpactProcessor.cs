using System;
using Pulsar4X.Damage;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Interfaces;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;

namespace Pulsar4X.Weapons
{
    /// <summary>
    /// Checks every second whether any in-flight missile is close enough to its target to
    /// count as a hit. If so, delivers kinetic damage and destroys the missile entity.
    ///
    /// Impact threshold: 1000 m.  Kinetic energy = 0.5 × dry-mass × |v_missile − v_target|².
    ///
    /// Calibration note: beam-weapon damage is tuned for kJ–MJ energies.  A missile at
    /// orbital closing speed (1–10 km/s) with a 100 kg dry mass carries 50 MJ–5 GJ of
    /// kinetic energy, which destroys many components in one hit.  Tune the divisor in
    /// this method, or add warhead energy from OrdnanceExplosivePayload, once warhead
    /// energy values are finalized in ordnanceDesigns/.
    /// </summary>
    public class MissileImpactProcessor : IHotloopProcessor
    {
        private const double ImpactRadius_m = 1000.0;

        public void Init(Game game) { }

        public TimeSpan RunFrequency => TimeSpan.FromSeconds(1);
        public TimeSpan FirstRunOffset => TimeSpan.FromSeconds(0);
        public Type GetParameterType => typeof(ProjectileInfoDB);

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            if (!entity.IsValid)
                return;

            var info = entity.GetDataBlob<ProjectileInfoDB>();
            var target = info.TargetEntity;

            if (target == null || !target.IsValid)
                return;

            if (!entity.TryGetDataBlob<PositionDB>(out var myPosDB))
                return;
            if (!target.TryGetDataBlob<PositionDB>(out var tgtPosDB))
                return;

            double distance = (myPosDB.AbsolutePosition - tgtPosDB.AbsolutePosition).Length();
            if (distance > ImpactRadius_m)
                return;

            var atDateTime = entity.StarSysDateTime;

            double dryMass = entity.TryGetDataBlob<MassVolumeDB>(out var mvDB) ? mvDB.MassDry : 1.0;
            var missileVel = MoveMath.GetRelativeFutureVelocity(entity, atDateTime);
            var targetVel  = MoveMath.GetRelativeFutureVelocity(target, atDateTime);
            double closingSpeed = (missileVel - targetVel).Length();
            double kineticEnergy = 0.5 * dryMass * closingSpeed * closingSpeed;

            var fragment = new DamageFragment
            {
                Energy     = Math.Max(kineticEnergy, 1.0),
                Wavelength = 0,        // kinetic: no wavelength
                Position   = (0, 0),
                Velocity   = new Vector2(1, 0),
            };

            DamageProcessor.OnTakingDamage(target, fragment);
            entity.Destroy();
        }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var dbs = manager.GetAllDataBlobsOfType<ProjectileInfoDB>();
            int count = 0;
            foreach (var db in dbs)
            {
                ProcessEntity(db.OwningEntity, deltaSeconds);
                count++;
            }
            return count;
        }
    }
}
