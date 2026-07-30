using System;
using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Energy;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// FUEL BECOMES REAL — the developer's three asks, 2026-07-30 (docs/economy/DESIGNER-NORTH-STAR.md §39.7):
    /// <i>"wire the fuel gate and make lifetime longer and shouldn't I be able to set a burn rate?"</i>
    ///
    /// <para><b>1. The gate.</b> Every fuel-burning generator was charged <c>fissile-fuels</c> at build, given a fuel
    /// load, and burned it down every tick — but <c>LocalFuel</c> was <b>never read as a condition anywhere</b>, so it
    /// ran negative and output never stopped. <see cref="EnergyGenProcessor.EnableFuelExhaustion"/> makes a dry
    /// generator produce nothing. Default OFF (it changes behaviour; the client arms it), so CI stays byte-identical.</para>
    ///
    /// <para><b>2. "Longer" was a UNIT BUG, not a balance call.</b> <c>EnergyGenerationAtb</c> consumes its
    /// <c>Lifetime</c> in <b>seconds</b> (<c>LocalFuel = maxUse[kg/s] × Lifetime</c>, drained by
    /// <c>fueluse × t.TotalSeconds</c>). The reactor authored it in <b>hours</b> (8760) and the RTG in <b>years</b> (5),
    /// so their real endurance was <b>2.4 hours</b> and <b>5 seconds</b>. Only the steam turbine was dimensionally
    /// correct — and it is the only one with a shipped design, which is why nobody had noticed. Both now convert
    /// explicitly, which makes the reactor's life <b>3600× longer without changing a single authored number.</b></para>
    ///
    /// <para><b>3. The burn rate is now a dial.</b> <c>Output vs Economy</c> (0.5–2.0, default 1) drives the core
    /// harder for more power out of the same mass — and a hard-driven core burns more fuel <b>per kilowatt</b>, so
    /// total burn rises as the <b>square</b>: 2× the power costs 4× the fuel for the same endurance. Its specific rate
    /// at the default is anchored on the steam turbine's measured 3.125e-11 kg/s per kW, the one calibrated fuelled
    /// generator in the game (the old <c>1e-7</c> was ~3200× that, which made a year of fuel cost 236 tonnes).</para>
    /// </summary>
    [TestFixture]
    public class PowerFuelGateTests
    {
        private const string Reactor = "default-design-fission-reactor";
        private const string Derated = "default-design-fission-reactor-derated";
        private const string Turbine = "default-design-reactor-2t";

        private static void Log(string m) => TestContext.Progress.WriteLine("[fuel] " + m);

        private static ComponentDesign Design(TestScenario s, string id)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            Assert.That(designs.ContainsKey(id), Is.True,
                $"base-mod design '{id}' should be built for the start faction");
            return designs[id];
        }

        private static EnergyGenerationAtb Gen(ComponentDesign d)
        {
            Assert.That(d.TryGetAttribute<EnergyGenerationAtb>(out var atb), Is.True,
                $"'{d.Name}' should carry an EnergyGenerationAtb from the JSON template");
            return atb;
        }

        /// <summary>
        /// The unit fix, stated as the thing a player would actually notice: a reactor lasts a YEAR, not an afternoon.
        /// This is an exact assertion (8760 h × 3600) rather than a tuned one, so it pins the conversion itself.
        /// </summary>
        [Test]
        [Description("Lifetime reaches the attribute in SECONDS: the shipped reactor's fuel load is exactly its 8760 authored hours (one game year), where the un-converted value would have been 8760 seconds — 2.4 hours. And the fuel it CARRIES equals the fuel it was CHARGED, so the books balance.")]
        public void Lifetime_ReachesTheAtbInSeconds_SoAReactorLastsAYearNotAnAfternoon()
        {
            var s = TestScenario.CreateWithColony();
            var design = Design(s, Reactor);
            var atb = Gen(design);

            const double authoredHours = 8760;
            double expectedSeconds = authoredHours * 3600;
            Log($"{Reactor}: output {atb.PowerOutputMax:0} kW · burn {atb.FuelUsedAtMax:0.###E+0} kg/s · life {atb.Lifetime:0} s");

            Assert.That(atb.Lifetime, Is.EqualTo(expectedSeconds).Within(1.0),
                "the atb consumes Lifetime as SECONDS — an hours value handed over raw is the 3600x bug");

            // Endurance at full load is LocalFuel / maxUse, which is Lifetime by construction. State it in years so
            // the assertion reads as the gameplay claim rather than as arithmetic.
            double enduranceYears = atb.Lifetime / 3600.0 / 8760.0;
            Log($"endurance at full load: {enduranceYears:0.00} years");
            Assert.That(enduranceYears, Is.EqualTo(1.0).Within(0.01),
                "a stock fission reactor should run for a game year, not 2.4 hours");

            // The books balance: carried fuel == charged fuel. (ResourceCost = FuelConsumption x Fuel Load Seconds.)
            double carried = atb.FuelUsedAtMax * atb.Lifetime;
            Assert.That(design.ResourceCosts.ContainsKey("fissile-fuels"), Is.True,
                "a fuelled reactor must be charged fissile fuel to build");
            double charged = design.ResourceCosts["fissile-fuels"];
            Log($"carried {carried:0.#} kg vs charged {charged:0.#} kg");
            // ResourceCosts is a long, so the charged figure is the carried one rounded.
            Assert.That(carried, Is.EqualTo(charged).Within(1.5),
                "the fuel a reactor carries must be the fuel you paid for");
        }

        /// <summary>
        /// The developer's third ask. The dial has to COST something or it is a ladder (§34.7a): more output per kg is
        /// paid for in fuel per kilowatt, so the total burn rises as the square. Asserted as exact ratios so it cannot
        /// drift.
        /// </summary>
        [Test]
        [Description("Output vs Economy is a real trade: the de-rated reactor makes exactly half the power of the stock one on the same mass, and burns exactly a QUARTER of the fuel — so driving a core harder costs more fuel for every kilowatt it makes, not just more fuel.")]
        public void OutputVsEconomy_BuysPowerWithFuelPerKilowatt()
        {
            var s = TestScenario.CreateWithColony();
            var stock = Gen(Design(s, Reactor));
            var derated = Gen(Design(s, Derated));

            Log($"stock   : {stock.PowerOutputMax:0} kW on {stock.FuelUsedAtMax:0.###E+0} kg/s");
            Log($"de-rated: {derated.PowerOutputMax:0} kW on {derated.FuelUsedAtMax:0.###E+0} kg/s");

            // Same mass, half the dial → half the power.
            Assert.That(derated.PowerOutputMax, Is.EqualTo(stock.PowerOutputMax / 2.0).Within(1.0),
                "output scales with the dial");

            // …and a QUARTER of the burn, because fuel-per-kilowatt scales with the dial too.
            Assert.That(derated.FuelUsedAtMax, Is.EqualTo(stock.FuelUsedAtMax / 4.0).Within(1e-12),
                "total burn scales as the SQUARE of the dial — that is the cost that makes it a trade");

            // Stated the way it matters: fuel per kilowatt is strictly better on the de-rated core.
            double stockPerKw = stock.FuelUsedAtMax / stock.PowerOutputMax;
            double deratedPerKw = derated.FuelUsedAtMax / derated.PowerOutputMax;
            Log($"fuel per kW — stock {stockPerKw:0.###E+0} · de-rated {deratedPerKw:0.###E+0}");
            Assert.That(deratedPerKw, Is.EqualTo(stockPerKw / 2.0).Within(1e-14),
                "the de-rated core is twice as economical per kilowatt — the whole point of the dial");

            // And the de-rated design is genuinely cheaper to build in fuel, so the trade is visible at the yard too.
            double stockFuel = Design(s, Reactor).ResourceCosts["fissile-fuels"];
            double deratedFuel = Design(s, Derated).ResourceCosts["fissile-fuels"];
            Log($"fissile-fuels to build — stock {stockFuel:0.#} kg · de-rated {deratedFuel:0.#} kg");
            Assert.That(deratedFuel, Is.LessThan(stockFuel),
                "a de-rated reactor should cost less fuel to build, for the same service life");
        }

        /// <summary>
        /// The specific fuel rate is anchored on the one calibrated fuelled generator rather than picked. Asserting the
        /// ANCHOR (not the absolute number) is what makes this robust: if the turbine is ever re-tuned, this test says
        /// so instead of silently drifting.
        /// </summary>
        [Test]
        [Description("The reactor's fuel rate per kilowatt matches the steam turbine's — the only fuelled generator with a shipped design, and therefore the only calibrated one. The old coefficient was ~3200x the turbine's, which made a year of fuel cost 236 tonnes.")]
        public void TheReactorsSpecificFuelRate_IsAnchoredOnTheTurbines()
        {
            var s = TestScenario.CreateWithColony();
            var reactor = Gen(Design(s, Reactor));
            var turbine = Gen(Design(s, Turbine));

            double rPerKw = reactor.FuelUsedAtMax / reactor.PowerOutputMax;
            double tPerKw = turbine.FuelUsedAtMax / turbine.PowerOutputMax;
            Log($"kg/s per kW — reactor {rPerKw:0.####E+0} · turbine {tPerKw:0.####E+0} · ratio {rPerKw / tPerKw:0.###}");

            Assert.That(rPerKw / tPerKw, Is.EqualTo(1.0).Within(0.02),
                "the two fuelled generators should burn at the same specific rate, so choosing between them is about "
              + "output-per-mass and endurance rather than a hidden fuel discrepancy");

            // A year of fuel for a 1500 kg reactor should be tens of kilograms, not hundreds of tonnes.
            double yearOfFuel = reactor.FuelUsedAtMax * reactor.Lifetime;
            Log($"a year of fuel weighs {yearOfFuel:0.#} kg");
            Assert.That(yearOfFuel, Is.LessThan(1000),
                "a reactor's fuel load must be a plausible fraction of its own mass");
        }

        /// <summary>
        /// The gate itself, driven directly — and its byte-identity guard. The flag is reset in a
        /// <c>finally</c> so it can never leak into another fixture (the <c>RequireDetectionToEngage</c> lesson).
        /// </summary>
        [Test]
        [Description("The gate bites: a generator run dry produces nothing with the flag ON, and produces its full output with the flag OFF (byte-identical). LocalFuel also floors at zero instead of running negative.")]
        public void TheFuelGate_StopsADryGenerator_AndIsInertWhenOff()
        {
            var s = TestScenario.CreateWithColony();
            // Install on the COLONY, which is definitely faction-owned — the atb's install reads
            // parentEntity.GetFactionOwner.GetDataBlob<FactionInfoDB>() to resolve the energy cargo type.
            var host = s.Colony;
            Assert.That(host, Is.Not.Null);

            // Drive the atb's install and then the processor directly, so this gauge is about the PROCESSOR rather
            // than about the industry queue (Tests/CLAUDE.md gotcha 7 — drive the mechanism, not the pipeline).
            var gen = Gen(Design(s, Reactor));
            gen.OnComponentInstallation(host, null!);
            Assert.That(host.TryGetDataBlob<EnergyGenAbilityDB>(out var installed), Is.True,
                "installing a reactor should give the host an EnergyGenAbilityDB");
            Assert.That(installed.EnergyType, Is.Not.Null, "the install resolves the energy cargo type");

            Log($"installed capacity {installed.TotalOutputMax:0} kW · fuel load {installed.LocalFuel:0.#} kg");
            Assert.That(installed.TotalOutputMax, Is.GreaterThan(0));
            Assert.That(installed.LocalFuel, Is.GreaterThan(0), "a fresh reactor arrives fuelled");
            Assert.That(installed.IsFuelStarved, Is.False, "…and therefore not starved");

            bool saved = EnergyGenProcessor.EnableFuelExhaustion;
            try
            {
                // Run it dry.
                installed.LocalFuel = 0;
                Assert.That(installed.IsFuelStarved, Is.True, "no fuel and a non-zero burn rate ⇒ starved");

                // FLAG OFF — byte-identical: a dry generator still fills its store, exactly as before.
                EnergyGenProcessor.EnableFuelExhaustion = false;
                installed.EnergyStored[installed.EnergyType.UniqueID] = 0;
                // Pin dateTimeLastProcess so the elapsed span is a known +1 s (a prior tick may already have moved it,
                // and a negative span would REFUND fuel instead of burning it).
                installed.dateTimeLastProcess = host.StarSysDateTime;
                EnergyGenProcessor.EnergyGen(host, host.StarSysDateTime + TimeSpan.FromSeconds(1));
                double storedFlagOff = installed.EnergyStored[installed.EnergyType.UniqueID];
                Log($"flag OFF, dry: stored {storedFlagOff:0.#} kJ");
                Assert.That(storedFlagOff, Is.GreaterThan(0),
                    "with the gate off a dry reactor still generates — the pre-change behaviour, preserved");

                // FLAG ON — the consequence.
                EnergyGenProcessor.EnableFuelExhaustion = true;
                installed.LocalFuel = 0;
                installed.EnergyStored[installed.EnergyType.UniqueID] = 0;
                installed.dateTimeLastProcess = host.StarSysDateTime;
                EnergyGenProcessor.EnergyGen(host, host.StarSysDateTime + TimeSpan.FromSeconds(1));
                double storedFlagOn = installed.EnergyStored[installed.EnergyType.UniqueID];
                Log($"flag ON,  dry: stored {storedFlagOn:0.#} kJ · output {installed.Output:0.#}");
                Assert.That(storedFlagOn, Is.EqualTo(0).Within(1e-9),
                    "a dry reactor must generate NOTHING — this is the consequence that was missing");

                // And fuel never goes negative.
                Assert.That(installed.LocalFuel, Is.GreaterThanOrEqualTo(0),
                    "LocalFuel floors at zero — a tank cannot hold less than nothing");
            }
            finally
            {
                EnergyGenProcessor.EnableFuelExhaustion = saved;
            }
        }
    }
}
