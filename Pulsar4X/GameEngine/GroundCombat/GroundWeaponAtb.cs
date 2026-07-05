using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.GroundCombat
{
    /// <summary>How a ground weapon delivers its hurt — flavour + a hook for later resolve nuances (guidance, cover).</summary>
    public enum GroundWeaponMode : byte
    {
        Melee,      // range 0 — claws, lightsabres, chainswords
        Ballistic,  // solid rounds — autocannon, bolter
        Energy,     // beams/plasma — las, beam rifle
        Artillery   // long-range indirect — siege cannon, basilisk
    }

    /// <summary>
    /// A WEAPON part bolted onto a unit's frame — the ground echo of a ship weapon component, and the GENERAL one:
    /// there is no BolterAtb / AutocannonAtb / LightsabreAtb, just this one attribute with knobs, so every weapon
    /// flavour across every franchise is the SAME part with different numbers + a name (the developer's
    /// generalise-by-function call, 2026-07-05). Its <see cref="Mass"/> is what the frame's carry budget must bear —
    /// a heavy autocannon needs a strong frame (or an augment), a laspistol fits anything.
    ///
    /// Contributes to the assembled unit: <see cref="Attack"/> sums into the unit's firepower, <see cref="Range"/>
    /// (in hexes) sets how far this weapon reaches (the unit's reach = its longest weapon), <see cref="Mode"/> flavours
    /// the fire. A component attribute (CONVENTIONS §6); inert on install (the assembler reads it — G-D3).
    /// Design: docs/GROUND-COMBAT-MAP-DESIGN.md → unit designer.
    /// </summary>
    public class GroundWeaponAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>What the frame's carry budget must bear to mount this weapon.</summary>
        [JsonProperty] public double Mass { get; internal set; }
        /// <summary>Hurt this weapon adds to the unit's per-round firepower.</summary>
        [JsonProperty] public double Attack { get; internal set; }
        /// <summary>Reach in HEXES (0 = melee / same hex only).</summary>
        [JsonProperty] public int Range { get; internal set; }
        [JsonProperty] public GroundWeaponMode Mode { get; internal set; } = GroundWeaponMode.Ballistic;

        public GroundWeaponAtb() { }

        // double args for the JSON/NCalc binder (gotcha L7). Order = template PropertyFormula order.
        public GroundWeaponAtb(double mass, double attack, double range, double mode)
        {
            Mass = mass < 0 ? 0 : mass;
            Attack = attack < 0 ? 0 : attack;
            Range = range < 0 ? 0 : (int)range;
            Mode = (GroundWeaponMode)(int)mode;
        }

        public override object Clone() => new GroundWeaponAtb(Mass, Attack, Range, (double)(int)Mode);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Ground Weapon";
        public string AtbDescription()
            => $"A {Mode} weapon — attack {Attack:0}, reach {Range} hex, mass {Mass:0} (the frame must bear it).";
    }
}
