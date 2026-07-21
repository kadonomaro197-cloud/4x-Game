using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// A TRAINING CADRE — the ground echo of a ship's <see cref="Pulsar4X.Combat.UnitCaliberAtb"/> elite stamp
    /// (Enhancers ⚙6.2, the "make this unit a veteran" door). A component you design / research / build / mount that
    /// raises the unit's <see cref="GroundUnitDesign.TrainingMultiplier"/>, which <see cref="GroundForces.RaiseUnit"/>
    /// bakes into the unit's Attack AND toughness when it's fielded. So a Space-Marine chapter cadre ≠ a militia levy
    /// built on the SAME frame — the developer's "training reflected in game."
    ///
    /// A COMPONENT (CONVENTIONS §6): designed / researched / built / mounted / lost like any part, so it earns
    /// research-gating, construction-from-materials, save/load, and the design UI for free. The assembler
    /// (<see cref="GroundUnitAssembly.Compute"/>) reads the BEST mounted cadre's multiplier (like the ship combat value
    /// reads the best caliber module). Its COST is real — the base-mod template's Mass scales with the multiplier, so a
    /// more-trained unit is dearer in credits/research/build-time to field (the developer's "realistic cost of a training
    /// variable"). Inert on install (the assembler reads the number; install/uninstall are no-ops). <b>1.0 = green/
    /// untrained → byte-identical</b> (nothing mounts one until a player builds it). Never throws.
    /// </summary>
    public class GroundTrainingAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>The veterancy multiplier this cadre grants (clamped ≥ 1.0; 1.0 = green, 1.5 = a hardened outfit that
        /// hits 50% harder and soaks 50% more). Read at assembly, baked into the unit's stats at raise — never re-read
        /// by the combat resolver.</summary>
        [JsonProperty] public double TrainingMultiplier { get; internal set; } = 1.0;

        public GroundTrainingAtb() { }

        // ONE double arg for the JSON/NCalc binder (gotcha L7): the base-mod template feeds a SINGLE
        // AtbConstrArgs(PropertyValue('TrainingMultiplier')) value — so the ctor takes exactly one, matching arity
        // (the binder is exact-arity — see GroundCombat CLAUDE.md gotcha 6).
        public GroundTrainingAtb(double trainingMultiplier)
        {
            TrainingMultiplier = trainingMultiplier < 1.0 ? 1.0 : trainingMultiplier;
        }

        public override object Clone() => new GroundTrainingAtb(TrainingMultiplier);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Training Cadre";
        public string AtbDescription()
            => $"A veteran cadre — raises this unit above its chassis: attack + toughness ×{TrainingMultiplier:0.00} (baked in when the unit is raised). The higher the training, the dearer the unit is to field.";
    }
}
