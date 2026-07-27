using System;
using Pulsar4X.Blueprints;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// The pure reader for the UNIFIED doctrine catalog (<see cref="CombatDoctrineBlueprint"/>) — one catalog used
    /// by all combat, space and ground alike (the developer's call, 2026-07-24).
    ///
    /// <para>Everything here is a pure static function of a blueprint: no entity reads, no game state, never throws.
    /// That keeps it unit-testable without standing up a game, and it means the resolvers can call it on the hot
    /// path without worrying about order of evaluation.</para>
    ///
    /// <para><b>Why this class exists at all — the reciprocal.</b> The two catalogs encoded "tougher" in opposite
    /// directions: a space doctrine says <c>ToughnessMult 1.4</c> (I am 1.4× as tough), a ground stance says
    /// <c>DamageTakenMult 0.75</c> (I take 0.75× the damage). Those describe the same posture and they are
    /// RECIPROCALS of each other, so merging the catalogs naively would silently flip a defensive stance into a
    /// fragile one. An author now sets whichever field reads naturally and the other is derived here, in one place.</para>
    /// </summary>
    public static class CombatDoctrine
    {
        /// <summary>Clamp guard so a malformed blueprint can never produce a divide-by-zero or a negative multiplier.</summary>
        public const double MinMultiplier = 0.01;

        /// <summary>The toughness multiplier to use for a blueprint (&gt;1 = tougher).
        /// Prefers an authored <see cref="CombatDoctrineBlueprint.DamageTakenMult"/> (the ground encoding) and
        /// inverts it; otherwise uses the space-encoded <see cref="CombatDoctrineBlueprint.ToughnessMult"/>.</summary>
        public static double EffectiveToughnessMult(CombatDoctrineBlueprint bp)
        {
            if (bp == null) return 1.0;
            if (bp.DamageTakenMult > 0.0) return Reciprocal(bp.DamageTakenMult);
            return Sane(bp.ToughnessMult);
        }

        /// <summary>The damage-taken multiplier to use for a blueprint (&lt;1 = takes less).
        /// The mirror of <see cref="EffectiveToughnessMult"/>: an authored DamageTakenMult wins, else it is derived
        /// by inverting ToughnessMult.</summary>
        public static double EffectiveDamageTakenMult(CombatDoctrineBlueprint bp)
        {
            if (bp == null) return 1.0;
            if (bp.DamageTakenMult > 0.0) return Sane(bp.DamageTakenMult);
            return Reciprocal(bp.ToughnessMult);
        }

        /// <summary>The firepower/attack multiplier (the ground catalog's "AttackMult" is the same number).</summary>
        public static double EffectiveFirepowerMult(CombatDoctrineBlueprint bp) => bp == null ? 1.0 : Sane(bp.FirepowerMult);

        /// <summary>Parse the weapons-release posture; anything unrecognised (or absent) reads as WeaponsFree, which
        /// is the legacy "fights on contact" behaviour.</summary>
        public static EngagementPosture ParsePosture(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return EngagementPosture.WeaponsFree;
            return Enum.TryParse<EngagementPosture>(s.Trim(), true, out var p) ? p : EngagementPosture.WeaponsFree;
        }

        /// <summary>Parse the target-selection dial; anything unrecognised reads as Balanced, which is the legacy
        /// spread-fire-by-health behaviour.</summary>
        public static TargetPriority ParseTargetPriority(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return TargetPriority.Balanced;
            return Enum.TryParse<TargetPriority>(s.Trim(), true, out var t) ? t : TargetPriority.Balanced;
        }

        /// <summary>Parse the domain gate; anything unrecognised reads as Both (permissive, so a typo can never
        /// make a doctrine vanish from every list).</summary>
        public static DoctrineDomain ParseDomain(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return DoctrineDomain.Both;
            return Enum.TryParse<DoctrineDomain>(s.Trim(), true, out var d) ? d : DoctrineDomain.Both;
        }

        /// <summary>Is this entry selectable by the given domain? A "Both" entry is selectable by everyone.</summary>
        public static bool IsSelectableBy(CombatDoctrineBlueprint bp, DoctrineDomain domain)
        {
            if (bp == null) return false;
            var d = ParseDomain(bp.Domain);
            return d == DoctrineDomain.Both || d == domain;
        }

        /// <summary>The retreat threshold to apply — the blueprint's if authored (0..1), otherwise the engine
        /// default the caller passes in (today <c>CombatEngagement.RetreatCasualtyThreshold</c>, a flat 0.5).</summary>
        public static double EffectiveRetreatThreshold(CombatDoctrineBlueprint bp, double engineDefault)
        {
            if (bp == null || bp.RetreatCasualtyThreshold < 0.0) return engineDefault;
            return Math.Min(1.0, bp.RetreatCasualtyThreshold);
        }

        /// <summary>Seconds a force spends breaking away after calling a retreat (never negative). 0 = instant,
        /// the legacy behaviour.</summary>
        public static double EffectiveBreakAwaySeconds(CombatDoctrineBlueprint bp)
            => bp == null || bp.BreakAwaySeconds <= 0.0 ? 0.0 : bp.BreakAwaySeconds;

        /// <summary>Does this doctrine chase a withdrawing enemy?</summary>
        public static bool Pursues(CombatDoctrineBlueprint bp) => bp != null && bp.Pursues;

        // ── guards ──────────────────────────────────────────────────────────────────────────────────────────

        private static double Sane(double v)
            => !double.IsFinite(v) || v < MinMultiplier ? (double.IsFinite(v) && v >= 0 ? Math.Max(v, MinMultiplier) : 1.0) : v;

        private static double Reciprocal(double v)
        {
            var s = Sane(v);
            return s <= 0.0 ? 1.0 : 1.0 / s;
        }
    }
}
