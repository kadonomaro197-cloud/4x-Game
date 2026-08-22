using NUnit.Framework;
using Pulsar4X.Components.Designers;
using Pulsar4X.Sensors;
using Pulsar4X.Weapons;
using Pulsar4X.GeoSurveys;
using Pulsar4X.JumpPoints;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the SENSORS door parametric designer, slice-1 gauge.
    ///
    /// Proves the pure <see cref="SensorsDesignModel"/> (the engine half of the sensors form) REPRODUCES every base-mod
    /// sensor component's produced <c>*Atb</c> from its job choice (+ sub-choice) + dials — every hand-authored sensor
    /// falls out of the one parametric form (the DESIGNER-NORTH-STAR reproduction claim, made executable). Pure (no
    /// colony harness → fast, not the slow CI shard); it constructs the REAL <c>*Atb</c> objects and asserts their real
    /// stored fields + the ctor-arg arity + the resolved mass, so a drift in the model's per-job arithmetic (a swapped
    /// ctor arg, a missing clamp, a wrong tech chain) fails here. Byte-identical to the live game — nothing calls the
    /// model yet.
    ///
    /// THE PURE-GAUGE SCOPE (honest): the Listen sensitivity chain reads antenna efficiency / bandwidth-ceiling /
    /// sensitivity from the faction tech DB, which a pure value type cannot touch — so those are an INPUT
    /// (<see cref="SensorTechEnv.L0"/>, the starting-tech level the base-mod sensors are authored against, verified
    /// against techs.json: efficiency 0.75 · bandwidth 500 · sensitivity 0.01). This gauge therefore proves the FORMULA
    /// reproduction at L0: given each base-mod design's authored dials + L0 tech, the model computes the EXACT <c>*Atb</c>
    /// fields the template chain does — including the L7 antenna clamp (Deep-Space Array 12000 → 2500) and the exact-arity
    /// ctor the template binds (the 7-arg receiver, the 3-arg PD director). Reproduction values verified against
    /// GameData/basemod/TemplateFiles/electronics.json + componentDesigns.json.
    /// </summary>
    [TestFixture]
    public class SensorsDesignModelTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[sensors-model] " + m);

        // ---- LISTEN --------------------------------------------------------------------------------------------------

        // Compute the template's ground-truth best/worst detectable signal (WATTS) for a Listen design at L0 tech, the
        // EXACT arithmetic passive-sensor uses (clamp antenna to [1,2500], effSize = antenna*0.75, eff = 1/(bw/500)).
        private static void ListenSensitivityW(double antenna, double bandwidth, out double bestW, out double worstW)
        {
            double clampedAntenna = antenna < 1 ? 1 : antenna > 2500 ? 2500 : antenna;
            double effSize = clampedAntenna * 0.75;                 // tech-antenna-efficiency
            double eff = 1.0 / (bandwidth / 500.0);                 // tech-antenna-bandwidth
            bestW = 0.01 / (effSize * effSize * eff);               // tech-antenna-sensitivity
            worstW = 0.01 / (effSize * effSize * eff * 0.1);
        }

        private static void AssertReceiver(string name, SensorsDesignModel m,
            double expMinNm, double expAvgNm, double expMaxNm, double expBestW, double expWorstW,
            float expRes, int expScan, double expMass)
        {
            var p = m.Compute(SensorTechEnv.L0);
            var r = p.Receiver;
            Log($"{name}: LISTEN wave=({r.RecevingWaveformCapabilty.WavelengthMin_nm},{r.RecevingWaveformCapabilty.WavelengthAverage_nm},{r.RecevingWaveformCapabilty.WavelengthMax_nm}) bestKw={r.BestSensitivity_kW} worstKw={r.WorstSensitivity_kW} res={r.Resolution} scan={r.ScanTime} mass={p.ComponentMass}");
            Assert.That(p.Job, Is.EqualTo(SensorJob.Listen), $"{name} Job");
            Assert.That(r, Is.Not.Null, $"{name} produced a SensorReceiverAtb");
            Assert.That(p.FireControl, Is.Null, $"{name} no fire-control");
            Assert.That(p.Cloak, Is.Null, $"{name} no cloak");
            Assert.That(p.Jammer, Is.Null, $"{name} no jammer");
            Assert.That(p.GeoSurvey, Is.Null, $"{name} no geo survey");
            Assert.That(p.GravSurvey, Is.Null, $"{name} no grav survey");
            // EXACT ARITY — the passive-sensor template binds the 7-arg horizon-capped ctor (Weapons gotcha #0).
            Assert.That(p.CtorArgs.Length, Is.EqualTo(7), $"{name} 7-arg receiver ctor");
            // waveform (min = peak - bw/2, avg = peak, max = peak + bw/2)
            Assert.That(r.RecevingWaveformCapabilty.WavelengthMin_nm, Is.EqualTo(expMinNm).Within(1e-6), $"{name} waveform min");
            Assert.That(r.RecevingWaveformCapabilty.WavelengthAverage_nm, Is.EqualTo(expAvgNm).Within(1e-6), $"{name} waveform avg");
            Assert.That(r.RecevingWaveformCapabilty.WavelengthMax_nm, Is.EqualTo(expMaxNm).Within(1e-6), $"{name} waveform max");
            // sensitivities: the atb stores WATTS × 0.001 as kW (tiny values → relative tolerance)
            Assert.That(r.BestSensitivity_kW, Is.EqualTo(expBestW * 0.001).Within(1e-6).Percent, $"{name} BestSensitivity_kW");
            Assert.That(r.WorstSensitivity_kW, Is.EqualTo(expWorstW * 0.001).Within(1e-6).Percent, $"{name} WorstSensitivity_kW");
            Assert.That(r.Resolution, Is.EqualTo(expRes), $"{name} Resolution ((float) cast)");
            Assert.That(r.ScanTime, Is.EqualTo(expScan), $"{name} ScanTime ((int) cast)");
            Assert.That(r.MaxDetectionRange_m, Is.EqualTo(SensorsDesignModel.DefaultDetectionHorizon_m).Within(1e-6), $"{name} MaxDetectionRange_m (200 Gm horizon)");
            Assert.That(p.ComponentMass, Is.EqualTo(expMass).Within(1e-6), $"{name} mass");
            // the emergent readouts carried on the profile (raw WATTS)
            Assert.That(p.BestSensitivity_W, Is.EqualTo(expBestW).Within(1e-6).Percent, $"{name} emergent best (W)");
            Assert.That(p.WorstSensitivity_W, Is.EqualTo(expWorstW).Within(1e-6).Percent, $"{name} emergent worst (W)");
        }

        [Test]
        [Description("Every base-mod LISTEN receiver (Passive Scanner / Passive Sensor S50 / Deep-Space Listening Array) falls out of the form: the antenna/bandwidth/tech chain yields the best/worst sensitivity, the 7-arg ctor carries the 200 Gm horizon, and the Deep-Space Array's 12000 antenna CLAMPS to 2500 (the L7 trap) so its mass is 62,590 not 1.44M.")]
        public void ListenReceivers_ReproduceFromTheForm()
        {
            // Passive Scanner: antenna 2500, wave 470, bw 250, res 100, scan 3600.
            ListenSensitivityW(2500, 250, out double b1, out double w1);
            AssertReceiver("passive-scanner",
                SensorsDesignModel.Listen(antennaSize_m2: 2500, idealWavelength_nm: 470, bandwidth_nm: 250, resolution_MP: 100, scanTime_s: 3600),
                expMinNm: 345, expAvgNm: 470, expMaxNm: 595, expBestW: b1, expWorstW: w1,
                expRes: 100f, expScan: 3600, expMass: 90 + 0.01 * 2500 * 2500); // 62590

            // Passive Sensor S50: antenna 5.5, wave 479, bw 200, res 1, scan 3600.
            ListenSensitivityW(5.5, 200, out double b2, out double w2);
            AssertReceiver("passive-sensor-s50",
                SensorsDesignModel.Listen(antennaSize_m2: 5.5, idealWavelength_nm: 479, bandwidth_nm: 200, resolution_MP: 1, scanTime_s: 3600),
                expMinNm: 379, expAvgNm: 479, expMaxNm: 579, expBestW: b2, expWorstW: w2,
                expRes: 1f, expScan: 3600, expMass: 90 + 0.01 * 5.5 * 5.5); // 90.3025

            // Deep-Space Listening Array: antenna 12000 (CLAMPED to 2500), wave 470, bw 300, res 200, scan 3600.
            ListenSensitivityW(12000, 300, out double b3, out double w3);   // helper clamps internally
            AssertReceiver("deep-space-array",
                SensorsDesignModel.Listen(antennaSize_m2: 12000, idealWavelength_nm: 470, bandwidth_nm: 300, resolution_MP: 200, scanTime_s: 3600),
                expMinNm: 320, expAvgNm: 470, expMaxNm: 620, expBestW: b3, expWorstW: w3,
                expRes: 200f, expScan: 3600, expMass: 90 + 0.01 * 2500 * 2500); // clamp governs → 62590, NOT 1.44M
        }

        [Test]
        [Description("The passive-sensor TEMPLATE DEFAULT ('open-on') reproduces, and its best sensitivity at starting tech is the HTML headline anchor 5.689e-6 kW — so a future tech-formula change is a visible, deliberate act.")]
        public void ListenTemplateDefault_HitsTheTechAnchor()
        {
            // template defaults: antenna 1.25, wave 600, bw 250, res 1, scan 3600.
            ListenSensitivityW(1.25, 250, out double bDef, out double wDef);
            AssertReceiver("passive-default",
                SensorsDesignModel.Listen(antennaSize_m2: 1.25, idealWavelength_nm: 600, bandwidth_nm: 250, resolution_MP: 1, scanTime_s: 3600),
                expMinNm: 475, expAvgNm: 600, expMaxNm: 725, expBestW: bDef, expWorstW: wDef,
                expRes: 1f, expScan: 3600, expMass: 90 + 0.01 * 1.25 * 1.25); // 90.015625

            var p = SensorsDesignModel.Listen(1.25, 600, 250, 1, 3600).Compute(SensorTechEnv.L0);
            Log($"tech anchor: BestSensitivity_kW = {p.Receiver.BestSensitivity_kW}");
            Assert.That(p.Receiver.BestSensitivity_kW, Is.EqualTo(5.689e-6).Within(0.01).Percent,
                "the HTML headline anchor: passive default best sensitivity at L0 = 5.689e-6 kW");
        }

        // ---- TRACK ---------------------------------------------------------------------------------------------------

        [Test]
        [Description("The base-mod TRACK directors reproduce: Beam Fire Control (anti-ship, 2-arg ctor, FinalFireOnly false, mass Range + Track/100 = 150) and Point-Defense Director (3-arg ctor, FinalFireOnly true, mass ×1.5 = 315). The sub-choice forces the arity + the mass multiplier.")]
        public void TrackDirectors_ReproduceFromTheForm()
        {
            // Beam Fire Control: Range 100 kkm, Track 5000 km/s.
            var bfc = SensorsDesignModel.Track(SensorDirectorRole.AntiShip, range_kkm: 100, trackingSpeed_kmps: 5000).Compute();
            Log($"beam-fire-control: range={bfc.FireControl.Range} track={bfc.FireControl.TrackingSpeed} finalFire={bfc.FireControl.FinalFireOnly} args={bfc.CtorArgs.Length} mass={bfc.ComponentMass}");
            Assert.That(bfc.Job, Is.EqualTo(SensorJob.Track));
            Assert.That(bfc.FireControl, Is.Not.Null, "produced a BeamFireControlAtbDB");
            Assert.That(bfc.CtorArgs.Length, Is.EqualTo(2), "anti-ship binds the 2-arg ctor");
            Assert.That(bfc.FireControl.Range, Is.EqualTo(100), "Range ((int))");
            Assert.That(bfc.FireControl.TrackingSpeed, Is.EqualTo(5000), "TrackingSpeed ((int))");
            Assert.That(bfc.FireControl.FinalFireOnly, Is.False, "anti-ship is not final-fire-only");
            Assert.That(bfc.ComponentMass, Is.EqualTo(100 + 5000 / 100.0).Within(1e-6), "mass = Range + Track/100 = 150");

            // Point-Defense Director: Range 10 kkm (template default), Track 20000 km/s, FinalFireOnly = 1.
            var pd = SensorsDesignModel.Track(SensorDirectorRole.PointDefense, range_kkm: 10, trackingSpeed_kmps: 20000).Compute();
            Log($"pd-director: range={pd.FireControl.Range} track={pd.FireControl.TrackingSpeed} finalFire={pd.FireControl.FinalFireOnly} args={pd.CtorArgs.Length} mass={pd.ComponentMass}");
            Assert.That(pd.CtorArgs.Length, Is.EqualTo(3), "point-defense binds the 3-arg ctor (finalFireOnly)");
            Assert.That(pd.FireControl.Range, Is.EqualTo(10));
            Assert.That(pd.FireControl.TrackingSpeed, Is.EqualTo(20000));
            Assert.That(pd.FireControl.FinalFireOnly, Is.True, "point-defense IS final-fire-only (CIWS)");
            Assert.That(pd.ComponentMass, Is.EqualTo((10 + 20000 / 100.0) * 1.5).Within(1e-6), "PD mass = (Range + Track/100) * 1.5 = 315");
        }

        // ---- HIDE ----------------------------------------------------------------------------------------------------

        [Test]
        [Description("The base-mod HIDE cloak reproduces: CloakAtb(0.2) → SignatureMultiplier 0.2, mass 200 + 400*(1-0.2) = 520. Also proves the atb ctor clamp (a below-floor multiplier pins to 0.02).")]
        public void HideCloak_ReproducesFromTheForm()
        {
            var cloak = SensorsDesignModel.Hide(signatureMultiplier: 0.2).Compute();
            Log($"cloak: sigMult={cloak.Cloak.SignatureMultiplier} args={cloak.CtorArgs.Length} mass={cloak.ComponentMass}");
            Assert.That(cloak.Job, Is.EqualTo(SensorJob.Hide));
            Assert.That(cloak.Cloak, Is.Not.Null, "produced a CloakAtb");
            Assert.That(cloak.CtorArgs.Length, Is.EqualTo(1), "1-arg ctor");
            Assert.That(cloak.Cloak.SignatureMultiplier, Is.EqualTo(0.2).Within(1e-6));
            Assert.That(cloak.ComponentMass, Is.EqualTo(200 + 400 * (1 - 0.2)).Within(1e-6), "mass = 520");

            // the atb ctor clamps a below-floor multiplier to MinSignatureFactor (0.02).
            var clamped = SensorsDesignModel.Hide(signatureMultiplier: 0.0).Compute();
            Assert.That(clamped.Cloak.SignatureMultiplier, Is.EqualTo(CloakAtb.MinSignatureFactor).Within(1e-9), "below-floor multiplier clamped to 0.02");
        }

        // ---- BLIND ---------------------------------------------------------------------------------------------------

        [Test]
        [Description("The base-mod BLIND jammer reproduces: JammerAtb(4, 1e9, 5) → SensitivityDegrade 4, Range_m 1 Gm (the slider is in Gm, stored in metres ×1e9), SelfSignatureBoost 5, mass 200 + 100*4 + 50*1 = 650.")]
        public void BlindJammer_ReproducesFromTheForm()
        {
            var j = SensorsDesignModel.Blind(sensitivityDegrade: 4, jammerRange_Gm: 1, selfSignatureBoost: 5).Compute();
            Log($"jammer: degrade={j.Jammer.SensitivityDegrade} range_m={j.Jammer.Range_m} boost={j.Jammer.SelfSignatureBoost} args={j.CtorArgs.Length} mass={j.ComponentMass}");
            Assert.That(j.Job, Is.EqualTo(SensorJob.Blind));
            Assert.That(j.Jammer, Is.Not.Null, "produced a JammerAtb");
            Assert.That(j.CtorArgs.Length, Is.EqualTo(3), "3-arg ctor");
            Assert.That(j.Jammer.SensitivityDegrade, Is.EqualTo(4).Within(1e-6));
            Assert.That(j.Jammer.Range_m, Is.EqualTo(1e9).Within(1e-6), "1 Gm slider stored as 1e9 m");
            Assert.That(j.Jammer.SelfSignatureBoost, Is.EqualTo(5).Within(1e-6));
            Assert.That(j.ComponentMass, Is.EqualTo(200 + 100 * 4 + 50 * 1).Within(1e-6), "mass = 650");
        }

        // ---- LOOK ----------------------------------------------------------------------------------------------------

        [Test]
        [Description("The base-mod LOOK surveyors reproduce: Geo Surveyor Mk1 (Geological → GeoSurveyAtb, speed 10) and Gravitational Surveyor Mk1 (Gravitational → GravSurveyAtb, speed 10), each mass (10*10)^2 = 10000. The survey-type sub-choice forces WHICH attribute is produced.")]
        public void LookSurveyors_ReproduceFromTheForm()
        {
            var geo = SensorsDesignModel.Look(SensorSurveyType.Geological, surveySpeed: 10).Compute();
            Log($"geo-surveyor: speed={geo.GeoSurvey.Speed} args={geo.CtorArgs.Length} mass={geo.ComponentMass}");
            Assert.That(geo.Job, Is.EqualTo(SensorJob.Look));
            Assert.That(geo.GeoSurvey, Is.Not.Null, "produced a GeoSurveyAtb");
            Assert.That(geo.GravSurvey, Is.Null, "geological → no grav atb");
            Assert.That(geo.CtorArgs.Length, Is.EqualTo(1), "1-arg ctor");
            Assert.That(geo.GeoSurvey.Speed, Is.EqualTo(10u), "Speed ((uint))");
            Assert.That(geo.ComponentMass, Is.EqualTo(10000).Within(1e-6), "mass = (10*10)^2 = 10000");

            var grav = SensorsDesignModel.Look(SensorSurveyType.Gravitational, surveySpeed: 10).Compute();
            Log($"grav-surveyor: speed={grav.GravSurvey.Speed} args={grav.CtorArgs.Length} mass={grav.ComponentMass}");
            Assert.That(grav.GravSurvey, Is.Not.Null, "produced a GravSurveyAtb");
            Assert.That(grav.GeoSurvey, Is.Null, "gravitational → no geo atb");
            Assert.That(grav.GravSurvey.Speed, Is.EqualTo(10u), "Speed ((uint))");
            Assert.That(grav.ComponentMass, Is.EqualTo(10000).Within(1e-6), "mass = (10*10)^2 = 10000");
        }
    }
}
