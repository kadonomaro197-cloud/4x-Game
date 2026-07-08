using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// Unit SPEED — it FALLS OUT of the chassis, not a new stat (the developer's call). A unit's march time is divided
    /// by a speed multiplier read from its frame's existing <see cref="GroundChassisAtb.Locomotion"/> (Foot / Tracked /
    /// Walker / Hover), which until now was carried but unused for timing. Because a raised unit carries its components
    /// (units-as-entities, Option A), the chassis — and thus the speed — falls out of the component store via
    /// <c>TryGetComponentsByAttribute</c>, exactly like reading any other ability. A unit with no backing (monolithic /
    /// dev / garrison) defaults to Foot (×1.0) — so existing units are byte-unchanged; only a designed unit on a faster
    /// frame moves faster.
    ///
    /// ⚠ FLAGGED NEW GAMEPLAY NUMBERS (tunable dials — Foot is the ×1.0 baseline; higher = faster = less march time):
    ///   Tracked ×2.0, Walker ×1.5, Hover ×3.0. Move these to per-chassis JSON data once the numbers are tuned.
    /// </summary>
    public static class GroundMobility
    {
        public const double TrackedSpeed = 2.0;   // FLAGGED
        public const double WalkerSpeed  = 1.5;   // FLAGGED
        public const double HoverSpeed   = 3.0;   // FLAGGED

        /// <summary>Speed multiplier for a locomotion kind (Foot = 1.0 baseline; higher = faster). Pure.</summary>
        public static double SpeedMultFor(GroundLocomotion loco)
        {
            switch (loco)
            {
                case GroundLocomotion.Tracked: return TrackedSpeed;
                case GroundLocomotion.Walker:  return WalkerSpeed;
                case GroundLocomotion.Hover:   return HoverSpeed;
                default:                       return 1.0;   // Foot
            }
        }

        /// <summary>The unit's speed multiplier, read off its CHASSIS component (falls out of the backing store). Foot
        /// (×1.0) if it has no backing / no chassis. Always ≥ 1.0 (safe as a divisor). Never throws.</summary>
        public static double SpeedMultForUnit(Entity body, GroundUnit unit)
        {
            try
            {
                if (GroundUnitEntity.TryGetBacking(body, unit, out var backing)
                    && backing.TryGetDataBlob<ComponentInstancesDB>(out var cidb)
                    && cidb.TryGetComponentsByAttribute<GroundChassisAtb>(out var chassis) && chassis.Count > 0)
                {
                    var atb = chassis[0].Design?.GetAttribute<GroundChassisAtb>();
                    if (atb != null) return SpeedMultFor(atb.Locomotion);
                }
            }
            catch { }
            return 1.0;
        }
    }
}
