using Pulsar4X.Movement;
using Pulsar4X.Sensors;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Components.Designers
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the PROPULSION door parametric designer, slice 1 (ENGINE MODEL).
    ///
    /// WHAT IT IS, in plain English: propulsion is "one door for everything that MOVES" — a chemical rocket, a nuclear
    /// thermal engine, an antimatter drive, a warp (FTL) drive, an exotic reactionless drive, and a ground vehicle's
    /// locomotion. Instead of six separate screens, you pick ONE family and slide a couple of dials, and the right
    /// engine component falls out. This class is the ENGINE HALF of that form: a pure calculator that turns those picks
    /// + dials into the exact <c>*Atb</c> (component design attribute) objects the movement + sensor processors read —
    /// so a design made through this model behaves IDENTICALLY to a hand-authored base-mod engine. The ImGui screen
    /// that drives it is a later client slice (CI can compile the client but can't run it).
    ///
    /// WHY IT'S SHAPED THIS WAY (the derivation, per <c>docs/economy/DESIGNER-NORTH-STAR.md</c>): a "door" is DERIVED
    /// from the numbers the simulation actually reads off an engine. Group the engine templates by the question each
    /// answers, split the choices the physics FORCES from the dials the player is FREE to set:
    ///   • CHOICE 1 — <see cref="PropulsionFamily"/> (Reaction / Surface / WarpFtl / Reactionless): which KIND of drive.
    ///     It FORCES which <c>*Atb</c> type is produced and the per-family arithmetic. (The three reaction templates —
    ///     chemical / nuclear-thermal / antimatter — are ONE family; they differ only in the burn coefficient + the
    ///     exhaust-velocity tech add, which the FUEL choice picks, so fuel is choice 2 for the reaction family.)
    ///   • SLIDER 1 — DRIVE MASS (kg): the always-live size dial. On a reaction drive it sets the fuel-burn rate
    ///     (Mass × coefficient × grade); on a warp drive it sets the engine power (EvP × Mass × 1000); on a
    ///     reactionless drive it is the mass floor.
    ///   • SLIDER 2 — THE SPLIT (zero-sum). On a warp drive this ships today as "Startup vs Endurance" (SvE): creation
    ///     cost ×= SvE, sustain cost ÷= SvE, so neither end is free. (A reaction push↔economy split is a PROPOSED
    ///     slice-2 dial; at its neutral default the arithmetic here reduces to the shipped formula, byte-identical.)
    ///   • SLIDER 3 — REACH/POWER. On a warp drive this ships as "Efficiency vs Power" (EvP) → engine power; on a
    ///     reactionless drive it is the direct Thrust (N).
    ///
    /// THE HONEST CAVEAT (load-bearing, flagged for the developer): the reaction/warp arithmetic reads numbers the
    /// engine looks up from the faction DATA STORE at build time — a fuel's exhaust velocity + grade (off the material
    /// blueprint) and the tech adds/mults (off the faction tech DB). A pure value type CANNOT read those, so this model
    /// accepts them as INPUTS (the slice-2 UI feeds them from the same store the game reads). That means the model
    /// reproduces the FORMULA exactly given the same inputs; a byte-identical match to a LIVE game's numbers is only
    /// guaranteed when the caller supplies that game's fuel/tech values (the non-pure colony-harness gauge's job).
    ///
    /// DOOR BOUNDARY (deliberate exclusion): the <c>inertialess-drive</c> ships in <c>engines.json</c>, but its attribute
    /// (<c>Pulsar4X.Combat.InertialessDriveAtb</c>) is a DEFENSIVE evasion floor, not thrust — per the spec it belongs to
    /// the Defense door, not Propulsion. This model does NOT fold it into the movement collapse.
    ///
    /// BYTE-IDENTICAL / SAFE (slice-1 rules): this is a NEW file, a pure value type with no serialized state, no new
    /// DataBlob, no <c>*Atb</c> ctor change — nothing in the live game calls it yet, so the whole engine is unchanged. It
    /// mirrors the per-family template arithmetic EXACTLY (same coefficients, same formulas, same ctor arg order, the
    /// same <c>(int)</c> cast <see cref="WarpDriveAtb"/> makes on its power) so the gauge <c>PropulsionDesignModelTests</c>
    /// can prove it reproduces every base-mod propulsion component. It constructs the REAL <c>*Atb</c> objects through
    /// their REAL constructors, so the gauge reads the same fields the sim would.
    /// </summary>
    public readonly struct PropulsionDesignModel
    {
        // ---- CHOICE 1 ----
        /// <summary>CHOICE 1 — which KIND of drive. Forces the produced <c>*Atb</c> type + the per-family arithmetic.</summary>
        public PropulsionFamily Family { get; }

        // ---- Common slider ----
        /// <summary>SLIDER 1 — drive mass (kg). Reaction: sets fuel-burn rate. Warp: sets engine power. Reactionless:
        /// the mass floor. Surface: unused (surface has no mass dial in this model).</summary>
        public double DriveMass { get; }

        // ---- Reaction family (choice 2 = fuel; the fuel choice supplies these) ----
        /// <summary>The fuel material id written verbatim into <see cref="NewtonionThrustAtb.FuelType"/>.</summary>
        public string FuelType { get; }
        /// <summary>Reaction: the fuel's base exhaust velocity (m/s), i.e. <c>ExhaustVelocityLookup(fuel)</c>.</summary>
        public double ExhaustVelocityBase { get; }
        /// <summary>Reaction: the fuel's grade, i.e. <c>FuelGradeLookup(fuel)</c> (a mass-flow multiplier).</summary>
        public double FuelGrade { get; }
        /// <summary>Reaction: the burn coefficient — a per-family template constant (chemical 0.3, nuclear/antimatter 0.017).</summary>
        public double BurnCoefficient { get; }
        /// <summary>Reaction: exhaust-velocity tech add (chemical adds the beam-EV tech; nuclear adds tech×250; antimatter 0).</summary>
        public double ExhaustVelocityTechAdd { get; }
        /// <summary>Reaction: burn-rate tech multiplier (chemical multiplies by the fuel-consumption tech; nuclear/antimatter 1.0).</summary>
        public double BurnRateTechMult { get; }

        // ---- Warp family ----
        /// <summary>Warp: "Efficiency vs Power" (EvP) — the reach/power slider (default 1.0). Feeds engine power = EvP × Mass × 1000.</summary>
        public double Evp { get; }
        /// <summary>Warp: "Startup vs Endurance" (SvE) — the zero-sum split (default 1.0). creation ×= SvE, sustain ÷= SvE.</summary>
        public double Sve { get; }
        /// <summary>Warp: <c>tech-alcubierre-bubble-creation-cost</c>.</summary>
        public double WarpCreationCostTech { get; }
        /// <summary>Warp: <c>tech-alcubierre-sustain-cost</c>.</summary>
        public double WarpSustainCostTech { get; }
        /// <summary>Warp: <c>tech-alcubierre-collapse-efficiency</c>.</summary>
        public double WarpCollapseEfficiencyTech { get; }

        // ---- Reactionless family ----
        /// <summary>Reactionless: thrust (N), set directly (no propellant). SLIDER 3 for this family.</summary>
        public double Thrust { get; }

        // ---- Surface family ----
        /// <summary>Surface: speed multiplier on march time (the benefit dial).</summary>
        public double SpeedFactor { get; }
        /// <summary>Surface: rough-terrain handling 0..1.</summary>
        public double RoughHandling { get; }
        /// <summary>Surface: can it cross water.</summary>
        public bool Amphibious { get; }

        /// <summary>Full constructor — private; use the per-family factory helpers, which mirror "the choice forces the
        /// per-family fields, the sliders are free."</summary>
        private PropulsionDesignModel(PropulsionFamily family, double driveMass, string fuelType,
            double exhaustVelocityBase, double fuelGrade, double burnCoefficient, double exhaustVelocityTechAdd,
            double burnRateTechMult, double evp, double sve, double warpCreationCostTech, double warpSustainCostTech,
            double warpCollapseEfficiencyTech, double thrust, double speedFactor, double roughHandling, bool amphibious)
        {
            Family = family;
            DriveMass = driveMass;
            FuelType = fuelType;
            ExhaustVelocityBase = exhaustVelocityBase;
            FuelGrade = fuelGrade;
            BurnCoefficient = burnCoefficient;
            ExhaustVelocityTechAdd = exhaustVelocityTechAdd;
            BurnRateTechMult = burnRateTechMult;
            Evp = evp;
            Sve = sve;
            WarpCreationCostTech = warpCreationCostTech;
            WarpSustainCostTech = warpSustainCostTech;
            WarpCollapseEfficiencyTech = warpCollapseEfficiencyTech;
            Thrust = thrust;
            SpeedFactor = speedFactor;
            RoughHandling = roughHandling;
            Amphibious = amphibious;
        }

        /// <summary>Fixed exhaust velocity (m/s) a reactionless drive carries for the future in-space burn model
        /// (unused by delta-V) — the template's literal second <see cref="ReactionlessThrustAtb"/> ctor arg.</summary>
        public const double ReactionlessExhaustVelocity = 1000000;
        /// <summary>The mass floor baseline (kg) for a reactionless drive — thrust above 200,000 N raises the floor.</summary>
        public const double ReactionlessMassFloorBase = 5000;
        /// <summary>The reactionless thrust baseline (N) above which extra thrust adds mass floor.</summary>
        public const double ReactionlessThrustBaseline = 200000;
        /// <summary>Kilograms of mass floor added per Newton of thrust above the baseline (= 1/200).</summary>
        public const double ReactionlessMassPerNewton = 1.0 / 200.0;
        /// <summary>Every reaction/warp engine radiates in this thermal band (K) — the template's <see cref="SensorSignatureAtb"/> first arg.</summary>
        public const double SignatureBandKelvin = 3500;

        /// <summary>
        /// A REACTION drive (chemical / nuclear-thermal / antimatter). The FUEL choice supplies the fuel's base exhaust
        /// velocity + grade (read from the material blueprint in the live game) and the per-family burn coefficient +
        /// tech terms. Produces a <see cref="NewtonionThrustAtb"/> + its paired thrust <see cref="SensorSignatureAtb"/>.
        /// </summary>
        public static PropulsionDesignModel Reaction(string fuelType, double driveMass, double exhaustVelocityBase,
            double fuelGrade, double burnCoefficient, double exhaustVelocityTechAdd = 0, double burnRateTechMult = 1.0)
            => new PropulsionDesignModel(PropulsionFamily.Reaction, driveMass, fuelType, exhaustVelocityBase, fuelGrade,
                burnCoefficient, exhaustVelocityTechAdd, burnRateTechMult, 1.0, 1.0, 0, 0, 0, 0, 0, 0, false);

        /// <summary>
        /// A WARP (FTL) drive. <paramref name="evp"/> is the reach/power slider (default 1.0), <paramref name="sve"/> the
        /// zero-sum startup-vs-endurance split (default 1.0). The three tech terms come from the faction tech DB. Produces
        /// a <see cref="WarpDriveAtb"/> (its power stored as <c>(int)</c>) + a sustain-cost <see cref="SensorSignatureAtb"/>.
        /// </summary>
        public static PropulsionDesignModel WarpFtl(double driveMass, double warpCreationCostTech, double warpSustainCostTech,
            double warpCollapseEfficiencyTech, double evp = 1.0, double sve = 1.0)
            => new PropulsionDesignModel(PropulsionFamily.WarpFtl, driveMass, "electricity", 0, 0, 0, 0, 1.0, evp, sve,
                warpCreationCostTech, warpSustainCostTech, warpCollapseEfficiencyTech, 0, 0, 0, false);

        /// <summary>
        /// A REACTIONLESS (exotic, no-propellant) drive. Produces a <see cref="ReactionlessThrustAtb"/>; NO
        /// <see cref="SensorSignatureAtb"/> (the template carries none). The component mass is a floor:
        /// <c>max(driveMass, 5000 + max(0, thrust − 200000)/200)</c>.
        /// </summary>
        public static PropulsionDesignModel Reactionless(double thrust = 200000, double driveMass = 5000)
            => new PropulsionDesignModel(PropulsionFamily.Reactionless, driveMass, "", 0, 0, 0, 0, 1.0, 1.0, 1.0,
                0, 0, 0, thrust, 0, 0, false);

        /// <summary>
        /// A SURFACE (ground vehicle) locomotion. Produces a <see cref="GroundLocomotionAtb"/> (whose ctor clamps
        /// speedFactor to ≥0.1, roughHandling to [0,1], and treats amphibious ≥0.5 as true); NO signature.
        /// </summary>
        public static PropulsionDesignModel Surface(double speedFactor, double roughHandling, bool amphibious = false)
            => new PropulsionDesignModel(PropulsionFamily.Surface, 0, "", 0, 0, 0, 0, 1.0, 1.0, 1.0, 0, 0, 0, 0,
                speedFactor, roughHandling, amphibious);

        /// <summary>
        /// Build the propulsion component's <c>*Atb</c> objects + the derived scalars the sim computes — the same numbers
        /// the ComponentDesigner produces for the matching base-mod engine, so a design made through this model moves
        /// identically. Only the fields relevant to <see cref="Family"/> are populated; the rest are null / 0.
        /// </summary>
        public PropulsionProfile Compute()
        {
            switch (Family)
            {
                case PropulsionFamily.Reaction:
                {
                    // Mirrors conventional-engine / scntr-engine / antimatter-engine:
                    //   EV  = ExhaustVelocityLookup(fuel) + techAdd
                    //   FBR = Mass * coef * burnRateTechMult * FuelGradeLookup(fuel)
                    //   Thrust = EV * FBR ; SensorSignature(3500, Thrust)
                    double ev = ExhaustVelocityBase + ExhaustVelocityTechAdd;
                    double fbr = DriveMass * BurnCoefficient * BurnRateTechMult * FuelGrade;
                    double thrust = ev * fbr;
                    var reaction = new NewtonionThrustAtb(ev, FuelType, fbr);
                    var sig = new SensorSignatureAtb(SignatureBandKelvin, thrust);
                    return new PropulsionProfile(Family, reaction, null, null, null, sig, DriveMass,
                        exhaustVelocity: ev, fuelBurnRate: fbr, thrust: thrust,
                        enginePower: 0, bubbleCreationCost: 0, bubbleSustainCost: 0, bubbleCollapseCost: 0);
                }

                case PropulsionFamily.WarpFtl:
                {
                    // Mirrors alcubierre-warp-drive:
                    //   EnginePower = EvP * Mass * 1000
                    //   creation = EnginePower * creationTech * 0.5 * SvE
                    //   sustain  = EnginePower * 0.001 * sustainTech / SvE
                    //   collapse = creation * collapseTech            (stored NEGATIVE)
                    //   SensorSignature(3500, sustain * 1000)
                    double enginePower = Evp * DriveMass * 1000;
                    double creation = enginePower * WarpCreationCostTech * 0.5 * Sve;
                    double sustain = enginePower * 0.001 * WarpSustainCostTech / Sve;
                    double collapse = creation * WarpCollapseEfficiencyTech;
                    var warp = new WarpDriveAtb(enginePower, "electricity", creation, sustain, -collapse);
                    var sig = new SensorSignatureAtb(SignatureBandKelvin, sustain * 1000);
                    return new PropulsionProfile(Family, null, warp, null, null, sig, DriveMass,
                        exhaustVelocity: 0, fuelBurnRate: 0, thrust: 0,
                        enginePower: enginePower, bubbleCreationCost: creation, bubbleSustainCost: sustain,
                        bubbleCollapseCost: -collapse);
                }

                case PropulsionFamily.Reactionless:
                {
                    // Mirrors reactionless-drive: ReactionlessThrustAtb(thrust, 1e6); no signature.
                    // Component mass is a floor that rises above the 200,000 N baseline.
                    var rl = new ReactionlessThrustAtb(Thrust, ReactionlessExhaustVelocity);
                    double mass = System.Math.Max(DriveMass,
                        ReactionlessMassFloorBase + System.Math.Max(0, Thrust - ReactionlessThrustBaseline) * ReactionlessMassPerNewton);
                    return new PropulsionProfile(Family, null, null, rl, null, null, mass,
                        exhaustVelocity: ReactionlessExhaustVelocity, fuelBurnRate: 0, thrust: Thrust,
                        enginePower: 0, bubbleCreationCost: 0, bubbleSustainCost: 0, bubbleCollapseCost: 0);
                }

                case PropulsionFamily.Surface:
                default:
                {
                    // Mirrors ground-locomotion: GroundLocomotionAtb(speedFactor, roughHandling, amphibious); no signature.
                    var loco = new GroundLocomotionAtb(SpeedFactor, RoughHandling, Amphibious ? 1.0 : 0.0);
                    return new PropulsionProfile(Family, null, null, null, loco, null, 0,
                        exhaustVelocity: 0, fuelBurnRate: 0, thrust: 0,
                        enginePower: 0, bubbleCreationCost: 0, bubbleSustainCost: 0, bubbleCollapseCost: 0);
                }
            }
        }
    }

    /// <summary>CHOICE 1 for the propulsion door — which KIND of drive. There is no pre-existing engine enum for this
    /// (the family is encoded only as the <c>*Atb</c> type + template), so this is defined here; it is NOT a duplicate
    /// of any existing engine type.</summary>
    public enum PropulsionFamily
    {
        /// <summary>A reaction rocket (chemical / nuclear-thermal / antimatter) → <see cref="NewtonionThrustAtb"/>.</summary>
        Reaction = 0,
        /// <summary>A ground vehicle drive → <see cref="GroundLocomotionAtb"/>.</summary>
        Surface = 1,
        /// <summary>A warp (FTL) drive → <see cref="WarpDriveAtb"/>.</summary>
        WarpFtl = 2,
        /// <summary>An exotic no-propellant drive → <see cref="ReactionlessThrustAtb"/>.</summary>
        Reactionless = 3
    }

    /// <summary>
    /// The output of <see cref="PropulsionDesignModel.Compute"/> — the REAL <c>*Atb</c> objects the movement + sensor
    /// processors read, plus the derived scalars the templates compute (so a gauge can assert them without re-deriving).
    /// Only the fields for the model's <see cref="Family"/> are populated; the rest are null / 0.
    /// </summary>
    public readonly struct PropulsionProfile
    {
        /// <summary>Which family produced this profile.</summary>
        public PropulsionFamily Family { get; }
        /// <summary>The reaction-drive attribute (non-null only for <see cref="PropulsionFamily.Reaction"/>).</summary>
        public NewtonionThrustAtb Reaction { get; }
        /// <summary>The warp-drive attribute (non-null only for <see cref="PropulsionFamily.WarpFtl"/>).</summary>
        public WarpDriveAtb Warp { get; }
        /// <summary>The reactionless-drive attribute (non-null only for <see cref="PropulsionFamily.Reactionless"/>).</summary>
        public ReactionlessThrustAtb Reactionless { get; }
        /// <summary>The ground-locomotion attribute (non-null only for <see cref="PropulsionFamily.Surface"/>).</summary>
        public GroundLocomotionAtb Surface { get; }
        /// <summary>The paired thermal signature every reaction/warp engine emits (null for reactionless/surface — those
        /// templates carry no signature).</summary>
        public SensorSignatureAtb Signature { get; }
        /// <summary>The resolved component mass (kg): the drive mass, except a reactionless drive's mass FLOOR.</summary>
        public double ComponentMass { get; }
        /// <summary>Derived: exhaust velocity (m/s) — reaction & reactionless.</summary>
        public double ExhaustVelocity { get; }
        /// <summary>Derived: fuel-burn rate (kg/s) — reaction only.</summary>
        public double FuelBurnRate { get; }
        /// <summary>Derived: thrust (N) — reaction (EV × FBR) & reactionless (direct).</summary>
        public double Thrust { get; }
        /// <summary>Derived: warp engine power — the pre-cast value; <see cref="WarpDriveAtb.WarpPower"/> holds its <c>(int)</c>.</summary>
        public double EnginePower { get; }
        /// <summary>Derived: warp bubble creation cost.</summary>
        public double BubbleCreationCost { get; }
        /// <summary>Derived: warp bubble sustain cost (the signature magnitude is this × 1000).</summary>
        public double BubbleSustainCost { get; }
        /// <summary>Derived: warp bubble collapse energy returned (stored NEGATIVE, mirroring the template).</summary>
        public double BubbleCollapseCost { get; }

        public PropulsionProfile(PropulsionFamily family, NewtonionThrustAtb reaction, WarpDriveAtb warp,
            ReactionlessThrustAtb reactionless, GroundLocomotionAtb surface, SensorSignatureAtb signature,
            double componentMass, double exhaustVelocity, double fuelBurnRate, double thrust, double enginePower,
            double bubbleCreationCost, double bubbleSustainCost, double bubbleCollapseCost)
        {
            Family = family;
            Reaction = reaction;
            Warp = warp;
            Reactionless = reactionless;
            Surface = surface;
            Signature = signature;
            ComponentMass = componentMass;
            ExhaustVelocity = exhaustVelocity;
            FuelBurnRate = fuelBurnRate;
            Thrust = thrust;
            EnginePower = enginePower;
            BubbleCreationCost = bubbleCreationCost;
            BubbleSustainCost = bubbleSustainCost;
            BubbleCollapseCost = bubbleCollapseCost;
        }
    }
}
