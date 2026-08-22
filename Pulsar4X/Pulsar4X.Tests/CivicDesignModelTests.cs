using System.Collections.Generic;
using NUnit.Framework;
using GameEngine.People;                 // AdminLevel enum
using Pulsar4X.Components.Designers;      // CivicDesignModel / CivicProfile / CivicFunction / AcademyDomain

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the CIVIC door parametric designer, slice-1 gauge.
    ///
    /// Proves the pure <see cref="CivicDesignModel"/> (the engine half of the Civic door form) REPRODUCES every
    /// base-mod civic building's cost sextet (mass / volume / crew / research / credit / build points) + material
    /// costs + attribute stamps from its one function choice + a couple of dials — i.e. every hand-authored civic
    /// installation falls out of the one parametric form (the DESIGNER-NORTH-STAR reproduction claim, made
    /// executable). PURE — no colony harness (fast, not the slow CI shard); the reproduction VALUES are the exact
    /// numbers the <see cref="Pulsar4X.Components.ComponentDesigner"/> computes for each civic template
    /// (verified against <c>installations.json</c> + <c>componentDesigns.json</c>), so a drift in the model's
    /// per-function transcription — or in its truncation rules — fails here. Byte-identical to the live game
    /// (nothing calls the model yet).
    ///
    /// The load-bearing checks are the two byte-identity rules the model must honour (see the model's doc-comment):
    /// double→long/int is TRUNCATION toward zero (research-academy crew 10×0.25=2.5 → 2; kithrin-nexus 50×0.25=12.5
    /// → 12), and a downstream [Mass] reference reads the ALREADY-TRUNCATED mass. This is the CIVIC mirror of
    /// <c>WeaponsDesignModelTests</c>.
    /// </summary>
    [TestFixture]
    public class CivicDesignModelTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[civic-model] " + m);

        /// <summary>Assert the cost sextet (the numbers every civic building has), and log the readout.</summary>
        private static CivicProfile AssertCost(string name, CivicDesignModel m,
            CivicFunction function, long mass, double volume, int crew, long research, int credit, long buildPoints)
        {
            var p = m.Compute();
            Log($"{name}: {p.Function} mass={p.Mass} vol={p.Volume} crew={p.Crew} rp={p.Research} cr={p.Credit} bp={p.BuildPoints}");
            Assert.That(p.Function, Is.EqualTo(function), $"{name} Function");
            Assert.That(p.Mass, Is.EqualTo(mass), $"{name} Mass");
            Assert.That(p.Volume, Is.EqualTo(volume).Within(1e-6), $"{name} Volume");
            Assert.That(p.Crew, Is.EqualTo(crew), $"{name} Crew");
            Assert.That(p.Research, Is.EqualTo(research), $"{name} Research");
            Assert.That(p.Credit, Is.EqualTo(credit), $"{name} Credit");
            Assert.That(p.BuildPoints, Is.EqualTo(buildPoints), $"{name} BuildPoints");
            return p;
        }

        private static void AssertResources(string name, CivicProfile p, Dictionary<string, long> expected)
        {
            Assert.That(p.ResourceCosts.Count, Is.EqualTo(expected.Count), $"{name} resource-cost key count");
            foreach (var kvp in expected)
            {
                Assert.That(p.ResourceCosts.ContainsKey(kvp.Key), Is.True, $"{name} missing resource '{kvp.Key}'");
                Assert.That(p.ResourceCosts[kvp.Key], Is.EqualTo(kvp.Value), $"{name} resource '{kvp.Key}'");
            }
        }

        [Test]
        [Description("The two FOOD designs (agri-complex quality 1.0, hydroponics-arcology quality 2.5/automation 0.3) "
                     + "fall out of the Food function: mass cubes the quality dial, automation trades crew for mass.")]
        public void FoodBuildings_ReproduceFromTheForm()
        {
            // agri-complex: Food Output 5000, Food Quality 1.0, Automation 0
            // Mass = 5000*0.1 + 1.0^3*200 + 0 = 700 ; Crew = Max(1, 5000*0.02*1) = 100 ; RP = 700*2 = 1400 ; Cr/BP = 700
            var agri = AssertCost("agri-complex",
                new CivicDesignModel(CivicFunction.Food, capacity: 5000, quality: 1.0, automation: 0),
                CivicFunction.Food, mass: 700, volume: 700, crew: 100, research: 1400, credit: 700, buildPoints: 700);
            Assert.That(agri.FoodOutput, Is.EqualTo(5000).Within(1e-6), "agri FoodOutput");
            Assert.That(agri.FoodQuality, Is.EqualTo(1.0).Within(1e-6), "agri FoodQuality");
            AssertResources("agri-complex", agri, new Dictionary<string, long>
            {
                ["stainless-steel"] = 280, ["plastic"] = 140, ["water"] = 140, ["aluminium"] = 140
            });

            // hydroponics-arcology: Food Output 5000, Food Quality 2.5, Automation 0.3
            // Mass = 500 + 2.5^3*200 + 0.3*500 = 500 + 3125 + 150 = 3775 ; Crew = Max(1, 100*0.7) = 70 ; RP = 7550
            var hydro = AssertCost("hydroponics-arcology",
                new CivicDesignModel(CivicFunction.Food, capacity: 5000, quality: 2.5, automation: 0.3),
                CivicFunction.Food, mass: 3775, volume: 3775, crew: 70, research: 7550, credit: 3775, buildPoints: 3775);
            Assert.That(hydro.FoodOutput, Is.EqualTo(5000).Within(1e-6), "hydro FoodOutput");
            Assert.That(hydro.FoodQuality, Is.EqualTo(2.5).Within(1e-6), "hydro FoodQuality");
        }

        [Test]
        [Description("The single-dial civic terms — a Security precinct (rating 10) and a Medical hospital (care 10) — "
                     + "share the Rating*400 mass shape; the Medical dial 'Care Rating' maps to the atb's HealthRating.")]
        public void SecurityAndMedical_ReproduceFromTheForm()
        {
            // security-precinct: Security Rating 10 → Mass = 10*400 = 4000 ; Crew = Max(1, 10*5) = 50 ; RP/Cr/BP = 4000
            var sec = AssertCost("security-precinct",
                new CivicDesignModel(CivicFunction.Security, capacity: 10),
                CivicFunction.Security, mass: 4000, volume: 4000, crew: 50, research: 4000, credit: 4000, buildPoints: 4000);
            Assert.That(sec.SecurityRating, Is.EqualTo(10).Within(1e-6), "security SecurityRating");
            AssertResources("security-precinct", sec, new Dictionary<string, long>
            {
                ["stainless-steel"] = 2400, ["plastic"] = 1600
            });

            // hospital: Care Rating 10 → identical cost shape ; the atb field is HealthRating
            var hosp = AssertCost("hospital",
                new CivicDesignModel(CivicFunction.Medical, capacity: 10),
                CivicFunction.Medical, mass: 4000, volume: 4000, crew: 50, research: 4000, credit: 4000, buildPoints: 4000);
            Assert.That(hosp.HealthRating, Is.EqualTo(10).Within(1e-6), "hospital HealthRating (Care Rating -> HealthRating)");
        }

        [Test]
        [Description("Life-support infrastructure: mass is a flat 1000 (no dial in the formula), it binds all four host "
                     + "attributes (pop support / housing / capacity / storage) plus the grav/press envelope.")]
        public void LifeSupportInfrastructure_ReproducesFromTheForm()
        {
            // infrastructure: Support Colonists 500, Housing Comfort 5, Support Capacity 1000, Storage 500,
            //                 grav 8.8-10.8 / press 0.9-1.1 → Mass = 1000 (constant) ; Crew = 10 ; RP=0 Cr=0 BP=100
            var infra = AssertCost("infrastructure",
                new CivicDesignModel(CivicFunction.LifeSupport, capacity: 500, quality: 5,
                    supportCapacity: 1000, storageAmount: 500,
                    minGravity: 8.8, maxGravity: 10.8, minPressure: 0.9, maxPressure: 1.1),
                CivicFunction.LifeSupport, mass: 1000, volume: 1000, crew: 10, research: 0, credit: 0, buildPoints: 100);

            Assert.That(infra.PopulationCapacity, Is.EqualTo(500), "infra PopulationCapacity");
            Assert.That(infra.HousingComfort, Is.EqualTo(5).Within(1e-6), "infra HousingComfort");
            Assert.That(infra.InfrastructureCapacity, Is.EqualTo(1000).Within(1e-6), "infra Support Capacity");
            Assert.That(infra.StorageAmount, Is.EqualTo(500).Within(1e-6), "infra Storage Amount");
            Assert.That(infra.MinGravity, Is.EqualTo(8.8).Within(1e-6), "infra Min Gravity");
            Assert.That(infra.MaxGravity, Is.EqualTo(10.8).Within(1e-6), "infra Max Gravity");
            Assert.That(infra.MinPressure, Is.EqualTo(0.9).Within(1e-6), "infra Min Pressure");
            Assert.That(infra.MaxPressure, Is.EqualTo(1.1).Within(1e-6), "infra Max Pressure");
            AssertResources("infrastructure", infra, new Dictionary<string, long>
            {
                ["iron"] = 500, ["aluminium"] = 200, ["copper"] = 100, ["plastic"] = 100, ["stainless-steel"] = 100
            });
        }

        [Test]
        [Description("Space habitats: the shipped default (500 colonists / comfort 5 → 2000) and the Kithrin hive "
                     + "(200,000 colonists → 201,500). Mass reads the capacity + comfort dials; build points follow [Mass]/10.")]
        public void SpaceHabitats_ReproduceFromTheForm()
        {
            // space-habitat default: Support Colonists 500, Comfort 5 → Mass = 1000*(1 + 500/500*0.5 + 5/10) = 2000 ; BP = 200
            var hab = AssertCost("space-habitat",
                new CivicDesignModel(CivicFunction.SpaceHabitat, capacity: 500, quality: 5,
                    supportCapacity: 1000, storageAmount: 500),
                CivicFunction.SpaceHabitat, mass: 2000, volume: 2000, crew: 10, research: 0, credit: 0, buildPoints: 200);
            Assert.That(hab.PopulationCapacity, Is.EqualTo(500), "habitat PopulationCapacity");
            Assert.That(hab.HousingComfort, Is.EqualTo(5).Within(1e-6), "habitat HousingComfort");
            Assert.That(hab.InfrastructureCapacity, Is.EqualTo(1000).Within(1e-6), "habitat Support Capacity");
            Assert.That(hab.StorageAmount, Is.EqualTo(500).Within(1e-6), "habitat Storage Amount");

            // kithrin-hive-habitat: Support Colonists 200000 (comfort default 5) → Mass = 1000*(1 + 200000/500*0.5 + 0.5) = 201500 ; BP = 20150
            var hive = AssertCost("kithrin-hive-habitat",
                new CivicDesignModel(CivicFunction.SpaceHabitat, capacity: 200000, quality: 5,
                    supportCapacity: 1000, storageAmount: 500),
                CivicFunction.SpaceHabitat, mass: 201500, volume: 201500, crew: 10, research: 0, credit: 0, buildPoints: 20150);
            Assert.That(hive.PopulationCapacity, Is.EqualTo(200000), "hive PopulationCapacity");
        }

        [Test]
        [Description("Administration complexes: city-hall (Office Space 1000) and federation-ministry (2000). Mass = "
                     + "Office*100, crew/research follow the office dial, credit is a flat 120, and the seat scope is the AdminLevel.")]
        public void AdministrationComplexes_ReproduceFromTheForm()
        {
            // city-hall: Office Space 1000, Admin Level 5 (=Colony) → Mass = 100000, Vol = 10000, Crew = 250, RP = 500, Cr = 120, BP = 100000
            var cityHall = AssertCost("city-hall",
                new CivicDesignModel(CivicFunction.Administration, capacity: 1000, adminLevel: AdminLevel.Colony),
                CivicFunction.Administration, mass: 100000, volume: 10000, crew: 250, research: 500, credit: 120, buildPoints: 100000);
            Assert.That(cityHall.AdminLevel, Is.EqualTo(AdminLevel.Colony), "city-hall AdminLevel");
            Assert.That(cityHall.ConsoleSpace, Is.EqualTo(1000), "city-hall ConsoleSpace");

            // federation-ministry: Office Space 2000 → Mass = 200000, Vol = 20000, Crew = 500, RP = 1000, Cr = 120, BP = 200000
            var ministry = AssertCost("federation-ministry",
                new CivicDesignModel(CivicFunction.Administration, capacity: 2000, adminLevel: AdminLevel.Colony),
                CivicFunction.Administration, mass: 200000, volume: 20000, crew: 500, research: 1000, credit: 120, buildPoints: 200000);
            Assert.That(ministry.ConsoleSpace, Is.EqualTo(2000), "federation-ministry ConsoleSpace");
        }

        [Test]
        [Description("Research academies: the default (Class Size 10), Olympus (100), Kithrin nexus (50). THE TRUNCATION "
                     + "PROOF — crew = ClassSize*0.25 truncates (10->2.5->2, 50->12.5->12, NOT rounded), and the science "
                     + "domain carries the tech specialty string.")]
        public void ResearchAcademies_ReproduceFromTheForm()
        {
            // research-academy default: Class Size 10, Class Length 24 → Mass = 1000, Vol = 100, Crew = (int)2.5 = 2, RP = (int)5 = 5, Cr = 120, BP = 1000
            var academy = AssertCost("research-academy",
                new CivicDesignModel(CivicFunction.Academy, capacity: 10, quality: 24, domain: AcademyDomain.Science,
                    specialty: "tech-category-power-propulsion"),
                CivicFunction.Academy, mass: 1000, volume: 100, crew: 2, research: 5, credit: 120, buildPoints: 1000);
            Assert.That(academy.AcademyDomain, Is.EqualTo(AcademyDomain.Science), "research-academy domain");
            Assert.That(academy.ClassSize, Is.EqualTo(10), "research-academy ClassSize");
            Assert.That(academy.TrainingMonths, Is.EqualTo(24), "research-academy TrainingMonths");
            Assert.That(academy.Specialty, Is.EqualTo("tech-category-power-propulsion"), "research-academy Specialty");
            AssertResources("research-academy", academy, new Dictionary<string, long>
            {
                ["iron"] = 450, ["aluminium"] = 200, ["copper"] = 100, ["plastic"] = 100,
                ["stainless-steel"] = 100, ["electronics"] = 50
            });

            // olympus-university: Class Size 100 → Mass = 10000, Vol = 1000, Crew = 25, RP = 50, BP = 10000
            var olympus = AssertCost("olympus-university",
                new CivicDesignModel(CivicFunction.Academy, capacity: 100, quality: 24, domain: AcademyDomain.Science),
                CivicFunction.Academy, mass: 10000, volume: 1000, crew: 25, research: 50, credit: 120, buildPoints: 10000);
            Assert.That(olympus.ClassSize, Is.EqualTo(100), "olympus ClassSize");

            // kithrin-nexus: Class Size 50 → Mass = 5000, Vol = 500, Crew = (int)12.5 = 12, RP = 25, BP = 5000
            var nexus = AssertCost("kithrin-nexus",
                new CivicDesignModel(CivicFunction.Academy, capacity: 50, quality: 24, domain: AcademyDomain.Science),
                CivicFunction.Academy, mass: 5000, volume: 500, crew: 12, research: 25, credit: 120, buildPoints: 5000);
            Assert.That(nexus.ClassSize, Is.EqualTo(50), "kithrin-nexus ClassSize");
        }

        [Test]
        [Description("The naval academy (Officers domain) has NO shipped design — reproduce the TEMPLATE DEFAULT only "
                     + "(Class Size 1000, Class Length 24): Mass = 100000, Vol = 10000, Crew = 250, RP = 500, Cr = 120, BP = 100000. "
                     + "Same cost formula as the research academy; the Officers domain carries no specialty string.")]
        public void NavalAcademy_ReproducesTheTemplateDefault()
        {
            var naval = AssertCost("naval-academy(template-default)",
                new CivicDesignModel(CivicFunction.Academy, capacity: 1000, quality: 24, domain: AcademyDomain.Officers),
                CivicFunction.Academy, mass: 100000, volume: 10000, crew: 250, research: 500, credit: 120, buildPoints: 100000);
            Assert.That(naval.AcademyDomain, Is.EqualTo(AcademyDomain.Officers), "naval domain");
            Assert.That(naval.ClassSize, Is.EqualTo(1000), "naval ClassSize");
            Assert.That(naval.TrainingMonths, Is.EqualTo(24), "naval TrainingMonths");
            Assert.That(naval.Specialty, Is.Null, "a naval academy carries no research specialty");
        }
    }
}
