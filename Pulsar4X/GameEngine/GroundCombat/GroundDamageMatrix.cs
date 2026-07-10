using System;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// SYSTEM ① — the damage × defence MATCHUP (slice A). The rock-paper-scissors that makes *what you build* depend on
    /// *what they field*: a hit's damage is scaled by how the attacker's damage-flavour meets the target's defence.
    /// Two rules in v1, both reading the fields slice B put on the unit (Evasion, Shield, DamageType):
    ///
    ///   • DODGE (evasion) beats AIMED fire only. Ballistic/Energy shots can be evaded — ×(1 − evasion). Artillery
    ///     (area) and Melee (contact) can't be dodged — ×1. So a Jedi/Zergling shrugs off rifle fire but eats a flamer
    ///     or a shell. "Saturation beats dodge."
    ///   • SHIELD is an innate % damage reduction (v1: a toughness stat, NOT yet a depleting pool — that's v2), and it
    ///     is WEAKER vs Energy, which overloads it. So a shielded soldier soaks bullets but energy bleeds through.
    ///   • ARMOUR (the unit's Defense stat) is a FLAT reduction taken off EACH incoming attacker-source (a "volley"),
    ///     floored so a source always lands a minimum fraction. This is the THIRD, DISTINCT defence flavour and the one
    ///     that makes armour armour: because the soak is flat-PER-SOURCE, a swarm of many small attackers has most of
    ///     each little volley bounced off (armour ≈ negates it), while ONE big alpha strike barely notices the flat
    ///     plating and punches through. Flat armour is the counter to chip-damage-by-numbers; % shield and dodge are not.
    ///
    /// The dodge×shield part is a pure multiplier (≤ 1) via <see cref="Matchup"/>; the flat armour part needs the
    /// ABSOLUTE per-source damage so it lives in <see cref="ArmourSoak"/> (the resolver calls it once per attacker→target
    /// contribution, AFTER the matchup multiplier). ALL constants are flagged for the developer. Applied by
    /// <c>GroundForcesProcessor.ResolveRegionCombat</c>. Design: docs/GROUND-UNIT-DESIGNER-DESIGN.md §6b system ①.
    /// </summary>
    public static class GroundDamageMatrix
    {
        // ── NUMBERS TO REVIEW (flagged defaults) ──────────────────────────────────────────────────────────────────
        /// <summary>Most damage a maxed-out dodger can avoid (so nothing is ever untouchable).</summary>
        public const double EvasionCap = 0.9;
        /// <summary>Shield value that yields a 50% innate reduction (s = Shield / (Shield + this)). Bigger = shields
        /// need more to matter.</summary>
        public const double ShieldRefK = 150.0;
        /// <summary>Ceiling on a shield's innate reduction (so a huge shield isn't invulnerable).</summary>
        public const double ShieldReductionCap = 0.75;
        // shield effectiveness vs each damage flavour — energy overloads shields; area explosions partly bypass.
        public const double ShieldEffVsEnergy = 0.5;
        public const double ShieldEffVsArtillery = 0.75;
        public const double ShieldEffVsPhysical = 1.0;   // Ballistic + Melee
        /// <summary>Damage soaked off EACH incoming attacker-source, per point of the target's Defense (armour).
        /// Flat — the whole point is that it scales with the NUMBER of volleys, not their size, so many small hits are
        /// bounced while one big hit isn't. Bigger = armour matters more. Forwards to
        /// <c>Combat.CombatKernel.ArmourSoakPerPoint</c> (resolver merge, slice 3a) — the shared kernel owns the single
        /// value so the ground path and the kernel can never read different armour numbers.</summary>
        public const double ArmourSoakPerPoint = Pulsar4X.Combat.CombatKernel.ArmourSoakPerPoint;
        /// <summary>A source always lands at least this fraction of its damage no matter how much armour it meets — so
        /// flat armour is never total immunity (the counterpart to the evasion/shield caps). Forwards to
        /// <c>Combat.CombatKernel.ArmourMinPassFraction</c> (resolver merge, slice 3a).</summary>
        public const double ArmourMinPassFraction = Pulsar4X.Combat.CombatKernel.ArmourMinPassFraction;

        /// <summary>Only aimed fire can be dodged; area (Artillery) and contact (Melee) can't.</summary>
        public static bool IsAimed(GroundWeaponMode t) => t == GroundWeaponMode.Ballistic || t == GroundWeaponMode.Energy;

        private static double ShieldEff(GroundWeaponMode t) => t switch
        {
            GroundWeaponMode.Energy => ShieldEffVsEnergy,
            GroundWeaponMode.Artillery => ShieldEffVsArtillery,
            _ => ShieldEffVsPhysical,   // Ballistic, Melee
        };

        /// <summary>Damage multiplier (0..1) for an attack of <paramref name="dmgType"/> landing on
        /// <paramref name="target"/> — the combined dodge × shield matchup. 1.0 = full damage. Never throws.</summary>
        public static double Matchup(GroundWeaponMode dmgType, GroundUnit target)
        {
            if (target == null) return 1.0;
            double m = 1.0;

            // dodge — only aimed fire can be evaded
            if (IsAimed(dmgType))
            {
                double ev = target.Evasion;
                if (ev > EvasionCap) ev = EvasionCap;
                if (ev > 0) m *= 1.0 - ev;
            }

            // shield — innate % reduction, weaker vs energy
            if (target.Shield > 0)
            {
                double s = target.Shield / (target.Shield + ShieldRefK);
                if (s > ShieldReductionCap) s = ShieldReductionCap;
                m *= 1.0 - s * ShieldEff(dmgType);
            }

            return m < 0 ? 0 : m;
        }

        /// <summary>Flat ARMOUR soak for ONE attacker-source's contribution: subtract a flat amount (scaled by the
        /// target's Defense), floored so the source always lands <see cref="ArmourMinPassFraction"/> of its damage.
        /// Called per attacker→target contribution AFTER <see cref="Matchup"/> — because it's flat-per-source, N small
        /// volleys lose N×(flat) total while one big volley loses only (flat): armour bounces the swarm, the alpha
        /// punches through. <paramref name="sourceDamage"/> is one attacker's post-matchup contribution to this target.
        /// Never throws.</summary>
        public static double ArmourSoak(double defense, double sourceDamage)
            => Pulsar4X.Combat.CombatKernel.ArmourSoak(defense, sourceDamage);

        /// <summary>Flat ARMOUR soak WITH the firing weapon's PENETRATION (Weapons pilot W1b) — penetration cancels
        /// the target's Defense point-for-point before the flat soak, so an AP/sabot/lance weapon cracks plate a
        /// normal round bounces off. Forwards to the shared <c>CombatKernel.ArmourSoak</c> 3-arg. Penetration 0 is
        /// byte-for-byte the 2-arg soak above (so an ordinary unit is unchanged). Never throws.</summary>
        public static double ArmourSoak(double defense, double sourceDamage, double penetration)
            => Pulsar4X.Combat.CombatKernel.ArmourSoak(defense, sourceDamage, penetration);
    }
}
