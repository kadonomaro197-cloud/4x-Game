using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Components;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The "turn resources into products" gauge — the BUILD link, the last node of the Stage-0 economy
    /// substrate (gather -> refine -> BUILD). Mining and refining are already gauged in EconomyReadoutTests;
    /// this proves the colony can consume stored materials and produce a finished, INSTALLED product.
    ///
    /// It builds a Refinery (an installation), installed on the colony. Refinery costs only iron/aluminium/
    /// copper — all in the starting cargo, so it needs no prior refining — and ~5000 build points at the
    /// Factory's 500/day installation-construction rate, so ~10 days. The path exercised here —
    /// IndustryProcessor -> ConstructStuff (consumes resources) -> ComponentDesign.OnConstructionComplete ->
    /// AddComponent(InstallOn) — is the EXACT mechanism a built ground unit will ride to reach its holding
    /// DataBlob. If this is green, "resources -> products" works and units are a data/DataBlob problem, not a
    /// pipeline problem.
    /// </summary>
    [TestFixture]
    public class ProductionBuildTests
    {
        [Test]
        [Description("The factory consumes stored minerals and installs a new Refinery on the colony.")]
        public void Factory_TurnsResourcesInto_AnInstalledProduct()
        {
            var s = TestScenario.CreateWithColony();

            int before = CountInstalled(s.Colony, "Refinery");
            TestContext.Progress.WriteLine($"[build] Refineries installed at start: {before}");

            // Order one Refinery, installed on the colony (InstallOn) rather than stockpiled.
            s.QueueProductionJob("default-design-refinery", count: 1, installOnColony: true);

            // ~10 days to build; give it 60 to be safe against infra throttling / scheduling.
            s.AdvanceTime(TimeSpan.FromDays(60));

            int after = CountInstalled(s.Colony, "Refinery");
            TestContext.Progress.WriteLine($"[build] Refineries installed after 60 days: {after}");

            Assert.That(after, Is.EqualTo(before + 1),
                "The factory did not turn stored minerals into a new INSTALLED Refinery over 60 days. " +
                "The build link is IndustryProcessor -> ConstructStuff (resource consume) -> " +
                "ComponentDesign.OnConstructionComplete -> AddComponent(InstallOn). Check the job Status " +
                "(MissingResources / not Processing), the Factory's installation-construction rate, and that " +
                "InstallOn was set so the product installs instead of going to cargo.");
        }

        private static int CountInstalled(Entity colony, string designName)
        {
            if (!colony.TryGetDataBlob<ComponentInstancesDB>(out var comps))
                return 0;
            return comps.DesignsAndComponentCount
                .Where(kv => kv.Key.Name == designName)
                .Sum(kv => kv.Value);
        }
    }
}
