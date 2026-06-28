using System;
using System.Collections.Generic;
using Pulsar4X.Damage;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Ships;

namespace Pulsar4X.Hazards
{
    /// <summary>
    /// Drives every <see cref="SpaceHazardDB"/> region each tick: it grows/fades transient flares (and removes
    /// them when they expire), and applies the per-tick effects — hull DAMAGE and Newtonian DRAG — to ships
    /// caught inside. The query-time effects (sensor cut, warp slow) are NOT here; they're read at the point of
    /// use by the sensor/warp code via <see cref="SpaceHazardTools"/>.
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
            // Snapshot to a list — a transient flare can destroy its own entity mid-loop.
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

            // Per-tick effects only apply if there's something to apply.
            bool doesDamage = hazDb.DamagePerSecond > 0;
            bool doesDrag = hazDb.MoveSpeedMultiplier < 1.0;
            if (!doesDamage && !doesDrag)
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
                if ((shipPos.AbsolutePosition - center).Length() > radius)
                    continue;

                if (doesDamage)
                {
                    double energy = hazDb.DamagePerSecond * deltaSeconds;
                    var fragment = new DamageFragment
                    {
                        Energy = Math.Max(energy, 1.0),
                        Wavelength = 0,            // environmental: not a tuned beam
                        Position = (0, 0),
                        Velocity = new Vector2(1, 0),
                    };
                    DamageProcessor.OnTakingDamage(ship, fragment);
                }

                // Drag only bites a ship in free-flight (it has a NewtonMoveDB — actually coasting/thrusting
                // through the medium). A ship in a stable Kepler orbit isn't "ploughing through" the cloud.
                if (doesDrag && ship.TryGetDataBlob<NewtonMoveDB>(out var nmdb))
                {
                    // Apply the per-tick share of the drag so the slow-down is independent of tick length:
                    // MoveSpeedMultiplier is the factor per game-hour; raise it to (this tick / 1 hour).
                    double dragThisTick = Math.Pow(hazDb.MoveSpeedMultiplier, deltaSeconds / 3600.0);
                    nmdb.CurrentVector_ms *= dragThisTick;
                }
            }
        }

        /// <summary>
        /// A flare's radius over its life: grows from a point to <see cref="SpaceHazardDB.MaxRadius_m"/> at the
        /// halfway point (peak), then fades back toward nothing. Pure function so a test can check the shape.
        /// </summary>
        public static double FlareRadiusAt(SpaceHazardDB hazDb, DateTime now)
        {
            if (now <= hazDb.StartedAt)
                return 1.0;
            if (now >= hazDb.ExpiresAt)
                return 0.0;

            double total = (hazDb.ExpiresAt - hazDb.StartedAt).TotalSeconds;
            if (total <= 0)
                return hazDb.MaxRadius_m;

            double t = (now - hazDb.StartedAt).TotalSeconds / total;     // 0..1 across the flare's life
            double shape = t < 0.5 ? (t / 0.5) : (1.0 - (t - 0.5) / 0.5); // triangle: 0 -> 1 at peak -> 0
            return Math.Max(1.0, hazDb.MaxRadius_m * shape);
        }
    }
}
