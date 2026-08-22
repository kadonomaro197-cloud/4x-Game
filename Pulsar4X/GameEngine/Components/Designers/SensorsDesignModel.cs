using Pulsar4X.Sensors;
using Pulsar4X.Weapons;
using Pulsar4X.GeoSurveys;
using Pulsar4X.JumpPoints;

namespace Pulsar4X.Components.Designers
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the SENSORS door parametric designer, slice 1 (ENGINE MODEL).
    ///
    /// WHAT IT IS, in plain English: the sensors door is "one screen for everything that reads (or hides from) the
    /// spectrum." Instead of a menu of hand-built parts — a passive listening antenna, a geological/gravitational
    /// surveyor, a beam or point-defense fire-control director, a cloak, a barrage jammer — you pick ONE JOB and slide a
    /// couple of dials, and the right sensor component falls out. This class is the ENGINE HALF of that form: a pure
    /// calculator that turns those picks + dials into the exact <c>*Atb</c> (component design attribute) objects the
    /// sensor / fire-control / survey processors read — so a design made through this model behaves IDENTICALLY to a
    /// hand-authored base-mod sensor. The ImGui screen that drives it is a later client slice (CI can compile the client
    /// but can't run it).
    ///
    /// WHY IT'S SHAPED THIS WAY (the derivation, per <c>docs/economy/DESIGNER-NORTH-STAR.md</c>): a "door" is DERIVED
    /// from the numbers the simulation actually reads off a sensor. Group the sensor templates by the question each
    /// answers, split the choices the physics FORCES from the dials the player is FREE to set. For sensors the honest
    /// collapse is NOT "two choices + four sliders" (that was the Weapons result) — it is:
    ///   • CHOICE 1 (always) — <see cref="SensorJob"/> (Listen / Look / Track / Hide / Blind): WHAT the part does with the
    ///     spectrum. FORCED, because each job is a DIFFERENT <c>*Atb</c> class + ability, and it decides which sliders
    ///     even appear. This is the load-bearing choice.
    ///   • CHOICE 2 (only two jobs have one, FORCED):
    ///       – Look → <see cref="SensorSurveyType"/> (Geological → <see cref="GeoSurveyAtb"/>, per-hour, vs Gravitational
    ///         → <see cref="GravSurveyAtb"/>, per-day). Two different attributes, so it is a choice, not a slider.
    ///       – Track → <see cref="SensorDirectorRole"/> (Anti-ship, an ordinary director, vs Point-defense, a CIWS whose
    ///         <c>FinalFireOnly</c> flag routes its beams to missile interception). That flag is a real forced bool.
    ///   • SLIDERS (all FREE, each job-dependent, each writes a real sim variable or the component mass):
    ///       – Listen: antenna size, ideal wavelength, bandwidth, resolution, scan time.
    ///       – Look: survey speed.
    ///       – Track: range, tracking speed.
    ///       – Hide: signature multiplier (concealment).
    ///       – Blind: sensitivity degrade (barrage strength), jam reach, self-signature boost (the beacon catch).
    ///
    /// THE HONEST CAVEATS (load-bearing, flagged for the developer):
    ///   1. TECH IS AN INPUT, NOT A DIAL. The Listen sensitivity chain (effective antenna size, efficiency, best/worst
    ///      detectable signal) reads three tech numbers the engine looks up from the faction tech DB at build time —
    ///      antenna EFFICIENCY, antenna BANDWIDTH ceiling, antenna SENSITIVITY. A pure value type cannot touch that
    ///      store, so this model accepts them as a <see cref="SensorTechEnv"/> input (the slice-2 UI feeds them from the
    ///      same store the game reads; the gauge feeds the starting-tech level, <see cref="SensorTechEnv.L0"/>). That is
    ///      also WHY sensitivity is an EMERGENT readout, never a slider — you cannot set it knowing only the part.
    ///   2. THE ANTENNA CLAMP IS REAL AND BITES. The <c>passive-sensor</c> template caps Antenna Size at 2500 m², and the
    ///      live designer clamps any override to that ceiling at instantiation (the L7 template-clamp gotcha). The
    ///      base-mod Deep-Space Listening Array ships an antenna of 12000 → it is actually BUILT at 2500 (mass 62,590,
    ///      not 1.44 M). This model applies the identical clamp, or a design would diverge from the real component.
    ///   3. THE MODEL SETS DIALS THE UI PROPOSAL WANTS TO CUT. For byte-identical reproduction the model must still write
    ///      Resolution (Listen), Self-Signature-Boost (Blind), and FinalFireOnly (Track PD) — the template carries them
    ///      and the shipped designs set them — even though the design HTML flags them as "free / dead" candidates to
    ///      couple or drop. Reproduce the EXISTING numbers, not the proposed fixes.
    ///
    /// BYTE-IDENTICAL / SAFE (slice-1 rules): this is a NEW file, a pure value type with no serialized state, no new
    /// DataBlob, no <c>*Atb</c> ctor change, no template edit — nothing in the live game calls it yet, so the whole
    /// engine is unchanged. It mirrors the per-job template arithmetic EXACTLY (same tech chain, same mass formulas, the
    /// same 7-arg vs 6-arg / 2-arg vs 3-arg ctor arity the templates bind, the same <c>(float)</c>/<c>(int)</c> casts the
    /// <c>*Atb</c> ctors make) so the gauge <c>SensorsDesignModelTests</c> can prove it reproduces every base-mod sensor.
    /// It constructs the REAL <c>*Atb</c> objects through their REAL constructors, so the gauge reads the same fields the
    /// sim would. It does NOT touch any of the behaviour flags in flight (EnableBandMatchFix / EnableResolutionQuality /
    /// EnableFireControlRange / EnableJamming) — slice 1 reproduces component STATS, never detection/combat behaviour.
    ///
    /// OUT OF SCOPE (deliberate exclusions, per the door census): the EMITTER-side <c>SensorSignatureAtb</c> (owned by
    /// Power/Propulsion/Chassis), the <c>IntelDirectorateAtb</c> (Command door), and the ordnance
    /// <c>missle-electronics-suite</c> <c>SensorReceiverAtb</c> (Weapons/ordnance door) are NOT this door's outputs and
    /// are not modelled here.
    /// </summary>
    public readonly struct SensorsDesignModel
    {
        // ---- CHOICE 1 ----
        /// <summary>CHOICE 1 — WHAT the part does with the spectrum. Forces the produced <c>*Atb</c> type + which sliders apply.</summary>
        public SensorJob Job { get; }

        // ---- CHOICE 2 (job-dependent) ----
        /// <summary>Look only — geological vs gravitational survey (different <c>*Atb</c>).</summary>
        public SensorSurveyType SurveyType { get; }
        /// <summary>Track only — anti-ship director vs point-defense (CIWS) director (the FinalFireOnly bool).</summary>
        public SensorDirectorRole DirectorRole { get; }

        // ---- LISTEN sliders ----
        /// <summary>Listen — antenna area (m²). Clamped to [<see cref="AntennaSizeMin"/>, <see cref="AntennaSizeMax"/>] like
        /// the live designer; sets effective size → best/worst sensitivity, and the component mass (90 + 0.01·size²).</summary>
        public double AntennaSize_m2 { get; }
        /// <summary>Listen — the wavelength (nm) the receiver is tuned to (the waveform average).</summary>
        public double IdealWavelength_nm { get; }
        /// <summary>Listen — how far from the ideal wavelength it still detects (nm) → the waveform ±bw/2 AND the
        /// efficiency term (a wider band is less efficient).</summary>
        public double Bandwidth_nm { get; }
        /// <summary>Listen — resolution (MegaPixels): how much detail a detection resolves.</summary>
        public double Resolution_MP { get; }
        /// <summary>Listen — seconds for a full 360° sweep.</summary>
        public double ScanTime_s { get; }

        // ---- LOOK slider ----
        /// <summary>Look — survey points per hour (geological) / per day (gravitational). Sets the survey Speed + mass ((10·speed)²).</summary>
        public double SurveySpeed { get; }

        // ---- TRACK sliders ----
        /// <summary>Track — director reach (kkm). Stored on the fire-control as an int; drives its mass.</summary>
        public double Range_kkm { get; }
        /// <summary>Track — how fast the director slews (km/s). Stored as an int; drives its mass.</summary>
        public double TrackingSpeed_kmps { get; }

        // ---- HIDE slider ----
        /// <summary>Hide — the fraction of its normal signature a cloaked ship shows (lower = stealthier). Clamped
        /// [<see cref="CloakAtb.MinSignatureFactor"/>, 1] by the atb ctor; mass = 200 + 400·(1 − mult).</summary>
        public double SignatureMultiplier { get; }

        // ---- BLIND sliders ----
        /// <summary>Blind — how much a hostile receiver's usable signal is DIVIDED within range (≥1; the barrage strength).</summary>
        public double SensitivityDegrade { get; }
        /// <summary>Blind — jam reach in GIGAMETRES (the template slider unit). The atb stores it in METRES (×1e9).</summary>
        public double JammerRange_Gm { get; }
        /// <summary>Blind — how much LOUDER the jammer ship runs while active (≥1; the beacon catch).</summary>
        public double SelfSignatureBoost { get; }

        private SensorsDesignModel(SensorJob job, SensorSurveyType surveyType, SensorDirectorRole directorRole,
            double antennaSize_m2, double idealWavelength_nm, double bandwidth_nm, double resolution_MP, double scanTime_s,
            double surveySpeed, double range_kkm, double trackingSpeed_kmps, double signatureMultiplier,
            double sensitivityDegrade, double jammerRange_Gm, double selfSignatureBoost)
        {
            Job = job;
            SurveyType = surveyType;
            DirectorRole = directorRole;
            AntennaSize_m2 = antennaSize_m2;
            IdealWavelength_nm = idealWavelength_nm;
            Bandwidth_nm = bandwidth_nm;
            Resolution_MP = resolution_MP;
            ScanTime_s = scanTime_s;
            SurveySpeed = surveySpeed;
            Range_kkm = range_kkm;
            TrackingSpeed_kmps = trackingSpeed_kmps;
            SignatureMultiplier = signatureMultiplier;
            SensitivityDegrade = sensitivityDegrade;
            JammerRange_Gm = jammerRange_Gm;
            SelfSignatureBoost = selfSignatureBoost;
        }

        // ---- Template-level constants (from GameData/basemod/TemplateFiles/electronics.json) ----

        /// <summary>The hard detection horizon (m) the <c>passive-sensor</c> template passes as the 7th ctor arg (200 Gm)
        /// — a signature-independent reach cap so a colony megasensor can't see the whole system.</summary>
        public const double DefaultDetectionHorizon_m = 200_000_000_000;
        /// <summary>The <c>passive-sensor</c> Antenna Size template Min (m²).</summary>
        public const double AntennaSizeMin = 1;
        /// <summary>The <c>passive-sensor</c> Antenna Size template Max (m²) — the L7 clamp the Deep-Space Array hits.</summary>
        public const double AntennaSizeMax = 2500;
        /// <summary>Listen mass base (kg): mass = <see cref="ListenMassBase"/> + <see cref="ListenMassPerAreaSq"/>·size².</summary>
        public const double ListenMassBase = 90;
        /// <summary>Listen mass coefficient on antenna-size-squared (kg/m⁴).</summary>
        public const double ListenMassPerAreaSq = 0.01;
        /// <summary>The multiplier a receiver's worst-wavelength sensitivity denominator carries (the template's ·0.1),
        /// which makes the worst sensitivity 10× the best.</summary>
        public const double WorstSensitivityDenominatorFactor = 0.1;
        /// <summary>Gigametres → metres, the jammer template's Range unit conversion (·1e9).</summary>
        public const double GmToMetres = 1_000_000_000;
        /// <summary>Point-defense director mass multiplier over an anti-ship director of the same dials (the ·1.5 in pd-director).</summary>
        public const double PdDirectorMassMultiplier = 1.5;

        // ---- Per-job factory helpers (the choice forces the fields; the sliders are free) ----

        /// <summary>
        /// A LISTEN receiver (passive antenna). Produces a <see cref="SensorReceiverAtb"/> through the horizon-capped
        /// 7-arg ctor the <c>passive-sensor</c> template binds. The best/worst detectable signal are EMERGENT — computed
        /// from the antenna size, bandwidth and the tech chain (see <see cref="Compute"/>), never dialled directly.
        /// </summary>
        public static SensorsDesignModel Listen(double antennaSize_m2, double idealWavelength_nm, double bandwidth_nm,
            double resolution_MP, double scanTime_s)
            => new SensorsDesignModel(SensorJob.Listen, default, default, antennaSize_m2, idealWavelength_nm, bandwidth_nm,
                resolution_MP, scanTime_s, 0, 0, 0, 1.0, 1.0, 0, 1.0);

        /// <summary>
        /// A LOOK surveyor. <paramref name="surveyType"/> forces the attribute (<see cref="GeoSurveyAtb"/> per-hour vs
        /// <see cref="GravSurveyAtb"/> per-day). Both carry the same integer survey Speed + mass = (10·speed)².
        /// </summary>
        public static SensorsDesignModel Look(SensorSurveyType surveyType, double surveySpeed)
            => new SensorsDesignModel(SensorJob.Look, surveyType, default, 0, 0, 0, 0, 0,
                surveySpeed, 0, 0, 1.0, 1.0, 0, 1.0);

        /// <summary>
        /// A TRACK fire-control director. <paramref name="role"/> forces the <c>FinalFireOnly</c> bool and the mass
        /// formula (a point-defense CIWS costs 1.5× an anti-ship director of the same dials). Produces a
        /// <see cref="BeamFireControlAtbDB"/> through the 2-arg (anti-ship) or 3-arg (point-defense) ctor.
        /// </summary>
        public static SensorsDesignModel Track(SensorDirectorRole role, double range_kkm, double trackingSpeed_kmps)
            => new SensorsDesignModel(SensorJob.Track, default, role, 0, 0, 0, 0, 0,
                0, range_kkm, trackingSpeed_kmps, 1.0, 1.0, 0, 1.0);

        /// <summary>
        /// A HIDE cloak (signature-damping). Produces a <see cref="CloakAtb"/> (whose ctor clamps the multiplier to
        /// [0.02, 1]); mass = 200 + 400·(1 − multiplier).
        /// </summary>
        public static SensorsDesignModel Hide(double signatureMultiplier)
            => new SensorsDesignModel(SensorJob.Hide, default, default, 0, 0, 0, 0, 0,
                0, 0, 0, signatureMultiplier, 1.0, 0, 1.0);

        /// <summary>
        /// A BLIND barrage jammer. Produces a <see cref="JammerAtb"/> (ctor clamps degrade ≥ 1, range ≥ 0, boost ≥ 1);
        /// the reach slider is in GIGAMETRES, stored on the atb in METRES. Mass = 200 + 100·degrade + 50·range(Gm).
        /// </summary>
        public static SensorsDesignModel Blind(double sensitivityDegrade, double jammerRange_Gm, double selfSignatureBoost)
            => new SensorsDesignModel(SensorJob.Blind, default, default, 0, 0, 0, 0, 0,
                0, 0, 0, 1.0, sensitivityDegrade, jammerRange_Gm, selfSignatureBoost);

        /// <summary>
        /// Build the sensor component's <c>*Atb</c> object + the ctor-arg array the template passes + the resolved
        /// component mass — the same numbers the ComponentDesigner produces for the matching base-mod sensor, so a design
        /// made through this model detects / tracks / hides / blinds / surveys identically. Only the field for the
        /// model's <see cref="Job"/> is populated; the rest are null.
        ///
        /// <paramref name="tech"/> supplies the antenna efficiency / bandwidth-ceiling / sensitivity the Listen chain
        /// reads (the game reads these from the faction tech DB; the gauge feeds <see cref="SensorTechEnv.L0"/>). Only
        /// Listen consults it; the other jobs ignore it.
        /// </summary>
        public SensorProfile Compute(SensorTechEnv tech)
        {
            switch (Job)
            {
                case SensorJob.Listen:
                {
                    // Mirrors the passive-sensor template chain (electronics.json):
                    //   EffectiveSize = clamp(AntennaSize) * tech-antenna-efficiency
                    //   Efficiency    = 1 / (Bandwidth / tech-antenna-bandwidth)
                    //   SensIdeal (W) = tech-antenna-sensitivity / (EffSize² * Efficiency)
                    //   SensWorst (W) = tech-antenna-sensitivity / (EffSize² * Efficiency * 0.1)   ( = SensIdeal * 10 )
                    //   Mass          = 90 + 0.01 * clamp(AntennaSize)²
                    //   AtbConstrArgs = (Wavelength, Bandwidth, SensIdeal, SensWorst, Resolution, ScanTime, 2e11)
                    double antenna = Clamp(AntennaSize_m2, AntennaSizeMin, AntennaSizeMax);   // the L7 clamp — bites the Deep-Space Array
                    double bandwidth = Clamp(Bandwidth_nm, 1, tech.AntennaBandwidthMax);      // template Max is tech-driven
                    double effSize = antenna * tech.AntennaEfficiency;
                    double efficiency = 1.0 / (bandwidth / tech.AntennaBandwidthMax);
                    double bestSensWatts = tech.AntennaSensitivity / (effSize * effSize * efficiency);
                    double worstSensWatts = tech.AntennaSensitivity / (effSize * effSize * efficiency * WorstSensitivityDenominatorFactor);

                    var args = new object[]
                    {
                        IdealWavelength_nm, bandwidth, bestSensWatts, worstSensWatts, Resolution_MP, ScanTime_s,
                        DefaultDetectionHorizon_m
                    };
                    var receiver = new SensorReceiverAtb(IdealWavelength_nm, bandwidth, bestSensWatts, worstSensWatts,
                        Resolution_MP, ScanTime_s, DefaultDetectionHorizon_m);
                    double mass = ListenMassBase + ListenMassPerAreaSq * antenna * antenna;

                    return SensorProfile.ForReceiver(receiver, args, mass, bestSensWatts, worstSensWatts);
                }

                case SensorJob.Look:
                {
                    // Mirrors geo-surveyor / gravitational-surveyor: AtbConstrArgs(Survey Speed); mass = (10*speed)².
                    var args = new object[] { SurveySpeed };
                    double mass = Pow2(10 * SurveySpeed);
                    if (SurveyType == SensorSurveyType.Gravitational)
                    {
                        var grav = new GravSurveyAtb((int)SurveySpeed);
                        return SensorProfile.ForGravSurvey(grav, args, mass);
                    }
                    var geo = new GeoSurveyAtb((int)SurveySpeed);
                    return SensorProfile.ForGeoSurvey(geo, args, mass);
                }

                case SensorJob.Track:
                {
                    // Mirrors beam-fire-control (2-arg) / pd-director (3-arg, finalFireOnly=1):
                    //   anti-ship mass = Range + TrackingSpeed/100 ; PD mass = (Range + TrackingSpeed/100) * 1.5
                    double coreMass = Range_kkm + TrackingSpeed_kmps / 100.0;
                    if (DirectorRole == SensorDirectorRole.PointDefense)
                    {
                        var args = new object[] { Range_kkm, TrackingSpeed_kmps, 1.0 };
                        var pd = new BeamFireControlAtbDB(Range_kkm, TrackingSpeed_kmps, 1.0);
                        return SensorProfile.ForFireControl(pd, args, coreMass * PdDirectorMassMultiplier);
                    }
                    else
                    {
                        var args = new object[] { Range_kkm, TrackingSpeed_kmps };
                        var fc = new BeamFireControlAtbDB(Range_kkm, TrackingSpeed_kmps);
                        return SensorProfile.ForFireControl(fc, args, coreMass);
                    }
                }

                case SensorJob.Hide:
                {
                    // Mirrors cloak-device: AtbConstrArgs(Signature Multiplier); mass = 200 + 400*(1 - mult).
                    var args = new object[] { SignatureMultiplier };
                    var cloak = new CloakAtb(SignatureMultiplier);
                    double mass = 200 + 400 * (1 - SignatureMultiplier);
                    return SensorProfile.ForCloak(cloak, args, mass);
                }

                case SensorJob.Blind:
                default:
                {
                    // Mirrors jammer: AtbConstrArgs(Sensitivity Degrade, Range*1e9, Self Signature Boost);
                    //   mass = 200 + 100*Degrade + 50*Range(Gm).
                    double range_m = JammerRange_Gm * GmToMetres;
                    var args = new object[] { SensitivityDegrade, range_m, SelfSignatureBoost };
                    var jammer = new JammerAtb(SensitivityDegrade, range_m, SelfSignatureBoost);
                    double mass = 200 + 100 * SensitivityDegrade + 50 * JammerRange_Gm;
                    return SensorProfile.ForJammer(jammer, args, mass);
                }
            }
        }

        /// <summary>Convenience overload using the starting-tech baseline (<see cref="SensorTechEnv.L0"/>) — the tech
        /// level the base-mod sensors are authored against.</summary>
        public SensorProfile Compute() => Compute(SensorTechEnv.L0);

        private static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
        private static double Pow2(double v) => v * v;
    }

    /// <summary>CHOICE 1 for the sensors door — WHAT the part does with the spectrum. There is no pre-existing engine
    /// enum for this (a job is encoded only as the produced <c>*Atb</c> type + ability DB), so it is defined here; it is
    /// NOT a duplicate of any existing engine type.</summary>
    public enum SensorJob
    {
        /// <summary>Passive receiver → <see cref="SensorReceiverAtb"/> (detect emissions / reflections).</summary>
        Listen = 0,
        /// <summary>Surveyor → <see cref="GeoSurveyAtb"/> / <see cref="GravSurveyAtb"/> (survey a body / find jump points).</summary>
        Look = 1,
        /// <summary>Fire-control director → <see cref="BeamFireControlAtbDB"/> (aim beams; anti-ship or point-defense).</summary>
        Track = 2,
        /// <summary>Cloak → <see cref="CloakAtb"/> (damp your own emitted signature).</summary>
        Hide = 3,
        /// <summary>Barrage jammer → <see cref="JammerAtb"/> (blind hostile receivers).</summary>
        Blind = 4
    }

    /// <summary>CHOICE 2 for the Look job — which SURVEY the part performs. Forced because each is a different attribute
    /// (geological reads a body's minerals per hour; gravitational finds jump points per day).</summary>
    public enum SensorSurveyType
    {
        /// <summary>Geological survey → <see cref="GeoSurveyAtb"/> (per hour).</summary>
        Geological = 0,
        /// <summary>Gravitational (jump-point) survey → <see cref="GravSurveyAtb"/> (per day).</summary>
        Gravitational = 1
    }

    /// <summary>CHOICE 2 for the Track job — the director's ROLE. Forced because point-defense sets the
    /// <c>FinalFireOnly</c> bool (routing its beams to missile interception) and carries a heavier mass formula.</summary>
    public enum SensorDirectorRole
    {
        /// <summary>An ordinary anti-ship director (FinalFireOnly = false).</summary>
        AntiShip = 0,
        /// <summary>A point-defense / CIWS director (FinalFireOnly = true, mass ×1.5).</summary>
        PointDefense = 1
    }

    /// <summary>
    /// The tech values the Listen sensitivity chain reads — the game looks these up from the faction tech DB at build
    /// time; a pure model cannot, so they are supplied here. Only the Listen job consults them.
    /// </summary>
    public readonly struct SensorTechEnv
    {
        /// <summary><c>tech-antenna-efficiency</c> — fraction of the antenna area that is effective (L0 = 0.75).</summary>
        public double AntennaEfficiency { get; }
        /// <summary><c>tech-antenna-bandwidth</c> — the detection-bandwidth ceiling (nm) AND the efficiency reference (L0 = 500).</summary>
        public double AntennaBandwidthMax { get; }
        /// <summary><c>tech-antenna-sensitivity</c> — the numerator of the best/worst detectable-signal formula (L0 = 0.01).</summary>
        public double AntennaSensitivity { get; }

        public SensorTechEnv(double antennaEfficiency, double antennaBandwidthMax, double antennaSensitivity)
        {
            AntennaEfficiency = antennaEfficiency;
            AntennaBandwidthMax = antennaBandwidthMax;
            AntennaSensitivity = antennaSensitivity;
        }

        /// <summary>The starting-tech baseline (techs.json) the base-mod sensors are authored against:
        /// efficiency 0.75, bandwidth 500, sensitivity 0.01.</summary>
        public static SensorTechEnv L0 => new SensorTechEnv(0.75, 500, 0.01);
    }

    /// <summary>
    /// The output of <see cref="SensorsDesignModel.Compute(SensorTechEnv)"/> — the REAL <c>*Atb</c> object the sensor /
    /// fire-control / survey processors read, plus the ctor-arg array the template passes and the resolved component
    /// mass (so a gauge can assert them without re-deriving). Only the field for the model's <see cref="Job"/> is
    /// populated; the rest are null.
    /// </summary>
    public readonly struct SensorProfile
    {
        /// <summary>Which job produced this profile.</summary>
        public SensorJob Job { get; }
        /// <summary>The passive receiver (non-null only for <see cref="SensorJob.Listen"/>).</summary>
        public SensorReceiverAtb Receiver { get; }
        /// <summary>The fire-control director (non-null only for <see cref="SensorJob.Track"/>).</summary>
        public BeamFireControlAtbDB FireControl { get; }
        /// <summary>The cloak (non-null only for <see cref="SensorJob.Hide"/>).</summary>
        public CloakAtb Cloak { get; }
        /// <summary>The barrage jammer (non-null only for <see cref="SensorJob.Blind"/>).</summary>
        public JammerAtb Jammer { get; }
        /// <summary>The geological surveyor (non-null only for Look + Geological).</summary>
        public GeoSurveyAtb GeoSurvey { get; }
        /// <summary>The gravitational surveyor (non-null only for Look + Gravitational).</summary>
        public GravSurveyAtb GravSurvey { get; }
        /// <summary>The ctor-arg array the template's <c>AtbConstrArgs(...)</c> passes — element-by-element, so a gauge can
        /// prove exact arity + values (7 for Listen, 2/3 for Track, 1 for Hide, 3 for Blind, 1 for Look).</summary>
        public object[] CtorArgs { get; }
        /// <summary>The resolved component mass (kg).</summary>
        public double ComponentMass { get; }
        /// <summary>Listen only — the emergent best detectable signal (WATTS), the raw value passed to the ctor (the atb
        /// stores it ×0.001 as <see cref="SensorReceiverAtb.BestSensitivity_kW"/>). 0 for other jobs.</summary>
        public double BestSensitivity_W { get; }
        /// <summary>Listen only — the emergent worst detectable signal (WATTS), the raw value passed to the ctor. 0 for other jobs.</summary>
        public double WorstSensitivity_W { get; }

        private SensorProfile(SensorJob job, SensorReceiverAtb receiver, BeamFireControlAtbDB fireControl, CloakAtb cloak,
            JammerAtb jammer, GeoSurveyAtb geoSurvey, GravSurveyAtb gravSurvey, object[] ctorArgs, double componentMass,
            double bestSensitivity_W, double worstSensitivity_W)
        {
            Job = job;
            Receiver = receiver;
            FireControl = fireControl;
            Cloak = cloak;
            Jammer = jammer;
            GeoSurvey = geoSurvey;
            GravSurvey = gravSurvey;
            CtorArgs = ctorArgs;
            ComponentMass = componentMass;
            BestSensitivity_W = bestSensitivity_W;
            WorstSensitivity_W = worstSensitivity_W;
        }

        internal static SensorProfile ForReceiver(SensorReceiverAtb r, object[] args, double mass, double bestW, double worstW)
            => new SensorProfile(SensorJob.Listen, r, null, null, null, null, null, args, mass, bestW, worstW);
        internal static SensorProfile ForFireControl(BeamFireControlAtbDB fc, object[] args, double mass)
            => new SensorProfile(SensorJob.Track, null, fc, null, null, null, null, args, mass, 0, 0);
        internal static SensorProfile ForCloak(CloakAtb c, object[] args, double mass)
            => new SensorProfile(SensorJob.Hide, null, null, c, null, null, null, args, mass, 0, 0);
        internal static SensorProfile ForJammer(JammerAtb j, object[] args, double mass)
            => new SensorProfile(SensorJob.Blind, null, null, null, j, null, null, args, mass, 0, 0);
        internal static SensorProfile ForGeoSurvey(GeoSurveyAtb g, object[] args, double mass)
            => new SensorProfile(SensorJob.Look, null, null, null, null, g, null, args, mass, 0, 0);
        internal static SensorProfile ForGravSurvey(GravSurveyAtb g, object[] args, double mass)
            => new SensorProfile(SensorJob.Look, null, null, null, null, null, g, args, mass, 0, 0);
    }
}
