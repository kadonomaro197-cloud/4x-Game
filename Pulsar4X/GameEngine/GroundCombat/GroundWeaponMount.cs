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
        /// truth, the hex is only the ruler). Populated by <c>GroundUnitAssembly.Compute</c> from the hex range × a fixed
        /// nominal reference pitch (a real per-body pitch is a later slice); 0 = unset. <b>ADDITIVE + UNREAD by the
        /// resolver</b> (it still gates on <see cref="RangeHexes"/>) → byte-identical; the range gate flips to this in
        /// Slice 2. Deep-copied below. Design: docs/combat/REAL-DISTANCE-COMBAT-DESIGN.md.</summary>
        [JsonProperty] public double Range_m { get; internal set; }
        /// <summary>This weapon's damage flavour (Ballistic / Energy / Artillery / Melee …).</summary>
        [JsonProperty] public GroundWeaponMode Mode { get; internal set; } = GroundWeaponMode.Ballistic;

        public GroundWeaponMount() { }
        public GroundWeaponMount(GroundWeaponMount o) { Attack = o.Attack; RangeHexes = o.RangeHexes; Range_m = o.Range_m; Mode = o.Mode; }
    }
}
