using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Factories;   // ComponentDesignFromJson
using Pulsar4X.Factions;
using Pulsar4X.Components;          // ComponentDesign, ComponentInstance
using Pulsar4X.DataStructures;      // ComponentMountType (enum)
using Pulsar4X.Datablobs;          // ComponentInstancesDB (namespace ≠ folder)
using Pulsar4X.Colonies;
using Pulsar4X.Modding;
using Pulsar4X.Extensions;         // GetTotalFoodOutput / GetAverageFoodQuality

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The FOOD-PRODUCTION supply side (M5c, docs/society/MORALE-AND-POPULATION-DESIGN.md) — the cradle-to-grave gauge for
    /// the new food door. Before this, a colony's food supply was hardcoded 0, so ANY food demand was an unwinnable 100%
    /// shortage → a permanent −40 morale floor on the hostile-world factions (Mars/Venus). Now a colony BUILDS food
    /// buildings (agri-dome / hydroponics-arcology) carrying a <see cref="FoodProductionAtbDB"/>, which:
    ///   • feed <see cref="SustenanceProcessor"/>'s food SUPPLY (enough output ends the shortage), and
    ///   • lift morale by their dialled food QUALITY (the offset a harsh world needs).
    ///
    /// Three layers, weakest-coupling first: the pure atb + morale math (always runs), then the gotcha-10 JSON→atb
    /// binding sensor (the base-mod `food-production` template through the REAL data path — the <see cref="RailgunWeaponTests"/>
    /// equivalent), then the supply END-TO-END on a real colony. Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class FoodProductionTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[food] " + m);

        [Test]
        [Description("The atb clamps negative dials at 0 and Clone preserves both dials (save/load + move-between-managers safety).")]
        public void FoodProductionAtb_ClampsNegatives_AndClones()
        {
            var atb = new FoodProductionAtbDB(-5, -2);
            Assert.That(atb.FoodOutput, Is.EqualTo(0.0), "negative output floors at 0");
            Assert.That(atb.FoodQuality, Is.EqualTo(0.0), "negative quality floors at 0");

            var good = new FoodProductionAtbDB(5000, 2.5);
            var clone = (FoodProductionAtbDB)good.Clone();
            Assert.That(clone.FoodOutput, Is.EqualTo(5000));
            Assert.That(clone.FoodQuality, Is.EqualTo(2.5).Within(1e-9));
        }

        [Test]
        [Description("Food QUALITY above the 1.0 baseline is a morale BONUS (the harsh-world offset), capped, and scaled to 0 as the food SHORTAGE rises — a starving colony gets no bonus however fancy the recipe.")]
        public void Morale_FoodQuality_LiftsAboveBaseline_CappedAndShortageScaled()
        {
            // baseline food (quality 1.0) → no bonus, no penalty (just "not starving")
            var f0 = new System.Collections.Generic.Dictionary<string, double>();
            double baseline = ColonyMoraleDB.ComputeMorale(new MoraleInputs { EmploymentRatio = -1, FoodQuality = 1.0 }, f0);
            Assert.That(f0["food quality"], Is.EqualTo(0.0), "quality at the baseline gives no bonus");

            // quality 2.5 → +30 bonus ((2.5-1.0)*20), well under the 40 cap, and it lifts total morale above neutral
            var f1 = new System.Collections.Generic.Dictionary<string, double>();
            double gourmet = ColonyMoraleDB.ComputeMorale(new MoraleInputs { EmploymentRatio = -1, FoodQuality = 2.5 }, f1);
            Assert.That(f1["food quality"], Is.EqualTo(30.0).Within(1e-9), "quality 2.5 → +30 morale");
            Assert.That(gourmet, Is.GreaterThan(baseline), "good food lifts morale above the baseline case");
            Log($"quality 1.0 → {baseline:0}, quality 2.5 → {gourmet:0} (bonus {f1["food quality"]:0})");

            // quality high enough to exceed the cap is held at MaxFoodQualityBonus
            var f2 = new System.Collections.Generic.Dictionary<string, double>();
            ColonyMoraleDB.ComputeMorale(new MoraleInputs { EmploymentRatio = -1, FoodQuality = 100.0 }, f2);
            Assert.That(f2["food quality"], Is.EqualTo(ColonyMoraleDB.MaxFoodQualityBonus), "the quality bonus is capped");

            // a total food shortage cancels the quality bonus (you can't be gourmet AND starving)
            var f3 = new System.Collections.Generic.Dictionary<string, double>();
            ColonyMoraleDB.ComputeMorale(new MoraleInputs { EmploymentRatio = -1, FoodQuality = 2.5, FoodShortage = 1.0 }, f3);
            Assert.That(f3["food quality"], Is.EqualTo(0.0), "a starving colony gets no quality bonus");
        }

        [Test]
        [Description("gotcha-10 JSON→atb sensor: the base-mod food-production template + the two food designs load through the REAL data path (template → NCalc → FoodProductionAtbDB via reflection) with the authored output/quality dials, and mount as a PlanetInstallation so building one grows the colony's food supply.")]
        public void BaseModFoodDesigns_BindFoodProductionAtb_FromJson()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction;
            var fData = faction.GetDataBlob<FactionInfoDB>().Data;
            fData.Unlock("food-production");   // the DevTest/NPC scenarios unlock this via StartingItems; the default start doesn't

            // Pull the design blueprints from a fresh base-mod load (same data the game reads).
            var baseMod = new ModDataStore();
            new ModLoader().LoadModManifest("Data/basemod/modInfo.json", baseMod);

            foreach (var (id, expectOut, expectQual) in new[]
            {
                ("default-design-agri-complex",         5000.0, 1.0),
                ("default-design-hydroponics-arcology",  5000.0, 2.5),
            })
            {
                Assert.That(baseMod.ComponentDesigns.ContainsKey(id), Is.True, $"{id} is a defined base-mod design");
                var design = ComponentDesignFromJson.Create(faction, fData, baseMod.ComponentDesigns[id]);

                Assert.That(design.HasAttribute<FoodProductionAtbDB>(), Is.True,
                    $"{id}: the JSON food-atb bound a FoodProductionAtbDB (gotcha-10 template→atb path works)");
                var atb = design.GetAttribute<FoodProductionAtbDB>();
                Log($"{id}: output={atb.FoodOutput:0}/day quality={atb.FoodQuality:0.0} mount={design.ComponentMountType}");

                Assert.That(atb.FoodOutput, Is.EqualTo(expectOut).Within(1), $"{id}: food output dial bound from JSON");
                Assert.That(atb.FoodQuality, Is.EqualTo(expectQual).Within(0.01), $"{id}: food quality dial bound from JSON");
                Assert.That(design.ComponentMountType.HasFlag(ComponentMountType.PlanetInstallation), Is.True,
                    $"{id}: mounts as a PlanetInstallation — buildable on a colony");
            }
        }

        [Test]
        [Description("Supply END-TO-END: installing a real base-mod hydroponics-arcology on a colony reports its food output + quality through the ComponentInstancesDB extensions, and (via SustenanceProcessor) that supply ENDS a food shortage the same demand would otherwise leave at 100% — the cradle-to-grave payoff, built → installed → feeds the colony.")]
        public void InstalledArcology_EndsFoodShortage_AndReportsQuality()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction;
            var fData = faction.GetDataBlob<FactionInfoDB>().Data;
            fData.Unlock("food-production");

            var baseMod = new ModDataStore();
            new ModLoader().LoadModManifest("Data/basemod/modInfo.json", baseMod);
            var design = ComponentDesignFromJson.Create(faction, fData, baseMod.ComponentDesigns["default-design-hydroponics-arcology"]);

            var comps = s.Colony.GetDataBlob<ComponentInstancesDB>();
            var sust = s.Colony.GetDataBlob<ColonySustenanceDB>();
            long pop = s.Colony.GetDataBlob<ColonyInfoDB>().Population.Values.Sum();

            // Set a food demand well UNDER the arcology's 5000/day output (per-capita sized to the colony's real pop),
            // so a working supply must be what clears it. First prove the demand-with-no-supply case is a total shortage.
            double perCapita = 1000.0 / pop;   // demand ≈ 1000/day
            sust.SetDemand(0, perCapita);
            SustenanceProcessor.Recalc(s.Colony);
            Assert.That(sust.FoodShortage, Is.EqualTo(1.0), "demand with no food building yet = total shortage (the old permanent floor)");

            s.Colony.AddComponent(new ComponentInstance(design));

            Assert.That(comps.GetTotalFoodOutput(), Is.EqualTo(5000.0).Within(1),
                "the installed arcology reports its 5000/day food output (health 100%)");
            Assert.That(comps.GetAverageFoodQuality(), Is.EqualTo(2.5).Within(0.01),
                "and its gourmet quality (the morale-lifting dial)");

            SustenanceProcessor.Recalc(s.Colony);
            Assert.That(sust.FoodShortage, Is.EqualTo(0.0),
                "the installed arcology covers the demand — the shortage (and its −40 morale floor) is gone");
            Log($"pop {pop:N0}, demand ≈1000/day, supply {comps.GetTotalFoodOutput():0}/day → shortage {sust.FoodShortage:0.00}");
        }

        [Test]
        [Description("INTEGRATION through the REAL monthly processors (not the pieces in isolation): a colony with a food demand and no farm develops a food-shortage morale penalty after the clock advances (SustenanceProcessor → PopulationProcessor → ColonyMoraleDB, the way the game runs it); installing an arcology and advancing again CLEARS the penalty and ADDS the quality bonus, so morale measurably rises. Exercises the whole supply→sustenance→morale pipeline end to end.")]
        public void FoodPipeline_EndToEnd_MoraleTracksFoodOverTime()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction;
            var fData = faction.GetDataBlob<FactionInfoDB>().Data;
            fData.Unlock("food-production");
            var baseMod = new ModDataStore();
            new ModLoader().LoadModManifest("Data/basemod/modInfo.json", baseMod);
            var arcology = ComponentDesignFromJson.Create(faction, fData, baseMod.ComponentDesigns["default-design-hydroponics-arcology"]);

            var sust = s.Colony.GetDataBlob<ColonySustenanceDB>();
            var morale = s.Colony.GetDataBlob<ColonyMoraleDB>();
            long pop = s.Colony.GetDataBlob<ColonyInfoDB>().Population.Values.Sum();

            // A food demand comfortably under one arcology's 5000/day output, so SUPPLY (not luck) decides the shortage.
            sust.SetDemand(0, 2000.0 / pop);   // demand ≈ 2000/day

            // Starving: advance the real clock so the monthly SustenanceProcessor + PopulationProcessor actually run.
            s.AdvanceTime(TimeSpan.FromDays(65));
            double starvingMorale = morale.Morale;
            Assert.That(morale.Factors.TryGetValue("food", out var foodPenalty) && foodPenalty < 0.0, Is.True,
                "with demand and no farm, the live monthly pipeline puts a negative food (starvation) factor on morale");
            Log($"starving: morale {starvingMorale:0}, food factor {foodPenalty:0}");

            // Feed them: install the arcology, advance again — the pipeline should clear the shortage AND add a quality bonus.
            s.Colony.AddComponent(new ComponentInstance(arcology));
            s.AdvanceTime(TimeSpan.FromDays(65));
            double fedMorale = morale.Morale;
            Assert.That(sust.FoodShortage, Is.EqualTo(0.0), "the arcology's output cleared the shortage in the live pipeline");
            Assert.That(morale.Factors["food"], Is.EqualTo(0.0), "no starvation penalty once fed");
            Assert.That(morale.Factors.TryGetValue("food quality", out var qb) && qb > 0.0, Is.True,
                "and the arcology's quality 2.5 adds a positive morale bonus");
            Assert.That(fedMorale, Is.GreaterThan(starvingMorale),
                "net: feeding the colony measurably raised morale through the REAL processors");
            Log($"fed: morale {fedMorale:0}, quality bonus {qb:0} (Δmorale +{fedMorale - starvingMorale:0})");
        }

        [Test]
        [Description("The GRAVE rung: once an installed arcology is feeding the colony, DESTROYING it (what an orbital bombardment does — removes the component instance) drops food supply to 0 and the shortage returns. Proves food is a real, LOSABLE capability — starve an enemy by bombing their farms — not a permanent grant.")]
        public void FoodProduction_GraveRung_DestroyingTheFarmReturnsStarvation()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction;
            var fData = faction.GetDataBlob<FactionInfoDB>().Data;
            fData.Unlock("food-production");
            var baseMod = new ModDataStore();
            new ModLoader().LoadModManifest("Data/basemod/modInfo.json", baseMod);
            var arcology = ComponentDesignFromJson.Create(faction, fData, baseMod.ComponentDesigns["default-design-hydroponics-arcology"]);

            var comps = s.Colony.GetDataBlob<ComponentInstancesDB>();
            var sust = s.Colony.GetDataBlob<ColonySustenanceDB>();
            long pop = s.Colony.GetDataBlob<ColonyInfoDB>().Population.Values.Sum();
            sust.SetDemand(0, 2000.0 / pop);

            var instance = new ComponentInstance(arcology);
            s.Colony.AddComponent(instance);
            SustenanceProcessor.Recalc(s.Colony);
            Assert.That(sust.FoodShortage, Is.EqualTo(0.0), "fed while the farm stands");

            // Destroy the farm — the exact removal an orbital-bombardment installation-kill performs.
            comps.RemoveComponentInstance(instance);
            Assert.That(comps.GetTotalFoodOutput(), Is.EqualTo(0.0), "no food output once the farm is gone");
            SustenanceProcessor.Recalc(s.Colony);
            Assert.That(sust.FoodShortage, Is.EqualTo(1.0),
                "destroying the farm returns the colony to total food shortage — food is a losable capability");
        }
    }
}
