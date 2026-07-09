using Pulsar4X.Combat;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// THE BRIDGE — turns a <see cref="GroundUnit"/> into the neutral <see cref="CombatKernel.Combatant"/> view the
    /// shared salvo kernel reads, so a planetary unit fights on the SAME math a ship does (resolver merge, slice 3b —
    /// docs/RESOLVER-MERGE-DESIGN.md §7, the north star: "one resolver, both domains").
    ///
    /// The load-bearing idea: a ground unit's Attack + <see cref="GroundWeaponMode"/> + hex Range become a real
    /// <see cref="WeaponProfile"/>, and the old Armor▸Infantry▸Artillery triangle / dodge / shield semantics FALL OUT
    /// of that profile through the kernel instead of being bolted on — because physically-sensible weapon specs
    /// reproduce them:
    ///   • Ballistic (a ~1 km/s slug): velocityTerm≈0 → the kernel's dodge = evasion → AIMED fire is dodgeable,
    ///     exactly the ground rule <c>IsAimed × (1−evasion)</c>.
    ///   • Energy (a finite-velocity bolt): dodgeable like ballistic, but <see cref="WeaponNature.Energy"/> so a
    ///     shield only HALF-soaks it (the ground "energy overloads shields" rule = the kernel's SoakFraction 0.5).
    ///   • Artillery (an area blast): a huge saturation floors the kernel's hit fraction to ~1 → UNDODGEABLE, the
    ///     ground "area can't be dodged" rule.
    ///   • Melee (contact): tracks perfectly (Tracking 1) → undodgeable, "you can't dodge what's on top of you."
    /// So the triangle DISSOLVES into weapon-nature × the target's evasion/shield/armour — the merge's whole point.
    ///
    /// **Slice 3b-i is ADDITIVE and UNWIRED**: this mapper exists and is proven by <c>GroundKernelBridgeTests</c>, but
    /// <c>GroundForcesProcessor.ResolveRegionCombat</c> does NOT call it yet — so live ground combat is byte-identical.
    /// Slice 3b-ii routes the resolver through the kernel via this bridge (the deliberate rebalance; the developer
    /// reviews the number changes and <c>GroundForcesTests</c> is re-baselined). Slice 4 adds the closing model
    /// (Position_m evolving by move speed) so the fight plays out over distance and time — the north-star finish.
    /// </summary>
    public static class GroundCombatant
    {
        // ── Weapon-spec synthesis (flagged defaults — the numbers that make the ground semantics emerge) ────────────

        /// <summary>Muzzle velocity (m/s) given to a DODGEABLE ground weapon (ballistic slug / energy bolt). Chosen far
        /// below <see cref="CombatKernel.VelocityReference_mps"/> (1e6) so the kernel's velocity term is ≈0 and the
        /// dodge reduces to the target's raw evasion — i.e. aimed fire is dodged exactly as the ground matrix intends.</summary>
        public const double AimedVelocity_mps = 1000.0;

        /// <summary>Saturation given to an AREA weapon (artillery blast) — large enough that the kernel's saturation
        /// floor drives the hit fraction to ~1 regardless of evasion, so area fire is UNDODGEABLE (the ground rule).</summary>
        public const double AreaSaturation = 100_000.0;

        /// <summary>Saturation given to a single-target ground weapon (ballistic/energy/melee) — low, so it does NOT
        /// floor the dodge; the target's evasion decides how much lands (aimed fire stays dodgeable).</summary>
        public const double PointSaturation = 1.0;

        /// <summary>Nominal metres-per-hex used to convert a unit's hex <see cref="GroundUnit.Range"/> into the kernel's
        /// metric <see cref="WeaponProfile.Range_m"/> when a real per-body hex pitch isn't supplied. Slice 4 wires the
        /// true pitch (<c>GroundRangeTools.HexPitchKm</c>) so "1 hex on Earth ≠ 1 hex on Io" holds; until the closing
        /// model lands the exact metres don't affect a co-located (separation-0) resolve.</summary>
        public const double NominalHexPitch_m = 1000.0;

        /// <summary>The two-axis weapon identity a ground <see cref="GroundWeaponMode"/> maps to — Nature meets the
        /// defence (shield/armour), Delivery meets the dodge. This is where the ground flavour becomes kernel-native.</summary>
        public static (WeaponNature nature, WeaponDelivery delivery) NatureDeliveryFor(GroundWeaponMode mode) => mode switch
        {
            GroundWeaponMode.Melee     => (WeaponNature.Kinetic,   WeaponDelivery.Slug),   // contact; undodgeable via Tracking 1
            GroundWeaponMode.Ballistic => (WeaponNature.Kinetic,   WeaponDelivery.Slug),   // aimed, dodgeable, shield stops it best
            GroundWeaponMode.Energy    => (WeaponNature.Energy,    WeaponDelivery.Bolt),   // aimed, dodgeable, bleeds through shields
            GroundWeaponMode.Artillery => (WeaponNature.Explosive, WeaponDelivery.Blast),  // area, undodgeable, partly bypasses shields
            _                          => (WeaponNature.Kinetic,   WeaponDelivery.Slug),
        };

        /// <summary>Build the <see cref="WeaponProfile"/> for a unit's primary weapon from its Attack / DamageType /
        /// hex Range. The velocity/tracking/saturation are chosen (per the constants above) so the kernel reproduces the
        /// ground dodge semantics for that mode. <paramref name="hexPitch_m"/> converts hex range → metres.</summary>
        public static WeaponProfile ToWeaponProfile(GroundUnit unit, double hexPitch_m = NominalHexPitch_m)
        {
            var (nature, delivery) = NatureDeliveryFor(unit.DamageType);
            double range_m = unit.Range > 0 ? unit.Range * hexPitch_m : 0; // 0 = melee / same-hex (kernel: 0 = unbounded-at-contact)

            double velocity, tracking, saturation;
            switch (unit.DamageType)
            {
                case GroundWeaponMode.Melee:
                    velocity = 1.0; tracking = 1.0; saturation = PointSaturation;          // Tracking 1 → undodgeable contact
                    break;
                case GroundWeaponMode.Artillery:
                    velocity = 300.0; tracking = 0.0; saturation = AreaSaturation;          // area → saturation floors dodge to ~1
                    break;
                default: // Ballistic / Energy — aimed, dodgeable
                    velocity = AimedVelocity_mps; tracking = 0.0; saturation = PointSaturation;
                    break;
            }
            return new WeaponProfile(unit.Attack, velocity, tracking, saturation, range_m, nature, delivery);
        }

        /// <summary>Present a <see cref="GroundUnit"/> as the neutral <see cref="CombatKernel.Combatant"/> the shared
        /// kernel reads. The unit's flat <see cref="GroundUnit.Shield"/> seeds a depleting shield POOL (the ship model —
        /// slice 3b unifies ground onto the pool, a deliberate change from the old innate-% shield). <see cref="GroundUnit.Defense"/>
        /// is the flat armour; <see cref="GroundUnit.Evasion"/> the dodge. Position_m starts at 0 (co-located); slice 4
        /// evolves it by move speed for the closing fight.</summary>
        public static CombatKernel.Combatant ToCombatant(GroundUnit unit, double hexPitch_m = NominalHexPitch_m)
        {
            return new CombatKernel.Combatant
            {
                FactionId = unit.FactionOwnerID,
                Health = unit.Health,
                MaxHealth = unit.MaxHealth,
                Evasion = unit.Evasion,
                Armour = unit.Defense,
                ShieldPool = unit.Shield,       // seed full; a depleting pool (kernel ResolveShield), regen 0 in v1
                ShieldCapacity = unit.Shield,
                ShieldRegen = 0,
                Weapons = new System.Collections.Generic.List<WeaponProfile> { ToWeaponProfile(unit, hexPitch_m) },
                Position_m = 0,
            };
        }
    }
}
