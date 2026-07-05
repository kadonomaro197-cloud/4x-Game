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
    ///
    /// A pure multiplier (≤ 1), so it drops straight into the proportional salvo resolver (multiply the damage an
    /// attacker deals to a target). Armour/Defense is deliberately out of v1 (it's currently unused by the resolver;
    /// folding it in is a separate rebalance). ALL constants are flagged for the developer. Applied by
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
    }
}
