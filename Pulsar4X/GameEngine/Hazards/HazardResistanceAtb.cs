using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Components;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Hazards
{
    /// <summary>
    /// The generic COUNTER to a space hazard: a component that reduces one kind of hazard effect on the ship it's
    /// installed on. Heat/radiation damage is countered by ARMOUR (the wavelength model), so this is for the
    /// non-damage kinds — sensor jamming, drag, warp inhibition (and any future kind). ONE generic attribute
    /// covers them all: a new counter is a new component TEMPLATE (data) that sets which kind it resists, not new
    /// C#. Stacks across installed components and scales by each component's health (a half-wrecked module gives
    /// half its resistance — and a destroyed one gives none, the grave rung).
    ///
    /// Built from JSON via <c>AtbConstrArgs(resistedEffectTypeId, resistanceFraction)</c> — arg order MUST match
    /// that formula in the component template. <c>resistedEffectTypeId</c> is the <see cref="HazardEffectType"/>
    /// enum value (SensorJam = 3, MovementDrag = 4, WarpInhibit = 5 — do NOT reorder the enum).
    /// </summary>
    public class HazardResistanceAtb : IComponentDesignAttribute
    {
        /// <summary>Which hazard effect kind this resists, as the <see cref="HazardEffectType"/> enum value.</summary>
        [JsonProperty] public int ResistedEffectTypeId { get; internal set; }

        /// <summary>How strongly it resists, 0..1 (0 = none, 1 = would fully negate). Stacks; capped in the aggregator.</summary>
        [JsonProperty] public double ResistanceFraction { get; internal set; }

        public HazardEffectType ResistedEffectType => (HazardEffectType)(byte)ResistedEffectTypeId;

        public HazardResistanceAtb() { }

        /// <summary>JSON constructor. Arg order MUST match <c>AtbConstrArgs(...)</c> in the component template.</summary>
        public HazardResistanceAtb(double resistedEffectTypeId, double resistanceFraction)
        {
            ResistedEffectTypeId = (int)resistedEffectTypeId;
            ResistanceFraction = resistanceFraction;
        }

        public HazardResistanceAtb(HazardResistanceAtb db)
        {
            ResistedEffectTypeId = db.ResistedEffectTypeId;
            ResistanceFraction = db.ResistanceFraction;
        }

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Hazard Resistance";
        public string AtbDescription() => "Reduces one kind of space-hazard effect (sensor jamming, drag, or warp inhibition) on this ship.";
    }
}
