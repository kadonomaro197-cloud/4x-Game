using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;    // SustenanceProcessor, ColonySustenanceDB, ColonyInfoDB
using Pulsar4X.Datablobs;   // ComponentInstancesDB
using Pulsar4X.Extensions;  // GetTotalFoodOutput

namespace Pulsar4X.Tests
{
    /// <summary>
    /// TIER 2.5 gauge — the colony FOOD-demand on-switch (developer ruling 2026-08-16, "for 2 [Food] do your rec";
    /// campaign log C-FOOD-DEMAND / C-FOOD make-live). Proves that when the flag is ON, a colony's people EAT (demand =
    /// pop × per-capita), and that the make-live slice keeps the start colony SAFE by construction: Earth now installs 4
    /// agri-complexes (20,000 food/day) against its ~8,200/day demand, so it is food-POSITIVE and reads zero shortage —
    /// while a farmless colony would starve (the mechanism has teeth / the grave rung). With the flag OFF (the engine
    /// default) demand is 0 → shortage 0, so the suite is byte-identical.
    /// </summary>
    [TestFixture]
    public class FoodDemandTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[food] " + m);

        // The flag is a process-global static — always restore it so it can't leak into the rest of the shard.
        [TearDown]
        public void ResetFlag() => SustenanceProcessor.EnableFoodDemand = false;

        [Test]
        [Description("THE MAKE-LIVE GAUGE: with food demand ON, the start colony's farms (4 agri-complexes = 20,000/day) cover its population's food demand (~8,200/day at the default coefficient) → food-POSITIVE, zero shortage. A farmless colony at the same demand fully starves (the mechanism has teeth). With the flag OFF, demand is 0 → shortage 0 (byte-identical).")]
        public void FoodDemand_StartColony_IsFoodPositive_FlagGated_AndHasTeeth()
        {
            var s = TestScenario.CreateWithColony();
            var colony = s.Colony;
            var sust = colony.GetDataBlob<ColonySustenanceDB>();

            long pop = colony.GetDataBlob<ColonyInfoDB>().Population.Values.Sum();
            double farmOutput = colony.GetDataBlob<ComponentInstancesDB>().GetTotalFoodOutput();
            double demand = pop * SustenanceProcessor.DefaultPerCapitaFoodDemand;
            Log($"start colony food: farms {farmOutput:N0}/day vs demand {demand:N0}/day " +
                $"(pop {pop:N0} × {SustenanceProcessor.DefaultPerCapitaFoodDemand:0.0e-0}) → margin {(demand > 0 ? farmOutput / demand : 0):0.00}×");

            Assert.That(farmOutput, Is.GreaterThan(0.0), "the start colony now installs agri-complexes → real food output");
            Assert.That(farmOutput, Is.GreaterThanOrEqualTo(demand),
                "the start colony's farms cover its population's food demand → food-positive, menu-game safe");

            // (1) Flag OFF → demand 0 → shortage 0, whatever the population is (the byte-identical default).
            SustenanceProcessor.EnableFoodDemand = false;
            SustenanceProcessor.Recalc(colony);
            Assert.That(sust.FoodShortage, Is.EqualTo(0.0).Within(1e-9), "flag off → no food demand → no shortage");

            // (2) Flag ON, farms cover demand → still zero shortage (nobody starves on the real start colony).
            SustenanceProcessor.EnableFoodDemand = true;
            SustenanceProcessor.Recalc(colony);
            Assert.That(sust.FoodShortage, Is.EqualTo(0.0).Within(1e-9),
                "flag on + farms ≥ demand → food-positive → zero shortage on the real start colony");

            // (3) THE TEETH / grave rung: the demand is REAL — a farmless colony at this same population would fully
            //     starve (Shortage clamps to 1.0 when supply is 0). So losing the farms (bombardment) brings back famine.
            double farmlessShortage = ColonySustenanceDB.Shortage(demand, 0.0);
            Assert.That(farmlessShortage, Is.EqualTo(1.0).Within(1e-9),
                "a farmless colony at the start population fully starves → the demand has teeth (grave rung)");
        }

        [Test]
        [Description("The default coefficient only fills a colony that hasn't authored its own per-capita food demand — a colony with its own value keeps it (a DevTest strain node stays honored). And with the flag off, the default is never applied.")]
        public void FoodDemand_DefaultOnlyFillsAnUnsetColony()
        {
            var s = TestScenario.CreateWithColony();
            var sust = s.Colony.GetDataBlob<ColonySustenanceDB>();

            // A colony that authors its own demand (via the public SetDemand) is NOT overridden by the default.
            sust.SetDemand(0.0, 5.0e-7);
            long pop = s.Colony.GetDataBlob<ColonyInfoDB>().Population.Values.Sum();
            SustenanceProcessor.EnableFoodDemand = true;
            SustenanceProcessor.Recalc(s.Colony);
            // With farms covering the (smaller) authored demand too, shortage stays 0 — the point is that SetDemand's
            // value is honored (the flag's default only fills a 0), which the sust blob now carries.
            Assert.That(sust.PerCapitaFoodDemand, Is.EqualTo(5.0e-7).Within(1e-12),
                "an authored per-capita food demand is preserved — the default only fills an unset (0) colony");
            Assert.That(pop, Is.GreaterThan(0L));
        }
    }
}
