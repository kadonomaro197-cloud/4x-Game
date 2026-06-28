using Pulsar4X.Engine;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;

namespace Pulsar4X.Hazards
{
    /// <summary>
    /// The combined effect of every hazard overlapping a single point in space. Multipliers multiply together
    /// (two overlapping clouds slow you more), damage sums, and "blinds" is true if ANY overlapping hazard
    /// blinds. <see cref="None"/> is the no-hazard identity (everything at 1.0, no damage, not blinded).
    /// </summary>
    public struct HazardModifiers
    {
        public bool InAnyHazard;
        public double SensorRangeMultiplier;
        public double MoveSpeedMultiplier;
        public double WarpSpeedMultiplier;
        public double DamagePerSecond;
        public bool BlindsSensors;

        public static HazardModifiers None => new HazardModifiers
        {
            InAnyHazard = false,
            SensorRangeMultiplier = 1.0,
            MoveSpeedMultiplier = 1.0,
            WarpSpeedMultiplier = 1.0,
            DamagePerSecond = 0.0,
            BlindsSensors = false,
        };
    }

    /// <summary>
    /// Read-side helper: "what hazards is this point/ship inside, and what do they do to it?" Movement, warp and
    /// sensor code call this at the moment they need the answer, so the effects are always current. Cheap — a
    /// system holds at most a handful of hazards.
    /// </summary>
    public static class SpaceHazardTools
    {
        /// <summary>Combine every hazard in <paramref name="system"/> that contains <paramref name="position"/>.</summary>
        public static HazardModifiers CombinedAt(StarSystem system, Vector3 position)
        {
            var mods = HazardModifiers.None;
            if (system == null)
                return mods;

            foreach (var hazDb in system.GetAllDataBlobsOfType<SpaceHazardDB>())
            {
                var hazEntity = hazDb.OwningEntity;
                if (hazEntity == null || !hazEntity.IsValid)
                    continue;
                if (!hazEntity.TryGetDataBlob<PositionDB>(out var hazPos))
                    continue;

                double dist = (position - hazPos.AbsolutePosition).Length();
                if (dist > hazDb.Radius_m)
                    continue;

                mods.InAnyHazard = true;
                mods.SensorRangeMultiplier *= hazDb.SensorRangeMultiplier;
                mods.MoveSpeedMultiplier *= hazDb.MoveSpeedMultiplier;
                mods.WarpSpeedMultiplier *= hazDb.WarpSpeedMultiplier;
                mods.DamagePerSecond += hazDb.DamagePerSecond;
                if (hazDb.BlindsSensors)
                    mods.BlindsSensors = true;
            }
            return mods;
        }

        /// <summary>Combine every hazard the entity is currently standing inside.</summary>
        public static HazardModifiers CombinedForEntity(Entity entity)
        {
            if (entity == null || !entity.IsValid)
                return HazardModifiers.None;
            if (!entity.TryGetDataBlob<PositionDB>(out var pos))
                return HazardModifiers.None;
            return CombinedAt(entity.Manager as StarSystem, pos.AbsolutePosition);
        }
    }
}
