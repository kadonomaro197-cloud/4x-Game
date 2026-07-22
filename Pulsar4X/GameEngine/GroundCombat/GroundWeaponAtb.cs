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
        /// <summary>Reach in HEXES (0 = melee / same hex only). The DISPLAY ruler; <see cref="Range_m"/> is the truth.</summary>
        [JsonProperty] public int Range { get; internal set; }
        /// <summary>REAL-DISTANCE (K1) — this weapon's reach in real METRES: the km on the gun is the TRUTH, the hex
        /// <see cref="Range"/> is only the display ruler (a hex is a different real distance on every body). Authored per
        /// weapon (rifle 500 m, autocannon 2000 m, tank cannon 4000 m, tube artillery 30000 m, ground laser 20000 m,
        /// melee/claw 0). 0 = derive from the hex range × the nominal pitch as a fallback (a mod template that omits it).
        /// Snapshotted through the assembler → the design → the raised unit; the resolver's real-distance gate reads it.
        /// Design: docs/combat/REAL-DISTANCE-COMBAT-DESIGN.md.</summary>
        [JsonProperty] public double Range_m { get; internal set; }
        [JsonProperty] public GroundWeaponMode Mode { get; internal set; } = GroundWeaponMode.Ballistic;

        public GroundWeaponAtb() { }

        // double args for the JSON/NCalc binder (gotcha L7). Order = template PropertyFormula order. NOTE (gotcha 6): the
        // JSON binder is EXACT-ARITY — it calls this ctor with exactly the number of AtbConstrArgs values the template
        // passes, so every base-mod ground-weapon template now passes 5 values (CarryMass, Attack, Range, Mode, Range_m).
        // The `= 0` default is only for C# callers/tests (a code-built weapon that omits the real range).
        public GroundWeaponAtb(double mass, double attack, double range, double mode, double range_m = 0)
        {
            Mass = mass < 0 ? 0 : mass;
            Attack = attack < 0 ? 0 : attack;
            Range = range < 0 ? 0 : (int)range;
            Mode = (GroundWeaponMode)(int)mode;
            Range_m = range_m < 0 ? 0 : range_m;
        }

        public override object Clone() => new GroundWeaponAtb(Mass, Attack, Range, (double)(int)Mode, Range_m);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Ground Weapon";
        public string AtbDescription()
            => $"A {Mode} weapon — attack {Attack:0}, reach {Range} hex" +
               (Range_m > 0 ? $" (~{Range_m / 1000.0:0.###} km real)" : "") +
               $", mass {Mass:0} (the frame must bear it).";
    }
}
