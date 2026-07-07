using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Units-as-entities (Option A) — SLICE 1 gauge: the assembler must KEEP the unit's mounted components (frame +
    /// parts) on the design, not just the flattened combat stats. This is the foundation for turning a raised unit
    /// into an entity that carries those components, so every ability falls out of the shared component infrastructure
    /// (docs/GROUND-UNITS-AS-ENTITIES-DESIGN.md). Additive — the flat stats still drive combat today.
    /// </summary>
    [TestFixture]
    public class GroundUnitComponentsTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[units-as-entities] " + m);

        [Test]
        [Description("Assembling a unit (frame + rifle + plating) records the mounted component ids -> count on the design.")]
        public void AssembledDesign_KeepsItsComponentList()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            var design = GroundUnitAssembly.ToGroundUnitDesign(
                "test-components-trooper", "Components Trooper",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)>
                {
                    (Part("default-design-ground-rifle"), 1),
                    (Part("default-design-ground-plating"), 1),
                });

            Assert.That(design.ComponentDesignIds, Is.Not.Null);
            Assert.That(design.ComponentDesignIds.ContainsKey("default-design-human-frame"), Is.True, "the chassis frame is kept");
            Assert.That(design.ComponentDesignIds.ContainsKey("default-design-ground-rifle"), Is.True, "the rifle is kept");
            Assert.That(design.ComponentDesignIds.ContainsKey("default-design-ground-plating"), Is.True, "the plating is kept");
            Assert.That(design.ComponentDesignIds["default-design-ground-rifle"], Is.EqualTo(1), "part count is recorded");
            Log($"kept {design.ComponentDesignIds.Count} component(s): " + string.Join(", ", design.ComponentDesignIds.Keys));
        }

        [Test]
        [Description("Part counts accumulate — two rifles record count 2.")]
        public void AssembledDesign_AccumulatesPartCounts()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            var design = GroundUnitAssembly.ToGroundUnitDesign(
                "test-two-rifles", "Two Rifles",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)> { (Part("default-design-ground-rifle"), 2) });

            Assert.That(design.ComponentDesignIds["default-design-ground-rifle"], Is.EqualTo(2), "two rifles record count 2");
        }
    }
}
