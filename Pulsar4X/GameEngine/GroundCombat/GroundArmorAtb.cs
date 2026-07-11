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

        // ── NATURE TUNING (⚙3 Defense — ablative/composite/reactive plating) ──
        // How well THIS plating's Defense soaks each incoming damage NATURE. 1.0 = its rated strength (a plain plate,
        // and every base-mod plating until a tuned one is authored → byte-identical); >1 = tuned to that nature
        // (ablative laminate shrugs off energy); <1 = a poor match (that same laminate is thin against a kinetic slug).
        // These scale the flat armour soak per source (Pulsar4X.Combat.CombatKernel.ArmourSoak natureFactor), so a build
        // decides "armour against WHAT" — the armour half of the weapon-nature matchup the shield already has.
        /// <summary>Soak effectiveness vs KINETIC damage (slugs, railguns). 1.0 = rated.</summary>
        [JsonProperty] public double VsKinetic { get; internal set; } = 1.0;
        /// <summary>Soak effectiveness vs ENERGY damage (lasers, plasma). 1.0 = rated.</summary>
        [JsonProperty] public double VsEnergy { get; internal set; } = 1.0;
        /// <summary>Soak effectiveness vs EXPLOSIVE damage (HE, missiles). 1.0 = rated.</summary>
        [JsonProperty] public double VsExplosive { get; internal set; } = 1.0;
        /// <summary>Soak effectiveness vs EXOTIC damage (disruptor / anti-armour specials). 1.0 = rated.</summary>
        [JsonProperty] public double VsExotic { get; internal set; } = 1.0;

        public GroundArmorAtb() { }

        // double args for the JSON/NCalc binder (gotcha L7). The 3-arg ctor keeps every EXISTING base-mod plating
        // template (which feeds exactly 3 AtbConstrArgs) byte-identical — its natures default to 1.0 (a plain plate).
        public GroundArmorAtb(double mass, double hp, double defense)
        {
            Mass = mass < 0 ? 0 : mass;
            HP = hp < 0 ? 0 : hp;
            Defense = defense < 0 ? 0 : defense;
        }

        // 7-arg ctor for a NATURE-TUNED plating (a template feeding all four nature factors). Binder matches by exact
        // arg count (L7), so only a 7-value AtbConstrArgs template reaches this — the 3-arg plate is untouched.
        public GroundArmorAtb(double mass, double hp, double defense, double vsKinetic, double vsEnergy, double vsExplosive, double vsExotic)
        {
            Mass = mass < 0 ? 0 : mass;
            HP = hp < 0 ? 0 : hp;
            Defense = defense < 0 ? 0 : defense;
            VsKinetic = vsKinetic < 0 ? 0 : vsKinetic;
            VsEnergy = vsEnergy < 0 ? 0 : vsEnergy;
            VsExplosive = vsExplosive < 0 ? 0 : vsExplosive;
            VsExotic = vsExotic < 0 ? 0 : vsExotic;
        }

        /// <summary>This plating's soak effectiveness vs a given damage nature (the four fields above). 1.0 for a plain
        /// plate.</summary>
        public double ResistFor(Pulsar4X.Combat.WeaponNature nature) => nature switch
        {
            Pulsar4X.Combat.WeaponNature.Kinetic => VsKinetic,
            Pulsar4X.Combat.WeaponNature.Energy => VsEnergy,
            Pulsar4X.Combat.WeaponNature.Explosive => VsExplosive,
            Pulsar4X.Combat.WeaponNature.Exotic => VsExotic,
            _ => 1.0,
        };

        public override object Clone() => new GroundArmorAtb(Mass, HP, Defense, VsKinetic, VsEnergy, VsExplosive, VsExotic);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Ground Armour";
        public string AtbDescription()
            => $"Plating — +{HP:0} HP, +{Defense:0} defence, mass {Mass:0} (the frame must bear it).";
    }
}
