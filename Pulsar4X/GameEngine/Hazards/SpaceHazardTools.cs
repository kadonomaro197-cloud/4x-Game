using System;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;

namespace Pulsar4X.Hazards
{
    /// <summary>
    /// The combined RAW effect of every hazard overlapping a point in space (before any ship resistance). The
    /// stat multipliers multiply together, damage sums, "blinds" is true if any overlapping hazard blinds.
    /// Resistance is applied later, at the point of use, because it depends on the specific SHIP.
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
    /// Read-side helper for the hazard spine: "what hazards is this point/ship inside, what do they do, and how
    /// much does this ship resist them?" Cheap — a system holds at most a handful of hazards.
    /// </summary>
    public static class SpaceHazardTools
    {
        /// <summary>Combine the RAW (pre-resistance) effects of every hazard in <paramref name="system"/> that contains <paramref name="position"/>.</summary>
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
                mods.SensorRangeMultiplier *= hazDb.MultiplierFor(HazardEffectType.SensorJam);
                mods.MoveSpeedMultiplier *= hazDb.MultiplierFor(HazardEffectType.MovementDrag);
                mods.WarpSpeedMultiplier *= hazDb.MultiplierFor(HazardEffectType.WarpInhibit);
                if (hazDb.Effects != null)
                    foreach (var e in hazDb.Effects)
                        if (e.IsDamage)
                            mods.DamagePerSecond += e.Magnitude;
                if (hazDb.BlindsSensors)
                    mods.BlindsSensors = true;
            }
            return mods;
        }

        /// <summary>Combine the RAW effects of every hazard the entity is currently inside.</summary>
        public static HazardModifiers CombinedForEntity(Entity entity)
        {
            if (entity == null || !entity.IsValid)
                return HazardModifiers.None;
            if (!entity.TryGetDataBlob<PositionDB>(out var pos))
                return HazardModifiers.None;
            return CombinedAt(entity.Manager as StarSystem, pos.AbsolutePosition);
        }

        /// <summary>
        /// How much this ship resists a given hazard effect kind, 0..1 (0 = none, capped below 1 so a hazard
        /// always bites a little). Sums every installed <see cref="HazardResistanceAtb"/> that covers this kind,
        /// scaled by each component's health — so a damaged module resists less and a DESTROYED one (gone from
        /// ComponentInstancesDB) resists nothing. Read live, so this IS the grave rung: shoot the shielding off
        /// and the ship is re-exposed, no extra wiring.
        /// </summary>
        public static double ResistanceFraction(Entity ship, HazardEffectType kind)
        {
            if (ship == null || !ship.IsValid)
                return 0.0;
            if (!ship.TryGetDataBlob<ComponentInstancesDB>(out var comps))
                return 0.0;
            if (!comps.TryGetComponentsByAttribute<HazardResistanceAtb>(out var list))
                return 0.0;

            double total = 0.0;
            foreach (var inst in list)
            {
                var atb = inst.Design.GetAttribute<HazardResistanceAtb>();
                if (atb != null && atb.ResistedEffectType == kind)
                    total += atb.ResistanceFraction * inst.HealthPercent;
            }
            return Math.Clamp(total, 0.0, 0.9);
        }

        /// <summary>
        /// Apply a ship's resistance to a hazard's stat MULTIPLIER. A hazard cuts a stat to <paramref name="rawMultiplier"/>
        /// (e.g. 0.3 = sensors at 30%); resistance shrinks that cut toward "no effect" (1.0). Full resistance (0.9)
        /// leaves only 10% of the original cut. resistance 0 → unchanged; rawMultiplier 1 → unchanged.
        /// </summary>
        public static double ApplyResistance(double rawMultiplier, double resistanceFraction)
        {
            double cut = 1.0 - rawMultiplier;              // how much the hazard takes away
            return 1.0 - cut * (1.0 - resistanceFraction); // resistance keeps part of it
        }
    }
}
