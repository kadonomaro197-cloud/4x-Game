using GameEngine.People;
using Pulsar4X.Sites;

namespace Pulsar4X.Components.Designers
{
    /// <summary>CHOICE 1 — the class of command seat, the FORCED pick that decides which real engine part gets built.
    /// It passes the "intrinsic test": you can set it knowing nothing else about the design. It picks the component's
    /// <c>*Atb</c> class, its JSON template, its mount, and WHICH dials apply.</summary>
    public enum CommandSeatClass
    {
        /// <summary>A planetary Administrative Complex — the office that runs a colony/sector/empire. Builds an
        /// <see cref="AdminSpaceAtb"/> from the <c>admin-complex</c> template; mounts as a planet installation.</summary>
        Office,
        /// <summary>A ship's Bridge — the command watch station on a hull. Builds an <see cref="AdminSpaceAtb"/> from the
        /// <c>ship-command</c> template; mounts as a ship component.</summary>
        Bridge,
        /// <summary>A Command Berth — the delegate seat a posted leader works a field-site from (an anomaly, a ruin).
        /// Builds a <see cref="CommandBerthAtb"/> from the <c>command-berth</c> template; mounts anywhere.</summary>
        Berth
    }

    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the COMMAND door parametric designer, slice 1 (ENGINE MODEL).
    ///
    /// WHAT IT IS, in plain English: this is the engine-side calculator behind the "design a command seat" screen. Today
    /// the game ships THREE hand-authored command parts — a planetary admin office, a ship's bridge, and a field-site
    /// command berth. This one form replaces those three menu picks: you choose the SEAT CLASS (office / bridge / berth),
    /// pick its COMMAND LINE (how far its authority reaches, or which kind of site it works), and slide a few dials — and
    /// the exact part the base game hand-built falls out. Think of it like sizing a watch station: pick which station it is
    /// (engineering office vs. bridge vs. a damage-control command post), then dial its size, its quality tier, and its
    /// crew — and the drawing (mass, crew, cost, materials) is fixed by those choices.
    ///
    /// WHY IT'S SHAPED THIS WAY (the derivation, per <c>docs/economy/DESIGNER-NORTH-STAR.md</c>): a "door" is DERIVED from
    /// the numbers the simulation actually reads off a component. Read the two command parts and group the inputs by the
    /// question each answers, then split the ones the design FORCES from the ones the player is FREE to set:
    ///   • CHOICE 1 — <see cref="CommandSeatClass"/> (Office / Bridge / Berth): FORCED. It decides which <c>*Atb</c> class
    ///     is built (<see cref="AdminSpaceAtb"/> for an office or bridge, <see cref="CommandBerthAtb"/> for a berth), which
    ///     JSON template, which mount, and which cost formula the mass runs through.
    ///   • CHOICE 2 — the COMMAND LINE: FORCED and class-dependent. This is the honest as-built shape — an office/bridge
    ///     carries an <see cref="AdminLevel"/> (how far its authority reaches, Ship..Empire) and a berth carries a
    ///     <see cref="SiteRole"/> (which kind of site it works, Science..Engineering). A part never carries both.
    ///   • SLIDERS — the FREE dials, partitioned by CHOICE 1:
    ///       – <see cref="CommandSpace"/> (Office/Bridge only): the workstation count. Drives mass, crew, and research.
    ///       – <see cref="Grade"/> (Berth only): the quality tier 1..10. A higher grade works a site faster and costs more.
    ///       – <see cref="Support"/> (Berth only): a flat competence boost to the posted leader.
    ///       – <see cref="Survivability"/> (Berth only): how well the berth protects its leader at a dangerous posting.
    ///
    /// THE HONEST CAVEAT (flagged for the developer, not hidden): three of these dials are honestly imperfect as-built and
    /// this model reproduces them FAITHFULLY (byte-identical) rather than pretending they're clean —
    ///   • <see cref="CommandBerthAtb.Span"/> is a DEAD dial: it writes to nothing the sim reads today. The base-mod berths
    ///     all ship it at 1, so the model emits it as the constant <see cref="FixedSpan"/> and never exposes it as a slider
    ///     (a slider that writes to nothing is a bug in the design — DESIGNER-NORTH-STAR test 1).
    ///   • Support and Survivability cost nothing today (they're not in any cost formula), and Survivability = 100 zeroes the
    ///     leader-death risk for free. The model keeps them as real dials because the sim DOES read them (work-rate and
    ///     incident-chance respectively); the "price them" fix is a later slice, not this one.
    ///   • <see cref="CommandSpace"/> on a BRIDGE feeds an <see cref="AdminSpaceAtb.ConsoleSpace"/> the sim never reads (the
    ///     consumer is colony-gated), so a bridge's console count sizes only its mass/crew. Reproduced as-is.
    ///
    /// BYTE-IDENTICAL / SAFE (slice-1 rules): this is a NEW file, a pure value type with no serialized state, no DataBlob,
    /// no <c>*Atb</c> ctor change — nothing in the live game calls it yet, so the whole engine is unchanged. It mirrors the
    /// exact arithmetic the base-mod JSON templates use (<c>admin-complex</c> / <c>ship-command</c> in
    /// <c>GameData/basemod/TemplateFiles/installations.json</c> + <c>storage.json</c>, and <c>command-berth</c> in
    /// <c>installations.json</c>) so the gauge <c>CommandDesignModelTests</c> can prove it reproduces every base-mod
    /// command component's <c>*Atb</c> args AND emergent stats. Those coefficients live in JSON (not as C# consts), so they
    /// are named consts here that cite the template — there is no engine const to reference the way the weapons model
    /// references <c>ShipCombatValueDB</c>.
    /// </summary>
    public readonly struct CommandDesignModel
    {
        // ---- Template coefficients, mirrored from the base-mod JSON (no C# const exists to reference) ----
        // Office/Bridge (admin-complex / ship-command): Mass = space*100, CrewReq = space*0.25, ResearchCost = space*0.5,
        //   CreditCost = 120 (a flat template constant).
        private const double MassPerSpace = 100.0;
        private const double CrewPerSpace = 0.25;
        private const double ResearchPerSpace = 0.5;
        private const double OfficeCreditCost = 120.0;
        // Berth (command-berth): Mass = grade*200, CrewReq = grade*2, ResearchCost = grade*10, CreditCost = 150 (flat).
        private const double MassPerGrade = 200.0;
        private const double CrewPerGrade = 2.0;
        private const double ResearchPerGrade = 10.0;
        private const double BerthCreditCost = 150.0;
        // Shared across all three templates.
        private const double VolumePerMass = 0.1;     // Volume = [Mass] * 0.1
        private const double BuildPointPerMass = 1.0; // BuildPointCost = [Mass]
        private const double HTKConst = 0.5;           // HTK = 0.5
        // ResourceCost fractions of [Mass] (identical in all three templates).
        private const double IronFrac = 0.5;
        private const double AluminiumFrac = 0.2;
        private const double CopperFrac = 0.1;
        private const double PlasticFrac = 0.1;
        private const double StainlessSteelFrac = 0.1;

        // ---- The as-built attribute FQNs + template ids + mounts the choices force ----
        public const string AdminAttributeType = "GameEngine.People.AdminSpaceAtb";
        public const string BerthAttributeType = "Pulsar4X.Sites.CommandBerthAtb";
        public const string OfficeTemplateId = "admin-complex";
        public const string BridgeTemplateId = "ship-command";
        public const string BerthTemplateId = "command-berth";

        /// <summary>The berth's force-size dial is DEAD (writes to nothing), so it is emitted as this constant, never as a
        /// slider — the base-mod berths all ship it at 1.</summary>
        public const int FixedSpan = 1;

        /// <summary>CHOICE 1 — which class of command seat this is (forces the *Atb class + template + mount + cost set).</summary>
        public CommandSeatClass SeatClass { get; }
        /// <summary>CHOICE 2 (Office/Bridge) — how far the seat's authority reaches (Ship..Empire). Emitted as the
        /// <see cref="AdminSpaceAtb"/> ordinal <c>level</c> arg. Ignored for a Berth.</summary>
        public AdminLevel AdminLevel { get; }
        /// <summary>CHOICE 2 (Berth) — which kind of field-site this berth works (Science..Engineering). Emitted as the
        /// <see cref="CommandBerthAtb"/> <c>roleIndex</c> arg. Ignored for an Office/Bridge.</summary>
        public SiteRole Role { get; }
        /// <summary>SLIDER (Office/Bridge) — workstation count. Office 10..10000 step 100; Bridge 1..20 step 1. Drives
        /// mass/crew/research and becomes <see cref="AdminSpaceAtb.ConsoleSpace"/>.</summary>
        public int CommandSpace { get; }
        /// <summary>SLIDER (Berth) — quality tier 1..10. Drives mass/crew/research and the site work-rate.</summary>
        public int Grade { get; }
        /// <summary>SLIDER (Berth) — flat competence boost to the posted leader, 0..50 step 5. (Costs nothing today.)</summary>
        public int Support { get; }
        /// <summary>SLIDER (Berth) — leader-protection %, 0..100 step 5. Buys down the incident risk. (Costs nothing today.)</summary>
        public int Survivability { get; }

        public CommandDesignModel(CommandSeatClass seatClass, AdminLevel adminLevel = AdminLevel.Colony,
            SiteRole role = SiteRole.Science, int commandSpace = 0, int grade = 0, int support = 0, int survivability = 0)
        {
            SeatClass = seatClass;
            AdminLevel = adminLevel;
            Role = role;
            CommandSpace = commandSpace;
            Grade = grade;
            Support = support;
            Survivability = survivability;
        }

        /// <summary>An Administrative Complex (planet office). <paramref name="level"/> = how far its authority reaches;
        /// <paramref name="officeSpace"/> = the workstation count.</summary>
        public static CommandDesignModel Office(AdminLevel level, int officeSpace) =>
            new CommandDesignModel(CommandSeatClass.Office, adminLevel: level, commandSpace: officeSpace);

        /// <summary>A ship's Bridge. <paramref name="level"/> = command level; <paramref name="consoleSpace"/> = bridge
        /// console stations (sizes mass/crew only — the sim doesn't read a ship's console span-of-control).</summary>
        public static CommandDesignModel Bridge(AdminLevel level, int consoleSpace) =>
            new CommandDesignModel(CommandSeatClass.Bridge, adminLevel: level, commandSpace: consoleSpace);

        /// <summary>A Command Berth (field-site delegate seat).</summary>
        public static CommandDesignModel BerthSeat(SiteRole role, int grade, int support, int survivability) =>
            new CommandDesignModel(CommandSeatClass.Berth, role: role, grade: grade, support: support,
                survivability: survivability);

        /// <summary>
        /// Build the <see cref="CommandProfile"/> — the <c>*Atb</c> ctor args plus the emergent component stats the game
        /// computes for the matching base-mod component, so a design made through this model is byte-identical to the
        /// hand-authored part. Office and Bridge share the same space-driven arithmetic (they differ only in the *Atb
        /// consumer, the mount, and the command-level range); Berth uses the grade-driven arithmetic.
        /// </summary>
        public CommandProfile BuildProfile()
        {
            double mass;
            double crew;
            double research;
            double credit;

            if (SeatClass == CommandSeatClass.Berth)
            {
                mass = Grade * MassPerGrade;
                crew = Grade * CrewPerGrade;
                research = Grade * ResearchPerGrade;
                credit = BerthCreditCost;
            }
            else // Office or Bridge — identical space-driven formulas
            {
                mass = CommandSpace * MassPerSpace;
                crew = CommandSpace * CrewPerSpace;
                research = CommandSpace * ResearchPerSpace;
                credit = OfficeCreditCost;
            }

            double volume = mass * VolumePerMass;
            double buildPoints = mass * BuildPointPerMass;

            string attrType;
            string templateId;
            string mount;
            int adminLevelOrdinal;
            int consoleSpace;
            int roleIndex;
            int grade;
            int support;
            int survivability;
            int span;

            switch (SeatClass)
            {
                case CommandSeatClass.Office:
                    attrType = AdminAttributeType;
                    templateId = OfficeTemplateId;
                    mount = "PlanetInstallation";
                    adminLevelOrdinal = (int)AdminLevel;
                    consoleSpace = CommandSpace;
                    roleIndex = -1; grade = -1; support = -1; survivability = -1; span = -1;
                    break;

                case CommandSeatClass.Bridge:
                    attrType = AdminAttributeType;
                    templateId = BridgeTemplateId;
                    mount = "ShipComponent, ShipCargo";
                    adminLevelOrdinal = (int)AdminLevel;
                    consoleSpace = CommandSpace;
                    roleIndex = -1; grade = -1; support = -1; survivability = -1; span = -1;
                    break;

                default: // Berth
                    attrType = BerthAttributeType;
                    templateId = BerthTemplateId;
                    mount = "ShipComponent, ShipCargo, PlanetInstallation";
                    adminLevelOrdinal = -1; consoleSpace = -1;
                    roleIndex = (int)Role;
                    grade = Grade;
                    support = Support;
                    survivability = Survivability;
                    span = FixedSpan;
                    break;
            }

            return new CommandProfile(
                SeatClass, attrType, templateId, mount,
                adminLevelOrdinal, consoleSpace,
                roleIndex, grade, support, survivability, span,
                mass, volume, crew, research, credit, buildPoints, HTKConst,
                mass * IronFrac, mass * AluminiumFrac, mass * CopperFrac, mass * PlasticFrac, mass * StainlessSteelFrac);
        }
    }

    /// <summary>
    /// The output of <see cref="CommandDesignModel.BuildProfile"/> — everything the game computes for a command component:
    /// the <c>*Atb</c> ctor args (which differ by seat class) and the emergent stats (mass/volume/crew/costs/resources).
    /// Fields that don't apply to a given seat class carry the sentinel -1 (e.g. a Berth's <see cref="AdminLevelOrdinal"/>
    /// / <see cref="ConsoleSpace"/>, an Office/Bridge's <see cref="RoleIndex"/> / <see cref="Grade"/> / … ).
    /// </summary>
    public readonly struct CommandProfile
    {
        public CommandSeatClass SeatClass { get; }
        /// <summary>The fully-qualified <c>*Atb</c> type name the built component carries.</summary>
        public string AttributeTypeName { get; }
        /// <summary>The base-mod template UniqueID this seat class builds from.</summary>
        public string TemplateId { get; }
        /// <summary>The component mount string (as authored in the template).</summary>
        public string MountType { get; }

        // ---- AdminSpaceAtb(level, space) args — Office / Bridge (-1 for a Berth) ----
        public int AdminLevelOrdinal { get; }
        public int ConsoleSpace { get; }

        // ---- CommandBerthAtb(roleIndex, grade, support, survivability, span) args — Berth (-1 for Office/Bridge) ----
        public int RoleIndex { get; }
        public int Grade { get; }
        public int Support { get; }
        public int Survivability { get; }
        public int Span { get; }

        // ---- Emergent component stats (what the industry/economy reads) ----
        public double Mass { get; }
        public double Volume { get; }
        public double CrewReq { get; }
        public double ResearchCost { get; }
        public double CreditCost { get; }
        public double BuildPointCost { get; }
        public double HTK { get; }
        public double IronCost { get; }
        public double AluminiumCost { get; }
        public double CopperCost { get; }
        public double PlasticCost { get; }
        public double StainlessSteelCost { get; }

        public CommandProfile(CommandSeatClass seatClass, string attributeTypeName, string templateId, string mountType,
            int adminLevelOrdinal, int consoleSpace,
            int roleIndex, int grade, int support, int survivability, int span,
            double mass, double volume, double crewReq, double researchCost, double creditCost, double buildPointCost,
            double htk, double ironCost, double aluminiumCost, double copperCost, double plasticCost,
            double stainlessSteelCost)
        {
            SeatClass = seatClass;
            AttributeTypeName = attributeTypeName;
            TemplateId = templateId;
            MountType = mountType;
            AdminLevelOrdinal = adminLevelOrdinal;
            ConsoleSpace = consoleSpace;
            RoleIndex = roleIndex;
            Grade = grade;
            Support = support;
            Survivability = survivability;
            Span = span;
            Mass = mass;
            Volume = volume;
            CrewReq = crewReq;
            ResearchCost = researchCost;
            CreditCost = creditCost;
            BuildPointCost = buildPointCost;
            HTK = htk;
            IronCost = ironCost;
            AluminiumCost = aluminiumCost;
            CopperCost = copperCost;
            PlasticCost = plasticCost;
            StainlessSteelCost = stainlessSteelCost;
        }
    }
}
