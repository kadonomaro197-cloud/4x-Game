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
    /// SYSTEM — the weapon designer's TWO independent axes (docs/WEAPON-TAXONOMY-DESIGN.md, developer's call 2026-07-06).
    /// The old single <see cref="WeaponClass"/> fused these; a blaster pistol (energy nature, dodgeable delivery) proved
    /// they must split. The player picks Nature × Delivery, dials specs, and the triangle position EMERGES from
    /// Velocity/Saturation/Tracking (so <see cref="WeaponClass"/> becomes a computed READOUT, not an authored choice).
    /// </summary>
    /// <summary>What the weapon does to the DEFENCE (shields/armour). Not the dodge — that's <see cref="WeaponDelivery"/>.</summary>
    public enum WeaponNature
    {
        /// <summary>Mass at velocity — slugs, flak, coilguns. Soaked by armour; shields resist it best.</summary>
        Kinetic,
        /// <summary>Coherent/charged energy — beams, blasters, plasma. Bleeds through shields; ablates armour.</summary>
        Energy,
        /// <summary>Chemical/nuclear detonation — warheads, shells. Area effect, big alpha.</summary>
        Explosive,
        /// <summary>Does something OTHER than raw damage — EMP/ion (disable), matter-strip (bypass armour),
        /// anti-shield, stun. Only real if it has a visible in-game effect (see the taxonomy doc §5).</summary>
        Exotic,
    }

    /// <summary>How the weapon DELIVERS its hurt — sets the velocity regime + pattern, which is what meets the DODGE.</summary>
    public enum WeaponDelivery
    {
        /// <summary>Continuous, ~light-speed — a phaser/laser. Undodgeable.</summary>
        Beam,
        /// <summary>Discrete, FINITE-velocity shot — a blaster bolt / plasma / staff. Dodgeable (the gap the old model couldn't build).</summary>
        Bolt,
        /// <summary>A single fast projectile — a railgun/coilgun slug. Dodgeable by the nimble, brutal vs the slow.</summary>
        Slug,
        /// <summary>Many low-value projectiles — flak/PDC/minigun. High saturation floors the dodge.</summary>
        Cloud,
        /// <summary>Tracks the target — a missile/needler/beam-funnel. High tracking beats evasion.</summary>
        Guided,
        /// <summary>Area effect — a BFG/fuel-rod/artillery shell. Hits a region, not a point.</summary>
        Blast,
    }

    /// <summary>
    /// One weapon's contribution to a ship's combat value, carrying the "flavor" stats the dodge model and the
    /// weapon triangle read (docs/WEAPONS-AND-DODGE-DESIGN.md). Computed once at build from the weapon component's
    /// design. <see cref="DamagePerSecond"/> is summed into <c>ShipCombatValueDB.Firepower</c>; the rest decide
    /// WHO gets hit (a beam ignores evasion; a ballistic slug is dodged; saturation floors the hit fraction).
    /// </summary>
    public class WeaponProfile
    {
        /// <summary>Which corner of the weapon triangle this weapon is. (Transitional: today authored; the taxonomy
        /// plan makes it a COMPUTED readout derived from Nature × Delivery × the specs — see WEAPON-TAXONOMY-DESIGN.md.)</summary>
        [JsonProperty] public WeaponClass Class { get; internal set; }

        /// <summary>Damage nature (Kinetic/Energy/Explosive/Exotic) — what it does to the defence. Axis 1 of 2.</summary>
        [JsonProperty] public WeaponNature Nature { get; internal set; } = WeaponNature.Kinetic;

        /// <summary>Delivery physics (Beam/Bolt/Slug/Cloud/Guided/Blast) — what meets the dodge. Axis 2 of 2.</summary>
        [JsonProperty] public WeaponDelivery Delivery { get; internal set; } = WeaponDelivery.Slug;

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

        /// <summary>The triangle corner DERIVED from this weapon's Delivery + specs (via <see cref="WeaponClassifier"/>)
        /// — the unification read-out. Not serialized; computed on demand. For every real weapon today it equals the
        /// authored <see cref="Class"/> (the invariant that lets a later slice drop the authored field and make the
        /// class purely emergent). See docs/WEAPON-TAXONOMY-DESIGN.md.</summary>
        [JsonIgnore]
        public WeaponClass ComputedClass => WeaponClassifier.Classify(Delivery, Velocity, Tracking, Saturation);

        public WeaponProfile() { }

        public WeaponProfile(WeaponClass cls, double damagePerSecond, double velocity, double tracking, double saturation, double range_m = 0,
            WeaponNature nature = WeaponNature.Kinetic, WeaponDelivery delivery = WeaponDelivery.Slug)
        {
            Class = cls;
            DamagePerSecond = damagePerSecond;
            Velocity = velocity;
            Tracking = tracking;
            Saturation = saturation;
            Range_m = range_m;
            Nature = nature;
            Delivery = delivery;
        }

        public WeaponProfile(WeaponProfile p)
        {
            Class = p.Class;
            DamagePerSecond = p.DamagePerSecond;
            Velocity = p.Velocity;
            Tracking = p.Tracking;
            Saturation = p.Saturation;
            Range_m = p.Range_m;
            Nature = p.Nature;
            Delivery = p.Delivery;
        }
    }
}
