namespace Pulsar4X.Blueprints
{
    /// <summary>
    /// A selectable combat posture (moddable; loaded from JSON via ModLoader into
    /// <c>ModDataStore.CombatDoctrines</c>). A fleet's active posture (<c>FleetDoctrineDB</c>) is set by copying
    /// one of these. Effects are read-time multipliers on the auto-resolver (the BonusesDB pattern) — never baked
    /// into a ship's base stats, so switching is reversible. See docs/combat/COMBAT-DESIGN.md System 4.
    ///
    /// <para><b>THE UNIFIED DOCTRINE (developer's call, 2026-07-24): "one doctrine catalog used in all combat for
    /// all scenarios."</b> This blueprint is now the single shape for SPACE fleets and GROUND formations alike, and
    /// it is the container for every combat BEHAVIOUR decision — not just a strength multiplier. The rulings it
    /// carries: target priority (who we shoot first), the retreat trigger and its break-away timer, whether we
    /// pursue a withdrawing enemy, and whether we will open fire at all.</para>
    ///
    /// <para><b>⚠ The reciprocal trap.</b> Space authored toughness as <see cref="ToughnessMult"/> (1.4 = tougher)
    /// while ground authored the SAME idea as <see cref="DamageTakenMult"/> (0.75 = takes less). They are
    /// reciprocals, not synonyms. Author EITHER one and let <c>CombatDoctrine.EffectiveToughnessMult</c> /
    /// <c>EffectiveDamageTakenMult</c> derive the other — never hand-author both to inconsistent values.</para>
    ///
    /// NOT the same as <c>FactionInfoDB.Doctrine</c> (the strategic Economic/Military/Tech/Expansion AI vector) —
    /// same word, different thing.
    /// </summary>
    public class CombatDoctrineBlueprint : Blueprint
    {
        // UniqueID comes from Blueprint.

        public string DisplayName { get; set; } = "";

        /// <summary>Offensive | Defensive | Utilitarian | Balanced — the family the posture belongs to.</summary>
        public string Family { get; set; } = "";

        /// <summary>Which combat domain may select this entry: "Both" (default) | "Space" | "Ground".
        /// Parsed by <c>CombatDoctrine.ParseDomain</c>; an unrecognised value falls back to Both.</summary>
        public string Domain { get; set; } = "Both";

        /// <summary>Multiplier on this force's firepower while the posture is active (1.0 = neutral).
        /// The ground catalog called this "AttackMult" — same number, one name now.</summary>
        public double FirepowerMult { get; set; } = 1.0;

        /// <summary>Multiplier on this force's toughness while the posture is active (1.0 = neutral; &gt;1 = tougher).
        /// Leave at 1.0 and author <see cref="DamageTakenMult"/> instead if you prefer the ground encoding —
        /// see the reciprocal note on the class.</summary>
        public double ToughnessMult { get; set; } = 1.0;

        /// <summary>Multiplier on the DAMAGE this force TAKES (1.0 = neutral; &lt;1 = takes less, &gt;1 = takes more).
        /// The RECIPROCAL of <see cref="ToughnessMult"/>; the ground catalog's native encoding. 0 or negative means
        /// "not authored — derive it from ToughnessMult".</summary>
        public double DamageTakenMult { get; set; } = 0.0;

        /// <summary>Multiplier on movement speed (1.0 = neutral).</summary>
        public double SpeedMult { get; set; } = 1.0;

        /// <summary>Game-time seconds before a force on this posture can switch again.</summary>
        public double CooldownSeconds { get; set; } = 0;

        /// <summary>True if this posture is a standing disengage/withdraw order.</summary>
        public bool IsRetreat { get; set; } = false;

        // ── The behaviour dials the 2026-07-24 rulings added ────────────────────────────────────────────────

        /// <summary>Weapons-release posture: "WeaponsFree" (default — fights on contact) | "WeaponsHold" (never
        /// shoots first) | "ReturnFire" (won't start a fight, defends once in one). The ENGAGE decision, ruling #19.
        /// Parsed by <c>CombatDoctrine.ParsePosture</c>.</summary>
        public string EngagementPosture { get; set; } = "WeaponsFree";

        /// <summary>Who this force shoots first — see <c>Pulsar4X.Combat.TargetPriority</c>. Default "Balanced"
        /// reproduces the legacy spread-by-health behaviour exactly. Ruling #18.</summary>
        public string TargetPriority { get; set; } = "Balanced";

        /// <summary>Fraction of its starting strength this force will lose before breaking off (0..1).
        /// Negative = "not authored — use the engine default" (<c>CombatEngagement.RetreatCasualtyThreshold</c>,
        /// today a flat 0.5 for every fleet). Ruling #15 — the retreat TRIGGER becomes per-doctrine.</summary>
        public double RetreatCasualtyThreshold { get; set; } = -1.0;

        /// <summary>Game-time seconds a force spends BREAKING AWAY once it calls a retreat — it is still in the
        /// fight, and still being shot, until this elapses. 0 = instant (the legacy behaviour). Ruling #15: "retreat
        /// sets a timer for the units to break away."</summary>
        public double BreakAwaySeconds { get; set; } = 0.0;

        /// <summary>Does this force CHASE an enemy that is breaking away? Ruling #15 — "pursuit can also be a
        /// doctrine that the opponent can use." Default false = the legacy free disengage.</summary>
        public bool Pursues { get; set; } = false;
    }
}
