using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Galaxy;

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
                    && backing.TryGetDataBlob<ComponentInstancesDB>(out var cidb))
                {
                    // A designed, parametric LOCOMOTION component wins — the player's own drive (best mounted).
                    if (cidb.TryGetComponentsByAttribute<GroundLocomotionAtb>(out var locos) && locos.Count > 0)
                    {
                        double best = 0;
                        foreach (var l in locos)
                        {
                            var la = l.Design?.GetAttribute<GroundLocomotionAtb>();
                            if (la != null && la.SpeedFactor > best) best = la.SpeedFactor;
                        }
                        if (best > 0) return best;
                    }
                    // Fallback: the chassis frame's coarse Locomotion enum (slice 4) for units with no locomotion component.
                    if (cidb.TryGetComponentsByAttribute<GroundChassisAtb>(out var chassis) && chassis.Count > 0)
                    {
                        var atb = chassis[0].Design?.GetAttribute<GroundChassisAtb>();
                        if (atb != null) return SpeedMultFor(atb.Locomotion);
                    }
                }
            }
            catch { }
            return 1.0;   // Foot baseline / no backing
        }

        /// <summary>The unit's rough-terrain handling (0..1) from its designed locomotion component, or a moderate 0.5
        /// default if it has none. Consumed by the terrain move-penalty (slice 5b). Never throws.</summary>
        public static double RoughHandlingForUnit(Entity body, GroundUnit unit)
        {
            try
            {
                if (GroundUnitEntity.TryGetBacking(body, unit, out var backing)
                    && backing.TryGetDataBlob<ComponentInstancesDB>(out var cidb)
                    && cidb.TryGetComponentsByAttribute<GroundLocomotionAtb>(out var locos) && locos.Count > 0)
                {
                    var la = locos[0].Design?.GetAttribute<GroundLocomotionAtb>();
                    if (la != null) return la.RoughHandling;
                }
            }
            catch { }
            return 0.5;
        }

        /// <summary>Adjust a terrain move-penalty by a unit's rough-terrain handling. PURE. Open terrain (baseMult ≤ 1)
        /// is never penalised. rough handling 0.5 is NEUTRAL (reproduces the un-tuned behaviour); a higher-handling
        /// drive (tracks/walker) EASES the rough penalty, a lower one (wheels) WORSENS it. ⚠ FLAGGED slope (the 1.5 −
        /// handling curve) — tunable.</summary>
        public static double TerrainMult(double baseMult, double roughHandling)
        {
            if (baseMult <= 1.0) return baseMult;          // open ground: no penalty to ease
            double f = 1.5 - roughHandling;                // rh 0.5 → ×1 (neutral); 1 → ×0.5 (eased); 0 → ×1.5 (worse)
            if (f < 0.1) f = 0.1;
            double eff = 1.0 + (baseMult - 1.0) * f;
            return eff < 1.0 ? 1.0 : eff;
        }

        /// <summary>The per-step march seconds for a <paramref name="unit"/> entering a hex of <paramref name="terrain"/>:
        /// the step base × the terrain penalty adjusted by the unit's rough-terrain handling (falls out of its designed
        /// locomotion). Centralises the four march-timing sites. Never throws (defaults to neutral handling).</summary>
        public static double StepSecondsFor(Entity body, GroundUnit unit, double stepBaseSeconds, RegionFeatureType terrain)
        {
            return stepBaseSeconds * TerrainMult(HexPathfinder.HexMoveMult(terrain), RoughHandlingForUnit(body, unit));
        }
    }
}
