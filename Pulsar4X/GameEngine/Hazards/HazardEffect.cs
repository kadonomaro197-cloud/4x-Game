using Newtonsoft.Json;

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
            Type == HazardEffectType.KineticDamage;
    }
}
