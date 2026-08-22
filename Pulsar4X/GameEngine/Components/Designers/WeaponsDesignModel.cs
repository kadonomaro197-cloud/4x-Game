using Pulsar4X.Combat;

namespace Pulsar4X.Components.Designers
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the WEAPONS door parametric designer, slice 1 (ENGINE MODEL).
    ///
    /// WHAT IT IS, in plain English: the design tool <c>docs/Actual HTMLs Of designers/weaponsderived.html</c> replaces
    /// the old "pick a weapon type from a menu" screen with ONE form — you pick two things (how the weapon DELIVERS its
    /// hit, and what NATURE of damage it does) and slide a few dials, and every weapon in the game falls out of that one
    /// form. This class is the ENGINE HALF of that form: a pure calculator that turns those picks + dials into the
    /// <see cref="WeaponProfile"/> the combat resolver actually reads. The ImGui screen that drives it is slice 2
    /// (client, verified on the developer's machine — CI can compile the client but can't run it).
    ///
    /// WHY IT'S SHAPED THIS WAY (the derivation, per <c>docs/economy/DESIGNER-NORTH-STAR.md</c>): a "door" is DERIVED
    /// from the numbers the simulation actually reads off a weapon — the ten fields of <see cref="WeaponProfile"/>. Group
    /// them by the question each answers, split the ones the physics FORCES (a beam is always light-speed) from the ones
    /// the player is FREE to set, and the FORCED ones become the two CHOICES while the FREE ones become the sliders:
    ///   • CHOICE 1 — <see cref="WeaponDelivery"/> (Beam / Bolt / Slug / Cloud / Guided): what meets the DODGE. It FORCES
    ///     the velocity regime + the effective range band (a beam knifes at light-speed; a missile is a long slow tracker).
    ///   • CHOICE 2 — <see cref="WeaponNature"/> (Kinetic / Energy / Explosive / Exotic): what meets the DEFENCE
    ///     (shields/armour). It sets nothing physical here beyond <see cref="WeaponProfile.Nature"/> — but combined with
    ///     Delivery it distinguishes an energy BEAM (a laser) from an exotic BEAM (an anti-shield ion disruptor).
    ///   • SLIDERS — total damage (<see cref="DamagePerSecond"/>), the shot-size↔rate split (<see cref="Saturation"/>),
    ///     reach (<see cref="Range_m"/>, which only a Beam actually writes — the others take a fixed class range), and the
    ///     armour dials (<see cref="Penetration"/> / <see cref="PerShotEnergy"/>).
    ///
    /// THE HONEST CAVEAT (the load-bearing finding, flagged for the developer): the engine reads a per-weapon MUZZLE
    /// VELOCITY and TRACKING off the finite-velocity weapons (a railgun at 50 km/s, a "high-velocity railgun" at 200 km/s),
    /// so a LITERAL two-choice/four-slider form cannot reproduce those variants — they would collapse to one forced
    /// velocity. This model therefore also accepts <see cref="Velocity"/> and <see cref="Tracking"/> as inputs, defaulting
    /// to the delivery-forced value when the caller leaves them unset. The slice-2 UI then decides whether to expose those
    /// as advanced dials or accept the collapse — parked as an ADJUDICATION item, it does NOT block this model.
    ///
    /// BYTE-IDENTICAL / SAFE (slice-1 rules): this is a NEW file, a pure value type with no serialized state, no DataBlob,
    /// no <c>*Atb</c> ctor change — nothing in the live game calls it yet, so the whole engine is unchanged. It mirrors
    /// <see cref="ShipCombatValueDB.Calculate"/>'s per-weapon arithmetic EXACTLY (same constants, same formulas) so the
    /// gauge <c>WeaponsDesignModelTests</c> can prove it reproduces every base-mod weapon's profile. RECOIL is deliberately
    /// NOT applied here: a weapon's recoil→tracking penalty depends on the HULL it's bolted to (chassis mass), so it is a
    /// mount-time adjustment (<see cref="ShipCombatValueDB.RecoilTrackingFactor"/>), not a design-time property — the model
    /// carries the DESIGN tracking, and every base-mod weapon has recoil 0 anyway.
    /// </summary>
    public readonly struct WeaponsDesignModel
    {
        /// <summary>Sentinel for "let the delivery force the default" on <see cref="Tracking"/> (a real tracking is 0..1).</summary>
        public const double UseDeliveryDefault = -1.0;

        /// <summary>CHOICE 2 — what the weapon does to the defence (shields/armour).</summary>
        public WeaponNature Nature { get; }
        /// <summary>CHOICE 1 — how the weapon delivers its hit (what meets the dodge). Forces the velocity/range regime.</summary>
        public WeaponDelivery Delivery { get; }
        /// <summary>SLIDER — total damage per second (joules/sec). Summed into a ship's Firepower.</summary>
        public double DamagePerSecond { get; }
        /// <summary>SLIDER — effective tracks per second (rate-of-fire × projectiles): the floor on how much lands on an
        /// evasive target. A single-shot beam is 1/charge-period; a flak cloud is rounds/sec × pellets/shot.</summary>
        public double Saturation { get; }
        /// <summary>SLIDER — reach in metres. Only a BEAM writes its own reach (the design MaxRange); every other delivery
        /// takes a fixed class range (railgun/plasma 500 km, flak 50 km, disruptor 400 km, missile 1000 km), so this input
        /// is used only for a non-exotic Beam and ignored otherwise.</summary>
        public double Range_m { get; }
        /// <summary>FREE input — shot velocity (m/s). Defaults to the delivery-forced value (≤0) for Beam/Guided; a
        /// finite-velocity delivery (Slug/Bolt/Cloud) MUST supply it (that is the "high-velocity railgun" dial).</summary>
        public double Velocity { get; }
        /// <summary>FREE input — how well the weapon follows an evasive target, 0..1. Pass <see cref="UseDeliveryDefault"/>
        /// to take the delivery-forced default (laser 0.95, disruptor/missile their stubs); a finite delivery supplies its
        /// own (railgun 0.05, flak 0.1).</summary>
        public double Tracking { get; }
        /// <summary>SLIDER (armour) — how much of the target's flat armour this weapon ignores (0 = a normal round).</summary>
        public double Penetration { get; }
        /// <summary>SLIDER (armour) — joules in one shot (the alpha-vs-chip dial). 0 = treat the fire as one lump.</summary>
        public double PerShotEnergy { get; }
        /// <summary>Waste heat dumped into the ship while firing (kJ/s). 0 for a "cool" weapon; a hot beam sets it.</summary>
        public double HeatPerSecond { get; }

        public WeaponsDesignModel(WeaponNature nature, WeaponDelivery delivery, double damagePerSecond, double saturation,
            double range_m = 0, double velocity = 0, double tracking = UseDeliveryDefault,
            double penetration = 0, double perShotEnergy = 0, double heatPerSecond = 0)
        {
            Nature = nature;
            Delivery = delivery;
            DamagePerSecond = damagePerSecond;
            Saturation = saturation;
            Range_m = range_m;
            Velocity = velocity;
            Tracking = tracking;
            Penetration = penetration;
            PerShotEnergy = perShotEnergy;
            HeatPerSecond = heatPerSecond;
        }

        /// <summary>
        /// Build the <see cref="WeaponProfile"/> the combat resolver reads — the same numbers
        /// <see cref="ShipCombatValueDB.Calculate"/> produces for the matching weapon component, so a design made through
        /// this model fights identically to the hand-authored base-mod weapon. Velocity/tracking/range fall out of the
        /// (Delivery, Nature) choice where the caller left them at the delivery default.
        /// </summary>
        public WeaponProfile BuildProfile()
        {
            double velocity;
            double tracking;
            double range;

            switch (Delivery)
            {
                case WeaponDelivery.Beam:
                    if (Nature == WeaponNature.Exotic)
                    {
                        // Ion disruptor: light-speed (undodgeable), tracks perfectly, its own class reach.
                        velocity = ShipCombatValueDB.LightSpeed_mps;
                        tracking = Tracking >= 0 ? Tracking : 1.0;
                        range = ShipCombatValueDB.DisruptorRange_m;
                    }
                    else
                    {
                        // Laser/pulse beam: light-speed by default, tracks at the base hit chance, carries its OWN reach.
                        velocity = Velocity > 0 ? Velocity : ShipCombatValueDB.LightSpeed_mps;
                        tracking = Tracking >= 0 ? Tracking : 0.95; // GenericBeamWeaponAtb.BaseHitChance default
                        range = Range_m;
                    }
                    break;

                case WeaponDelivery.Slug:   // railgun — finite kinetic, ballistic, mid class range
                    velocity = Velocity;
                    tracking = Tracking >= 0 ? Tracking : 0.0;
                    range = ShipCombatValueDB.RailgunRange_m;
                    break;

                case WeaponDelivery.Bolt:   // plasma — finite bolt (dodgeable) but energy nature, reuses the mid range
                    velocity = Velocity;
                    tracking = Tracking >= 0 ? Tracking : 0.0;
                    range = ShipCombatValueDB.RailgunRange_m;
                    break;

                case WeaponDelivery.Cloud:  // flak — short-range saturating pellet cloud
                    velocity = Velocity;
                    tracking = Tracking >= 0 ? Tracking : 0.0;
                    range = ShipCombatValueDB.FlakRange_m;
                    break;

                case WeaponDelivery.Guided: // missile — long slow tracker (stubs by default)
                    velocity = Velocity > 0 ? Velocity : ShipCombatValueDB.MissileVelocityStub_mps;
                    tracking = Tracking >= 0 ? Tracking : ShipCombatValueDB.MissileTrackingStub;
                    range = ShipCombatValueDB.MissileRange_m;
                    break;

                default:                    // Blast / any future delivery: no forced regime, use what was given
                    velocity = Velocity;
                    tracking = Tracking >= 0 ? Tracking : 0.0;
                    range = Range_m;
                    break;
            }

            return new WeaponProfile(DamagePerSecond, velocity, tracking, Saturation, range,
                Nature, Delivery, Penetration, PerShotEnergy, HeatPerSecond);
        }
    }
}
