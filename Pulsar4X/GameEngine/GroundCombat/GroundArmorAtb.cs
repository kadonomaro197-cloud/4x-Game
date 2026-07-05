using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// An ARMOUR / plating part — the survivability-by-soaking option in the parts bin (the other path is
    /// survivability-by-dodging, an <see cref="GroundAugmentAtb"/> with evasion — how a Jedi survives with no armour).
    /// Adds <see cref="HP"/> to the unit's health pool and <see cref="Defense"/> (damage mitigation) at the cost of
    /// <see cref="Mass"/> the frame must bear — the classic trade of protection for weight.
    ///
    /// A component attribute (CONVENTIONS §6), general (one attribute for ceramite, reactive plating, carapace — knobs,
    /// not new types). Inert on install (the assembler reads it — G-D3). Design: docs/GROUND-COMBAT-MAP-DESIGN.md.
    /// </summary>
    public class GroundArmorAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>Mass the frame must bear to wear this plating.</summary>
        [JsonProperty] public double Mass { get; internal set; }
        /// <summary>Health this plating adds to the unit's pool.</summary>
        [JsonProperty] public double HP { get; internal set; }
        /// <summary>Damage mitigation this plating adds (soaks a fraction of incoming hurt).</summary>
        [JsonProperty] public double Defense { get; internal set; }

        public GroundArmorAtb() { }

        // double args for the JSON/NCalc binder (gotcha L7).
        public GroundArmorAtb(double mass, double hp, double defense)
        {
            Mass = mass < 0 ? 0 : mass;
            HP = hp < 0 ? 0 : hp;
            Defense = defense < 0 ? 0 : defense;
        }

        public override object Clone() => new GroundArmorAtb(Mass, HP, Defense);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Ground Armour";
        public string AtbDescription()
            => $"Plating — +{HP:0} HP, +{Defense:0} defence, mass {Mass:0} (the frame must bear it).";
    }
}
