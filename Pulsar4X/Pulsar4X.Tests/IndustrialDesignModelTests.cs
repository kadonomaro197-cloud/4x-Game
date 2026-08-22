using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Components.Designers;
using Pulsar4X.Industry;      // MineResourcesAtbDB, IndustryAtb, LocalConstructionAtb, InfrastructureCapacityAtb
using Pulsar4X.Technology;    // ResearchPointsAtbDB
using Pulsar4X.Ships;         // LaunchComplexAtb
using Pulsar4X.Construction;  // ConstructorAtb
using Pulsar4X.GroundCombat;  // GroundConstructorAtb, GroundDefenseAtb, GroundFootprintAtb

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the INDUSTRIAL door parametric designer, slice-1 gauge.
    ///
    /// Proves the pure <see cref="IndustrialDesignModel"/> (the engine half of the one-choice/N-slider industrial form)
    /// REPRODUCES every base-mod industrial component from its choice + dials — every hand-authored plant (mine · robo-miner
    /// · refinery · factory · shipyard · research-lab · local-construction · launch-complex · field-constructor ·
    /// ground-constructor · bunker · infrastructure) falls out of the one parametric form (the DESIGNER-NORTH-STAR
    /// reproduction claim, made executable). Pure — NO <c>TestScenario.CreateWithColony</c>, so it stays out of the slow CI
    /// shard; the reproduction VALUES are the shipped template numbers (verified against
    /// <c>GameData/basemod/TemplateFiles/installations.json</c>), so a drift in the model's per-kind arithmetic fails here.
    /// Byte-identical to the live game (nothing calls the model yet).
    ///
    /// The load-bearing assertions are (1) the six COST fields (Mass / Volume / CrewReq / ResearchCost / CreditCost /
    /// BuildPointCost — the currency the plant is priced in) and (2) the exact <c>*Atb</c> constructor arguments the sim
    /// reads, INCLUDING the casts the atb ctors apply: the 15-key mineral dict with the <c>(long)</c> cast for mine AND the
    /// RoboMiner's all-zero-yield quirk (Size 5 → 0.025 → <c>(long)</c>0), the <c>(int)</c> industry-point dicts (1-arg for
    /// refinery/factory, 2-arg with MaxProductionVolume = 10,000 for the shipyard), the research triple, the
    /// local-construction (1, 5), the launch tonnage, the two constructor capacities, the bunker's two atbs, and the
    /// infrastructure capacity (Industrial owns ONLY <see cref="InfrastructureCapacityAtb"/> of the shared infrastructure
    /// template — Civic owns the other five, and this gauge asserts only Industrial's).
    /// </summary>
    [TestFixture]
    public class IndustrialDesignModelTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[industrial-model] " + m);

        // The 15 minerals a mine/robo-miner works, in template order (installations.json:255-271).
        private static readonly string[] Minerals =
        {
            "hydrocarbons", "iron", "aluminium", "copper", "lithium", "chromium", "fissionables", "titanium",
            "tungsten", "silicon", "graphite", "nickel", "water", "rare-earth-elements", "regolith"
        };

        private static void AssertCosts(string name, IndustrialProfile p,
            double mass, double volume, double crewReq, double researchCost, double creditCost, double buildPointCost)
        {
            Log($"{name}: atb={p.PrimaryAtb?.Name}{(p.SecondaryAtb != null ? "+" + p.SecondaryAtb.Name : "")} " +
                $"mass={p.Mass} vol={p.Volume} crew={p.CrewReq} research={p.ResearchCost} credit={p.CreditCost} bp={p.BuildPointCost}");
            Assert.That(p.Mass, Is.EqualTo(mass).Within(1e-6), $"{name} Mass");
            Assert.That(p.Volume, Is.EqualTo(volume).Within(1e-6), $"{name} Volume");
            Assert.That(p.CrewReq, Is.EqualTo(crewReq).Within(1e-6), $"{name} CrewReq");
            Assert.That(p.ResearchCost, Is.EqualTo(researchCost).Within(1e-6), $"{name} ResearchCost");
            Assert.That(p.CreditCost, Is.EqualTo(creditCost).Within(1e-6), $"{name} CreditCost");
            Assert.That(p.BuildPointCost, Is.EqualTo(buildPointCost).Within(1e-6), $"{name} BuildPointCost");
        }

        private static void AssertIndustryPoints(string name, IReadOnlyDictionary<string, int> points,
            Dictionary<string, int> expected)
        {
            Assert.That(points, Is.Not.Null, $"{name} industry points present");
            Assert.That(points.Count, Is.EqualTo(expected.Count), $"{name} industry-type key count");
            foreach (var kvp in expected)
            {
                Assert.That(points.ContainsKey(kvp.Key), Is.True, $"{name} has industry type {kvp.Key}");
                Assert.That(points[kvp.Key], Is.EqualTo(kvp.Value), $"{name} {kvp.Key} points (int-cast)");
            }
        }

        private static void AssertAllMinerals(string name, IReadOnlyDictionary<string, long> yield, long expected)
        {
            Assert.That(yield, Is.Not.Null, $"{name} mineral yield present");
            Assert.That(yield.Count, Is.EqualTo(Minerals.Length), $"{name} 15-key mineral set");
            foreach (var m in Minerals)
            {
                Assert.That(yield.ContainsKey(m), Is.True, $"{name} has key {m}");
                Assert.That(yield[m], Is.EqualTo(expected), $"{name} {m} yield (long-cast)");
            }
        }

        [Test]
        [Description("The two MINING plants (Mine + RoboMiner) reproduce from KIND + Area/Size: the 15-key mineral dict with the (long) cast — including the shipped RoboMiner's all-zero-yield quirk (Size 5 → 0.025 → (long)0).")]
        public void MiningPlants_ReproduceFromTheForm()
        {
            // mine: Area 1,000,000 → MiningAmount 10 → 15 minerals each (long)10 = 10.
            var mine = new IndustrialDesignModel(IndustrialKind.Mine, scale: 1_000_000).Compute();
            AssertCosts("mine", mine, mass: 50_000, volume: 10_000, crewReq: 5_000, researchCost: 0, creditCost: 120, buildPointCost: 50_000);
            Assert.That(mine.PrimaryAtb, Is.EqualTo(typeof(MineResourcesAtbDB)), "mine atb");
            AssertAllMinerals("mine", mine.MineYield, 10);

            // automine: Size 5 → MiningAmount 0.025 → 15 minerals each (long)0.025 = 0 (the shipped zero-yield quirk).
            var automine = new IndustrialDesignModel(IndustrialKind.AutoMine, scale: 5).Compute();
            AssertCosts("automine", automine, mass: 10_000, volume: 5, crewReq: 0, researchCost: 0, creditCost: 120, buildPointCost: 10_000);
            Assert.That(automine.PrimaryAtb, Is.EqualTo(typeof(MineResourcesAtbDB)), "automine atb");
            AssertAllMinerals("automine", automine.MineYield, 0);
        }

        [Test]
        [Description("The three INDUSTRY plants reproduce: refinery + factory bind the 1-arg IndustryAtb (MaxProductionVolume = +inf), the shipyard binds the 2-arg (MaxProductionVolume = Slip Size 10,000) — a wrong-overload regression fails here. Every point rate is (int)-cast.")]
        public void IndustryPlants_ReproduceFromTheForm()
        {
            // refinery: Size 5,000 → refining 500; 1-arg ctor → MaxProductionVolume = +inf.
            var refinery = new IndustrialDesignModel(IndustrialKind.Refinery, scale: 5_000).Compute();
            AssertCosts("refinery", refinery, mass: 5_000, volume: 5_000, crewReq: 500, researchCost: 0, creditCost: 120, buildPointCost: 5_000);
            Assert.That(refinery.PrimaryAtb, Is.EqualTo(typeof(IndustryAtb)), "refinery atb");
            AssertIndustryPoints("refinery", refinery.IndustryPoints, new Dictionary<string, int> { ["refining"] = 500 });
            Assert.That(refinery.MaxProductionVolume, Is.EqualTo(double.PositiveInfinity), "refinery MaxProductionVolume (1-arg ctor)");

            // factory: Size 5,000 → each of 3 construction types 500; BP = Size × 20 = 100,000; 1-arg ctor.
            var factory = new IndustrialDesignModel(IndustrialKind.Factory, scale: 5_000).Compute();
            AssertCosts("factory", factory, mass: 5_000_000, volume: 7_500, crewReq: 25_000, researchCost: 0, creditCost: 120, buildPointCost: 100_000);
            Assert.That(factory.PrimaryAtb, Is.EqualTo(typeof(IndustryAtb)), "factory atb");
            AssertIndustryPoints("factory", factory.IndustryPoints, new Dictionary<string, int>
            {
                ["component-construction"] = 500,
                ["installation-construction"] = 500,
                ["ordnance-construction"] = 500
            });
            Assert.That(factory.MaxProductionVolume, Is.EqualTo(double.PositiveInfinity), "factory MaxProductionVolume (1-arg ctor)");

            // shipyard: Slip Size 10,000 + Crew Size 10,000 → component 100 / ship-assembly 200; 2-arg ctor → MaxProductionVolume = Slip Size.
            var shipyard = new IndustrialDesignModel(IndustrialKind.Shipyard, scale: 10_000, workforce: 10_000).Compute();
            AssertCosts("shipyard", shipyard, mass: 80_000, volume: 10_000, crewReq: 10_000, researchCost: 0, creditCost: 120, buildPointCost: 80_000);
            Assert.That(shipyard.PrimaryAtb, Is.EqualTo(typeof(IndustryAtb)), "shipyard atb");
            AssertIndustryPoints("shipyard", shipyard.IndustryPoints, new Dictionary<string, int>
            {
                ["component-construction"] = 100,
                ["ship-assembly"] = 200
            });
            Assert.That(shipyard.MaxProductionVolume, Is.EqualTo(10_000).Within(1e-6), "shipyard MaxProductionVolume (2-arg ctor = Slip Size)");
        }

        [Test]
        [Description("The RESEARCH LAB reproduces the constant-mass template + the ResearchPointsAtbDB triple (Points (int) / Cost Per Day (decimal) / Specialty) — the one kind that exercises CHOICE 2 (specialty) + SLIDER 3 (cost per day).")]
        public void ResearchLab_ReproducesFromTheForm()
        {
            var lab = new IndustrialDesignModel(IndustrialKind.ResearchLab, scale: 10, secondary: 10_000,
                specialty: "tech-category-power-propulsion").Compute();
            AssertCosts("research-lab", lab, mass: 100_000, volume: 1_000, crewReq: 20, researchCost: 10, creditCost: 120, buildPointCost: 50_000);
            Assert.That(lab.PrimaryAtb, Is.EqualTo(typeof(ResearchPointsAtbDB)), "research-lab atb");
            Assert.That(lab.ResearchPoints, Is.EqualTo(10), "research-lab Points");
            Assert.That(lab.ResearchCostPerDay, Is.EqualTo(10_000m), "research-lab Cost Per Day");
            Assert.That(lab.ResearchSpecialty, Is.EqualTo("tech-category-power-propulsion"), "research-lab Specialty");
        }

        [Test]
        [Description("The four single-dial installations reproduce: local-construction LocalConstructionAtb(1, 5) (2nd arg the template constant 5), launch-complex Max Tonnage 100,000, field-constructor Construction Capacity 50,000, ground-constructor BuildRate 100.")]
        public void SingleDialInstallations_ReproduceFromTheForm()
        {
            // local-construction: Level 1 → LocalConstructionAtb(1, 5); contribution = Level × PointsPerDay = 5.
            var local = new IndustrialDesignModel(IndustrialKind.LocalConstruction, scale: 1).Compute();
            AssertCosts("local-construction", local, mass: 5_000, volume: 500, crewReq: 55, researchCost: 0, creditCost: 0, buildPointCost: 200);
            Assert.That(local.PrimaryAtb, Is.EqualTo(typeof(LocalConstructionAtb)), "local-construction atb");
            Assert.That(local.LocalConstructionLevel, Is.EqualTo(1), "local-construction Level");
            Assert.That(local.LocalConstructionPointsPerDay, Is.EqualTo(5), "local-construction PointsPerDay (template constant)");

            // launch-complex: Max Tonnage 100,000.
            var launch = new IndustrialDesignModel(IndustrialKind.LaunchComplex, scale: 100_000).Compute();
            AssertCosts("launch-complex", launch, mass: 1_000, volume: 100, crewReq: 50, researchCost: 0, creditCost: 500, buildPointCost: 1_000);
            Assert.That(launch.PrimaryAtb, Is.EqualTo(typeof(LaunchComplexAtb)), "launch-complex atb");
            Assert.That(launch.LaunchMaxTonnage, Is.EqualTo(100_000).Within(1e-6), "launch-complex Max Tonnage");

            // field-constructor: Construction Capacity 50,000.
            var field = new IndustrialDesignModel(IndustrialKind.FieldConstructor, scale: 50_000).Compute();
            AssertCosts("field-constructor", field, mass: 50_000, volume: 500, crewReq: 10, researchCost: 0, creditCost: 500, buildPointCost: 50_000);
            Assert.That(field.PrimaryAtb, Is.EqualTo(typeof(ConstructorAtb)), "field-constructor atb");
            Assert.That(field.ConstructionCapacity, Is.EqualTo(50_000).Within(1e-6), "field-constructor Construction Capacity");

            // ground-constructor: BuildRate 100.
            var ground = new IndustrialDesignModel(IndustrialKind.GroundConstructor, scale: 100).Compute();
            AssertCosts("ground-constructor", ground, mass: 200, volume: 2, crewReq: 10, researchCost: 0, creditCost: 60, buildPointCost: 400);
            Assert.That(ground.PrimaryAtb, Is.EqualTo(typeof(GroundConstructorAtb)), "ground-constructor atb");
            Assert.That(ground.GroundBuildRate, Is.EqualTo(100).Within(1e-6), "ground-constructor BuildRate");
        }

        [Test]
        [Description("The BUNKER reproduces the constant-mass template AND the TWO atbs: GroundDefenseAtb(0.25, 0.12) + GroundFootprintAtb(4) — the only kind with a secondary atb, exercising SLIDER 3 (adjacent projection) + SLIDER 4 (footprint).")]
        public void Bunker_ReproducesFromTheForm()
        {
            var bunker = new IndustrialDesignModel(IndustrialKind.Bunker, scale: 0.25, secondary: 0.12, footprint: 4).Compute();
            AssertCosts("bunker", bunker, mass: 50_000, volume: 500, crewReq: 50, researchCost: 0, creditCost: 200, buildPointCost: 50_000);
            Assert.That(bunker.PrimaryAtb, Is.EqualTo(typeof(GroundDefenseAtb)), "bunker primary atb");
            Assert.That(bunker.SecondaryAtb, Is.EqualTo(typeof(GroundFootprintAtb)), "bunker secondary atb");
            Assert.That(bunker.LocalFortify, Is.EqualTo(0.25).Within(1e-9), "bunker LocalFortify");
            Assert.That(bunker.AdjacentProjection, Is.EqualTo(0.12).Within(1e-9), "bunker AdjacentProjection");
            Assert.That(bunker.TileFootprint, Is.EqualTo(4), "bunker TileFootprint");
        }

        [Test]
        [Description("INFRASTRUCTURE reproduces the constant-mass template + InfrastructureCapacityAtb(1000) — the model asserts ONLY Industrial's atb (Support Capacity); the other five infrastructure atbs are Civic-owned and are NOT this door's, so they are deliberately absent from the model.")]
        public void Infrastructure_ReproducesFromTheForm_IndustrialAtbOnly()
        {
            var infra = new IndustrialDesignModel(IndustrialKind.Infrastructure, scale: 1_000).Compute();
            AssertCosts("infrastructure", infra, mass: 1_000, volume: 1_000, crewReq: 10, researchCost: 0, creditCost: 0, buildPointCost: 100);
            Assert.That(infra.PrimaryAtb, Is.EqualTo(typeof(InfrastructureCapacityAtb)), "infrastructure atb");
            Assert.That(infra.SecondaryAtb, Is.Null, "infrastructure has no secondary atb (Civic owns the other five)");
            Assert.That(infra.InfrastructureCapacity, Is.EqualTo(1_000L), "infrastructure Support Capacity");
        }

        [Test]
        [Description("The honesty caveat: the mineral SET is a free input defaulting to the door-forced 15. Passing a custom set reproduces exactly that set at the same (long)-cast rate — the per-instance variant a literal choice/slider form cannot force.")]
        public void MineralSet_IsAFreeInput_HonestyCaveat()
        {
            var custom = new[] { "iron", "titanium", "fissionables" };
            var mine = new IndustrialDesignModel(IndustrialKind.Mine, scale: 1_000_000, mineralKeys: custom).Compute();
            Assert.That(mine.MineYield.Count, Is.EqualTo(3), "custom mineral set honoured");
            foreach (var m in custom)
                Assert.That(mine.MineYield[m], Is.EqualTo(10L), $"custom {m} at the door-forced rate");

            // Default (no set passed) still reproduces the shipped 15.
            var defaultMine = new IndustrialDesignModel(IndustrialKind.Mine, scale: 1_000_000).Compute();
            AssertAllMinerals("default-mine", defaultMine.MineYield, 10);
        }
    }
}
