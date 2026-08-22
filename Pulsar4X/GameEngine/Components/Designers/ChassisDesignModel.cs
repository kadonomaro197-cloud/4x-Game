using System;
using Pulsar4X.Ships;
using Pulsar4X.Stations;
using Pulsar4X.Colonies;
using Pulsar4X.GroundCombat;
using Pulsar4X.Interfaces;
using Pulsar4X.DataStructures;

namespace Pulsar4X.Components.Designers
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the CHASSIS door parametric designer, slice 1 (ENGINE MODEL).
    ///
    /// WHAT IT IS, in plain English: a CHASSIS is the FRAME the thing is built on — a ship's hull, a station's
    /// keel, a ground unit's chassis, a building's foundation. It is the FIRST part you pick in the Entity
    /// Assembler, because everything else bolts onto it, and it is the frame that stamps the "rating plate" —
    /// how much weight/module/carry the mounted parts are allowed to add up to (the budget). Today the game
    /// ships four SEPARATE frame parts, one per situation, each authored by hand in JSON. This class replaces
    /// that "pick a frame from a menu" screen with ONE form: you pick WHERE it lives and WHAT it is, slide a
    /// couple of dials, and the right frame falls out. This is the ENGINE HALF of that form — a pure calculator
    /// that turns those picks + dials into the exact numbers the four hand-authored frame parts already carry,
    /// so a frame designed through the form behaves identically to the shipped one. The ImGui screen that drives
    /// it is a later slice (client, verified on the developer's machine — CI can compile the client but can't run it).
    ///
    /// WHY IT'S SHAPED THIS WAY (the derivation, per <c>docs/economy/DESIGNER-NORTH-STAR.md</c>): a "door" is
    /// DERIVED from the numbers the simulation actually reads off the shipped frames, not invented. Read the four
    /// frame attribute classes — <see cref="ShipHullAtb"/>, <see cref="StationChassisAtb"/>,
    /// <see cref="BuildingChassisAtb"/>, <see cref="GroundChassisAtb"/> — group their fields by the question each
    /// answers, and split the ones the SITUATION forces (which is FORCED become the CHOICES) from the ones the
    /// player is free to size (which become the SLIDERS):
    ///   • CHOICE 1 — the HOST CELL, a 2x2: <see cref="ChassisEnvironment"/> (Orbital = in space vs Surface = on
    ///     a planet) x <see cref="ChassisKind"/> (Unit = a thing that moves/fights vs Infrastructure = a thing
    ///     that stays put). Those two picks name one of the four shipped cells and FORCE three things that the
    ///     player never sizes: WHICH frame class is produced, WHICH mount its parts use
    ///     (<see cref="ComponentMountType"/>), and WHAT CURRENCY the budget is counted in
    ///     (<see cref="ChassisBudgetKind"/> — a ship budgets in kilograms of mass, a ground unit in carry-strength,
    ///     a station in structure, a building in footprint). The four cells:
    ///       Orbital x Unit          -> a SHIP HULL       (<see cref="ShipHullAtb"/>,       budget = Mass,      mount = ShipComponent)
    ///       Orbital x Infrastructure-> a STATION CHASSIS (<see cref="StationChassisAtb"/>, budget = Structure,  mount = Station)
    ///       Surface x Unit          -> a GROUND FRAME    (<see cref="GroundChassisAtb"/>,  budget = Carry,      mount = GroundUnit)
    ///       Surface x Infrastructure-> a BUILDING FDN    (<see cref="BuildingChassisAtb"/>,budget = Footprint,  mount = PlanetInstallation)
    ///   • CHOICE 2 — the <see cref="GroundSubstrate"/> (Mechanical / Organic / Synthetic): what the frame is MADE
    ///     of (steel-and-reactors vs a living/bio hull that self-repairs vs a reserved nanite type). This is a
    ///     REAL engine enum (E13); it is a DESIGN-level dial (it writes <c>GroundUnitDesign.Substrate</c>, NOT a
    ///     chassis-atb constructor argument — that is what keeps it save-safe), it applies ONLY to the ground cell,
    ///     and its slice-1 value is always <see cref="GroundSubstrate.Mechanical"/> (= today, byte-identical).
    ///   • SLIDERS (the free reals): <see cref="FrameMass"/> (the built weight of the frame itself),
    ///     <see cref="StructuralBudget"/> (the single budget the frame declares), and — only on the ground cell —
    ///     <see cref="Hp"/> (frame toughness), <see cref="Size"/> + <see cref="Locomotion"/> +
    ///     <see cref="CarryClass"/> (how big it is, how it gets around, and which transport bay hauls it). The
    ///     building cell carries one extra dial, <see cref="TileFootprint"/>, feeding a co-mounted
    ///     <see cref="GroundFootprintAtb"/> (its located presence on the ground map).
    ///
    /// THE HONEST CAVEAT (the load-bearing finding, flagged for the developer): unlike the WEAPONS door, this form
    /// is NOT one flat set of sliders shared across all four cells — the shipped frames carry DIFFERENT numbers of
    /// dials (a ship hull has 2, a station/building has 1 [+ footprint], a ground frame has 5), so this model is a
    /// PER-CELL mapping, honest to that asymmetry, not an oversold "one universal slider set." Two more caveats:
    /// (1) two developer rulings gate the collapse — D10 DROPS the HTML's square-root-law "structural efficiency"
    /// slider (no such field exists in engine/JSON/client, 2026-08-17), which is WHY slice-1 reproduction is a
    /// clean PASS-THROUGH with no medium-hull recalibration; and the whole re-derived door is a PARKED proposal
    /// awaiting go/no-go. (2) <see cref="FrameMass"/> is a live dial only for the ship hull; for the other three
    /// cells the shipped frame carries a per-template CONSTANT weight (station 100000, building 50000, human 20,
    /// vehicle 4000, walker 2500, swarm 5), so the caller passes that constant in — a literal single "frame mass"
    /// slider that the non-ship cells lack is deferred (the HTML's "budget scales with mass" proposal, un-adjudicated).
    ///
    /// BYTE-IDENTICAL / SAFE (slice-1 rules): this is a NEW file, a pure value type with no serialized state, no
    /// DataBlob, no <c>*Atb</c> constructor change — nothing in the live game calls it yet, so the whole engine is
    /// unchanged. <see cref="BuildProfile"/> produces the CONSTRUCTOR ARGUMENTS for the EXISTING frame atb classes
    /// (<see cref="ShipHullAtb"/>(double), <see cref="StationChassisAtb"/>(double), <see cref="BuildingChassisAtb"/>(double),
    /// <see cref="GroundChassisAtb"/>(double x5)) in the exact order each template's <c>AtbConstrArgs</c> feeds them,
    /// so the gauge <c>ChassisDesignModelTests</c> can feed those args straight into the real ctors and prove it
    /// reproduces every shipped frame with no new overload (so no exact-arity save-binder risk, landmine L13).
    /// </summary>
    public readonly struct ChassisDesignModel
    {
        /// <summary>CHOICE 1a — where the frame lives (in orbit vs on a planet surface). Half of the host-cell pick.</summary>
        public ChassisEnvironment Environment { get; }
        /// <summary>CHOICE 1b — a mobile unit vs a fixed installation. The other half of the host-cell pick.</summary>
        public ChassisKind Kind { get; }
        /// <summary>CHOICE 2 — what the frame is MADE OF (a real E13 engine enum). A DESIGN-level dial (writes
        /// GroundUnitDesign.Substrate, NOT a chassis-atb ctor arg); ground cell only; slice-1 value = Mechanical.</summary>
        public GroundSubstrate Substrate { get; }

        /// <summary>SLIDER — the built weight (kg) of the frame itself: a live dial for the ship hull ('Hull Mass'),
        /// a per-template CONSTANT the caller passes for the other three cells. Becomes the component's Mass.</summary>
        public double FrameMass { get; }
        /// <summary>SLIDER — the single budget the frame declares, in the cell's currency: mass ceiling (ship),
        /// structure allowance (station), footprint allowance (building), or carry-strength (ground). The one budget
        /// ctor arg on every frame.</summary>
        public double StructuralBudget { get; }
        /// <summary>SLIDER (ground cell only) — the frame's own toughness before armour (GroundChassisAtb.BaseHP).</summary>
        public double Hp { get; }
        /// <summary>SLIDER (ground cell only) — the frame's bulk; feeds transport carry-size (GroundChassisAtb.Size).</summary>
        public double Size { get; }
        /// <summary>CHOICE (ground cell only) — how the frame gets around (Foot/Tracked/Walker/Hover). A real engine enum.</summary>
        public GroundLocomotion Locomotion { get; }
        /// <summary>CHOICE (ground cell only) — which transport bay class hauls it (Personnel vs Vehicle). A real engine enum.</summary>
        public GroundCarryClass CarryClass { get; }
        /// <summary>SLIDER (building cell only) — how many fine city-tiles the building occupies; feeds the co-mounted
        /// <see cref="GroundFootprintAtb"/> that gives the building a located presence on the ground map.</summary>
        public double TileFootprint { get; }

        public ChassisDesignModel(ChassisEnvironment environment, ChassisKind kind,
            double frameMass, double structuralBudget,
            double hp = 0, double size = 0,
            GroundLocomotion locomotion = GroundLocomotion.Foot,
            GroundCarryClass carryClass = GroundCarryClass.Personnel,
            double tileFootprint = 0,
            GroundSubstrate substrate = GroundSubstrate.Mechanical)
        {
            Environment = environment;
            Kind = kind;
            FrameMass = frameMass;
            StructuralBudget = structuralBudget;
            Hp = hp;
            Size = size;
            Locomotion = locomotion;
            CarryClass = carryClass;
            TileFootprint = tileFootprint;
            Substrate = substrate;
        }

        /// <summary>
        /// Build the <see cref="ChassisProfile"/> for this frame — which frame atb class to make, the constructor
        /// arguments to make it with (in the shipped template's <c>AtbConstrArgs</c> order), the component's Mass,
        /// and the shared chassis view (<see cref="ChassisBudgetKind"/> + <see cref="ComponentMountType"/>) the cell
        /// forces. These are the SAME numbers the four hand-authored base-mod frames carry, so a frame designed
        /// through this model is byte-identical to the shipped one (slice-1 is a pure pass-through — D10 dropped the
        /// square-root "structural efficiency" law, so nothing is recalibrated).
        /// </summary>
        public ChassisProfile BuildProfile()
        {
            switch (Environment, Kind)
            {
                case (ChassisEnvironment.Orbital, ChassisKind.Unit):
                    // SHIP HULL — the one budget arg is the Mass ceiling; ctor = ShipHullAtb(massBudget).
                    return new ChassisProfile(
                        typeof(ShipHullAtb), new[] { StructuralBudget }, FrameMass,
                        ChassisBudgetKind.Mass, ComponentMountType.ShipComponent, Substrate);

                case (ChassisEnvironment.Orbital, ChassisKind.Infrastructure):
                    // STATION CHASSIS — ctor = StationChassisAtb(structuralAllowance).
                    return new ChassisProfile(
                        typeof(StationChassisAtb), new[] { StructuralBudget }, FrameMass,
                        ChassisBudgetKind.Structure, ComponentMountType.Station, Substrate);

                case (ChassisEnvironment.Surface, ChassisKind.Infrastructure):
                    // BUILDING FOUNDATION — ctor = BuildingChassisAtb(footprintAllowance), plus the located-presence
                    // co-mount GroundFootprintAtb(tileFootprint) the shipped foundation template carries alongside it.
                    return new ChassisProfile(
                        typeof(BuildingChassisAtb), new[] { StructuralBudget }, FrameMass,
                        ChassisBudgetKind.Footprint, ComponentMountType.PlanetInstallation, Substrate,
                        typeof(GroundFootprintAtb), new[] { TileFootprint });

                case (ChassisEnvironment.Surface, ChassisKind.Unit):
                default:
                    // GROUND FRAME — ctor = GroundChassisAtb(baseStrength, baseHP, size, locomotion, carryClass); the
                    // budget arg is BaseStrength, and locomotion/carryClass are fed as doubles (the enum's int value)
                    // exactly as the template's PropertyValue(...) does (the ctor casts (GroundLocomotion)(int)loco).
                    return new ChassisProfile(
                        typeof(GroundChassisAtb),
                        new[] { StructuralBudget, Hp, Size, (double)(int)Locomotion, (double)(int)CarryClass },
                        FrameMass,
                        ChassisBudgetKind.Carry, ComponentMountType.GroundUnit, Substrate);
            }
        }
    }

    /// <summary>CHOICE 1a — where a chassis lives. Half of the host-cell pick. (Not an existing engine enum — the
    /// four frame atbs encode the cell implicitly via their <see cref="ComponentMountType"/>; this names the axis.)</summary>
    public enum ChassisEnvironment
    {
        /// <summary>In space — a ship hull or a station keel.</summary>
        Orbital,
        /// <summary>On a planet surface — a ground unit's frame or a building's foundation.</summary>
        Surface
    }

    /// <summary>CHOICE 1b — a mobile thing vs a fixed thing. The other half of the host-cell pick.</summary>
    public enum ChassisKind
    {
        /// <summary>A unit that moves and fights — a ship or a ground unit.</summary>
        Unit,
        /// <summary>A fixed installation that stays put — a station or a building.</summary>
        Infrastructure
    }

    /// <summary>
    /// The output of <see cref="ChassisDesignModel.BuildProfile"/>: everything needed to instantiate one of the four
    /// shipped frame components, plus the shared chassis view the host cell forces. A pure value carrier — no engine
    /// state.
    ///
    /// <see cref="AtbType"/> + <see cref="AtbArgs"/> are the frame attribute class to construct and the arguments to
    /// construct it with, in the exact order the shipped template's <c>AtbConstrArgs</c> feeds them, so the args go
    /// straight into the EXISTING ctor with no new overload. <see cref="Mass"/> is the built weight of the frame
    /// component. <see cref="BudgetKind"/>/<see cref="PartMount"/> are the currency + mount the cell forces (they
    /// match the atb's own <see cref="IChassisAtb"/> computed getters). <see cref="Substrate"/> is the design-level
    /// material dial (meaningful on the ground cell only). <see cref="CoMountAtbType"/>/<see cref="CoMountAtbArgs"/>
    /// name a SECOND part the frame co-mounts — used only by the building foundation, which carries a
    /// <see cref="GroundFootprintAtb"/> beside its budget atb; <see cref="CoMountAtbType"/> is null for the other cells.
    /// </summary>
    public readonly struct ChassisProfile
    {
        /// <summary>The frame attribute class this cell produces (e.g. typeof(<see cref="ShipHullAtb"/>)).</summary>
        public Type AtbType { get; }
        /// <summary>The ctor arguments for <see cref="AtbType"/>, in the template's <c>AtbConstrArgs</c> order.</summary>
        public double[] AtbArgs { get; }
        /// <summary>The built weight (kg) of the frame component.</summary>
        public double Mass { get; }
        /// <summary>The currency the <see cref="AtbArgs"/> budget is counted in (Mass/Carry/Structure/Footprint).</summary>
        public ChassisBudgetKind BudgetKind { get; }
        /// <summary>Which mount the parts on this frame use (ShipComponent/Station/GroundUnit/PlanetInstallation).</summary>
        public ComponentMountType PartMount { get; }
        /// <summary>What the frame is made of (design-level dial; ground cell only, else the pass-through default).</summary>
        public GroundSubstrate Substrate { get; }
        /// <summary>A second part the frame co-mounts (building foundation only → <see cref="GroundFootprintAtb"/>); null otherwise.</summary>
        public Type CoMountAtbType { get; }
        /// <summary>The co-mount's ctor args (building foundation → [tileFootprint]); empty otherwise.</summary>
        public double[] CoMountAtbArgs { get; }

        public ChassisProfile(Type atbType, double[] atbArgs, double mass,
            ChassisBudgetKind budgetKind, ComponentMountType partMount, GroundSubstrate substrate,
            Type coMountAtbType = null, double[] coMountAtbArgs = null)
        {
            AtbType = atbType;
            AtbArgs = atbArgs;
            Mass = mass;
            BudgetKind = budgetKind;
            PartMount = partMount;
            Substrate = substrate;
            CoMountAtbType = coMountAtbType;
            CoMountAtbArgs = coMountAtbArgs ?? Array.Empty<double>();
        }
    }
}
