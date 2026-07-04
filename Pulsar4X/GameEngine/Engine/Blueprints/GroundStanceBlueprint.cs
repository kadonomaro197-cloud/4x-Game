namespace Pulsar4X.Blueprints
{
    /// <summary>
    /// A selectable GROUND-FORMATION combat stance (moddable; loaded from JSON via ModLoader into
    /// <c>ModDataStore.GroundStances</c>) — the ground echo of <see cref="CombatDoctrineBlueprint"/>. A formation's
    /// active stance (<c>GroundFormation.StanceId</c> + its cached mults) is set by copying one of these via
    /// <c>GroundFormationDoctrine.TrySetStance</c>. Effects are read-time multipliers on the ground resolver
    /// (`GroundForcesProcessor.ResolveRegionCombat`) — never baked into a unit's base stats, so switching is reversible.
    ///
    /// The ±25% trade-off model (the developer's call): Offensive deals more but takes more; Defensive takes less but
    /// deals less; Balanced is neutral. Both mults on ONE stance, so the posture is a genuine give-and-take decision.
    /// </summary>
    public class GroundStanceBlueprint : Blueprint
    {
        // UniqueID comes from Blueprint.

        public string DisplayName { get; set; } = "";

        /// <summary>Offensive | Defensive | Balanced — the family the stance belongs to.</summary>
        public string Family { get; set; } = "";

        /// <summary>Multiplier on this formation's units' ATTACK while the stance is active (1.0 = neutral).</summary>
        public double AttackMult { get; set; } = 1.0;

        /// <summary>Multiplier on the DAMAGE this formation's units TAKE (1.0 = neutral; &gt;1 = take more, &lt;1 = take less).</summary>
        public double DamageTakenMult { get; set; } = 1.0;

        /// <summary>Game-time seconds before a formation on this stance can switch again (the switch cooldown).</summary>
        public double CooldownSeconds { get; set; } = 0;
    }
}
