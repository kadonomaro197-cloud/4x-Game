using NUnit.Framework;
using Pulsar4X.Components.Designers;
using Pulsar4X.Energy;
using Pulsar4X.Sensors;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the POWER door parametric designer, slice-1 gauge.
    ///
    /// Proves the pure <see cref="PowerDesignModel"/> (the engine half of the power-plant form) REPRODUCES every
    /// base-mod power component from its two choices (Job × Source) + dials — i.e. every hand-authored reactor / RTG /
    /// turbine / solar array / battery falls out of the ONE parametric form (the DESIGNER-NORTH-STAR reproduction claim,
    /// made executable). PURE (no colony harness → fast, not the slow CI shard); the reproduction VALUES are the exact
    /// numbers each `energy.json` template's NCalc formulas produce (verified against source + the component-design
    /// overrides), so a drift in the model's per-branch arithmetic fails here. Byte-identical to the live game (nothing
    /// calls the model yet).
    ///
    /// The load-bearing assertions are the produced `*Atb` ctor args (which the sim reads) + the design scalars
    /// (mass/volume/crew), plus two door truths: the FUEL-BURNERS carry a 1700 K sensor signature while SOLAR and the
    /// BATTERY are SILENT, and the battery authored below the template minimum is CLAMPED up (the L7 gotcha — a model
    /// that skips clamping reproduces the wrong store and fails). Tech-dependent branches (solar / battery / RTG) are fed
    /// the LEVEL-0 baseline the model defaults to — byte-identical means "equals the ComponentDesigner output at faction
    /// tech," and level-0 is the faction-start tech. This is the POWER twin of `WeaponsDesignModelTests`.
    /// </summary>
    [TestFixture]
    public class PowerDesignModelTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[power-model] " + m);

        // ---- assertion helpers (mirror WeaponsDesignModelTests' AssertProfile shape) ----

        private static void AssertScalars(string name, PowerProfile p, long mass, double volume, int crew)
        {
            Log($"{name}: produced={p.ProducedAttribute?.Name} mass={p.Mass} vol={p.Volume} crew={p.Crew}");
            Assert.That(p.Mass, Is.EqualTo(mass), $"{name} MassPerUnit (exact long)");
            Assert.That(p.Volume, Is.EqualTo(volume).Within(1e-6), $"{name} VolumePerUnit");
            Assert.That(p.Crew, Is.EqualTo(crew), $"{name} CrewReq (exact int)");
        }

        private static void AssertGeneration(string name, PowerProfile p,
            double fuelUsedAtMax, double powerOutputMax, double lifetime,
            string fuelType = "fissile-fuels", string energyTypeID = "electricity")
        {
            Assert.That(p.ProducedAttribute, Is.EqualTo(typeof(EnergyGenerationAtb)), $"{name} produces EnergyGenerationAtb");
            var g = p.Generation;
            Assert.That(g, Is.Not.Null, $"{name} Generation set");
            Log($"{name}: gen fuel={g.FuelType} used={g.FuelUsedAtMax} type={g.EnergyTypeID} power={g.PowerOutputMax} life={g.Lifetime}");
            Assert.That(g.FuelType, Is.EqualTo(fuelType), $"{name} FuelType");
            Assert.That(g.EnergyTypeID, Is.EqualTo(energyTypeID), $"{name} EnergyTypeID");
            Assert.That(g.FuelUsedAtMax, Is.EqualTo(fuelUsedAtMax).Within(1e-6 * (fuelUsedAtMax == 0 ? 1 : fuelUsedAtMax)), $"{name} FuelUsedAtMax (kg/s)");
            Assert.That(g.PowerOutputMax, Is.EqualTo(powerOutputMax).Within(1e-6 * (powerOutputMax == 0 ? 1 : powerOutputMax)), $"{name} PowerOutputMax (kW)");
            Assert.That(g.Lifetime, Is.EqualTo(lifetime).Within(1e-6 * (lifetime == 0 ? 1 : lifetime)), $"{name} Lifetime (seconds)");
        }

        private static void AssertSignature(string name, PowerProfile p, double magnitude_w)
        {
            var s = p.Signature;
            Assert.That(s, Is.Not.Null, $"{name} carries a SensorSignatureAtb (fuel-burner)");
            Log($"{name}: signature mag={s.PartWaveFormMag} band={s.PartWaveForm.WavelengthMin_nm}/{s.PartWaveForm.WavelengthAverage_nm}/{s.PartWaveForm.WavelengthMax_nm}");
            Assert.That(s.PartWaveFormMag, Is.EqualTo(magnitude_w).Within(1e-6 * (magnitude_w == 0 ? 1 : magnitude_w)), $"{name} signature magnitude (W)");
            // 1700 K blackbody → Wien peak 2898000/1700 ≈ 1704.7 nm, band = (peak-400, peak, peak+600).
            Assert.That(s.PartWaveForm.WavelengthAverage_nm, Is.EqualTo(2898000.0 / 1700.0).Within(1e-6), $"{name} signature peak wavelength (nm)");
        }

        // ---------------------------------------------------------------------------------------------------

        [Test]
        [Description("The two base-mod fission reactors fall out of Generate/Reactor: power is 50·mass·OvE, fuel scales with the Output-vs-Economy dial, life is hours→seconds, and each carries a 1700 K signature. The de-rated design is the SAME form with only the OvE dial turned down (half power for a quarter of the fuel).")]
        public void FissionReactors_ReproduceFromTheForm()
        {
            // default-design-fission-reactor: Mass 1500, OvE 1.0, Lifetime 8760 h.
            var reactor = new PowerDesignModel(PowerJob.Generate, PowerSource.Reactor, size: 1500, endurance: 8760, tune: 1.0).Compute();
            AssertGeneration("fission-reactor", reactor, fuelUsedAtMax: 2.34375e-6, powerOutputMax: 75000, lifetime: 31536000);
            AssertSignature("fission-reactor", reactor, magnitude_w: 11250000);
            AssertScalars("fission-reactor", reactor, mass: 1500, volume: 1500, crew: 3);

            // default-design-fission-reactor-derated: same, OvE 0.5 → half power, a quarter of the fuel.
            var derated = new PowerDesignModel(PowerJob.Generate, PowerSource.Reactor, size: 1500, endurance: 8760, tune: 0.5).Compute();
            AssertGeneration("fission-reactor-derated", derated, fuelUsedAtMax: 5.859375e-7, powerOutputMax: 37500, lifetime: 31536000);
            AssertSignature("fission-reactor-derated", derated, magnitude_w: 5625000);
            AssertScalars("fission-reactor-derated", derated, mass: 1500, volume: 1500, crew: 3);
        }

        [Test]
        [Description("The base-mod 48MW Steam Turbine Reactor falls out of Generate/SteamTurbine at the default Core-vs-Generator split (50% core): the core/genny/fuel masses, the fuel burn rate, the generator output and the (design-fixed) fuel duration all mirror the template chain; its signature magnitude is the CORE output, not power×mass.")]
        public void SteamTurbine_ReproducesFromTheForm()
        {
            // default-design-reactor-2t: Mass 2000, Core vs Generator = template default 50.
            var turbine = new PowerDesignModel(PowerJob.Generate, PowerSource.SteamTurbine, size: 2000, tune: 50).Compute();
            AssertGeneration("steam-turbine", turbine, fuelUsedAtMax: 1.5e-6, powerOutputMax: 48000, lifetime: 4e8);
            AssertSignature("steam-turbine", turbine, magnitude_w: 60000);
            AssertScalars("steam-turbine", turbine, mass: 2000, volume: 2.0, crew: 3);
        }

        [Test]
        [Description("The RTG falls out of Generate/RTG at the template defaults (no shipped design): efficiency = conductors+10, tiny fuel-consumption from the operational-lifetime dial, power = fuel·efficiency·consumption, life years→seconds, zero crew. At level-0 conductors 0 → 1.0 kW (the HTML's 1.1 kW used a wrong conductor value).")]
        public void Rtg_ReproducesFromTemplateDefaults()
        {
            // RTG has no shipped design — reproduce the template default (Mass 1000, Operational Lifetime 5 yr).
            var rtg = new PowerDesignModel(PowerJob.Generate, PowerSource.RTG, size: 1000, endurance: 5).Compute();
            AssertGeneration("rtg", rtg, fuelUsedAtMax: 0.0002, powerOutputMax: 1.0, lifetime: 157788000);
            AssertSignature("rtg", rtg, magnitude_w: 100);
            AssertScalars("rtg", rtg, mass: 1000, volume: 1000, crew: 0);
        }

        [Test]
        [Description("The base-mod solar panel falls out of Collect: mass = area·panel-density (20 kg at level-0 density 0.2, NOT the HTML's 200), best efficiency = panel-eff·(half-bandwidth/bandwidth) = 8.0% at level-0, worst = half that, the waveform centres on the tuned wavelength ±half-bandwidth — and solar is the ONLY silent source (no signature).")]
        public void SolarArray_ReproducesFromTheForm_AndIsSilent()
        {
            // default-design_solarpanel: Area 100, Ideal Absorption Wavelength 479, Bandwidth 150 (within level-0 [100,200]).
            var solar = new PowerDesignModel(PowerJob.Collect, size: 100, peakWavelength: 479, tune: 150).Compute();
            Assert.That(solar.ProducedAttribute, Is.EqualTo(typeof(EnergySolarGenerationAtb)), "solar produces EnergySolarGenerationAtb");
            var a = solar.Solar;
            Assert.That(a, Is.Not.Null, "solar atb set");
            Log($"solar: best={a.BestEfficiency} worst={a.WorstEfficiency} area={a.Area_m2} wave={a.AbsorptionWaveformCapability.WavelengthMin_nm}/{a.AbsorptionWaveformCapability.WavelengthAverage_nm}/{a.AbsorptionWaveformCapability.WavelengthMax_nm}");
            Assert.That(a.BestEfficiency, Is.EqualTo(8.0).Within(1e-6), "solar BestEfficiency (%)");
            Assert.That(a.WorstEfficiency, Is.EqualTo(4.0).Within(1e-6), "solar WorstEfficiency (%)");
            Assert.That(a.Area_m2, Is.EqualTo(100).Within(1e-6), "solar Area_m2");
            Assert.That(a.EnergyTypeID, Is.EqualTo("electricity"), "solar EnergyTypeID");
            // waveform = EMWaveForm(peak - bw*0.5, peak, peak + bw*0.5) = (404, 479, 554).
            Assert.That(a.AbsorptionWaveformCapability.WavelengthMin_nm, Is.EqualTo(404).Within(1e-6), "solar waveform min");
            Assert.That(a.AbsorptionWaveformCapability.WavelengthAverage_nm, Is.EqualTo(479).Within(1e-6), "solar waveform avg");
            Assert.That(a.AbsorptionWaveformCapability.WavelengthMax_nm, Is.EqualTo(554).Within(1e-6), "solar waveform max");
            Assert.That(solar.Signature, Is.Null, "solar is the ONLY silent source (no SensorSignatureAtb)");

            AssertScalars("solar", solar, mass: 20, volume: 0.01, crew: 0);
        }

        [Test]
        [Description("The two base-mod battery banks fall out of Store: capacity = mass·500·battery-tech (1.0 at level-0). The 2t bank is direct; the '25kg' bank is the CLAMP CASE — 25 kg is below the template minimum 1000, so it is silently built at 1000 kg / 500,000 kJ (the L7 gotcha; the name lies). A battery is a tank, not a generator — no signature.")]
        public void BatteryBanks_ReproduceFromTheForm_IncludingTheClamp()
        {
            // default-design-battery-2t: Mass 2000.
            var batt2t = new PowerDesignModel(PowerJob.Store, size: 2000).Compute();
            Assert.That(batt2t.ProducedAttribute, Is.EqualTo(typeof(EnergyStoreAtb)), "battery produces EnergyStoreAtb");
            Assert.That(batt2t.Store, Is.Not.Null, "battery atb set");
            Assert.That(batt2t.Store.EnergyTypeID, Is.EqualTo("electricity"), "battery EnergyTypeID");
            Assert.That(batt2t.Store.MaxStore, Is.EqualTo(1000000).Within(1e-6), "battery-2t MaxStore (kJ)");
            Assert.That(batt2t.Signature, Is.Null, "a battery is silent (no SensorSignatureAtb)");
            AssertScalars("battery-2t", batt2t, mass: 2000, volume: 2000 / 2896.6, crew: 0);

            // default-design-battery-25kg: Mass 25 → CLAMPED to the template minimum 1000.
            var batt25 = new PowerDesignModel(PowerJob.Store, size: 25).Compute();
            Log($"battery-25kg: authored 25 kg → clamped mass={batt25.Mass}, store={batt25.Store.MaxStore}");
            Assert.That(batt25.Mass, Is.EqualTo(1000), "battery-25kg mass CLAMPED up to template minimum 1000");
            Assert.That(batt25.Store.MaxStore, Is.EqualTo(500000).Within(1e-6), "battery-25kg MaxStore reflects the clamped 1000 kg");
            AssertScalars("battery-25kg", batt25, mass: 1000, volume: 1000 / 2896.6, crew: 0);
        }

        [Test]
        [Description("Coverage: the six base-mod (choices, sliders) settings map to six real, distinct designs across the three produced attribute types — the door is not degenerate (every option reaches a different component).")]
        public void SixBaseModSettings_MapToDistinctDesigns()
        {
            var reactor = new PowerDesignModel(PowerJob.Generate, PowerSource.Reactor, size: 1500, endurance: 8760, tune: 1.0).Compute();
            var derated = new PowerDesignModel(PowerJob.Generate, PowerSource.Reactor, size: 1500, endurance: 8760, tune: 0.5).Compute();
            var turbine = new PowerDesignModel(PowerJob.Generate, PowerSource.SteamTurbine, size: 2000, tune: 50).Compute();
            var solar = new PowerDesignModel(PowerJob.Collect, size: 100, peakWavelength: 479, tune: 150).Compute();
            var batt2t = new PowerDesignModel(PowerJob.Store, size: 2000).Compute();
            var batt25 = new PowerDesignModel(PowerJob.Store, size: 25).Compute();

            // Three distinct attribute types are exercised.
            Assert.That(reactor.ProducedAttribute, Is.EqualTo(typeof(EnergyGenerationAtb)));
            Assert.That(solar.ProducedAttribute, Is.EqualTo(typeof(EnergySolarGenerationAtb)));
            Assert.That(batt2t.ProducedAttribute, Is.EqualTo(typeof(EnergyStoreAtb)));

            // The two reactors differ only by the OvE dial → distinct power output.
            Assert.That(reactor.Generation.PowerOutputMax, Is.Not.EqualTo(derated.Generation.PowerOutputMax));
            // The turbine reads a different generator output again.
            Assert.That(turbine.Generation.PowerOutputMax, Is.EqualTo(48000).Within(1e-6));
            // The two batteries differ only because the 25 kg one clamped.
            Assert.That(batt2t.Store.MaxStore, Is.Not.EqualTo(batt25.Store.MaxStore));
        }
    }
}
