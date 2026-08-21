using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;    // SustenanceProcessor, ColonySustenanceDB, ColonyInfoDB
using Pulsar4X.Energy;      // EnergyGenAbilityDB

namespace Pulsar4X.Tests
{
    /// <summary>
    /// POWER→MORALE demand on-switch (the power twin of <see cref="FoodDemandTests"/>). The morale WIRE itself
    /// (<c>ColonySustenanceDB.PowerShortage</c> → <c>MoraleInputs.PowerShortage</c>, <c>MaxPowerShortagePenalty</c> 30) was
    /// already built + CI-green; this proves the DEMAND on-switch keeps the start colony SAFE: Earth's fission reactor
    /// (~75,000 kW) covers its population's power demand (~8,200 kW at the default coefficient) → power-POSITIVE, zero
    /// shortage — while a powerless colony would brown out (the mechanism has teeth / the grave rung). Flag OFF (the engine
    /// default) → demand 0 → shortage 0, so the suite is byte-identical.
    /// </summary>
    [TestFixture]
    public class PowerDemandTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[power] " + m);

        // The flag is a process-global static — always restore it so it can't leak into the rest of the shard.
        [TearDown]
        public void ResetFlag() => SustenanceProcessor.EnablePowerDemand = false;

        [Test]
        [Description("THE SAFETY GAUGE: with power demand ON, the start colony's generation (fission reactor ~75,000 kW) covers its population's power demand (~8,200 kW at the default coefficient) → power-POSITIVE, zero shortage. A powerless colony at the same demand fully browns out (the mechanism has teeth). With the flag OFF, demand is 0 → shortage 0 (byte-identical).")]
        public void PowerDemand_StartColony_IsPowerPositive_FlagGated_AndHasTeeth()
        {
            var s = TestScenario.CreateWithColony();
            var colony = s.Colony;
            var sust = colony.GetDataBlob<ColonySustenanceDB>();

            long pop = colony.GetDataBlob<ColonyInfoDB>().Population.Values.Sum();
            double supply = colony.TryGetDataBlob<EnergyGenAbilityDB>(out var egen) ? egen.TotalOutputMax : 0.0;
            double demand = pop * SustenanceProcessor.DefaultPerCapitaPowerDemand;
            Log($"start colony power: generation {supply:N0} kW vs demand {demand:N0} kW " +
                $"(pop {pop:N0} × {SustenanceProcessor.DefaultPerCapitaPowerDemand:0.0e-0}) → margin {(demand > 0 ? supply / demand : 0):0.00}×");

            Assert.That(supply, Is.GreaterThan(0.0), "the start colony installs a fission reactor → real power generation");
            Assert.That(supply, Is.GreaterThanOrEqualTo(demand),
                "the start colony's generation covers its population's power demand → power-positive, menu-game safe");

            // (1) Flag OFF → demand 0 → shortage 0, whatever the population is (the byte-identical default).
            SustenanceProcessor.EnablePowerDemand = false;
            SustenanceProcessor.Recalc(colony);
            Assert.That(sust.PowerShortage, Is.EqualTo(0.0).Within(1e-9), "flag off → no power demand → no shortage");

            // (2) Flag ON, generation covers demand → still zero shortage (no brownout on the real start colony).
            SustenanceProcessor.EnablePowerDemand = true;
            SustenanceProcessor.Recalc(colony);
            Assert.That(sust.PowerShortage, Is.EqualTo(0.0).Within(1e-9),
                "flag on + generation ≥ demand → power-positive → zero shortage on the real start colony");

            // (3) THE TEETH / grave rung: the demand is REAL — a powerless colony at this same population fully browns out
            //     (Shortage clamps to 1.0 when supply is 0). So losing generation (bombardment) sours morale.
            double powerlessShortage = ColonySustenanceDB.Shortage(demand, 0.0);
            Assert.That(powerlessShortage, Is.EqualTo(1.0).Within(1e-9),
                "a powerless colony at the start population fully browns out → the demand has teeth (grave rung)");
        }

        [Test]
        [Description("The default coefficient only fills a colony that hasn't authored its own per-capita power demand — a colony with its own value keeps it. And with the flag off, the default is never applied.")]
        public void PowerDemand_DefaultOnlyFillsAnUnsetColony()
        {
            var s = TestScenario.CreateWithColony();
            var sust = s.Colony.GetDataBlob<ColonySustenanceDB>();

            // A colony that authors its own power demand (via the public SetDemand) is NOT overridden by the default.
            sust.SetDemand(5.0e-7, 0.0);
            SustenanceProcessor.EnablePowerDemand = true;
            SustenanceProcessor.Recalc(s.Colony);
            Assert.That(sust.PerCapitaPowerDemand, Is.EqualTo(5.0e-7).Within(1e-12),
                "an authored per-capita power demand is preserved — the default only fills an unset (0) colony");
        }
    }
}
