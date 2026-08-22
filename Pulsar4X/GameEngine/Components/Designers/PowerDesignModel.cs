using System;
using Pulsar4X.Energy;
using Pulsar4X.Sensors;

namespace Pulsar4X.Components.Designers
{
    /// <summary>CHOICE 1 — the JOB a power component does. It decides WHICH power attribute the design produces
    /// (i.e. which template branch the game builds it from), the way the WEAPONS door's Delivery decides which weapon
    /// physics you get. Generate = burn fuel or split atoms for electricity; Collect = catch sunlight; Store = hold
    /// charge in a battery. This is the top forced fork — you can't be a reactor AND a battery.</summary>
    public enum PowerJob
    {
        /// <summary>Make power from fuel/isotopes → an <see cref="EnergyGenerationAtb"/> (a reactor / RTG / turbine).</summary>
        Generate,
        /// <summary>Catch sunlight → an <see cref="EnergySolarGenerationAtb"/> (a solar array). The only SILENT source.</summary>
        Collect,
        /// <summary>Hold charge → an <see cref="EnergyStoreAtb"/> (a battery bank). Not a generator — a tank for power.</summary>
        Store,
    }

    /// <summary>CHOICE 2 — the SOURCE, only meaningful when the JOB is <see cref="PowerJob.Generate"/>. It picks WHICH
    /// of the three fuel-burning templates you build, and each one is a DIFFERENT arithmetic law turning the size/tune
    /// dials into (power out, fuel burn, service life). A fission Reactor is the densest-per-kilogram warship core; an
    /// RTG is a tiny no-crew isotope block that runs for decades; a Steam-turbine plant is the volume-efficient
    /// station/colony workhorse.</summary>
    public enum PowerSource
    {
        /// <summary>Fission Reactor (`reactor` template) — highest power per kilogram; power is linear in mass × the
        /// Output-vs-Economy dial; not refuelable; the loudest thing on a ship.</summary>
        Reactor,
        /// <summary>RTG (`rtg` template) — an isotope block; tiny output, zero crew, decade-scale life; power is
        /// fuel × efficiency × the (tiny) consumption rate.</summary>
        RTG,
        /// <summary>Steam-turbine Reactor (`steam-turbine-reactor` template) — a big fuelled plant; enormous power per
        /// cubic metre; the Core-vs-Generator split is a MATERIALS trade, its fuel duration is fixed by design.</summary>
        SteamTurbine,
    }

    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the POWER door parametric designer, slice 1 (ENGINE MODEL).
    ///
    /// WHAT IT IS, in plain English: the power-plant design tool (`docs/Actual HTMLs Of designers/powerderived.html`)
    /// replaces the old "pick a component type from a menu" screen with ONE form — you pick a JOB (make / catch / store
    /// power), then, if you're making it, a SOURCE (reactor / RTG / turbine), and slide a few dials, and EVERY power
    /// component in the game falls out of that one form. This class is the ENGINE HALF of that form: a pure calculator
    /// that turns those picks + dials into the exact numbers the game feeds an <see cref="EnergyGenerationAtb"/> /
    /// <see cref="EnergyStoreAtb"/> / <see cref="EnergySolarGenerationAtb"/> — so a design made through this model
    /// behaves IDENTICALLY to the hand-authored base-mod component. The ImGui screen that drives it is a later slice
    /// (client, verified on the developer's machine — CI can compile the client but can't run it).
    ///
    /// WHY IT'S SHAPED THIS WAY (the derivation, per `docs/economy/DESIGNER-NORTH-STAR.md`): a "door" is DERIVED from
    /// the values the simulation actually reads off a power component — the ctor arguments of those three attributes,
    /// plus the design's own mass/volume/crew. Group them by the question each answers; split the ones the physics
    /// FORCES from the ones the player is FREE to set. The FORCED forks become the two CHOICES; the FREE numbers become
    /// the sliders:
    ///   • CHOICE 1 — <see cref="PowerJob"/> (Generate / Collect / Store): which attribute TYPE the design produces.
    ///   • CHOICE 2 — <see cref="PowerSource"/> (Reactor / RTG / SteamTurbine): which generation LAW, only when generating.
    ///   • SLIDER 1 — SIZE (<see cref="Size"/>): kilograms of plant (or square metres of panel for a solar array). Drives
    ///     mass/volume AND the primary output (reactor: 50·mass·tune kW; battery: mass·500·tech kJ; solar: area → panel).
    ///   • SLIDER 2 — ENDURANCE (<see cref="Endurance"/>): how long a fuel-burner runs — HOURS for a reactor, YEARS for an
    ///     RTG (each template authored its life in its own unit; the model keeps that unit and converts to the seconds the
    ///     attribute actually consumes). Hidden for turbine/battery/solar.
    ///   • SLIDER 3 — TUNE (<see cref="Tune"/>): the source's one shaping dial — reactor Output-vs-Economy (buys power with
    ///     fuel), turbine Core-vs-Generator % (a materials split), solar Bandwidth nm (peak efficiency vs versatility).
    ///   • SLIDER 4 — PEAK WAVELENGTH (<see cref="PeakWavelength"/>): solar only — the wavelength the panel is tuned to.
    ///
    /// THE HONEST CAVEAT (the load-bearing finding, flagged for the developer): the model is NOT a pure function of the
    /// choices + sliders alone. Four templates read faction TECH the same way the game does — solar panel density,
    /// efficiency and bandwidth; battery capacity; RTG conductor efficiency — so the SAME dials give a heavier panel or
    /// a bigger battery at a lower tech level. A literal N-choice/M-slider form can't reproduce that, so this model
    /// exposes those tech values as FREE inputs, each defaulting to the LEVEL-0 (game-start) value (the `techs.json`
    /// DataFormula at Level 0). A caller reproducing a real faction's component passes that faction's real tech; the
    /// pure gauge passes the level-0 defaults. Two more honest points: (1) every slider is CLAMPED to the template's
    /// Min/Max before it is used (the L7 template-clamp gotcha — a battery authored at 25 kg is silently built at the
    /// 1000 kg template minimum), so the model clamps too or it can't reproduce that battery; (2) the model faithfully
    /// reproduces the door's KNOWN DEFECTS as-is (the reactor's power is strictly linear in mass, endurance adds fissile
    /// cost but no mass, the install-time kW-into-a-kJ store) — those are the door's open design PROPOSALS, not this
    /// slice's job to fix.
    ///
    /// BYTE-IDENTICAL / SAFE (slice-1 rules): this is a NEW file, a pure value type with no serialized state, no
    /// DataBlob, no `*Atb` ctor change — nothing in the live game calls it yet, so the whole engine is unchanged. It
    /// calls the EXISTING public `*Atb` ctors and mirrors each template's own NCalc arithmetic EXACTLY (same literals,
    /// same order) so the gauge `PowerDesignModelTests` can prove it reproduces every base-mod power component. The
    /// hardcoded coefficients below are template JSON literals (there is no engine const to reference for them); each is
    /// cited to its `energy.json` formula.
    /// </summary>
    public readonly struct PowerDesignModel
    {
        // ---- Level-0 (game-start) tech baseline — the techs.json DataFormula evaluated at [Level] = 0. ----
        /// <summary>`tech-panel-density` at level 0 (`0.2 - Level*0.01`) — kg of panel per m² of area.</summary>
        public const double DefaultPanelDensity = 0.2;
        /// <summary>`tech-panel-efficiency` at level 0 (`12.0 + Level*3.0`) — best absorption %, before the bandwidth trade.</summary>
        public const double DefaultPanelEfficiency = 12.0;
        /// <summary>`tech-panel-bandwidth` at level 0 (`200 + Level*100`) — the widest bandwidth the panel can be tuned to (nm).</summary>
        public const double DefaultPanelBandwidth = 200.0;
        /// <summary>`tech-battery-capacity` at level 0 (`1 + Level/5.0`) — the kJ-per-kg multiplier on a battery bank.</summary>
        public const double DefaultBatteryCapacity = 1.0;
        /// <summary>`tech-conductors` at level 0 (`1 * Level`) — added to the RTG's base 10% thermoelectric efficiency.</summary>
        public const double DefaultConductors = 0.0;

        // ---- Template Min/Max bounds (energy.json Properties) — used to CLAMP each slider (the L7 gotcha). ----
        public const double ReactorMassMin = 1000, ReactorMassMax = 25000;
        public const double OutputVsEconomyMin = 0.5, OutputVsEconomyMax = 2.0;
        public const double ReactorLifetimeMinHours = 1, ReactorLifetimeMaxHours = 876000;
        public const double BatteryMassMin = 1000, BatteryMassMax = 25000;
        public const double TurbineMassMin = 2000, TurbineMassMax = 500000;
        public const double CoreVsGeneratorMin = 30, CoreVsGeneratorMax = 70;
        public const double SolarAreaMin = 1, SolarAreaMax = 10000;
        public const double SolarWavelengthMin = 300, SolarWavelengthMax = 2000;
        public const double RtgMassMin = 25, RtgMassMax = 25000;
        public const double RtgLifetimeMinYears = 1, RtgLifetimeMaxYears = 25;

        // ---- Template formula LITERALS (energy.json) — no engine const exists for these, so they live here, cited. ----
        /// <summary>reactor `Power Output` = 50 * [Mass] * OvE (kW).</summary>
        private const double ReactorPowerPerKg = 50.0;
        /// <summary>reactor `Fuel Consumption` = Power * 3.125e-11 * OvE (kg/s). Literal `0.00000000003125`.</summary>
        private const double ReactorFuelCoeff = 0.00000000003125;
        /// <summary>reactor `Fuel Load Seconds` = Lifetime(h) * 3600.</summary>
        private const double SecondsPerHour = 3600.0;
        /// <summary>battery `Energy Storage` = Mass * 500 * tech-battery-capacity (kJ).</summary>
        private const double BatteryKjPerKg = 500.0;
        /// <summary>turbine `FuelMass` = CoreMass * 0.6; `Heat per kg` = 100; `Core Efficiency` = 1.0; `Generator Efficiency` = 0.8; `Fuel Burn Rate` = CoreOutput / 40e9.</summary>
        private const double TurbineFuelFraction = 0.6, TurbineHeatPerKg = 100.0, TurbineCoreEff = 1.0, TurbineGenEff = 0.8, TurbineBurnDivisor = 40e9;
        /// <summary>rtg `Fuel` = Mass * 0.5; `Efficiency` = conductors + 10; `Fuel Consumption` = 0.001 / OpLife(yr); `Lifetime Seconds` = OpLife * 31557600.</summary>
        private const double RtgFuelFraction = 0.5, RtgEfficiencyBase = 10.0, RtgFuelConsumptionCoeff = 0.001, SecondsPerYear = 31557600.0;
        /// <summary>Every fuel-burner co-emits a `SensorSignatureAtb(1700, mag)` — the 1700 K blackbody temperature.</summary>
        private const double BurnerSignatureTemp_K = 1700.0;

        /// <summary>CHOICE 1 — the job (which attribute type is produced).</summary>
        public PowerJob Job { get; }
        /// <summary>CHOICE 2 — the generation source (only read when <see cref="Job"/> is Generate).</summary>
        public PowerSource Source { get; }
        /// <summary>SLIDER 1 — SIZE: kilograms of plant, or square metres of panel for a solar array.</summary>
        public double Size { get; }
        /// <summary>SLIDER 2 — ENDURANCE: reactor life in HOURS, RTG life in YEARS. Ignored by turbine/battery/solar.</summary>
        public double Endurance { get; }
        /// <summary>SLIDER 3 — TUNE: reactor Output-vs-Economy (×), turbine Core-vs-Generator (% core), solar Bandwidth (nm).</summary>
        public double Tune { get; }
        /// <summary>SLIDER 4 — PEAK WAVELENGTH (nm): solar only — the panel's ideal absorption wavelength.</summary>
        public double PeakWavelength { get; }

        // Tech inputs — free reals defaulting to the level-0 baseline (the honest caveat above).
        public double PanelDensity { get; }
        public double PanelEfficiency { get; }
        public double PanelBandwidth { get; }
        public double BatteryCapacity { get; }
        public double Conductors { get; }

        public PowerDesignModel(
            PowerJob job,
            PowerSource source = PowerSource.Reactor,
            double size = 0,
            double endurance = 0,
            double tune = 0,
            double peakWavelength = 0,
            double panelDensity = DefaultPanelDensity,
            double panelEfficiency = DefaultPanelEfficiency,
            double panelBandwidth = DefaultPanelBandwidth,
            double batteryCapacity = DefaultBatteryCapacity,
            double conductors = DefaultConductors)
        {
            Job = job;
            Source = source;
            Size = size;
            Endurance = endurance;
            Tune = tune;
            PeakWavelength = peakWavelength;
            PanelDensity = panelDensity;
            PanelEfficiency = panelEfficiency;
            PanelBandwidth = panelBandwidth;
            BatteryCapacity = batteryCapacity;
            Conductors = conductors;
        }

        /// <summary>
        /// Build the <see cref="PowerProfile"/> — the exact attribute instance(s) + the design's mass/volume/crew that
        /// the live ComponentDesigner produces for the matching base-mod component. Each slider is clamped to its
        /// template Min/Max first (the L7 gotcha), then the branch's own NCalc arithmetic is mirrored literal-for-literal.
        /// </summary>
        public PowerProfile Compute()
        {
            switch (Job)
            {
                case PowerJob.Store: return ComputeStore();
                case PowerJob.Collect: return ComputeSolar();
                default:
                    switch (Source)
                    {
                        case PowerSource.RTG: return ComputeRtg();
                        case PowerSource.SteamTurbine: return ComputeTurbine();
                        default: return ComputeReactor();
                    }
            }
        }

        // ---- Generate ▸ Fission Reactor (energy.json `reactor`) ----
        private PowerProfile ComputeReactor()
        {
            double mass = Math.Clamp(Size, ReactorMassMin, ReactorMassMax);
            double ove = Math.Clamp(Tune, OutputVsEconomyMin, OutputVsEconomyMax);
            double lifeHours = Math.Clamp(Endurance, ReactorLifetimeMinHours, ReactorLifetimeMaxHours);

            double powerOutput = ReactorPowerPerKg * mass * ove;              // "Power Output"
            double fuelConsumption = powerOutput * ReactorFuelCoeff * ove;    // "Fuel Consumption"
            double fuelLoadSeconds = lifeHours * SecondsPerHour;              // "Fuel Load Seconds"

            var gen = new EnergyGenerationAtb("fissile-fuels", fuelConsumption, "electricity", powerOutput, fuelLoadSeconds);
            var sig = new SensorSignatureAtb(BurnerSignatureTemp_K, powerOutput * 0.1 * mass); // "Sensor Signature"

            return new PowerProfile
            {
                Job = Job, Source = Source,
                Mass = (long)mass, Volume = mass, Crew = (int)Math.Max(2, mass / 500.0), // Mass=[Mass]; CrewReq=Max(2,[Mass]/500)
                Generation = gen, Signature = sig,
            };
        }

        // ---- Generate ▸ RTG (energy.json `rtg`) ----
        private PowerProfile ComputeRtg()
        {
            double mass = Math.Clamp(Size, RtgMassMin, RtgMassMax);
            double opLifeYears = Math.Clamp(Endurance, RtgLifetimeMinYears, RtgLifetimeMaxYears);

            double fuel = mass * RtgFuelFraction;                                  // "Fuel"
            double efficiency = Conductors + RtgEfficiencyBase;                    // "Efficiency"
            double fuelConsumption = RtgFuelConsumptionCoeff / opLifeYears;        // "Fuel Consumption"
            double powerOutput = fuel * efficiency * fuelConsumption;              // "Power Output"
            double lifeSeconds = opLifeYears * SecondsPerYear;                     // "Lifetime Seconds"

            var gen = new EnergyGenerationAtb("fissile-fuels", fuelConsumption, "electricity", powerOutput, lifeSeconds);
            var sig = new SensorSignatureAtb(BurnerSignatureTemp_K, powerOutput * 0.1 * mass);

            return new PowerProfile
            {
                Job = Job, Source = Source,
                Mass = (long)mass, Volume = mass, Crew = 0, // Volume=[Mass]; CrewReq=0
                Generation = gen, Signature = sig,
            };
        }

        // ---- Generate ▸ Steam-turbine Reactor (energy.json `steam-turbine-reactor`) ----
        private PowerProfile ComputeTurbine()
        {
            double mass = Math.Clamp(Size, TurbineMassMin, TurbineMassMax);
            double coreVsGen = Math.Clamp(Tune, CoreVsGeneratorMin, CoreVsGeneratorMax);

            double coreMass = mass * coreVsGen * 0.01;                     // "CoreMass"
            double fuelMass = coreMass * TurbineFuelFraction;             // "FuelMass"
            double coreOutput = fuelMass * TurbineHeatPerKg * TurbineCoreEff; // "CoreOutput"
            double fuelBurnRate = coreOutput / TurbineBurnDivisor;        // "Fuel Burn Rate"
            double generatorOutput = coreOutput * TurbineGenEff;         // "GeneratorOutput"
            double fuelDuration = fuelMass / fuelBurnRate;                // "FuelDuration"

            var gen = new EnergyGenerationAtb("fissile-fuels", fuelBurnRate, "electricity", generatorOutput, fuelDuration);
            var sig = new SensorSignatureAtb(BurnerSignatureTemp_K, coreOutput); // "Sensor Signature" mag = CoreOutput

            return new PowerProfile
            {
                Job = Job, Source = Source,
                Mass = (long)mass, Volume = mass / 1000.0, Crew = 3, // Volume=[Mass]/1000; CrewReq=3
                Generation = gen, Signature = sig,
            };
        }

        // ---- Collect ▸ Solar Array (energy.json `solarArray`) ----
        private PowerProfile ComputeSolar()
        {
            double area = Math.Clamp(Size, SolarAreaMin, SolarAreaMax);
            double bwMin = PanelBandwidth * 0.5, bwMax = PanelBandwidth; // "Bandwidth" MinFormula/MaxFormula
            double bandwidth = Math.Clamp(Tune, bwMin, bwMax);
            double peak = Math.Clamp(PeakWavelength, SolarWavelengthMin, SolarWavelengthMax);

            double mass = area * PanelDensity;                                              // "Mass" = Area * tech-panel-density
            double bestEff = PanelEfficiency * ((PanelBandwidth * 0.5) / bandwidth);        // "Best Efficiency" (a percent)
            double worstEff = bestEff * 0.5;                                                // "Worst Efficiency"

            var solar = new EnergySolarGenerationAtb(peak, bandwidth, bestEff, worstEff, area);

            return new PowerProfile
            {
                Job = Job, Source = Source,
                Mass = (long)mass, Volume = mass / 2000.0, Crew = 0, // Volume=[Mass]/2000; CrewReq=0
                Solar = solar,
            };
        }

        // ---- Store ▸ Battery Bank (energy.json `battery-bank`) ----
        private PowerProfile ComputeStore()
        {
            double mass = Math.Clamp(Size, BatteryMassMin, BatteryMassMax);
            double maxStore = mass * BatteryKjPerKg * BatteryCapacity; // "Energy Storage" = Mass * 500 * tech-battery-capacity

            var store = new EnergyStoreAtb("electricity", maxStore);

            return new PowerProfile
            {
                Job = Job, Source = Source,
                Mass = (long)mass, Volume = mass / 2896.6, Crew = 0, // Volume=[Mass]/2896.6; CrewReq=0
                Store = store,
            };
        }
    }

    /// <summary>
    /// The result of <see cref="PowerDesignModel.Compute"/> — the exact attribute instance(s) the live ComponentDesigner
    /// would build for this power component, plus the design's own mass/volume/crew. Exactly ONE of
    /// <see cref="Generation"/> / <see cref="Solar"/> / <see cref="Store"/> is non-null (which one = the JOB); a
    /// fuel-burner also carries a <see cref="Signature"/> (a solar array and a battery are SILENT — null). Nothing here
    /// is serialized: it is a derived value type built on demand, exactly like <c>WeaponProfile</c> off the weapons door.
    /// </summary>
    public readonly struct PowerProfile
    {
        public PowerJob Job { get; init; }
        public PowerSource Source { get; init; }

        /// <summary>Design mass (kg), truncated to a long exactly as <c>ComponentDesign.MassPerUnit</c> is.</summary>
        public long Mass { get; init; }
        /// <summary>Design volume (m³), the template's Volume formula (a double, as <c>VolumePerUnit</c>).</summary>
        public double Volume { get; init; }
        /// <summary>Operating crew, truncated to an int exactly as <c>ComponentDesign.CrewReq</c> is.</summary>
        public int Crew { get; init; }

        /// <summary>The produced generation attribute (Generate job), else null.</summary>
        public EnergyGenerationAtb Generation { get; init; }
        /// <summary>The produced solar attribute (Collect job), else null.</summary>
        public EnergySolarGenerationAtb Solar { get; init; }
        /// <summary>The produced storage attribute (Store job), else null.</summary>
        public EnergyStoreAtb Store { get; init; }
        /// <summary>The co-emitted signature (fuel-burners only) — a solar array / battery is silent, so null.</summary>
        public SensorSignatureAtb Signature { get; init; }

        /// <summary>The attribute Type this design produces — the key that would appear in <c>AttributesByType</c>.</summary>
        public Type ProducedAttribute =>
            Generation != null ? typeof(EnergyGenerationAtb)
            : Solar != null ? typeof(EnergySolarGenerationAtb)
            : Store != null ? typeof(EnergyStoreAtb)
            : null;
    }
}
