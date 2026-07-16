using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Colonies;
using Pulsar4X.GroundCombat; // GroundFootprintAtb
using Pulsar4X.Interfaces;   // ChassisBudgetKind, IChassisAtb
using Pulsar4X.DataStructures; // ComponentMountType
using Pulsar4X.Datablobs;    // ComponentInstancesDB

namespace Pulsar4X.Tests
{
    /// <summary>
    /// BUILDING-as-assembly — the engine gauge for the foundation+modules building buildable (the on-world twin of
    /// StationDesignTests / StationAssemblyTests). Proves: the base-mod foundation binds its BuildingChassisAtb (Footprint
    /// budget) AND a GroundFootprintAtb (so a finished building is a located surface presence); the assembler sums a
    /// foundation + modules against the footprint budget; RegisterBuildingDesign makes a buildable; and completing a
    /// building build INSTALLS its foundation + modules on the building colony. Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class BuildingDesignTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[building] " + m);

        private static ComponentDesign Part(TestScenario s, string id)
            => (ComponentDesign)s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns[id];

        [Test]
        [Description("The base-mod building-foundation loads onto the start faction and binds a BuildingChassisAtb (Footprint " +
                     "budget) AND a GroundFootprintAtb (located surface presence) from JSON — the gotcha-10 template->atb path.")]
        public void BuildingFoundation_LoadsFromJson_BindsTheAtbs()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            Assert.That(designs.ContainsKey("default-design-building-foundation"), Is.True,
                "the building foundation loads onto the faction (the six-point chain is wired)");
            var foundation = designs["default-design-building-foundation"] as ComponentDesign;
            Assert.That(foundation, Is.Not.Null, "the foundation is a ComponentDesign");

            Assert.That(foundation.HasAttribute<BuildingChassisAtb>(), Is.True, "the JSON bound a BuildingChassisAtb (budget)");
            Assert.That(foundation.HasAttribute<GroundFootprintAtb>(), Is.True, "the JSON bound a GroundFootprintAtb (located presence)");

            IChassisAtb asChassis = foundation.GetAttribute<BuildingChassisAtb>();
            Log($"foundation: budget={asChassis.StructuralBudget:0} kind={asChassis.BudgetKind} mount={asChassis.PartMount}");
            Assert.That(asChassis.BudgetKind, Is.EqualTo(ChassisBudgetKind.Footprint), "a building foundation budgets in Footprint");
            Assert.That(asChassis.StructuralBudget, Is.GreaterThan(0), "it carries a real footprint budget from the template");
            Assert.That(asChassis.PartMount, Is.EqualTo(ComponentMountType.PlanetInstallation), "its modules mount as planet installations");
        }

        [Test]
        [Description("BuildingAssembly.Compute sums modules against the foundation's footprint budget: a foundation alone " +
                     "is a valid empty building; adding modules consumes footprint and lifts mass/module count.")]
        public void Compute_FoundationGivesBudget_ModulesConsumeIt()
        {
            var s = TestScenario.CreateWithColony();
            var foundation = Part(s, "default-design-building-foundation");

            var bare = BuildingAssembly.Compute(foundation, new List<(ComponentDesign, int)>());
            Assert.That(bare.FootprintBudget, Is.GreaterThan(0), "the foundation provides the footprint budget");
            Assert.That(bare.UsedFootprint, Is.EqualTo(0), "no modules → nothing consumed");
            Assert.That(bare.Valid, Is.True, "a bare foundation is a valid (empty) building");

            var withLab = BuildingAssembly.Compute(foundation, new List<(ComponentDesign, int)>
            {
                (Part(s, "default-design-research-lab"), 1),
            });
            Log($"withLab: used={withLab.UsedFootprint:0} modules={withLab.ModuleCount} mass={withLab.BuildMass:0}");
            Assert.That(withLab.UsedFootprint, Is.GreaterThan(bare.UsedFootprint), "the module consumes footprint budget");
            Assert.That(withLab.ModuleCount, Is.EqualTo(1), "one module mounted");
            Assert.That(withLab.BuildMass, Is.GreaterThan(bare.BuildMass), "the module adds build mass");

            // Pile on enough labs to exceed even the max footprint budget → invalid.
            var over = BuildingAssembly.Compute(foundation, new List<(ComponentDesign, int)> { (Part(s, "default-design-research-lab"), 50) });
            Assert.That(over.Valid, Is.False, "over the footprint budget → invalid");
            Assert.That(over.Problems, Is.Not.Empty, "an invalid assembly reports why");
        }

        [Test]
        [Description("Completing a building build INSTALLS its foundation + modules on the building colony (industryEntity) " +
                     "— driven through OnConstructionComplete directly (deterministic). Never throws.")]
        public void BuildingBuild_OnCompletion_InstallsOnTheColony()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();

            var design = BuildingDesign.RegisterBuildingDesign(
                faction, "test-building-factory", "Test Factory Building",
                Part(s, "default-design-building-foundation"),
                new List<(ComponentDesign, int)>
                {
                    (Part(s, "default-design-research-lab"), 1),
                });

            var comps = s.Colony.GetDataBlob<ComponentInstancesDB>();
            int foundationsBefore = comps.AllComponents.Values.Count(c => c.Design.UniqueID == "default-design-building-foundation");

            var storage = s.Colony.GetDataBlob<Pulsar4X.Storage.CargoStorageDB>();
            var job = new Pulsar4X.Industry.IndustryJob(faction, design.UniqueID);
            Assert.DoesNotThrow(() => design.OnConstructionComplete(s.Colony, storage, "line", job, design),
                "the install hook never throws in the industry hotloop (gotcha L4)");

            int foundationsAfter = comps.AllComponents.Values.Count(c => c.Design.UniqueID == "default-design-building-foundation");
            Log($"foundations on colony: before={foundationsBefore} after={foundationsAfter}");
            Assert.That(foundationsAfter, Is.EqualTo(foundationsBefore + 1),
                "the building's foundation was installed on the building colony");
            Assert.That(comps.AllComponents.Values.Any(c => c.Design.UniqueID == "default-design-research-lab"), Is.True,
                "the building's research module was installed on the building colony");
        }
    }
}
