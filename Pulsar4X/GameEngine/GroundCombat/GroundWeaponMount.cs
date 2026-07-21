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
        /// <summary>This weapon's damage flavour (Ballistic / Energy / Artillery / Melee …).</summary>
        [JsonProperty] public GroundWeaponMode Mode { get; internal set; } = GroundWeaponMode.Ballistic;

        public GroundWeaponMount() { }
        public GroundWeaponMount(GroundWeaponMount o) { Attack = o.Attack; RangeHexes = o.RangeHexes; Mode = o.Mode; }
    }
}
