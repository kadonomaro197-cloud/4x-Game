namespace Pulsar4X.Damage
{
    /// <summary>
    /// The COARSE, shared "flavour of damage" vocabulary — the KEYSTONE that lets a space HAZARD and a WEAPON
    /// speak the same language, so one piece of armour (and, later, a shield) that resists a flavour resists it
    /// from BOTH sources. A thermal star-corona and a thermal beam are the same <see cref="Thermal"/> flavour;
    /// armour tuned against heat survives both.
    ///
    /// Deliberately small (the developer's locked "coarse ~5–8 classes" call) — finer named variants are DATA
    /// later, not new enum values. It sits ABOVE the two existing, narrower vocabularies and unifies them:
    ///   • <c>Hazards.HazardEffectType</c> is hazard-only (and also covers non-damage effects like SensorJam);
    ///   • <c>Combat.WeaponClass</c> (Beam/Railgun/Missile/Flak) is the weapon PLATFORM, not the damage flavour.
    /// Neither can be the shared word, so this is.
    ///
    /// IMPORTANT — this is a LABEL, not a new damage path. The fine physics already lives in the per-pixel
    /// wavelength-absorption model (<c>DamageFragment.Wavelength</c> → <c>DamageResistBlueprint.WavelengthAbsorption</c>),
    /// which ALREADY unifies the wavelength-based flavours for free (a thermal hazard and a thermal beam are both
    /// infrared → both hit the same armour band). The three flavours that are NOT a wavelength
    /// (<see cref="EMStorm"/>/<see cref="Gravimetric"/>/<see cref="Corrosive"/>) are the ones that still need their
    /// own damage application site built — see <see cref="DamageSignatures.UsesWavelengthArmorPath"/>.
    /// </summary>
    /// ORDER IS INDEX-STABLE — do NOT reorder. <c>DamageResistBlueprint.SignatureResistance</c> is an array
    /// indexed by <c>(int)DamageSignature</c>, and (later) JSON may reference these by position; append new
    /// signatures at the END only. Kinetic is first (= 0) so a bare/default-constructed <c>DamageFragment</c>
    /// reads as Kinetic (matching the wavelength-0 = physical-impact convention).
    public enum DamageSignature : byte
    {
        Kinetic,         // impacts / debris / pressure — micrometeoroids, a railgun slug (the wavelength-0 convention)
        Thermal,         // heat / infrared — a star corona, a hot nebula, a thermal beam
        HardRadiation,   // ionising UV/X/γ — a solar flare, a radiation belt, an X-ray binary
        EMStorm,         // electromagnetic interference — a magnetar, an ion storm (NOT a heating wavelength)
        Gravimetric,     // tidal / spacetime — a black hole, a neutron star (NO wavelength: needs its own site)
        Corrosive,       // chemical / dense medium — a corrosive gas cloud, an acid atmosphere (NO wavelength)
    }

    /// <summary>
    /// Intrinsic facts about a <see cref="DamageSignature"/> — kept here (not in Hazards/Weapons) so the keystone
    /// has no upward dependency; callers that already know about hazards/weapons map THEIR types onto a signature
    /// on their own side (e.g. <c>HazardEffect.SignatureFor</c>).
    /// </summary>
    public static class DamageSignatures
    {
        /// <summary>
        /// True if this flavour deposits damage through the wavelength-armour path
        /// (<c>DamageTools.DealDamageEnergyBeamSim</c>): <see cref="DamageSignature.Thermal"/> (infrared),
        /// <see cref="DamageSignature.HardRadiation"/> (UV), and <see cref="DamageSignature.Kinetic"/> (the
        /// wavelength-0 convention → the near-IR band). FALSE for the three that have NO wavelength
        /// (<see cref="DamageSignature.EMStorm"/>, <see cref="DamageSignature.Gravimetric"/>,
        /// <see cref="DamageSignature.Corrosive"/>) — these route to the FLAT non-wavelength damage site
        /// (<c>DamageProcessor.ApplyNonWavelengthDamage</c>, built 2026-06-28): a hit spread flat across the hull,
        /// reduced by the ship's armour-material <c>SignatureResistance</c>. This bool is the router between the two
        /// sites; both reduce damage by the same per-material resistance, so armour is the counter either way.
        /// </summary>
        public static bool UsesWavelengthArmorPath(DamageSignature sig) => sig switch
        {
            DamageSignature.Thermal => true,
            DamageSignature.HardRadiation => true,
            DamageSignature.Kinetic => true,
            _ => false,
        };

        /// <summary>
        /// A representative emit wavelength (nm) for the wavelength-based flavours, so a hazard/weapon of this
        /// flavour lands in the right armour absorption band (UV &lt;400 / Vis &lt;700 / NIR &lt;2000 / MIR &lt;5000 /
        /// FIR ≥5000; kinetic uses the 0 = near-IR convention). Returns -1 for a flavour with NO wavelength
        /// (<see cref="DamageSignature.EMStorm"/> / <see cref="DamageSignature.Gravimetric"/> /
        /// <see cref="DamageSignature.Corrosive"/>) — those are not photons and must be applied at their own site.
        /// </summary>
        public static double RepresentativeWavelength_nm(DamageSignature sig) => sig switch
        {
            DamageSignature.HardRadiation => 150.0,    // UV/ionising → armour band 0
            DamageSignature.Thermal => 10000.0,        // far-infrared heat → armour band 4
            DamageSignature.Kinetic => 0.0,            // the wavelength-0 kinetic convention → armour band 2
            _ => -1.0,                                  // EMStorm / Gravimetric / Corrosive: no wavelength
        };

        /// <summary>
        /// Classifies a beam/emission wavelength (nm) into its damage signature — the inverse of
        /// <see cref="RepresentativeWavelength_nm"/>, used to LABEL a weapon/hazard hit by what it physically is:
        /// 0 (the kinetic convention) = <see cref="DamageSignature.Kinetic"/>; UV/ionising (&lt;400 nm) =
        /// <see cref="DamageSignature.HardRadiation"/>; everything else (visible → infrared, the heat region) =
        /// <see cref="DamageSignature.Thermal"/>. Aligns with the armour band cut at 400 nm, so a UV laser reads
        /// HardRadiation and an IR laser reads Thermal — the same flavour the matching hazard would carry.
        /// </summary>
        public static DamageSignature FromWavelength_nm(double wavelength_nm)
        {
            if (wavelength_nm <= 0) return DamageSignature.Kinetic;
            if (wavelength_nm < 400) return DamageSignature.HardRadiation;
            return DamageSignature.Thermal;
        }
    }
}
