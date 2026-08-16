using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// A component design attribute: MEDICAL — the HEALTH side of the civic loop (the Medical option of the Civic door,
    /// docs/assembler/01-IO-civic.md §A.1/§B, whose effect reads "+N health"). A colony builds hospitals carrying this
    /// attribute; their combined <see cref="HealthRating"/> is a POSITIVE morale term in
    /// <see cref="ColonyMoraleDB.ComputeMorale"/> — good care makes people content, the offset a harsh-world colony
    /// needs (a sibling of the food-QUALITY bonus, and a NEW consumer: health is not one of the original six morale
    /// inputs).
    ///
    /// One dial:
    ///  • <see cref="HealthRating"/> — the care strength this installation provides. Summed across the colony's
    ///    hospitals (health-scaled) and added to morale, capped by <see cref="ColonyMoraleDB.MaxHealthBonus"/>, so
    ///    building hospitals lifts a struggling colony's morale. A bombarded hospital provides less care (the grave
    ///    rung); destroying it drops the total and morale falls.
    ///
    /// Wired behind <see cref="PopulationProcessor.EnableMedicalMorale"/> (default OFF → byte-identical), so the
    /// existing suite is unchanged until a menu game / test opts in — the sibling of the security/food/employment civic
    /// terms. Host-agnostic (colony or station). Summed on demand, so install/uninstall need no bookkeeping (the
    /// <see cref="FoodProductionAtbDB"/> pattern). Cradle-to-grave: designed in the component designer (Civic ▸ Medical)
    /// → built from materials at a colony → lifts morale → destroyed (bombardment) drops it.
    /// </summary>
    public class MedicalAtbDB : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>The care strength this installation provides (morale points, before the colony-wide cap).
        /// Summed across the colony's hospitals, health-scaled, into the morale health term.</summary>
        [JsonProperty] public double HealthRating { get; internal set; }

        public MedicalAtbDB() { }

        public MedicalAtbDB(double healthRating)
        {
            HealthRating = healthRating < 0 ? 0 : healthRating;
        }

        public override object Clone() => new MedicalAtbDB(HealthRating);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance component) { }

        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Medical";

        public string AtbDescription() => "Provides health care — lifts a colony's morale (better care, happier people).";
    }
}
