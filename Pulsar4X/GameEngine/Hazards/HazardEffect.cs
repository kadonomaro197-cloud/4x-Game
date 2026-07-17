using Newtonsoft.Json;
using Pulsar4X.Damage;

namespace Pulsar4X.Hazards
{
    /// <summary>
    /// The kinds of thing a space hazard can DO to a ship inside it. This is the typed vocabulary the whole
    /// "effect ↔ counter" spine turns on: a hazard is a list of these effects (data), and a resistance component
    /// declares which of these it counters (data). Adding a brand-new kind of hazard effect is the only thing
    /// that needs a new enum value + one application site; everything else (new hazards, new counters) is data.
    ///
    /// DAMAGE kinds carry a wavelength and are resisted by the ship's ARMOUR (the existing wavelength-absorption
    /// model — heat is infrared, radiation is ultraviolet). The NON-damage kinds are resisted by a dedicated
    /// <see cref="HazardResistanceAtb"/> component.
    /// </summary>
    public enum HazardEffectType : byte
    {
        // Damage kinds — applied through DamageProcessor with a wavelength; ARMOUR is the counter.
        HeatDamage,        // infrared/thermal (a star corona, a hot nebula)
        RadiationDamage,   // ultraviolet/ionising (a solar flare, a radiation belt)
        KineticDamage,     // debris / micrometeoroids

        // Non-damage kinds — applied at their own sites; a HazardResistanceAtb component is the counter.
        SensorJam,         // cuts sensor range; Magnitude 0 = fully blinded
        MovementDrag,      // slows sub-light (Newtonian) movement
        WarpInhibit,       // slows warp through/from the region

        // Non-WAVELENGTH damage kinds (APPENDED 2026-06-28 — never reorder, JSON refs by int, gotcha #10). These
        // are not photons, so they can't run through the per-pixel wavelength armour sim; they're applied at the
        // flat non-wavelength damage site (DamageProcessor.ApplyNonWavelengthDamage), reduced by the ship's
        // armour-material SignatureResistance for the flavour. So "armour material is the counter" holds for all six.
        CorrosiveDamage,   // chemical / dense medium (a corrosive nebula) → Corrosive
        EMDamage,          // electromagnetic interference (an ion storm, a magnetar) → EMStorm
        GravimetricDamage, // tidal / spacetime stress (a black hole, a neutron star) → Gravimetric

        // GROUND-ONLY surface hazards (APPENDED 2026-07-17 — never reorder, JSON refs by int, gotcha #10). A ship in
        // space isn't harmed by planetary vacuum, so these are NOT space-ship damage kinds (left out of IsDamage /
        // SignatureFor → the space hazard system is byte-identical). They feed the GROUND E4 attrition
        // (GroundForcesProcessor.IsDamageEffect): an UNSEALED unit standing on an airless/toxic world bleeds; a unit
        // with the matching EnvResistance (a sealed suit / life-support) survives. The "sealed power armour matters" lever.
        Vacuum,            // no atmosphere (airless world: the Moon, Mercury, thin-air Mars) — need a sealed suit
        ToxicAtmosphere,   // a poisonous / corrosive atmosphere (Venus-like) — unbreathable without life-support
    }

    /// <summary>
    /// One typed effect a hazard applies. The meaning of <see cref="Magnitude"/> depends on <see cref="Type"/>:
    ///   • damage kinds  → joules/second of damage (at the centre if <see cref="ScalesWithProximity"/>),
    ///                      delivered at <see cref="Wavelength_nm"/> so armour material resists it.
    ///   • SensorJam / MovementDrag / WarpInhibit → the MULTIPLIER applied to that stat inside the hazard
    ///                      (1 = no effect, 0.3 = reduced to 30%, 0 = fully stopped/blind).
    /// </summary>
    public class HazardEffect
    {
        [JsonProperty] public HazardEffectType Type { get; set; }
        [JsonProperty] public double Magnitude { get; set; }
        [JsonProperty] public double Wavelength_nm { get; set; }
        [JsonProperty] public bool ScalesWithProximity { get; set; }

        public HazardEffect() { }

        public HazardEffect(HazardEffectType type, double magnitude, double wavelength_nm = 0, bool scalesWithProximity = false)
        {
            Type = type;
            Magnitude = magnitude;
            Wavelength_nm = wavelength_nm;
            ScalesWithProximity = scalesWithProximity;
        }

        public HazardEffect(HazardEffect other)
            : this(other.Type, other.Magnitude, other.Wavelength_nm, other.ScalesWithProximity) { }

        /// <summary>True for the damage kinds (resisted by armour via wavelength), false for the stat-multiplier kinds.</summary>
        public bool IsDamage =>
            Type == HazardEffectType.HeatDamage ||
            Type == HazardEffectType.RadiationDamage ||
            Type == HazardEffectType.KineticDamage ||
            Type == HazardEffectType.CorrosiveDamage ||
            Type == HazardEffectType.EMDamage ||
            Type == HazardEffectType.GravimetricDamage;

        /// <summary>
        /// The coarse shared <see cref="DamageSignature"/> (the keystone hazard↔weapon damage flavour) this effect
        /// delivers, or null when it isn't a damage effect (SensorJam / MovementDrag / WarpInhibit aren't "damage").
        /// Derived from <see cref="Type"/> — the keystone LABEL layered over the existing effect kinds, so a thermal
        /// hazard and a thermal weapon are resisted as the same flavour. <c>[JsonIgnore]</c>: it's computed and never
        /// serialised, so it adds nothing to the save format.
        /// </summary>
        [JsonIgnore]
        public DamageSignature? Signature => SignatureFor(Type);

        /// <summary>
        /// Maps a hazard effect kind to its coarse <see cref="DamageSignature"/>; null for the non-damage (stat) kinds.
        /// The three existing damage kinds ARE three of the six signatures — the other three (EMStorm / Gravimetric /
        /// Corrosive) have no <see cref="HazardEffectType"/> yet because they have no damage application site yet
        /// (they'd be APPENDED to the enum when built, never reordered — JSON references these by int, gotcha #10).
        /// </summary>
        public static DamageSignature? SignatureFor(HazardEffectType type) => type switch
        {
            HazardEffectType.HeatDamage => DamageSignature.Thermal,
            HazardEffectType.RadiationDamage => DamageSignature.HardRadiation,
            HazardEffectType.KineticDamage => DamageSignature.Kinetic,
            HazardEffectType.CorrosiveDamage => DamageSignature.Corrosive,
            HazardEffectType.EMDamage => DamageSignature.EMStorm,
            HazardEffectType.GravimetricDamage => DamageSignature.Gravimetric,
            _ => null, // SensorJam / MovementDrag / WarpInhibit are stat effects, not damage
        };
    }
}
