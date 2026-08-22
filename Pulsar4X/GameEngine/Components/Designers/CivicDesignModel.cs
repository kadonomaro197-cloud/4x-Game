using System.Collections.Generic;
using GameEngine.People; // AdminLevel enum (namespace GameEngine.People)

namespace Pulsar4X.Components.Designers
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the CIVIC door parametric designer, slice 1 (ENGINE MODEL).
    ///
    /// WHAT IT IS, in plain English: the Civic door is the "colony-support building" screen — hospitals, precincts,
    /// farms, life-support, habitats, academies, city halls. Instead of a menu of one-off buildings, you pick ONE
    /// thing — what the building is FOR (its "civic function") — and slide a couple of dials, and every base-mod
    /// civic installation falls out of that one form. This class is the ENGINE HALF of that form: a pure calculator
    /// that turns the pick + dials into what a building COSTS to build (mass, volume, crew, research, credits, build
    /// points, materials) and the numbers that get stamped onto its capability attribute (a farm's food output, a
    /// precinct's order rating, an academy's class size, …). It is a faithful C# transcription of each civic
    /// template's <c>Formulas</c> block in <c>GameData/basemod/TemplateFiles/installations.json</c>.
    ///
    /// WHY IT'S SHAPED DIFFERENTLY FROM WEAPONS (the honest derivation, per <c>docs/economy/DESIGNER-NORTH-STAR.md</c>):
    /// a weapon is ONE family of numbers (the <c>WeaponProfile</c>) and its two choices just pick a position in that
    /// family. Civic is not like that. There are SEVEN different civic functions, each writing a DIFFERENT colony
    /// variable through a DIFFERENT attribute (a farm feeds the sustenance loop; a precinct props up legitimacy; a
    /// hospital lifts morale; life-support raises the population ceiling; an academy graduates people; a city hall
    /// grants admin seats). So the first choice is a genuine TEMPLATE SWAP, not a slider position:
    ///   • CHOICE 1 — <see cref="CivicFunction"/> (Food / Security / Medical / LifeSupport / SpaceHabitat / Academy /
    ///     Administration): picks the template, the cost formula, and the sim variable the building writes.
    ///   • CHOICE 2 — a FORCED, CONDITIONAL sub-choice that appears for only TWO functions:
    ///       – Academy → <see cref="AcademyDomain"/> (Officers vs Science): the mock's seven leader-domains collapse to
    ///         the two attributes the engine actually has — a naval academy (officers) or a research academy
    ///         (scientists, which also carries a tech <see cref="Specialty"/>).
    ///       – Administration → <see cref="AdminLevel"/> (Ship … Empire): the scope of the command seat the office grants.
    ///     For the other five functions Choice 2 is not present (it is ignored).
    ///   • SLIDERS (free, intrinsic) — each function exposes only the subset it prices, but the QUESTION each answers is
    ///     shared: <see cref="Capacity"/> (how MUCH it provides — food output / security rating / care rating / support
    ///     colonists / office space / class size), <see cref="Quality"/> (how GOOD per unit — food quality / housing
    ///     comfort / class length), <see cref="Automation"/> (food only — trade crew for mass), and the host-only
    ///     secondary dials <see cref="SupportCapacity"/> / <see cref="StorageAmount"/> plus the environment envelope
    ///     (<see cref="MinGravity"/>/<see cref="MaxGravity"/>/<see cref="MinPressure"/>/<see cref="MaxPressure"/>) on
    ///     life-support only.
    ///
    /// THE HONEST CAVEAT (the load-bearing finding, flagged for the developer): a literal "one function + a couple of
    /// sliders" form cannot express the per-instance variants the base mod ships (a default city hall vs a federation
    /// ministry differ only in Office Space; a research academy vs an Olympus university only in Class Size). This model
    /// therefore exposes EVERY priced dial as a free input, each defaulting to the template's own default value, so the
    /// caller can leave the ones the choice would force at their default and move only the one that makes a new design.
    /// The slice-2 UI decides which dials to surface — that does NOT block this model.
    ///
    /// BYTE-IDENTICAL / SAFE (slice-1 rules): this is a NEW file, a pure value type — no serialized state, no DataBlob,
    /// no <c>*Atb</c> ctor change. Nothing in the live game calls it yet, so the whole engine is unchanged. It mirrors
    /// the <see cref="Pulsar4X.Components.ComponentDesigner"/> arithmetic EXACTLY, including the two rules that make a
    /// design byte-identical (verified against <c>ComponentDesigner.SetMass/SetCrew/…</c> + <c>ChainedExpression</c>):
    ///   (1) double→long/int is TRUNCATION toward zero (<c>(long)</c>/<c>(int)</c>, NEVER rounding) — so a research
    ///       academy's crew <c>10 × 0.25 = 2.5</c> truncates to <b>2</b>, and a Kithrin nexus's <c>50 × 0.25 = 12.5</c>
    ///       truncates to <b>12</b>.
    ///   (2) a <c>[Mass]</c> reference in a downstream cost formula resolves to the ALREADY-TRUNCATED mass (cast back to
    ///       double — <c>ChainedExpression.NCalcPulsarParameters "Mass"</c> returns <c>(double)_designer.MassValue</c>),
    ///       so this model truncates Mass FIRST, then feeds that truncated value into volume / research / credit / build
    ///       points / material costs.
    /// The gauge <see cref="Pulsar4X.Components.Designers.CivicDesignModel"/>'s test (<c>CivicDesignModelTests</c>) proves it
    /// reproduces every shipped civic design's cost sextet + attribute stamps.
    /// </summary>
    public readonly struct CivicDesignModel
    {
        /// <summary>CHOICE 1 — what the building is FOR (picks the template, the cost formula, and the sim variable).</summary>
        public CivicFunction Function { get; }
        /// <summary>CHOICE 2 (Academy only) — an officers school (naval academy) vs a science school (research academy).</summary>
        public AcademyDomain Domain { get; }
        /// <summary>CHOICE 2 (Administration only) — the scope of the command seat this office grants (Ship … Empire).</summary>
        public AdminLevel AdminLevel { get; }
        /// <summary>SLIDER — how MUCH it provides: food output (units/day) / security rating / care rating /
        /// support colonists / office space / class size. Drives the primary attribute AND the mass.</summary>
        public double Capacity { get; }
        /// <summary>SLIDER — how GOOD per unit: food quality (cubic in mass) / housing comfort / class length (months).
        /// Unused for single-dial Security / Medical / Administration (pass anything).</summary>
        public double Quality { get; }
        /// <summary>SLIDER (Food only) — fraction of the workforce automated (0..0.9). Trades crew DOWN for mass UP.</summary>
        public double Automation { get; }
        /// <summary>SLIDER (LifeSupport / SpaceHabitat) — the support capacity other installations draw on.</summary>
        public double SupportCapacity { get; }
        /// <summary>SLIDER (LifeSupport / SpaceHabitat) — dedicated cargo storage (the CargoStorageAtb amount).</summary>
        public double StorageAmount { get; }
        /// <summary>SLIDER (LifeSupport only) — the gravity/pressure operating envelope (m/s² and atm).</summary>
        public double MinGravity { get; }
        public double MaxGravity { get; }
        public double MinPressure { get; }
        public double MaxPressure { get; }
        /// <summary>CHOICE 2 payload (Academy/Science only) — the tech category the school trains its scientists in.</summary>
        public string Specialty { get; }

        public CivicDesignModel(
            CivicFunction function,
            double capacity,
            double quality = 0,
            double automation = 0,
            AcademyDomain domain = AcademyDomain.Science,
            AdminLevel adminLevel = AdminLevel.Colony,
            string specialty = "tech-category-power-propulsion",
            double supportCapacity = 1000,
            double storageAmount = 500,
            double minGravity = 8.8,
            double maxGravity = 10.8,
            double minPressure = 0.9,
            double maxPressure = 1.1)
        {
            Function = function;
            Capacity = capacity;
            Quality = quality;
            Automation = automation;
            Domain = domain;
            AdminLevel = adminLevel;
            Specialty = specialty;
            SupportCapacity = supportCapacity;
            StorageAmount = storageAmount;
            MinGravity = minGravity;
            MaxGravity = maxGravity;
            MinPressure = minPressure;
            MaxPressure = maxPressure;
        }

        // --- The two byte-identity rules made explicit (see class doc-comment). ---
        private static long TruncL(double v) => (long)v;   // ChainedExpression.LongResult: (long)val, truncates toward 0
        private static int TruncI(double v) => (int)v;     // ChainedExpression.IntResult:  (int)val,  truncates toward 0

        /// <summary>
        /// Compute the full civic design profile — the cost sextet (mass/volume/crew/research/credit/build points),
        /// the material costs, and the attribute stamps — the same numbers <see cref="Pulsar4X.Components.ComponentDesigner"/>
        /// produces for the matching civic template, so a design made through this model is byte-identical to the
        /// hand-authored base-mod component.
        /// </summary>
        public CivicProfile Compute()
        {
            // 1) Mass, computed as a double then TRUNCATED to long (rule 1). Everything that references [Mass]
            //    downstream reads THIS truncated value cast back to double (rule 2).
            double massD;
            switch (Function)
            {
                case CivicFunction.Food:
                    // Mass = Food Output * 0.1 + Food Quality^3 * 200 + Automation * 500
                    massD = Capacity * 0.1 + Quality * Quality * Quality * 200 + Automation * 500;
                    break;
                case CivicFunction.Security:
                case CivicFunction.Medical:
                    // Mass = Rating * 400
                    massD = Capacity * 400;
                    break;
                case CivicFunction.LifeSupport:
                    // Infrastructure Mass is a flat constant 1000.
                    massD = 1000;
                    break;
                case CivicFunction.SpaceHabitat:
                    // Mass = 1000 * (1 + Support Colonists / 500 * 0.5 + Housing Comfort / 10)
                    massD = 1000 * (1 + Capacity / 500 * 0.5 + Quality / 10);
                    break;
                case CivicFunction.Academy:
                    // Mass = Class Size * 100 (naval and research share the formula)
                    massD = Capacity * 100;
                    break;
                case CivicFunction.Administration:
                    // Mass = Office Space * 100
                    massD = Capacity * 100;
                    break;
                default:
                    massD = 0;
                    break;
            }
            long mass = TruncL(massD);
            double massChain = mass; // [Mass] downstream = the truncated mass as a double

            // 2) Volume, Crew, Research, Credit, Build points, material costs — each per the template's formula.
            double volume;
            int crew;
            long research;
            int credit;
            long buildPoints;
            var resources = new Dictionary<string, long>();

            switch (Function)
            {
                case CivicFunction.Food:
                    volume = massChain;                                          // Volume = [Mass]
                    crew = TruncI(Max1(Capacity * 0.02 * (1 - Automation)));     // CrewReq = Max(1, Output*0.02*(1-Auto))
                    research = TruncL(massChain * 2);                            // ResearchCost = [Mass]*2
                    credit = TruncI(massChain);                                  // CreditCost = [Mass]
                    buildPoints = TruncL(massChain);                            // BuildPointCost = [Mass]
                    resources["stainless-steel"] = TruncL(massChain * 0.4);
                    resources["plastic"] = TruncL(massChain * 0.2);
                    resources["water"] = TruncL(massChain * 0.2);
                    resources["aluminium"] = TruncL(massChain * 0.2);
                    break;

                case CivicFunction.Security:
                case CivicFunction.Medical:
                    volume = massChain;                                          // Volume = [Mass]
                    crew = TruncI(Max1(Capacity * 5));                           // CrewReq = Max(1, Rating*5)
                    research = TruncL(massChain);                                // ResearchCost = [Mass]
                    credit = TruncI(massChain);                                  // CreditCost = [Mass]
                    buildPoints = TruncL(massChain);                           // BuildPointCost = [Mass]
                    resources["stainless-steel"] = TruncL(massChain * 0.6);
                    resources["plastic"] = TruncL(massChain * 0.4);
                    break;

                case CivicFunction.LifeSupport:
                    volume = massChain;                                          // Volume = [Mass]
                    crew = 10;                                                   // CrewReq = 10
                    research = 0;                                                // ResearchCost = 0
                    credit = 0;                                                  // CreditCost = 0
                    buildPoints = 100;                                          // BuildPointCost = 100
                    resources["iron"] = TruncL(massChain * 0.5);
                    resources["aluminium"] = TruncL(massChain * 0.2);
                    resources["copper"] = TruncL(massChain * 0.1);
                    resources["plastic"] = TruncL(massChain * 0.1);
                    resources["stainless-steel"] = TruncL(massChain * 0.1);
                    break;

                case CivicFunction.SpaceHabitat:
                    volume = massChain;                                          // Volume = [Mass]
                    crew = 10;                                                   // CrewReq = 10
                    research = 0;                                                // ResearchCost = 0
                    credit = 0;                                                  // CreditCost = 0
                    buildPoints = TruncL(massChain / 10);                       // BuildPointCost = [Mass]/10
                    resources["iron"] = TruncL(massChain * 0.5);
                    resources["aluminium"] = TruncL(massChain * 0.2);
                    resources["copper"] = TruncL(massChain * 0.1);
                    resources["plastic"] = TruncL(massChain * 0.1);
                    resources["stainless-steel"] = TruncL(massChain * 0.1);
                    break;

                case CivicFunction.Academy:
                    volume = massChain * 0.1;                                    // Volume = [Mass]*0.1
                    crew = TruncI(Capacity * 0.25);                             // CrewReq = Class Size * 0.25 (NO Max(1,..))
                    research = TruncL(Capacity * 0.5);                          // ResearchCost = Class Size * 0.5
                    credit = 120;                                               // CreditCost = 120
                    buildPoints = TruncL(massChain);                           // BuildPointCost = [Mass]
                    resources["iron"] = TruncL(massChain * 0.45);
                    resources["aluminium"] = TruncL(massChain * 0.2);
                    resources["copper"] = TruncL(massChain * 0.1);
                    resources["plastic"] = TruncL(massChain * 0.1);
                    resources["stainless-steel"] = TruncL(massChain * 0.1);
                    resources["electronics"] = TruncL(massChain * 0.05);
                    break;

                case CivicFunction.Administration:
                    volume = massChain * 0.1;                                    // Volume = [Mass]*0.1
                    crew = TruncI(Capacity * 0.25);                             // CrewReq = Office Space * 0.25
                    research = TruncL(Capacity * 0.5);                          // ResearchCost = Office Space * 0.5
                    credit = 120;                                               // CreditCost = 120
                    buildPoints = TruncL(massChain);                           // BuildPointCost = [Mass]
                    resources["iron"] = TruncL(massChain * 0.5);
                    resources["aluminium"] = TruncL(massChain * 0.2);
                    resources["copper"] = TruncL(massChain * 0.1);
                    resources["plastic"] = TruncL(massChain * 0.1);
                    resources["stainless-steel"] = TruncL(massChain * 0.1);
                    break;

                default:
                    volume = massChain;
                    crew = 0;
                    research = 0;
                    credit = 0;
                    buildPoints = 0;
                    break;
            }

            return new CivicProfile(
                Function, mass, volume, crew, research, credit, buildPoints, resources,
                // Attribute stamps — the AtbConstrArgs values each template hands to its *Atb ctor.
                // (Values that don't apply to this function are left at their zero/null default.)
                foodOutput: Function == CivicFunction.Food ? Capacity : 0,
                foodQuality: Function == CivicFunction.Food ? Quality : 0,
                securityRating: Function == CivicFunction.Security ? Capacity : 0,
                healthRating: Function == CivicFunction.Medical ? Capacity : 0,
                populationCapacity: (Function == CivicFunction.LifeSupport || Function == CivicFunction.SpaceHabitat)
                    ? TruncI(Capacity) : 0,               // PopulationSupportAtbDB casts (int)
                housingComfort: (Function == CivicFunction.LifeSupport || Function == CivicFunction.SpaceHabitat)
                    ? Quality : 0,
                infrastructureCapacity: (Function == CivicFunction.LifeSupport || Function == CivicFunction.SpaceHabitat)
                    ? SupportCapacity : 0,
                storageAmount: (Function == CivicFunction.LifeSupport || Function == CivicFunction.SpaceHabitat)
                    ? StorageAmount : 0,
                minGravity: Function == CivicFunction.LifeSupport ? MinGravity : 0,
                maxGravity: Function == CivicFunction.LifeSupport ? MaxGravity : 0,
                minPressure: Function == CivicFunction.LifeSupport ? MinPressure : 0,
                maxPressure: Function == CivicFunction.LifeSupport ? MaxPressure : 0,
                adminLevel: AdminLevel,
                consoleSpace: Function == CivicFunction.Administration ? TruncI(Capacity) : 0, // AdminSpaceAtb casts (int)
                academyDomain: Domain,
                classSize: Function == CivicFunction.Academy ? TruncI(Capacity) : 0,           // (int) cast
                trainingMonths: Function == CivicFunction.Academy ? TruncI(Quality) : 0,       // Class Length, (int) cast
                specialty: (Function == CivicFunction.Academy && Domain == AcademyDomain.Science) ? Specialty : null);
        }

        // NCalc's Max(1, x) — mirrors the CrewReq floor on the food/security/medical templates.
        private static double Max1(double x) => x < 1 ? 1 : x;
    }

    /// <summary>CHOICE 1 of the Civic door — what a building is FOR. Each value picks a different base-mod template,
    /// cost formula, and colony sim variable (see <see cref="CivicDesignModel"/>).</summary>
    public enum CivicFunction
    {
        /// <summary>Food production (agri-complex / hydroponics) → FoodProductionAtbDB → sustenance supply.</summary>
        Food,
        /// <summary>Security precincts → SecurityAtbDB → province legitimacy (order).</summary>
        Security,
        /// <summary>Hospitals → MedicalAtbDB → colony morale (health).</summary>
        Medical,
        /// <summary>Planetary life-support infrastructure → population support + housing + capacity + storage + grav/press envelope.</summary>
        LifeSupport,
        /// <summary>Orbital space habitat → the same support attributes, sealed (no grav/press envelope).</summary>
        SpaceHabitat,
        /// <summary>An academy (naval or research, per <see cref="AcademyDomain"/>) → graduates people.</summary>
        Academy,
        /// <summary>An administrative complex (city hall / ministry) → AdminSpaceAtb command seats.</summary>
        Administration
    }

    /// <summary>CHOICE 2 for <see cref="CivicFunction.Academy"/> — the two schools the engine actually has attributes for
    /// (the design mock's seven leader-domains collapse to these two).</summary>
    public enum AcademyDomain
    {
        /// <summary>Naval academy → NavalAcademyAtb (officers).</summary>
        Officers,
        /// <summary>Research academy → ResearchAcademyAtb (scientists, carries a tech specialty).</summary>
        Science
    }

    /// <summary>
    /// The emergent output of <see cref="CivicDesignModel.Compute"/> — the cost sextet + material costs the
    /// ComponentDesigner computes for a civic template, plus the attribute-constructor argument values each template
    /// hands to its <c>*Atb</c>. A readonly value type; only the fields relevant to the chosen
    /// <see cref="CivicFunction"/> are populated (the rest are 0 / null).
    /// </summary>
    public readonly struct CivicProfile
    {
        public CivicFunction Function { get; }

        // --- Cost sextet (what it takes to build). ---
        /// <summary>MassPerUnit (kg), already truncated to long.</summary>
        public long Mass { get; }
        /// <summary>VolumePerUnit (m³) — a double (the engine does not truncate volume).</summary>
        public double Volume { get; }
        /// <summary>CrewReq (operating crew), truncated to int.</summary>
        public int Crew { get; }
        /// <summary>ResearchCostValue, truncated to long.</summary>
        public long Research { get; }
        /// <summary>CreditCost, truncated to int.</summary>
        public int Credit { get; }
        /// <summary>IndustryPointCosts (build points), truncated to long.</summary>
        public long BuildPoints { get; }
        /// <summary>ResourceCosts (material id → tonnage), each truncated to long off the truncated mass.</summary>
        public IReadOnlyDictionary<string, long> ResourceCosts { get; }

        // --- Attribute stamps (the AtbConstrArgs values). ---
        /// <summary>FoodProductionAtbDB(FoodOutput, FoodQuality).</summary>
        public double FoodOutput { get; }
        public double FoodQuality { get; }
        /// <summary>SecurityAtbDB(SecurityRating).</summary>
        public double SecurityRating { get; }
        /// <summary>MedicalAtbDB(HealthRating) — note the template dial is "Care Rating", the atb field is HealthRating.</summary>
        public double HealthRating { get; }
        /// <summary>PopulationSupportAtbDB(PopulationCapacity) — the (int) cast the atb applies.</summary>
        public int PopulationCapacity { get; }
        /// <summary>HousingAtbDB(Comfort).</summary>
        public double HousingComfort { get; }
        /// <summary>InfrastructureCapacityAtb(Support Capacity).</summary>
        public double InfrastructureCapacity { get; }
        /// <summary>CargoStorageAtb second arg (Storage Amount).</summary>
        public double StorageAmount { get; }
        /// <summary>GravityToleranceAtb(Min, Max) — life-support only.</summary>
        public double MinGravity { get; }
        public double MaxGravity { get; }
        /// <summary>PressureToleranceAtb(Min, Max) — life-support only.</summary>
        public double MinPressure { get; }
        public double MaxPressure { get; }
        /// <summary>AdminSpaceAtb(AdminLevel, ConsoleSpace).</summary>
        public AdminLevel AdminLevel { get; }
        public int ConsoleSpace { get; }
        /// <summary>Which academy attribute — Officers (NavalAcademyAtb) or Science (ResearchAcademyAtb).</summary>
        public AcademyDomain AcademyDomain { get; }
        /// <summary>NavalAcademyAtb/ResearchAcademyAtb ClassSize (the (int) cast).</summary>
        public int ClassSize { get; }
        /// <summary>NavalAcademyAtb/ResearchAcademyAtb TrainingPeriodInMonths (the (int) cast of Class Length).</summary>
        public int TrainingMonths { get; }
        /// <summary>ResearchAcademyAtb SpecialtyCategory (null for a naval academy).</summary>
        public string Specialty { get; }

        public CivicProfile(
            CivicFunction function, long mass, double volume, int crew, long research, int credit, long buildPoints,
            IReadOnlyDictionary<string, long> resourceCosts,
            double foodOutput, double foodQuality, double securityRating, double healthRating,
            int populationCapacity, double housingComfort, double infrastructureCapacity, double storageAmount,
            double minGravity, double maxGravity, double minPressure, double maxPressure,
            AdminLevel adminLevel, int consoleSpace,
            AcademyDomain academyDomain, int classSize, int trainingMonths, string specialty)
        {
            Function = function;
            Mass = mass;
            Volume = volume;
            Crew = crew;
            Research = research;
            Credit = credit;
            BuildPoints = buildPoints;
            ResourceCosts = resourceCosts;
            FoodOutput = foodOutput;
            FoodQuality = foodQuality;
            SecurityRating = securityRating;
            HealthRating = healthRating;
            PopulationCapacity = populationCapacity;
            HousingComfort = housingComfort;
            InfrastructureCapacity = infrastructureCapacity;
            StorageAmount = storageAmount;
            MinGravity = minGravity;
            MaxGravity = maxGravity;
            MinPressure = minPressure;
            MaxPressure = maxPressure;
            AdminLevel = adminLevel;
            ConsoleSpace = consoleSpace;
            AcademyDomain = academyDomain;
            ClassSize = classSize;
            TrainingMonths = trainingMonths;
            Specialty = specialty;
        }
    }
}
