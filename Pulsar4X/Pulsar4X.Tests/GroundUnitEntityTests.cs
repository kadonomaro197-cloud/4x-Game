using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Datablobs;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Units-as-entities (Option A) — SLICE 2 gauge: a raised unit gets a BACKING ENTITY carrying its design's
    /// components (the same ComponentInstancesDB a ship has), so abilities fall out via TryGetComponentsByAttribute —
    /// exactly like a ship, with no per-ability special-casing (docs/GROUND-UNITS-AS-ENTITIES-DESIGN.md). This is the
    /// fix for "why doesn't the ability just fall out": the flat snapshot threw the components away; now they're kept.
    /// </summary>
    [TestFixture]
    public class GroundUnitEntityTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[units-as-entities] " + m);

        [Test]
        [Description("Raising an assembled unit builds a backing entity whose component store carries the chassis + weapon — abilities fall out via TryGetComponentsByAttribute, like a ship.")]
        public void RaisedUnit_HasBackingEntity_CarryingItsComponents()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            var design = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "test-backed-trooper", "Backed Trooper",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)>
                {
                    (Part("default-design-ground-rifle"), 1),
                    (Part("default-design-ground-plating"), 1),
                });

            var unit = GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);

            Assert.That(unit.BackingEntityId, Is.GreaterThanOrEqualTo(0), "the raised unit has a backing entity");
            Assert.That(GroundUnitEntity.TryGetBacking(body, unit, out var backing), Is.True, "the backing entity resolves");
            Assert.That(backing.TryGetDataBlob<ComponentInstancesDB>(out var cidb), Is.True, "the backing carries a component store");

            // The ABILITY falls out of the component store — exactly the ship path.
            Assert.That(cidb.TryGetComponentsByAttribute<GroundChassisAtb>(out var chassis), Is.True, "the chassis component is present");
            Assert.That(chassis.Count, Is.EqualTo(1), "one frame");
            Assert.That(cidb.TryGetComponentsByAttribute<GroundWeaponAtb>(out var weapons), Is.True, "the rifle is present");
            Assert.That(weapons.Count, Is.EqualTo(1), "one rifle");
            Log($"backing entity #{unit.BackingEntityId} carries chassis×{chassis.Count} + weapon×{weapons.Count} (abilities fall out via the component store)");
        }

        [Test]
        [Description("A design with no component list (monolithic/plain) raises fine with no backing (-1) — additive, never throws.")]
        public void PlainDesign_RaisesWithNoBacking()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var plain = new GroundUnitDesign { UniqueID = "test-plain", Name = "Plain", UnitType = GroundUnitType.Infantry, HitPoints = 100 };
            var unit = GroundForces.RaiseUnit(body, plain, s.Faction.Id, 0);
            Assert.That(unit.BackingEntityId, Is.EqualTo(-1), "no component list -> no backing, and no throw");
        }
    }
}
