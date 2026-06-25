namespace Pulsar4X.Blueprints
{
    /// <summary>
    /// A selectable combat posture (moddable; loaded from JSON via ModLoader into
    /// <c>ModDataStore.CombatDoctrines</c>). A fleet's active posture (<c>FleetDoctrineDB</c>) is set by copying
    /// one of these. Effects are read-time multipliers on the auto-resolver (the BonusesDB pattern) — never baked
    /// into a ship's base stats, so switching is reversible. See docs/COMBAT-DESIGN.md System 4.
    ///
    /// NOT the same as <c>FactionInfoDB.Doctrine</c> (the strategic Economic/Military/Tech/Expansion AI vector) —
    /// same word, different thing.
    /// </summary>
    public class CombatDoctrineBlueprint : Blueprint
    {
        // UniqueID comes from Blueprint.

        public string DisplayName { get; set; } = "";

        /// <summary>Offensive | Defensive | Utilitarian — the family the posture belongs to.</summary>
        public string Family { get; set; } = "";

        /// <summary>Multiplier on this fleet's firepower while the posture is active (1.0 = neutral).</summary>
        public double FirepowerMult { get; set; } = 1.0;

        /// <summary>Multiplier on this fleet's toughness while the posture is active (1.0 = neutral).</summary>
        public double ToughnessMult { get; set; } = 1.0;

        /// <summary>Multiplier on fleet movement speed (1.0 = neutral). v1: stored, applied in v2.</summary>
        public double SpeedMult { get; set; } = 1.0;

        /// <summary>Game-time seconds before a fleet on this posture can switch again.</summary>
        public double CooldownSeconds { get; set; } = 0;

        /// <summary>True if this posture is a disengage/withdraw order (the fleet retreats — see step 7).</summary>
        public bool IsRetreat { get; set; } = false;
    }
}
