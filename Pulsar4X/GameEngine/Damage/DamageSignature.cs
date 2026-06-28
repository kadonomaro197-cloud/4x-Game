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
    public enum DamageSignature : byte
    {
        HardRadiation,   // ionising UV/X/γ — a solar flare, a radiation belt, an X-ray binary
        Thermal,         // heat / infrared — a star corona, a hot nebula, a thermal beam
        Kinetic,         // impacts / debris / pressure — micrometeoroids, a railgun slug (the wavelength-0 convention)
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
        /// True if this flavour already deposits damage through the EXISTING wavelength-armour path
        /// (<c>DamageTools.DealDamageEnergyBeamSim</c>): <see cref="DamageSignature.Thermal"/> (infrared),
        /// <see cref="DamageSignature.HardRadiation"/> (UV), and <see cref="DamageSignature.Kinetic"/> (the
        /// wavelength-0 convention → the near-IR band). FALSE for the three that have NO wavelength and so need
        /// their own damage application site built before they can hurt anything: <see cref="DamageSignature.EMStorm"/>,
        /// <see cref="DamageSignature.Gravimetric"/>, <see cref="DamageSignature.Corrosive"/>. This is the
        /// load-bearing distinction the build plan + <c>Hazards/CLAUDE.md</c> call out (gravimetric needs its own site).
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
    }
}
