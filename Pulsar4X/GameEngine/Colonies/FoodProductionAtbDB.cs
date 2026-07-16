using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// A component design attribute: FOOD PRODUCTION — the supply side of the sustenance loop (M5c,
    /// docs/society/MORALE-AND-POPULATION-DESIGN.md). Before this, a colony's food supply was hardcoded to 0, so ANY
    /// food demand was an unwinnable 100% shortage → a permanent −40 morale floor (the DevTest hostile-world factions).
    /// Now a colony BUILDS food buildings (agri-domes, hydroponics) carrying this attribute, and they feed
    /// <see cref="SustenanceProcessor"/>'s food supply.
    ///
    /// Two dials, both morale-relevant:
    ///  • <see cref="FoodOutput"/> — how many food units/day this installation makes. Enough total output to cover the
    ///    colony's demand ends the food SHORTAGE (removes the −40 starvation penalty).
    ///  • <see cref="FoodQuality"/> — the "how GOOD is the food" tier on top of "is there enough." At the 1.0 baseline
    ///    it's plain sustenance (no bonus, just no starvation); dialled UP it is an active morale BONUS (people are
    ///    happier when they eat well — the offset a harsh-world colony needs). Quality is EXPONENTIALLY expensive in the
    ///    template's cost formulas, so a gourmet arcology is a real economic commitment, not a free win.
    ///
    /// Total food on a host = sum of <see cref="FoodOutput"/> across installed components; the colony's average quality
    /// is the output-weighted mean (see ComponentInstancesDBExtensions.GetTotalFoodOutput / GetAverageFoodQuality).
    /// Host-agnostic (colony or station). Summed on demand, so install/uninstall need no bookkeeping (the HousingAtbDB
    /// pattern). Cradle-to-grave: designed in the component designer (Civic ▸ Development door) → built from materials at
    /// a colony → produces food + lifts morale → destroyed (bombardment) drops the supply and morale falls.
    /// </summary>
    public class FoodProductionAtbDB : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>Food units produced per day by this installation. Summed across the colony vs the food demand.</summary>
        [JsonProperty] public double FoodOutput { get; internal set; }

        /// <summary>Food quality (≈0.5 subsistence … 3.0 gourmet; 1.0 = adequate baseline). Above 1.0 it is a morale
        /// BONUS; exponentially expensive to build (the template's cost formulas cube it).</summary>
        [JsonProperty] public double FoodQuality { get; internal set; }

        public FoodProductionAtbDB() { }

        public FoodProductionAtbDB(double foodOutput, double foodQuality)
        {
            FoodOutput = foodOutput < 0 ? 0 : foodOutput;
            FoodQuality = foodQuality < 0 ? 0 : foodQuality;
        }

        public override object Clone() => new FoodProductionAtbDB(FoodOutput, FoodQuality);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance component) { }

        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Food Production";

        public string AtbDescription() => "Produces food (ends starvation) at a dialled quality (better food = higher morale).";
    }
}
