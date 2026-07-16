using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Stations;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The engine gauge for the STATION assembler readout — the compute half the Entity Assembler's station panel shows
    /// live (the parallel of <c>GroundUnitAssembly.Compute</c>). Proves the "chassis gives the budget, modules consume
    /// it" rule is REAL and mechanical: the chassis provides a STRUCTURE budget (in volume), each module consumes its
    /// volume, and an over-budget assembly comes back Invalid with a reason (never throws). Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class StationAssemblyTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[station-assembly] " + m);

        private static ComponentDesign Part(TestScenario s, string id)
            => (ComponentDesign)s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns[id];

        [Test]
        [Description("StationAssembly.Compute sums modules against the chassis's structure budget: a chassis alone is a " +
                     "valid (empty) station; the budget comes from the chassis; adding modules consumes structure and lifts " +
                     "the build mass / module count.")]
        public void Compute_ChassisGivesBudget_ModulesConsumeIt()
        {
            var s = TestScenario.CreateWithColony();
            var chassis = Part(s, "default-design-station-chassis");

            // Chassis alone — a valid empty platform, budget provided by the frame, nothing consumed yet.
            var bare = StationAssembly.Compute(chassis, new List<(ComponentDesign, int)>());
            Log($"bare: budget={bare.StructuralBudget:0} used={bare.UsedStructure:0} valid={bare.Valid} mass={bare.BuildMass:0}");
            Assert.That(bare.StructuralBudget, Is.GreaterThan(0), "the chassis provides the structure budget");
            Assert.That(bare.UsedStructure, Is.EqualTo(0), "no modules → nothing consumed");
            Assert.That(bare.ModuleCount, Is.EqualTo(0));
            Assert.That(bare.Valid, Is.True, "a bare chassis is a valid (if empty) station");
            Assert.That(bare.BuildMass, Is.GreaterThan(0), "build mass includes the chassis");

            // Add a research lab — it consumes structure and adds to the totals.
            var withLab = StationAssembly.Compute(chassis, new List<(ComponentDesign, int)>
            {
                (Part(s, "default-design-research-lab"), 1),
            });
            Log($"withLab: used={withLab.UsedStructure:0} modules={withLab.ModuleCount} mass={withLab.BuildMass:0} crew={withLab.CrewRequired}");
            Assert.That(withLab.UsedStructure, Is.GreaterThan(bare.UsedStructure), "the module consumes structure budget");
            Assert.That(withLab.ModuleCount, Is.EqualTo(1), "one module mounted");
            Assert.That(withLab.BuildMass, Is.GreaterThan(bare.BuildMass), "the module adds build mass");
            Assert.That(withLab.CrewRequired, Is.GreaterThanOrEqualTo(bare.CrewRequired), "crew is chassis + modules");
        }

        [Test]
        [Description("Over the structure budget → Invalid with a reason. A chassis with no StationChassisAtb → Invalid. " +
                     "Never throws (matches GroundUnitAssembly's defensive contract).")]
        public void Compute_OverBudget_IsInvalid_AndNullSafe()
        {
            var s = TestScenario.CreateWithColony();
            var chassis = Part(s, "default-design-station-chassis");

            // Pile on enough research labs (Vol 1000 each) to exceed even the max structure budget (10000).
            var over = StationAssembly.Compute(chassis, new List<(ComponentDesign, int)>
            {
                (Part(s, "default-design-research-lab"), 50),
            });
            Log($"over: used={over.UsedStructure:0} budget={over.StructuralBudget:0} valid={over.Valid} problems={over.Problems.Count}");
            Assert.That(over.UsedStructure, Is.GreaterThan(over.StructuralBudget), "50 labs blow past the budget");
            Assert.That(over.Valid, Is.False, "over budget → invalid");
            Assert.That(over.Problems, Is.Not.Empty, "an invalid assembly reports why");

            // A non-chassis part passed as the frame → invalid, not a throw.
            var notAChassis = StationAssembly.Compute(Part(s, "default-design-research-lab"), new List<(ComponentDesign, int)>());
            Assert.That(notAChassis.Valid, Is.False, "a part with no StationChassisAtb is not a valid frame");
            Assert.That(notAChassis.Problems, Is.Not.Empty);

            // Null frame → invalid, not a throw.
            Assert.DoesNotThrow(() => StationAssembly.Compute(null, null));
            Assert.That(StationAssembly.Compute(null, null).Valid, Is.False);
        }
    }
}
