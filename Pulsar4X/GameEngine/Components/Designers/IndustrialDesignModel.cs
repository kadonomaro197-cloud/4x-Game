using System;
using System.Collections.Generic;
using Pulsar4X.Industry;      // MineResourcesAtbDB, IndustryAtb, LocalConstructionAtb, InfrastructureCapacityAtb
using Pulsar4X.Technology;    // ResearchPointsAtbDB
using Pulsar4X.Ships;         // LaunchComplexAtb
using Pulsar4X.Construction;  // ConstructorAtb
using Pulsar4X.GroundCombat;  // GroundConstructorAtb, GroundDefenseAtb, GroundFootprintAtb

namespace Pulsar4X.Components.Designers
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the INDUSTRIAL door parametric designer, slice 1 (ENGINE MODEL).
    ///
    /// WHAT IT IS, in plain English: the INDUSTRIAL door is the plant/installation door — every buildable that PRODUCES
    /// something on a colony: a mine, a robo-miner, a refinery, a factory, a shipyard, a research lab, a local-construction
    /// yard, a launch complex, a field constructor, a ground constructor (combat engineer), a bunker, and infrastructure.
    /// The old designer picks one of those off a menu; this class is the ENGINE HALF of the new ONE form: you pick a KIND
    /// of plant and set a SIZE slider (plus a few kind-specific dials), and everything the game needs to build and run that
    /// plant — its mass, its crew, its cost, and the ability values the simulation reads off it — falls out of that one
    /// form. This is a pure calculator; the ImGui screen that drives it is slice 2 (client, verified on the developer's
    /// machine — CI can compile the client but cannot run it).
    ///
    /// WHY THIS DOOR IS THE BACKBONE (and how it differs from Weapons): a weapon's output is a combat TOTAL that gets
    /// summed into a ship's firepower. An industrial plant's output is a RATE — ore/day, refining/day, research/day,
    /// build-points/day, launch tonnage, fortification — the rates every OTHER door's parts are built from. So this model
    /// does not build a single "profile number"; it reproduces, per kind, both the six COST fields (Mass / Volume / CrewReq
    /// / ResearchCost / CreditCost / BuildPointCost — the currency the Chassis/budget systems price the plant in) AND the
    /// exact constructor arguments of the kind's ability attribute (its <c>*Atb</c>), which is what the simulation actually
    /// reads. A plant designed through this model behaves identically to the hand-authored base-mod component.
    ///
    /// THE DERIVATION (per <c>docs/economy/DESIGNER-NORTH-STAR.md</c> §1: FORCED→choice, FREE→slider; §1a the intrinsic
    /// test — a dial belongs here only if you can set it knowing NOTHING else about the plant):
    ///   • CHOICE 1 — <see cref="IndustrialKind"/> (Mine / AutoMine / Refinery / Factory / Shipyard / ResearchLab /
    ///     LocalConstruction / LaunchComplex / FieldConstructor / GroundConstructor / Bunker / Infrastructure): this is the
    ///     door. It FORCES which <c>*Atb</c> the plant produces, what the OUTPUT means, the MOUNT, the Mass LAW (which
    ///     coefficient the size slider multiplies), and which of the sliders below are live.
    ///   • CHOICE 2 — <see cref="Specialty"/> (a tech-category id): live on the Research Lab only in the shipped set —
    ///     it sets <see cref="ResearchPointsAtbDB.BonusCategory"/>.
    ///   • SLIDER 1 — <see cref="Scale"/> (units per kind: mine Area[m²], refinery/factory Size, shipyard Slip Size[t],
    ///     Level, Max Tonnage[kg], Construction Capacity[m³], BuildRate, Research Points, Support Capacity, Local Fortify):
    ///     the universal size dial. It writes the plant's PRIMARY output arg AND (on the 7 "healthy" templates) the Mass.
    ///   • SLIDER 2 — <see cref="Workforce"/> (Crew Size): live on the Shipyard only — the one kind whose output is paid
    ///     in crew (Component/Ship-assembly points scale off crew, and CrewReq is 1:1). It also selects the 2-arg
    ///     <see cref="IndustryAtb"/> overload (a Slip-Size <see cref="IndustryAtb"/> MaxProductionVolume).
    ///   • SLIDER 3 — <see cref="Secondary"/>: live on the constant-mass kinds — Research Lab's Cost Per Day, Bunker's
    ///     Adjacent Projection.
    ///   • SLIDER 4 — <see cref="Footprint"/> (Tile Footprint): live on the Bunker only.
    ///
    /// THE HONEST CAVEAT (the load-bearing finding, flagged for the developer): a literal N-choice/M-slider form cannot
    /// express every per-instance variant a hand-authored template can. The mine/robo-miner's actual output is a
    /// DICTIONARY of 15 named minerals, not one number; the industry kinds write a dictionary of named industry TYPES.
    /// This model therefore accepts the mineral SET as a free input (<see cref="MineralKeys"/>) defaulting to the
    /// door-forced 15-key set, and forces the industry-type keys per kind (they are choice-forced, not free). The slice-2
    /// UI decides whether to expose the mineral set as an advanced dial or accept the default — parked as an ADJUDICATION
    /// item, it does NOT block this model.
    ///
    /// A NOTE ON THE CONSTANTS (why they are named here, unlike WeaponsDesignModel): the weapon model references the
    /// engine's own C# consts (<c>ShipCombatValueDB.LightSpeed_mps</c> and friends) because those numbers live in code.
    /// The industrial coefficients — a mine's mass is Area × 0.05, a factory's build cost is Size × 20 — live in the JSON
    /// TEMPLATES (<c>GameData/basemod/TemplateFiles/installations.json</c>), NOT as C# consts, so there is nothing to
    /// reference; this model names each one as a documented local const and reproduces the SHIPPED value exactly (incl. the
    /// <c>(long)</c>/<c>(int)</c> casts the <c>*Atb</c> constructors apply). Slice 1 reproduces the shipped values only —
    /// the several recalibration/pricing PROPOSALS in <c>industrialderived.html</c> are separate, non-byte-identical,
    /// developer-gated slices.
    ///
    /// BYTE-IDENTICAL / SAFE (slice-1 rules): this is a NEW file, a pure value type with no serialized state, no DataBlob,
    /// no <c>*Atb</c> ctor change — nothing in the live game calls it yet, so the whole engine is unchanged. The gauge
    /// <c>IndustrialDesignModelTests</c> proves it reproduces every base-mod industrial component's costs + atb args.
    /// </summary>
    public readonly struct IndustrialDesignModel
    {
        // ── The 15-mineral set a mine / robo-miner works, in the template's own order (installations.json:255-271). ──
        /// <summary>The door-forced mineral set a Mine / RoboMiner mines, in template order — the default for
        /// <see cref="MineralKeys"/>. A mine writes the SAME per-mineral rate to every one of these keys.</summary>
        public static readonly IReadOnlyList<string> StandardMineralKeys = new[]
        {
            "hydrocarbons", "iron", "aluminium", "copper", "lithium", "chromium", "fissionables", "titanium",
            "tungsten", "silicon", "graphite", "nickel", "water", "rare-earth-elements", "regolith"
        };

        // ── Kind-forced industry-type keys (choice-forced, not free): the dictionary keys each IndustryAtb kind writes. ──
        private static readonly string[] RefineryTypes = { "refining" };
        private static readonly string[] FactoryTypes  = { "component-construction", "installation-construction", "ordnance-construction" };
        // shipyard writes component-construction (×0.01 crew) + ship-assembly (×0.02 crew) — handled inline (two rates).

        // ── The template coefficients (installations.json), named. Slice 1 reproduces the SHIPPED numbers exactly. ──
        private const double MineMassPerArea      = 0.05;     // mine   Mass = Area × 0.05
        private const double MineVolumePerArea    = 0.01;     // mine   Volume = Area × 0.01
        private const double MineCrewPerArea      = 0.005;    // mine   CrewReq = Area × 0.005
        private const double MineYieldPerArea     = 0.00001;  // mine   MiningAmount = Area × 0.00001
        private const double AutoMineMassPerSize  = 2000.0;   // automine Mass = Size × 2000
        private const double AutoMineYieldPerSize = 0.005;    // automine MiningAmount = Size × 0.005
        private const double RefineryCrewFactor   = 0.1;      // refinery CrewReq = Mass × 0.1
        private const double RefinePointsPerSize  = 0.1;      // refinery Refinery Points = Size × 0.1
        private const double FactoryMassPerSize   = 1000.0;   // factory Mass = Size × 1000
        private const double FactoryVolPerSize    = 1.5;      // factory Volume = Size × 1.5
        private const double FactoryCrewPerSize   = 5.0;      // factory CrewReq = Size × 5
        private const double FactoryBpPerSize     = 20.0;     // factory BuildPointCost = Size × 20
        private const double FactoryPointsPerSize = 0.1;      // factory each construction point = Size × 0.1
        private const double ShipyardMassPerSlip  = 8.0;      // shipyard Mass = Slip Size × 8
        private const double ShipyardCompPerCrew  = 0.01;     // shipyard Component Construction Points = Crew × 0.01
        private const double ShipyardAsmPerCrew   = 0.02;     // shipyard Ship Assembly Points = Crew × 0.02
        private const double ResearchLabMass      = 100000.0; // research-lab Mass (CONSTANT)
        private const double ResearchLabVolume    = 1000.0;   // research-lab Volume (CONSTANT)
        private const double ResearchLabCrew      = 20.0;     // research-lab CrewReq (CONSTANT)
        private const double ResearchLabResCost   = 10.0;     // research-lab ResearchCost (CONSTANT)
        private const double LocalConMassPerLevel = 5000.0;   // local-construction Mass = Level × 5000
        private const double LocalConCrewPerLevel = 55.0;     // local-construction CrewReq = Level × 55
        private const double LocalConBpPerLevel   = 200.0;    // local-construction BuildPointCost = Level × 200
        private const int    LocalConPointsPerDay = 5;        // local-construction 2nd atb arg — a template constant (AtbConstrArgs(Level, 5))
        private const double LaunchMassPerTonnage = 0.01;     // launch-complex Mass = Max Tonnage × 0.01
        private const double LaunchCrew           = 50.0;     // launch-complex CrewReq (CONSTANT)
        private const double LaunchCreditCost     = 500.0;    // launch-complex CreditCost (CONSTANT)
        private const double FieldConCrewFactor   = 0.0002;   // field-constructor CrewReq = Capacity × 0.0002
        private const double FieldConCreditFactor = 0.01;     // field-constructor CreditCost = Capacity × 0.01
        private const double GroundConMassPerRate = 2.0;      // ground-constructor Mass = BuildRate × 2
        private const double GroundConCrew        = 10.0;     // ground-constructor CrewReq (CONSTANT)
        private const double GroundConCreditFactor= 0.3;      // ground-constructor CreditCost = Mass × 0.3
        private const double GroundConBpFactor    = 2.0;      // ground-constructor BuildPointCost = Mass × 2
        private const double BunkerMass           = 50000.0;  // bunker Mass (CONSTANT)
        private const double BunkerCrew           = 50.0;     // bunker CrewReq (CONSTANT)
        private const double BunkerCreditCost     = 200.0;    // bunker CreditCost (CONSTANT)
        private const double InfraMass            = 1000.0;   // infrastructure Mass (CONSTANT)
        private const double InfraCrew            = 10.0;     // infrastructure CrewReq (CONSTANT)
        private const double InfraBuildPointCost  = 100.0;    // infrastructure BuildPointCost (CONSTANT)
        private const double GenericVolPer01Mass  = 0.01;     // Volume = Mass × 0.01 (field-con / ground-con / bunker)
        private const double LocalConVolPer01Mass = 0.1;      // local-construction Volume = Mass × 0.1
        private const double LaunchVolPerMass     = 0.1;      // launch-complex Volume = Mass × 0.1 (installations.json:1560)
        private const double FacilityCreditCost   = 120.0;    // the shared "120" CreditCost of mine/automine/refinery/factory/shipyard/research-lab

        /// <summary>CHOICE 1 — which kind of plant. Forces the atb type, the output meaning, the mount, the Mass law,
        /// and which sliders are live. This is the door.</summary>
        public IndustrialKind Kind { get; }
        /// <summary>CHOICE 2 — the research specialty (a tech-category id). Live on the Research Lab only in the shipped
        /// set → <see cref="ResearchPointsAtbDB.BonusCategory"/>. Ignored by every other kind.</summary>
        public string Specialty { get; }
        /// <summary>SLIDER 1 — the universal SIZE dial (units per kind — see the class remarks). Writes the plant's
        /// primary output arg and, on the 7 healthy kinds, its Mass.</summary>
        public double Scale { get; }
        /// <summary>SLIDER 2 — Crew Size. Live on the Shipyard only: scales its build points AND its CrewReq 1:1, and
        /// selects the 2-arg <see cref="IndustryAtb"/> overload.</summary>
        public double Workforce { get; }
        /// <summary>SLIDER 3 — a secondary capability on the constant-mass kinds: Research Lab's Cost Per Day, Bunker's
        /// Adjacent Projection.</summary>
        public double Secondary { get; }
        /// <summary>SLIDER 4 — Tile Footprint. Live on the Bunker only → <see cref="GroundFootprintAtb.TileFootprint"/>.</summary>
        public double Footprint { get; }
        /// <summary>FREE input (the honesty caveat) — the mineral set a Mine / RoboMiner works. Defaults to
        /// <see cref="StandardMineralKeys"/> (the door-forced 15). Ignored by non-mining kinds.</summary>
        public IReadOnlyList<string> MineralKeys { get; }

        public IndustrialDesignModel(IndustrialKind kind, double scale, double workforce = 0, double secondary = 0,
            double footprint = 0, string specialty = null, IReadOnlyList<string> mineralKeys = null)
        {
            Kind = kind;
            Scale = scale;
            Workforce = workforce;
            Secondary = secondary;
            Footprint = footprint;
            Specialty = specialty;
            MineralKeys = mineralKeys ?? StandardMineralKeys;
        }

        /// <summary>
        /// Build the <see cref="IndustrialProfile"/> the simulation reads — the same cost fields and the same
        /// <c>*Atb</c> constructor arguments the matching base-mod template produces, so a plant designed through this
        /// model builds and runs identically to the hand-authored component. Applies the exact <c>(long)</c>/<c>(int)</c>
        /// casts the <c>*Atb</c> constructors apply, so the shipped RoboMiner's zero-yield quirk (Size 5 → 0.025 →
        /// <c>(long)</c>0 per mineral) is reproduced faithfully.
        /// </summary>
        public IndustrialProfile Compute()
        {
            switch (Kind)
            {
                case IndustrialKind.Mine:
                {
                    double mass = Scale * MineMassPerArea;
                    long yield = (long)(Scale * MineYieldPerArea);
                    return new IndustrialProfile(Kind,
                        mass: mass, volume: Scale * MineVolumePerArea, crewReq: Scale * MineCrewPerArea,
                        researchCost: 0, creditCost: FacilityCreditCost, buildPointCost: mass,
                        primaryAtb: typeof(MineResourcesAtbDB), mineYield: BuildMineDict(yield));
                }

                case IndustrialKind.AutoMine:
                {
                    double mass = Scale * AutoMineMassPerSize;
                    long yield = (long)(Scale * AutoMineYieldPerSize);
                    return new IndustrialProfile(Kind,
                        mass: mass, volume: Scale, crewReq: 0,
                        researchCost: 0, creditCost: FacilityCreditCost, buildPointCost: mass,
                        primaryAtb: typeof(MineResourcesAtbDB), mineYield: BuildMineDict(yield));
                }

                case IndustrialKind.Refinery:
                {
                    double mass = Scale;
                    int pts = (int)(Scale * RefinePointsPerSize);
                    return new IndustrialProfile(Kind,
                        mass: mass, volume: mass, crewReq: mass * RefineryCrewFactor,
                        researchCost: 0, creditCost: FacilityCreditCost, buildPointCost: mass,
                        primaryAtb: typeof(IndustryAtb),
                        industryPoints: BuildIndustryDict(RefineryTypes, pts),
                        maxProductionVolume: double.PositiveInfinity); // IndustryAtb 1-arg ctor
                }

                case IndustrialKind.Factory:
                {
                    double mass = Scale * FactoryMassPerSize;
                    int pts = (int)(Scale * FactoryPointsPerSize);
                    return new IndustrialProfile(Kind,
                        mass: mass, volume: Scale * FactoryVolPerSize, crewReq: Scale * FactoryCrewPerSize,
                        researchCost: 0, creditCost: FacilityCreditCost, buildPointCost: Scale * FactoryBpPerSize,
                        primaryAtb: typeof(IndustryAtb),
                        industryPoints: BuildIndustryDict(FactoryTypes, pts),
                        maxProductionVolume: double.PositiveInfinity); // IndustryAtb 1-arg ctor
                }

                case IndustrialKind.Shipyard:
                {
                    double mass = Scale * ShipyardMassPerSlip;
                    var pts = new Dictionary<string, int>
                    {
                        ["component-construction"] = (int)(Workforce * ShipyardCompPerCrew),
                        ["ship-assembly"]          = (int)(Workforce * ShipyardAsmPerCrew)
                    };
                    return new IndustrialProfile(Kind,
                        mass: mass, volume: Scale, crewReq: Workforce,
                        researchCost: 0, creditCost: FacilityCreditCost, buildPointCost: mass,
                        primaryAtb: typeof(IndustryAtb),
                        industryPoints: pts,
                        maxProductionVolume: Scale); // IndustryAtb 2-arg ctor — Slip Size
                }

                case IndustrialKind.ResearchLab:
                    return new IndustrialProfile(Kind,
                        mass: ResearchLabMass, volume: ResearchLabVolume, crewReq: ResearchLabCrew,
                        researchCost: ResearchLabResCost, creditCost: FacilityCreditCost,
                        buildPointCost: ResearchLabMass / 2.0,
                        primaryAtb: typeof(ResearchPointsAtbDB),
                        researchPoints: (int)Scale,
                        researchCostPerDay: (decimal)Secondary,
                        researchSpecialty: Specialty);

                case IndustrialKind.LocalConstruction:
                {
                    double mass = Scale * LocalConMassPerLevel;
                    return new IndustrialProfile(Kind,
                        mass: mass, volume: mass * LocalConVolPer01Mass, crewReq: Scale * LocalConCrewPerLevel,
                        researchCost: 0, creditCost: 0, buildPointCost: Scale * LocalConBpPerLevel,
                        primaryAtb: typeof(LocalConstructionAtb),
                        localConstructionLevel: (byte)(int)Scale,             // LocalConstructionAtb casts level → byte
                        localConstructionPointsPerDay: LocalConPointsPerDay); // template constant 5
                }

                case IndustrialKind.LaunchComplex:
                {
                    double mass = Scale * LaunchMassPerTonnage;
                    return new IndustrialProfile(Kind,
                        mass: mass, volume: mass * LaunchVolPerMass, crewReq: LaunchCrew,
                        researchCost: 0, creditCost: LaunchCreditCost, buildPointCost: mass,
                        primaryAtb: typeof(LaunchComplexAtb),
                        launchMaxTonnage: Scale); // LaunchComplexAtb(double maxTonnage) — no cast on the atb field
                }

                case IndustrialKind.FieldConstructor:
                {
                    double mass = Scale;
                    return new IndustrialProfile(Kind,
                        mass: mass, volume: mass * GenericVolPer01Mass, crewReq: Scale * FieldConCrewFactor,
                        researchCost: 0, creditCost: Scale * FieldConCreditFactor, buildPointCost: mass,
                        primaryAtb: typeof(ConstructorAtb),
                        constructionCapacity: Scale < 0 ? 0 : Scale); // ConstructorAtb clamps <0 → 0
                }

                case IndustrialKind.GroundConstructor:
                {
                    double mass = Scale * GroundConMassPerRate;
                    return new IndustrialProfile(Kind,
                        mass: mass, volume: mass * GenericVolPer01Mass, crewReq: GroundConCrew,
                        researchCost: 0, creditCost: mass * GroundConCreditFactor, buildPointCost: mass * GroundConBpFactor,
                        primaryAtb: typeof(GroundConstructorAtb),
                        groundBuildRate: Scale < 0 ? 0 : Scale); // GroundConstructorAtb clamps <0 → 0
                }

                case IndustrialKind.Bunker:
                    return new IndustrialProfile(Kind,
                        mass: BunkerMass, volume: BunkerMass * GenericVolPer01Mass, crewReq: BunkerCrew,
                        researchCost: 0, creditCost: BunkerCreditCost, buildPointCost: BunkerMass,
                        primaryAtb: typeof(GroundDefenseAtb), secondaryAtb: typeof(GroundFootprintAtb),
                        localFortify: Scale, adjacentProjection: Secondary,
                        tileFootprint: Footprint < 1 ? 1 : (int)Footprint); // GroundFootprintAtb floors at 1

                case IndustrialKind.Infrastructure:
                    return new IndustrialProfile(Kind,
                        mass: InfraMass, volume: InfraMass, crewReq: InfraCrew,
                        researchCost: 0, creditCost: 0, buildPointCost: InfraBuildPointCost,
                        primaryAtb: typeof(InfrastructureCapacityAtb),
                        infrastructureCapacity: (long)Scale); // InfrastructureCapacityAtb casts → long

                default:
                    throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown industrial kind.");
            }
        }

        private Dictionary<string, long> BuildMineDict(long ratePerMineral)
        {
            var d = new Dictionary<string, long>(MineralKeys.Count);
            foreach (var key in MineralKeys)
                d[key] = ratePerMineral; // every mineral gets the same (already-cast) rate, mirroring the template DataDict
            return d;
        }

        private static Dictionary<string, int> BuildIndustryDict(string[] types, int pointsPerType)
        {
            var d = new Dictionary<string, int>(types.Length);
            foreach (var t in types)
                d[t] = pointsPerType;
            return d;
        }
    }

    /// <summary>
    /// CHOICE 1 of the INDUSTRIAL door — the kind of plant. Each value forces a different <c>*Atb</c>, output meaning,
    /// mount, Mass law, and set of live sliders. There is no pre-existing engine enum for "kind of plant" (a plant is
    /// identified by which template it is), so this choice dimension is defined here for the parametric form.
    /// </summary>
    public enum IndustrialKind
    {
        /// <summary>A large fixed mining operation — <see cref="MineResourcesAtbDB"/>; size = Area[m²].</summary>
        Mine,
        /// <summary>A transportable robo-miner — <see cref="MineResourcesAtbDB"/>; size = Size.</summary>
        AutoMine,
        /// <summary>Refines minerals into materials — <see cref="IndustryAtb"/> {refining}; size = Size.</summary>
        Refinery,
        /// <summary>Builds components/installations/ordnance — <see cref="IndustryAtb"/> (3 types); size = Size.</summary>
        Factory,
        /// <summary>Builds + assembles ships — <see cref="IndustryAtb"/> 2-arg (Slip Size volume); size = Slip Size, workforce = Crew Size.</summary>
        Shipyard,
        /// <summary>Produces research points — <see cref="ResearchPointsAtbDB"/>; size = Research Points, secondary = Cost Per Day, specialty = a tech category.</summary>
        ResearchLab,
        /// <summary>Local construction points — <see cref="LocalConstructionAtb"/>; size = Level.</summary>
        LocalConstruction,
        /// <summary>A launch pad — <see cref="LaunchComplexAtb"/>; size = Max Tonnage[kg].</summary>
        LaunchComplex,
        /// <summary>An on-site station constructor — <see cref="ConstructorAtb"/>; size = Construction Capacity[m³].</summary>
        FieldConstructor,
        /// <summary>A combat-engineer kit for a ground unit — <see cref="GroundConstructorAtb"/>; size = BuildRate[bp/day].</summary>
        GroundConstructor,
        /// <summary>A fortification — <see cref="GroundDefenseAtb"/> + <see cref="GroundFootprintAtb"/>; size = Local Fortify, secondary = Adjacent Projection, footprint = Tile Footprint.</summary>
        Bunker,
        /// <summary>Colony support capacity — <see cref="InfrastructureCapacityAtb"/> (Industrial owns this one atb of the shared infrastructure template); size = Support Capacity.</summary>
        Infrastructure
    }

    /// <summary>
    /// The output of <see cref="IndustrialDesignModel.Compute"/> — the cost fields and the <c>*Atb</c> constructor
    /// arguments a designed industrial plant produces, in the same shape the simulation reads them. The atb-argument
    /// fields are typed per kind and left at their default (0 / null) for kinds that do not use them; only the fields
    /// relevant to <see cref="Kind"/> are populated. The <c>(long)</c>/<c>(int)</c>/byte casts the real <c>*Atb</c>
    /// constructors apply have already been applied to these values.
    /// </summary>
    public sealed class IndustrialProfile
    {
        // ── Cost fields (the six the Chassis/budget systems price the plant in). ──
        public IndustrialKind Kind { get; }
        public double Mass { get; }
        public double Volume { get; }
        public double CrewReq { get; }
        public double ResearchCost { get; }
        public double CreditCost { get; }
        public double BuildPointCost { get; }

        // ── The ability attribute(s) the plant produces. Secondary is null except for the Bunker (two atbs). ──
        public Type PrimaryAtb { get; }
        public Type SecondaryAtb { get; }

        // ── Mining kinds: the per-mineral yield dictionary the sim reads (values already cast to long). ──
        public IReadOnlyDictionary<string, long> MineYield { get; }

        // ── Industry kinds: the per-industry-type point rates (already cast to int) + the production-volume cap
        //    (double.PositiveInfinity for the 1-arg refinery/factory ctor; Slip Size for the shipyard 2-arg ctor). ──
        public IReadOnlyDictionary<string, int> IndustryPoints { get; }
        public double MaxProductionVolume { get; }

        // ── Research Lab. ──
        public int ResearchPoints { get; }
        public decimal ResearchCostPerDay { get; }
        public string ResearchSpecialty { get; }

        // ── Local construction (contribution to the colony = Level × PointsPerDay). ──
        public int LocalConstructionLevel { get; }
        public int LocalConstructionPointsPerDay { get; }

        // ── Launch / field constructor / ground constructor. ──
        public double LaunchMaxTonnage { get; }
        public double ConstructionCapacity { get; }
        public double GroundBuildRate { get; }

        // ── Bunker. ──
        public double LocalFortify { get; }
        public double AdjacentProjection { get; }
        public int TileFootprint { get; }

        // ── Infrastructure. ──
        public long InfrastructureCapacity { get; }

        public IndustrialProfile(
            IndustrialKind kind,
            double mass, double volume, double crewReq, double researchCost, double creditCost, double buildPointCost,
            Type primaryAtb, Type secondaryAtb = null,
            IReadOnlyDictionary<string, long> mineYield = null,
            IReadOnlyDictionary<string, int> industryPoints = null, double maxProductionVolume = 0,
            int researchPoints = 0, decimal researchCostPerDay = 0, string researchSpecialty = null,
            int localConstructionLevel = 0, int localConstructionPointsPerDay = 0,
            double launchMaxTonnage = 0, double constructionCapacity = 0, double groundBuildRate = 0,
            double localFortify = 0, double adjacentProjection = 0, int tileFootprint = 0,
            long infrastructureCapacity = 0)
        {
            Kind = kind;
            Mass = mass;
            Volume = volume;
            CrewReq = crewReq;
            ResearchCost = researchCost;
            CreditCost = creditCost;
            BuildPointCost = buildPointCost;
            PrimaryAtb = primaryAtb;
            SecondaryAtb = secondaryAtb;
            MineYield = mineYield;
            IndustryPoints = industryPoints;
            MaxProductionVolume = maxProductionVolume;
            ResearchPoints = researchPoints;
            ResearchCostPerDay = researchCostPerDay;
            ResearchSpecialty = researchSpecialty;
            LocalConstructionLevel = localConstructionLevel;
            LocalConstructionPointsPerDay = localConstructionPointsPerDay;
            LaunchMaxTonnage = launchMaxTonnage;
            ConstructionCapacity = constructionCapacity;
            GroundBuildRate = groundBuildRate;
            LocalFortify = localFortify;
            AdjacentProjection = adjacentProjection;
            TileFootprint = tileFootprint;
            InfrastructureCapacity = infrastructureCapacity;
        }
    }
}
