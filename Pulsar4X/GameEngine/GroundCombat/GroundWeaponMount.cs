using Newtonsoft.Json;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// One weapon in a <see cref="GroundUnit"/>'s LOADOUT (W-track, W1) — its OWN Attack, hex Range, and
    /// <see cref="GroundWeaponMode"/> flavour. A unit carries a LIST of these (one per mounted weapon component)
    /// so its weapons stay DISTINCT: a lascannon (long range) and a chainsword (melee) reach the enemy at
    /// different moments as the unit closes, instead of collapsing into a single Attack + Range. A plain
    /// serializable value object (the same choice as <see cref="GroundUnit"/> itself); deep-copied via its
    /// copy-ctor for save-safety. Populated by <c>GroundUnitAssembly.Compute</c>, snapshot onto the raised unit.
    /// ADDITIVE — W1 only carries the data; W2 wires per-weapon range banding into the resolver.
    /// </summary>
    public class GroundWeaponMount
    {
        /// <summary>This weapon's firepower (already ×count for the mounted component). Σ over a unit's loadout
        /// equals the unit's collapsed <see cref="GroundUnit.Attack"/> — the byte-identity invariant.</summary>
        [JsonProperty] public double Attack { get; internal set; }
        /// <summary>This weapon's strike range in HEXES. Max over a unit's loadout equals <see cref="GroundUnit.Range"/>.</summary>
        [JsonProperty] public int RangeHexes { get; internal set; }
        /// <summary>REAL-DISTANCE FOUNDATION (Slice 1b) — this weapon's reach in real METRES, the metric TRUTH alongside the
        /// display <see cref="RangeHexes"/> (a hex is a different real distance on every body; the km on the gun is the
        /// truth, the hex is only the ruler). Populated by <c>GroundUnitAssembly.Compute</c> from the weapon's authored
        /// <c>GroundWeaponAtb.Range_m</c> (K1), else derived from the hex range × a nominal reference pitch; 0 = unset.
        /// <b>THE RESOLVER READS THIS</b> since K3: <c>GroundForcesProcessor.WeaponReaches</c> is handed this value and,
        /// when <c>EnableMiniHexCombat</c> is on (OFF in CI, ON for menu games), gates on the REAL metre gap instead of
        /// <see cref="RangeHexes"/>. Deep-copied below.
        /// Design: docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md §12.</summary>
        [JsonProperty] public double Range_m { get; internal set; }
        /// <summary>This weapon's damage flavour (Ballistic / Energy / Artillery / Melee …).</summary>
        [JsonProperty] public GroundWeaponMode Mode { get; internal set; } = GroundWeaponMode.Ballistic;
        /// <summary>PER-MOUNT armour-crack (the "honest home" the assembler backlog names): this weapon's own
        /// <c>GroundWeaponAtb.Penetration</c>, so a unit with a rifle AND a railgun cracks plate only with the railgun.
        /// The resolver reads it via <c>GroundCombatant.ToWeaponProfile(unit, mount)</c> → the armour soak. 0 = a normal
        /// round.</summary>
        [JsonProperty] public double Penetration { get; internal set; }
        /// <summary>PER-MOUNT alpha-vs-chip: this weapon's own <c>GroundWeaponAtb.PerShotEnergy</c> (joules per shot),
        /// driving the kernel's burst-shot split. 0 = one lump.</summary>
        [JsonProperty] public double PerShotEnergy { get; internal set; }

        public GroundWeaponMount() { }
        public GroundWeaponMount(GroundWeaponMount o) { Attack = o.Attack; RangeHexes = o.RangeHexes; Range_m = o.Range_m; Mode = o.Mode; Penetration = o.Penetration; PerShotEnergy = o.PerShotEnergy; }
    }
}
