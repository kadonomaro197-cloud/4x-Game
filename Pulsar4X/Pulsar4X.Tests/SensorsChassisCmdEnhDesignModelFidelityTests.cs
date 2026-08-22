using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Components;              // ComponentDesign, GetAttribute / HasAttribute
using Pulsar4X.Components.Designers;   // the four door models
using Pulsar4X.Engine;
using Pulsar4X.Factions;               // FactionInfoDB
using Pulsar4X.Interfaces;             // IConstructableDesign, ChassisBudgetKind
using Pulsar4X.DataStructures;         // ComponentMountType
using Pulsar4X.Sensors;                // SensorReceiverAtb, CloakAtb, JammerAtb
using Pulsar4X.Weapons;                // BeamFireControlAtbDB
using Pulsar4X.GeoSurveys;             // GeoSurveyAtb
using Pulsar4X.JumpPoints;             // GravSurveyAtb
using Pulsar4X.Ships;                  // ShipHullAtb
using Pulsar4X.Stations;               // StationChassisAtb
using Pulsar4X.Colonies;               // BuildingChassisAtb
using Pulsar4X.GroundCombat;           // GroundChassisAtb, GroundFootprintAtb, GroundAugmentAtb, GroundTrainingAtb, GroundSealAtb
using Pulsar4X.Combat;                 // UnitCaliberAtb, CrewAutomationAtb
using GameEngine.People;               // AdminSpaceAtb, AdminLevel
using Pulsar4X.Sites;                  // CommandBerthAtb, SiteRole

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the SENSORS / CHASSIS / COMMAND / ENHANCERS door parametric designers,
    /// slice-1b FIDELITY cross-check.
    ///
    /// WHAT THIS ADDS OVER THE PURE GAUGES (and why it exists). The four pure gauges
    /// (<see cref="SensorsDesignModelTests"/>, <see cref="ChassisDesignModelTests"/>, <see cref="CommandDesignModelTests"/>,
    /// <see cref="EnhancersDesignModelTests"/>) assert each door model against numbers TRANSCRIBED into the test file —
    /// they prove the model's arithmetic is internally consistent, but they can't catch a case where BOTH the model and
    /// its transcribed twin drifted away from what the LIVE ComponentDesigner actually builds for the shipped component.
    /// This fixture closes that gap: it stands up the REAL base-mod start faction (the game path — JSON template → NCalc →
    /// <c>*Atb</c> via reflection), READS the real <c>*Atb</c> the sensor / fire-control / survey / chassis / command /
    /// enhancer processors will use, and proves each door model REPRODUCES that live attribute field-by-field. The expected
    /// value in every assertion is READ FROM THE LIVE DESIGN (<c>design.GetAttribute&lt;X&gt;()</c> field, or the design's
    /// emergent <c>MassPerUnit</c>/<c>CrewReq</c>/<c>ResearchCostValue</c>/<c>CreditCost</c>/<c>VolumePerUnit</c>), never a
    /// hardcoded literal — so a real reproduction bug fails here instead of shipping green.
    ///
    /// THE MODEL INPUTS are the design's authored dials (the choices + sliders — the same constants the pure gauge feeds,
    /// verified against the base-mod JSON). The OUTPUT (the produced atb's fields + the resolved component mass) is what
    /// must equal the LIVE attribute. So feeding the input constants is not "re-being the pure test": the pure test
    /// hardcodes both input AND expected output; this test hardcodes only the input and reads the expected output off the
    /// live design the ComponentDesigner built — catching template-default drift, a clamp the model missed, a tech-chain
    /// divergence, or a swapped ctor arg.
    ///
    /// Rides the colony harness (builds the real start faction) → the slow CI shard; the colony is stood up ONCE in
    /// <see cref="OneTimeSetUp"/>. Byte-identical to the live game (nothing calls the models yet).
    ///
    /// COVERAGE (honest): every door design UNLOCKED on the base-mod earth start is cross-checked —
    ///   • Sensors : passive-sensor / passive-sensor-s50 / outpost-sensor (Listen) · beam-fire-control / pd-director (Track)
    ///               · cloak-device (Hide) · jammer (Blind) · geo-surveyor / gravitational-surveyor (Look) — all 9.
    ///   • Chassis : ship-hull light/medium/heavy · station-chassis · building-foundation (+ footprint co-mount) ·
    ///               human/vehicle/walker/swarm frames — all 9.
    ///   • Command : city-hall (Office) · command-berth (Berth). The <c>ship-command</c> BRIDGE ships NO ComponentDesign
    ///               (a template-only coverage point — the pure gauge covers it), and <c>federation-ministry</c> /
    ///               <c>mars-high-command</c> are UMF/Mars designs NOT on the earth start — so they cannot be read from
    ///               this faction and are left to the pure gauge (noted in the manifest risks).
    ///   • Enhancers: unit-caliber · crew-automation · power-armor · shield-generator · ward-projector · reflex-booster ·
    ///               ground-training-cadre · sealed-systems — all 8.
    ///
    /// The Listen sensitivity chain reads antenna efficiency / bandwidth-ceiling / sensitivity from the faction tech DB;
    /// the model feeds those from <see cref="SensorTechEnv.L0"/>. The base-mod start faction is authored at tech level 0
    /// (techs.json: efficiency 0.75 + [Level]*0.1, bandwidth 500 + [Level]*100, sensitivity 0.01 − [Level]*0.001, and
    /// earth.json grants NO antenna tech levels), so L0 IS the live tech — which is exactly why the derived best/worst
    /// sensitivity must match the live receiver to floating precision.
    /// </summary>
    [TestFixture]
    public class SensorsChassisCmdEnhDesignModelFidelityTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[door-fidelity] " + m);

        private TestScenario _s;
        private IReadOnlyDictionary<string, ComponentDesign> _designs;
        private int _assertions;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _s = TestScenario.CreateWithColony();                 // the slow path — called ONCE
            // ComponentDesigns (InternalComponentDesigns) holds EVERY unlocked component design unconditionally
            // (ComponentDesigner.cs:217), ship-mount and installation-mount alike — the comprehensive dictionary, and the
            // one FireControlMassLeakTests reads ship components from. (IndustryDesigns is gated on research state.)
            _designs = _s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            Log($"total field-by-field assertions across all cross-checked base-mod door designs: {_assertions}");

        // ── shared helpers ────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Fetch a base-mod ComponentDesign the game way (the gotcha-10 registration sensor), failing loudly if
        /// the design or the cast is wrong.</summary>
        private ComponentDesign Design(string id)
        {
            Assert.That(_designs.ContainsKey(id), Is.True,
                $"base-mod design '{id}' is unlocked on the start faction (JSON registration chain intact)");
            var d = _designs[id];
            Assert.That(d, Is.Not.Null, $"'{id}' is a ComponentDesign");
            return d;
        }

        /// <summary>Assert one field of the model's output equals the value the LIVE ComponentDesigner produced. A
        /// magnitude-relative tolerance (floor 1e-6) absorbs an honest float/double precision gap; the EXPECTED is the
        /// live value, never a literal.</summary>
        private void Close(double actual, double expected, string field, string design)
        {
            double tol = Math.Max(1e-6, Math.Abs(expected) * 1e-9);
            _assertions++;
            Assert.That(actual, Is.EqualTo(expected).Within(tol), $"{design}: {field} (model vs LIVE)");
        }

        /// <summary>Relative-tolerance compare for the tiny transcendental sensitivity values (watts-scale kW).</summary>
        private void CloseRel(double actual, double expected, string field, string design)
        {
            _assertions++;
            Assert.That(actual, Is.EqualTo(expected).Within(1e-4).Percent, $"{design}: {field} (model vs LIVE)");
        }

        /// <summary>The component's resolved mass: the live design stores it as a rounded <c>long</c> (MassPerUnit), so a
        /// unit tolerance absorbs the round (every model mass here is either integer or within &lt;1 of one).</summary>
        private void CloseMass(ComponentDesign d, double modelMass, string design)
        {
            _assertions++;
            Assert.That((double)d.MassPerUnit, Is.EqualTo(modelMass).Within(1.0),
                $"{design}: MassPerUnit (LIVE) vs model component mass");
        }

        private void EqInt(int actual, int expected, string field, string design)
        {
            _assertions++;
            Assert.That(actual, Is.EqualTo(expected), $"{design}: {field} (model vs LIVE)");
        }

        private void EqBool(bool actual, bool expected, string field, string design)
        {
            _assertions++;
            Assert.That(actual, Is.EqualTo(expected), $"{design}: {field} (model vs LIVE)");
        }

        private void EqObj(object actual, object expected, string field, string design)
        {
            _assertions++;
            Assert.That(actual, Is.EqualTo(expected), $"{design}: {field} (model vs LIVE)");
        }

        // ══ SENSORS ═══════════════════════════════════════════════════════════════════════════════════════════════════

        [Test]
        [Description("The SENSORS door: every base-mod sensor design unlocked on the start faction (3 Listen receivers, 2 Track directors, 1 cloak, 1 jammer, 2 surveyors) is built the game way; the SensorsDesignModel reproduces each one's live *Atb field-by-field (waveform, derived best/worst sensitivity, resolution, scan, horizon, range/tracking/final-fire, signature, degrade/reach/beacon, survey speed) + its resolved mass — expected read from the live design.")]
        public void Sensors_ModelReproducesLiveAtbs()
        {
            // ---- LISTEN: antenna/wavelength/bandwidth/resolution/scan are the authored dials; best/worst sensitivity +
            //      the clamped mass are DERIVED and cross-checked against the live receiver at L0 tech.
            AssertListen("default-design-passive-sensor",
                SensorsDesignModel.Listen(antennaSize_m2: 2500, idealWavelength_nm: 470, bandwidth_nm: 250, resolution_MP: 100, scanTime_s: 3600));
            AssertListen("default-design-passive-sensor-s50",
                SensorsDesignModel.Listen(antennaSize_m2: 5.5, idealWavelength_nm: 479, bandwidth_nm: 200, resolution_MP: 1, scanTime_s: 3600));
            // outpost-sensor: authored antenna 12000 CLAMPS to 2500 (the L7 template-clamp) — the derived sensitivity + mass
            // must match the live receiver built through that same clamp, not the un-clamped 12000.
            AssertListen("default-design-outpost-sensor",
                SensorsDesignModel.Listen(antennaSize_m2: 12000, idealWavelength_nm: 470, bandwidth_nm: 300, resolution_MP: 200, scanTime_s: 3600));

            // ---- TRACK: the sub-choice (anti-ship vs point-defense) forces the ctor arity + FinalFireOnly + the mass ×1.5.
            AssertFireControl("default-design-beam-fire-control",
                SensorsDesignModel.Track(SensorDirectorRole.AntiShip, range_kkm: 100, trackingSpeed_kmps: 5000), expectFinalFire: false);
            AssertFireControl("default-design-pd-director",
                SensorsDesignModel.Track(SensorDirectorRole.PointDefense, range_kkm: 10, trackingSpeed_kmps: 20000), expectFinalFire: true);

            // ---- HIDE / BLIND / LOOK.
            AssertCloak("default-design-cloak-device", SensorsDesignModel.Hide(signatureMultiplier: 0.2));
            AssertJammer("default-design-jammer", SensorsDesignModel.Blind(sensitivityDegrade: 4, jammerRange_Gm: 1, selfSignatureBoost: 5));
            AssertSurvey("default-design-geo-surveyor", SensorsDesignModel.Look(SensorSurveyType.Geological, surveySpeed: 10), grav: false);
            AssertSurvey("default-design-gravitational-surveyor", SensorsDesignModel.Look(SensorSurveyType.Gravitational, surveySpeed: 10), grav: true);
        }

        private void AssertListen(string id, SensorsDesignModel model)
        {
            var d = Design(id);
            Assert.That(d.HasAttribute<SensorReceiverAtb>(), Is.True, $"{id} binds a SensorReceiverAtb");
            var live = d.GetAttribute<SensorReceiverAtb>();
            var p = model.Compute(SensorTechEnv.L0);
            var r = p.Receiver;
            Log($"LISTEN {id}: wave=({r.RecevingWaveformCapabilty.WavelengthMin_nm},{r.RecevingWaveformCapabilty.WavelengthAverage_nm},{r.RecevingWaveformCapabilty.WavelengthMax_nm}) " +
                $"model bestKw={r.BestSensitivity_kW} worstKw={r.WorstSensitivity_kW} | LIVE bestKw={live.BestSensitivity_kW} worstKw={live.WorstSensitivity_kW} mass(model={p.ComponentMass},live={d.MassPerUnit})");

            Close(r.RecevingWaveformCapabilty.WavelengthMin_nm, live.RecevingWaveformCapabilty.WavelengthMin_nm, "waveform min", id);
            Close(r.RecevingWaveformCapabilty.WavelengthAverage_nm, live.RecevingWaveformCapabilty.WavelengthAverage_nm, "waveform avg", id);
            Close(r.RecevingWaveformCapabilty.WavelengthMax_nm, live.RecevingWaveformCapabilty.WavelengthMax_nm, "waveform max", id);
            CloseRel(r.BestSensitivity_kW, live.BestSensitivity_kW, "BestSensitivity_kW (derived)", id);
            CloseRel(r.WorstSensitivity_kW, live.WorstSensitivity_kW, "WorstSensitivity_kW (derived)", id);
            _assertions++; Assert.That(r.Resolution, Is.EqualTo(live.Resolution), $"{id}: Resolution (model vs LIVE)");
            EqInt(r.ScanTime, live.ScanTime, "ScanTime", id);
            Close(r.MaxDetectionRange_m, live.MaxDetectionRange_m, "MaxDetectionRange_m (200 Gm horizon)", id);
            CloseMass(d, p.ComponentMass, id);
        }

        private void AssertFireControl(string id, SensorsDesignModel model, bool expectFinalFire)
        {
            var d = Design(id);
            Assert.That(d.HasAttribute<BeamFireControlAtbDB>(), Is.True, $"{id} binds a BeamFireControlAtbDB");
            var live = d.GetAttribute<BeamFireControlAtbDB>();
            var p = model.Compute();
            var fc = p.FireControl;
            Log($"TRACK {id}: model range={fc.Range} track={fc.TrackingSpeed} finalFire={fc.FinalFireOnly} mass={p.ComponentMass} | LIVE range={live.Range} track={live.TrackingSpeed} finalFire={live.FinalFireOnly} mass={d.MassPerUnit}");
            EqInt(fc.Range, live.Range, "FireControl.Range", id);
            EqInt(fc.TrackingSpeed, live.TrackingSpeed, "FireControl.TrackingSpeed", id);
            EqBool(fc.FinalFireOnly, live.FinalFireOnly, "FireControl.FinalFireOnly", id);
            EqBool(live.FinalFireOnly, expectFinalFire, "LIVE FinalFireOnly matches the sub-choice", id);
            CloseMass(d, p.ComponentMass, id);
        }

        private void AssertCloak(string id, SensorsDesignModel model)
        {
            var d = Design(id);
            Assert.That(d.HasAttribute<CloakAtb>(), Is.True, $"{id} binds a CloakAtb");
            var live = d.GetAttribute<CloakAtb>();
            var p = model.Compute();
            Log($"HIDE {id}: model sigMult={p.Cloak.SignatureMultiplier} mass={p.ComponentMass} | LIVE sigMult={live.SignatureMultiplier} mass={d.MassPerUnit}");
            Close(p.Cloak.SignatureMultiplier, live.SignatureMultiplier, "Cloak.SignatureMultiplier", id);
            CloseMass(d, p.ComponentMass, id);
        }

        private void AssertJammer(string id, SensorsDesignModel model)
        {
            var d = Design(id);
            Assert.That(d.HasAttribute<JammerAtb>(), Is.True, $"{id} binds a JammerAtb");
            var live = d.GetAttribute<JammerAtb>();
            var p = model.Compute();
            var j = p.Jammer;
            Log($"BLIND {id}: model degrade={j.SensitivityDegrade} range_m={j.Range_m} boost={j.SelfSignatureBoost} mass={p.ComponentMass} | LIVE degrade={live.SensitivityDegrade} range_m={live.Range_m} boost={live.SelfSignatureBoost} mass={d.MassPerUnit}");
            Close(j.SensitivityDegrade, live.SensitivityDegrade, "Jammer.SensitivityDegrade", id);
            Close(j.Range_m, live.Range_m, "Jammer.Range_m (Gm slider → metres)", id);
            Close(j.SelfSignatureBoost, live.SelfSignatureBoost, "Jammer.SelfSignatureBoost", id);
            CloseMass(d, p.ComponentMass, id);
        }

        private void AssertSurvey(string id, SensorsDesignModel model, bool grav)
        {
            var d = Design(id);
            var p = model.Compute();
            if (grav)
            {
                Assert.That(d.HasAttribute<GravSurveyAtb>(), Is.True, $"{id} binds a GravSurveyAtb");
                var live = d.GetAttribute<GravSurveyAtb>();
                Log($"LOOK {id}: model speed={p.GravSurvey.Speed} mass={p.ComponentMass} | LIVE speed={live.Speed} mass={d.MassPerUnit}");
                EqInt((int)p.GravSurvey.Speed, (int)live.Speed, "GravSurvey.Speed", id);
            }
            else
            {
                Assert.That(d.HasAttribute<GeoSurveyAtb>(), Is.True, $"{id} binds a GeoSurveyAtb");
                var live = d.GetAttribute<GeoSurveyAtb>();
                Log($"LOOK {id}: model speed={p.GeoSurvey.Speed} mass={p.ComponentMass} | LIVE speed={live.Speed} mass={d.MassPerUnit}");
                EqInt((int)p.GeoSurvey.Speed, (int)live.Speed, "GeoSurvey.Speed", id);
            }
            CloseMass(d, p.ComponentMass, id);
        }

        // ══ CHASSIS ═══════════════════════════════════════════════════════════════════════════════════════════════════

        [Test]
        [Description("The CHASSIS door: all 9 shipped frames across the 2x2 host cell (ship-hull light/medium/heavy, station chassis, building foundation + footprint co-mount, human/vehicle/walker/swarm ground frames) built the game way; the ChassisDesignModel's per-cell mapping reproduces each live frame *Atb (budget arg, budget-currency, part-mount, the five ground dials, the footprint co-mount) + the frame's resolved mass — expected read from the live design.")]
        public void Chassis_ModelReproducesLiveAtbs()
        {
            AssertShipHull("default-design-ship-hull",       new ChassisDesignModel(ChassisEnvironment.Orbital, ChassisKind.Unit, frameMass: 10000, structuralBudget: 90000));
            AssertShipHull("default-design-ship-hull-light", new ChassisDesignModel(ChassisEnvironment.Orbital, ChassisKind.Unit, frameMass: 500,   structuralBudget: 25000));
            AssertShipHull("default-design-ship-hull-heavy", new ChassisDesignModel(ChassisEnvironment.Orbital, ChassisKind.Unit, frameMass: 25000, structuralBudget: 180000));

            AssertStation("default-design-station-chassis",  new ChassisDesignModel(ChassisEnvironment.Orbital, ChassisKind.Infrastructure, frameMass: 100000, structuralBudget: 2000));

            AssertBuilding("default-design-building-foundation",
                new ChassisDesignModel(ChassisEnvironment.Surface, ChassisKind.Infrastructure, frameMass: 50000, structuralBudget: 2000, tileFootprint: 4));

            AssertGroundFrame("default-design-human-frame",
                new ChassisDesignModel(ChassisEnvironment.Surface, ChassisKind.Unit, frameMass: 20, structuralBudget: 100,
                    hp: 200, size: 1, locomotion: GroundLocomotion.Foot, carryClass: GroundCarryClass.Personnel));
            AssertGroundFrame("default-design-vehicle-frame",
                new ChassisDesignModel(ChassisEnvironment.Surface, ChassisKind.Unit, frameMass: 4000, structuralBudget: 800,
                    hp: 1500, size: 6, locomotion: GroundLocomotion.Tracked, carryClass: GroundCarryClass.Vehicle));
            AssertGroundFrame("default-design-walker-frame",
                new ChassisDesignModel(ChassisEnvironment.Surface, ChassisKind.Unit, frameMass: 2500, structuralBudget: 400,
                    hp: 1000, size: 4, locomotion: GroundLocomotion.Walker, carryClass: GroundCarryClass.Vehicle));
            AssertGroundFrame("default-design-swarm-frame",
                new ChassisDesignModel(ChassisEnvironment.Surface, ChassisKind.Unit, frameMass: 5, structuralBudget: 30,
                    hp: 40, size: 1, locomotion: GroundLocomotion.Foot, carryClass: GroundCarryClass.Personnel));
        }

        private void AssertShipHull(string id, ChassisDesignModel model)
        {
            var d = Design(id);
            Assert.That(d.HasAttribute<ShipHullAtb>(), Is.True, $"{id} binds a ShipHullAtb");
            var live = d.GetAttribute<ShipHullAtb>();
            var p = model.BuildProfile();
            Log($"HULL {id}: model budget={p.AtbArgs[0]} kind={p.BudgetKind} mount={p.PartMount} mass={p.Mass} | LIVE budget={live.MassBudget} kind={live.BudgetKind} mount={live.PartMount} mass={d.MassPerUnit}");
            Close(p.AtbArgs[0], live.MassBudget, "ShipHullAtb.MassBudget", id);
            EqObj(p.BudgetKind, live.BudgetKind, "ShipHullAtb.BudgetKind", id);
            EqObj(p.PartMount, live.PartMount, "ShipHullAtb.PartMount", id);
            CloseMass(d, p.Mass, id);
        }

        private void AssertStation(string id, ChassisDesignModel model)
        {
            var d = Design(id);
            Assert.That(d.HasAttribute<StationChassisAtb>(), Is.True, $"{id} binds a StationChassisAtb");
            var live = d.GetAttribute<StationChassisAtb>();
            var p = model.BuildProfile();
            Log($"STATION {id}: model budget={p.AtbArgs[0]} kind={p.BudgetKind} mount={p.PartMount} mass={p.Mass} | LIVE allowance={live.StructuralAllowance} kind={live.BudgetKind} mount={live.PartMount} mass={d.MassPerUnit}");
            Close(p.AtbArgs[0], live.StructuralAllowance, "StationChassisAtb.StructuralAllowance", id);
            EqObj(p.BudgetKind, live.BudgetKind, "StationChassisAtb.BudgetKind", id);
            EqObj(p.PartMount, live.PartMount, "StationChassisAtb.PartMount", id);
            CloseMass(d, p.Mass, id);
        }

        private void AssertBuilding(string id, ChassisDesignModel model)
        {
            var d = Design(id);
            Assert.That(d.HasAttribute<BuildingChassisAtb>(), Is.True, $"{id} binds a BuildingChassisAtb");
            var live = d.GetAttribute<BuildingChassisAtb>();
            var p = model.BuildProfile();
            Log($"BUILDING {id}: model budget={p.AtbArgs[0]} footprint={p.CoMountAtbArgs[0]} mass={p.Mass} | LIVE allowance={live.FootprintAllowance} mass={d.MassPerUnit}");
            Close(p.AtbArgs[0], live.FootprintAllowance, "BuildingChassisAtb.FootprintAllowance", id);
            EqObj(p.BudgetKind, live.BudgetKind, "BuildingChassisAtb.BudgetKind", id);
            EqObj(p.PartMount, live.PartMount, "BuildingChassisAtb.PartMount", id);
            // the co-mounted located-presence footprint part the shipped foundation carries alongside its budget atb.
            Assert.That(d.HasAttribute<GroundFootprintAtb>(), Is.True, $"{id} co-mounts a GroundFootprintAtb");
            var liveFp = d.GetAttribute<GroundFootprintAtb>();
            EqInt((int)p.CoMountAtbArgs[0], liveFp.TileFootprint, "GroundFootprintAtb.TileFootprint (co-mount)", id);
            CloseMass(d, p.Mass, id);
        }

        private void AssertGroundFrame(string id, ChassisDesignModel model)
        {
            var d = Design(id);
            Assert.That(d.HasAttribute<GroundChassisAtb>(), Is.True, $"{id} binds a GroundChassisAtb");
            var live = d.GetAttribute<GroundChassisAtb>();
            var p = model.BuildProfile();
            Log($"GROUND {id}: model args=[{string.Join(",", p.AtbArgs)}] mass={p.Mass} | LIVE str={live.BaseStrength} hp={live.BaseHP} size={live.Size} loco={(int)live.Locomotion} carry={(int)live.CarryClass} mass={d.MassPerUnit}");
            Close(p.AtbArgs[0], live.BaseStrength, "GroundChassisAtb.BaseStrength", id);
            Close(p.AtbArgs[1], live.BaseHP, "GroundChassisAtb.BaseHP", id);
            Close(p.AtbArgs[2], live.Size, "GroundChassisAtb.Size", id);
            EqInt((int)p.AtbArgs[3], (int)live.Locomotion, "GroundChassisAtb.Locomotion", id);
            EqInt((int)p.AtbArgs[4], (int)live.CarryClass, "GroundChassisAtb.CarryClass", id);
            EqObj(p.BudgetKind, live.BudgetKind, "GroundChassisAtb.BudgetKind", id);
            EqObj(p.PartMount, live.PartMount, "GroundChassisAtb.PartMount", id);
            CloseMass(d, p.Mass, id);
        }

        // ══ COMMAND ═══════════════════════════════════════════════════════════════════════════════════════════════════

        [Test]
        [Description("The COMMAND door: the two command designs unlocked on the earth start (city-hall Administrative Complex, command-berth) built the game way; the CommandDesignModel reproduces the live AdminSpaceAtb(level,space) / CommandBerthAtb(role,grade,support,survivability,span) args AND the emergent stats (mass/crew/research/credit/volume) — expected read from the live design. (ship-command bridge ships no design; federation-ministry/mars-high-command are UMF/Mars-only — covered by the pure gauge.)")]
        public void Command_ModelReproducesLiveAtbs()
        {
            // ---- OFFICE: default-design-city-hall — Office Space 1000, Admin Level = template default (Colony).
            var cityHall = Design("default-design-city-hall");
            Assert.That(cityHall.HasAttribute<AdminSpaceAtb>(), Is.True, "city-hall binds an AdminSpaceAtb");
            var liveAdmin = cityHall.GetAttribute<AdminSpaceAtb>();
            var pOffice = CommandDesignModel.Office(AdminLevel.Colony, officeSpace: 1000).BuildProfile();
            Log($"OFFICE city-hall: model level={pOffice.AdminLevelOrdinal} space={pOffice.ConsoleSpace} mass={pOffice.Mass} crew={pOffice.CrewReq} rp={pOffice.ResearchCost} cr={pOffice.CreditCost} vol={pOffice.Volume} | " +
                $"LIVE level={(int)liveAdmin.AdminLevel} space={liveAdmin.ConsoleSpace} mass={cityHall.MassPerUnit} crew={cityHall.CrewReq} rp={cityHall.ResearchCostValue} cr={cityHall.CreditCost} vol={cityHall.VolumePerUnit}");
            EqInt(pOffice.AdminLevelOrdinal, (int)liveAdmin.AdminLevel, "AdminSpaceAtb level ordinal", "city-hall");
            EqInt(pOffice.ConsoleSpace, liveAdmin.ConsoleSpace, "AdminSpaceAtb console/office space", "city-hall");
            AssertEmergentStats("city-hall", cityHall, pOffice.Mass, pOffice.CrewReq, pOffice.ResearchCost, pOffice.CreditCost, pOffice.Volume);

            // ---- BERTH: default-design-command-berth — Role Science, Grade 2, Support 10, Survivability 20 (template defaults).
            var berthDesign = Design("default-design-command-berth");
            Assert.That(berthDesign.HasAttribute<CommandBerthAtb>(), Is.True, "command-berth binds a CommandBerthAtb");
            var liveBerth = berthDesign.GetAttribute<CommandBerthAtb>();
            var pBerth = CommandDesignModel.BerthSeat(SiteRole.Science, grade: 2, support: 10, survivability: 20).BuildProfile();
            Log($"BERTH command-berth: model role={pBerth.RoleIndex} grade={pBerth.Grade} support={pBerth.Support} surv={pBerth.Survivability} span={pBerth.Span} mass={pBerth.Mass} | " +
                $"LIVE role={(int)liveBerth.Role} grade={liveBerth.Grade} support={liveBerth.Support} surv={liveBerth.Survivability} span={liveBerth.Span} mass={berthDesign.MassPerUnit}");
            EqInt(pBerth.RoleIndex, (int)liveBerth.Role, "CommandBerthAtb roleIndex", "command-berth");
            EqInt(pBerth.Grade, liveBerth.Grade, "CommandBerthAtb grade", "command-berth");
            EqInt(pBerth.Support, liveBerth.Support, "CommandBerthAtb support", "command-berth");
            EqInt(pBerth.Survivability, liveBerth.Survivability, "CommandBerthAtb survivability", "command-berth");
            EqInt(pBerth.Span, liveBerth.Span, "CommandBerthAtb span (fixed dead dial)", "command-berth");
            AssertEmergentStats("command-berth", berthDesign, pBerth.Mass, pBerth.CrewReq, pBerth.ResearchCost, pBerth.CreditCost, pBerth.Volume);
        }

        private void AssertEmergentStats(string id, ComponentDesign d,
            double modelMass, double modelCrew, double modelResearch, double modelCredit, double modelVolume)
        {
            CloseMass(d, modelMass, id);
            Close(d.CrewReq, modelCrew, "CrewReq (LIVE) vs model crew", id);
            Close((double)d.ResearchCostValue, modelResearch, "ResearchCostValue (LIVE) vs model research", id);
            EqInt(d.CreditCost, (int)modelCredit, "CreditCost (LIVE) vs model credit", id);
            Close(d.VolumePerUnit, modelVolume, "VolumePerUnit (LIVE) vs model volume", id);
        }

        // ══ ENHANCERS ═════════════════════════════════════════════════════════════════════════════════════════════════

        [Test]
        [Description("The ENHANCERS door: all 8 base-mod enhancers (unit-caliber, crew-automation, the four GroundAugment presets, ground-training-cadre, sealed-systems) built the game way; the EnhancersDesignModel reproduces each live *Atb's clamped fields (with the load-bearing 5-vs-6 augment arity) + the component mass — expected read from the live design.")]
        public void Enhancers_ModelReproducesLiveAtbs()
        {
            // ---- unit-caliber → UnitCaliberAtb(firepowerMult, toughnessMult).
            {
                var d = Design("default-design-unit-caliber");
                Assert.That(d.HasAttribute<UnitCaliberAtb>(), Is.True, "unit-caliber binds a UnitCaliberAtb");
                var live = d.GetAttribute<UnitCaliberAtb>();
                var p = new EnhancersDesignModel(EnhancerKind.AdvancedTraining, EnhancerType.ShipCadre).Compute();
                var m = (UnitCaliberAtb)p.Attribute;
                Log($"ENH unit-caliber: model fp={m.FirepowerMult} tuf={m.ToughnessMult} mass={p.Mass} | LIVE fp={live.FirepowerMult} tuf={live.ToughnessMult} mass={d.MassPerUnit}");
                EqInt(p.ArgCount, 2, "UnitCaliberAtb ctor arity", "unit-caliber");
                Close(m.FirepowerMult, live.FirepowerMult, "UnitCaliberAtb.FirepowerMult", "unit-caliber");
                Close(m.ToughnessMult, live.ToughnessMult, "UnitCaliberAtb.ToughnessMult", "unit-caliber");
                CloseMass(d, p.Mass, "unit-caliber");
            }

            // ---- crew-automation → CrewAutomationAtb(crewReduction).
            {
                var d = Design("default-design-crew-automation");
                Assert.That(d.HasAttribute<CrewAutomationAtb>(), Is.True, "crew-automation binds a CrewAutomationAtb");
                var live = d.GetAttribute<CrewAutomationAtb>();
                var p = new EnhancersDesignModel(EnhancerKind.Systems, EnhancerType.CrewAutomation).Compute();
                var m = (CrewAutomationAtb)p.Attribute;
                Log($"ENH crew-automation: model reduction={m.CrewReduction} mass={p.Mass} | LIVE reduction={live.CrewReduction} mass={d.MassPerUnit}");
                EqInt(p.ArgCount, 1, "CrewAutomationAtb ctor arity", "crew-automation");
                Close(m.CrewReduction, live.CrewReduction, "CrewAutomationAtb.CrewReduction", "crew-automation");
                CloseMass(d, p.Mass, "crew-automation");
            }

            // ---- the four GroundAugment presets — the 5-vs-6-arg split is load-bearing (ward is the only 6-arg).
            AssertAugment("default-design-power-armor",      EnhancerType.PowerArmour,     expectArgs: 5);
            AssertAugment("default-design-shield-generator", EnhancerType.ShieldGenerator, expectArgs: 5);
            AssertAugment("default-design-ward-projector",   EnhancerType.WardProjector,   expectArgs: 6);
            AssertAugment("default-design-reflex-booster",   EnhancerType.ReflexBooster,   expectArgs: 5);

            // ---- ground-training-cadre → GroundTrainingAtb(trainingMultiplier); mass computed from magnitude.
            {
                var d = Design("default-design-ground-training-cadre");
                Assert.That(d.HasAttribute<GroundTrainingAtb>(), Is.True, "ground-training-cadre binds a GroundTrainingAtb");
                var live = d.GetAttribute<GroundTrainingAtb>();
                var p = new EnhancersDesignModel(EnhancerKind.AdvancedTraining, EnhancerType.GroundCadre).Compute();
                var m = (GroundTrainingAtb)p.Attribute;
                Log($"ENH ground-training-cadre: model mult={m.TrainingMultiplier} mass={p.Mass} | LIVE mult={live.TrainingMultiplier} mass={d.MassPerUnit}");
                EqInt(p.ArgCount, 1, "GroundTrainingAtb ctor arity", "ground-training-cadre");
                Close(m.TrainingMultiplier, live.TrainingMultiplier, "GroundTrainingAtb.TrainingMultiplier", "ground-training-cadre");
                CloseMass(d, p.Mass, "ground-training-cadre");
            }

            // ---- sealed-systems → GroundSealAtb(sealing); mass computed from magnitude.
            {
                var d = Design("default-design-sealed-systems");
                Assert.That(d.HasAttribute<GroundSealAtb>(), Is.True, "sealed-systems binds a GroundSealAtb");
                var live = d.GetAttribute<GroundSealAtb>();
                var p = new EnhancersDesignModel(EnhancerKind.Augmentation, EnhancerType.SealedSystems).Compute();
                var m = (GroundSealAtb)p.Attribute;
                Log($"ENH sealed-systems: model sealing={m.Sealing} mass={p.Mass} | LIVE sealing={live.Sealing} mass={d.MassPerUnit}");
                EqInt(p.ArgCount, 1, "GroundSealAtb ctor arity", "sealed-systems");
                Close(m.Sealing, live.Sealing, "GroundSealAtb.Sealing", "sealed-systems");
                CloseMass(d, p.Mass, "sealed-systems");
            }
        }

        private void AssertAugment(string id, EnhancerType enhancer, int expectArgs)
        {
            var d = Design(id);
            Assert.That(d.HasAttribute<GroundAugmentAtb>(), Is.True, $"{id} binds a GroundAugmentAtb");
            var live = d.GetAttribute<GroundAugmentAtb>();
            var p = new EnhancersDesignModel(EnhancerKind.Augmentation, enhancer).Compute();
            var m = (GroundAugmentAtb)p.Attribute;
            Log($"ENH {id}: model carry={m.Mass} str={m.StrengthBonus} ev={m.EvasionBonus} tough={m.ToughnessBonus} shield={m.Shield} regen={m.ShieldRegenFraction} args={p.ArgCount} mass={p.Mass} | " +
                $"LIVE carry={live.Mass} str={live.StrengthBonus} ev={live.EvasionBonus} tough={live.ToughnessBonus} shield={live.Shield} regen={live.ShieldRegenFraction} mass={d.MassPerUnit}");
            EqInt(p.ArgCount, expectArgs, "GroundAugmentAtb ctor arity (5 vs 6 is load-bearing)", id);
            Close(m.Mass, live.Mass, "GroundAugmentAtb.Mass (CarryMass)", id);
            Close(m.StrengthBonus, live.StrengthBonus, "GroundAugmentAtb.StrengthBonus", id);
            Close(m.EvasionBonus, live.EvasionBonus, "GroundAugmentAtb.EvasionBonus", id);
            Close(m.ToughnessBonus, live.ToughnessBonus, "GroundAugmentAtb.ToughnessBonus", id);
            Close(m.Shield, live.Shield, "GroundAugmentAtb.Shield", id);
            Close(m.ShieldRegenFraction, live.ShieldRegenFraction, "GroundAugmentAtb.ShieldRegenFraction", id);
            CloseMass(d, p.Mass, id);
        }
    }
}
