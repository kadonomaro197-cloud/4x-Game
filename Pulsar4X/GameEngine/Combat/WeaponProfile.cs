using Newtonsoft.Json;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// The broad flavor of a weapon — the corners (and off-corners) of the weapon triangle. Drives the dodge
    /// model and the triangle bonus in the auto-resolver. See docs/WEAPONS-AND-DODGE-DESIGN.md.
    /// </summary>
    public enum WeaponClass
    {
        /// <summary>Light-speed energy: ignores evasion (can't dodge light), power-hungry, range falloff.</summary>
        Beam,
        /// <summary>Finite-velocity ballistic kinetic: dodged by nimble ships, brutal vs slow capitals.</summary>
        Railgun,
        /// <summary>Guided kinetic: tracks the evasive, long range, saturates in waves — answered by flak.</summary>
        Missile,
        /// <summary>High-saturation short-range: the fighter- and missile-killer; only tickles a capital.</summary>
        Flak,
    }

    /// <summary>
    /// One weapon's contribution to a ship's combat value, carrying the "flavor" stats the dodge model and the
    /// weapon triangle read (docs/WEAPONS-AND-DODGE-DESIGN.md). Computed once at build from the weapon component's
    /// design. <see cref="DamagePerSecond"/> is summed into <c>ShipCombatValueDB.Firepower</c>; the rest decide
    /// WHO gets hit (a beam ignores evasion; a ballistic slug is dodged; saturation floors the hit fraction).
    /// </summary>
    public class WeaponProfile
    {
        /// <summary>Which corner of the weapon triangle this weapon is.</summary>
        [JsonProperty] public WeaponClass Class { get; internal set; }

        /// <summary>Damage dealt per second (joules/sec), already scaled by the component's current health.</summary>
        [JsonProperty] public double DamagePerSecond { get; internal set; }

        /// <summary>Shot velocity (m/s). Higher = harder to dodge; a beam is ~light-speed.</summary>
        [JsonProperty] public double Velocity { get; internal set; }

        /// <summary>How well the weapon follows an evasive target, 0..1 (ballistic ≈ 0; guided/beam high).</summary>
        [JsonProperty] public double Tracking { get; internal set; }

        /// <summary>Effective tracks per second (rate-of-fire × projectiles × spread) — the FLOOR on how much of
        /// this weapon's damage still lands on an evasive target. A 1/min flak is tiny; a 1000/sec slug is huge.</summary>
        [JsonProperty] public double Saturation { get; internal set; }

        /// <summary>The farthest this weapon can land a hit (metres) — the ROOT of the closing-fight model: as two
        /// fleets close, a weapon only contributes once the gap is ≤ its range (see
        /// docs/FLEET-COMBAT-CLOSING-DESIGN.md, Root A). Uses the engine's existing range convention: **0 = unbounded
        /// / always in range** (same as <see cref="Weapons.GenericBeamWeaponAtb.IsInRange"/> treating MaxRange ≤ 0 as
        /// unlimited), which is also serialization-safe (no Infinity in JSON). Beams carry their design MaxRange;
        /// railgun/flak/missile default to 0 (rangeless) until their own range fields are added — a flagged follow-up.</summary>
        [JsonProperty] public double Range_m { get; internal set; }

        public WeaponProfile() { }

        public WeaponProfile(WeaponClass cls, double damagePerSecond, double velocity, double tracking, double saturation, double range_m = 0)
        {
            Class = cls;
            DamagePerSecond = damagePerSecond;
            Velocity = velocity;
            Tracking = tracking;
            Saturation = saturation;
            Range_m = range_m;
        }

        public WeaponProfile(WeaponProfile p)
        {
            Class = p.Class;
            DamagePerSecond = p.DamagePerSecond;
            Velocity = p.Velocity;
            Tracking = p.Tracking;
            Saturation = p.Saturation;
            Range_m = p.Range_m;
        }
    }
}
