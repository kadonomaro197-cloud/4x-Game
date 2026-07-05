using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// GROUND-UNIT DESIGNER track, slice G-D3 — THE ASSEMBLER. A ground unit's stats EMERGE from its parts (like a
    /// ship), and the one rule ships lack — the carry gate — is enforced: a bare human frame can't shoulder the heavy
    /// autocannon, but bolt on power armour (+strength) and it can (the developer's core "Space Marine" story, now a
    /// live, CI-tested rule). Uses the real base-mod parts through the faction's designs. Engine-only → runs in CI.
    /// Design: docs/GROUND-UNIT-DESIGNER-DESIGN.md → §2 (emergence) + §4 (the gate).
    /// </summary>
    [TestFixture]
    public class GroundUnitAssemblyTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[assemble] " + m);

        private static TestScenario _s;
        private static ComponentDesign Part(string id)
            => (ComponentDesign)_s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns[id];

        [Test]
        [Description("G-D3 emergence: a Guardsman (human frame + rifle + plating) assembles legally and its stats are the SUM of the parts — attack from the rifle, HP from frame + plating, reach from the weapon.")]
        public void Guardsman_AssemblesLegally_AndStatsEmergeFromParts()
        {
            _s = TestScenario.CreateWithColony();
            var frame = Part("default-design-human-frame");
            var parts = new List<(ComponentDesign, int)>
            {
                (Part("default-design-ground-rifle"), 1),
                (Part("default-design-ground-plating"), 1),
            };
            var r = GroundUnitAssembly.Compute(frame, parts);
            Log($"Guardsman: valid={r.Valid} atk={r.Attack:0} hp={r.HitPoints:0} range={r.Range} used={r.UsedCapacity:0}/{r.CarryCapacity:0}");

            Assert.That(r.Valid, Is.True, "the loadout fits the frame's carry budget");
            Assert.That(r.Attack, Is.EqualTo(40), "attack = the rifle (emergent)");
            Assert.That(r.Range, Is.EqualTo(1), "reach = the rifle");
            Assert.That(r.HitPoints, Is.EqualTo(350), "HP = frame 200 + plating 150 (emergent)");
            Assert.That(r.CarryClass, Is.EqualTo(GroundCarryClass.Personnel), "carry-class from the frame (→ troop bay)");
        }

        [Test]
        [Description("G-D3 the gate BLOCKS: a bare human frame cannot mount the heavy autocannon — it's over both the per-item weight limit and the total carry budget.")]
        public void BareHuman_CannotCarryTheAutocannon()
        {
            _s = TestScenario.CreateWithColony();
            var frame = Part("default-design-human-frame");
            var parts = new List<(ComponentDesign, int)> { (Part("default-design-ground-autocannon"), 1) };

            var r = GroundUnitAssembly.Compute(frame, parts);
            var probs = string.Join(" | ", r.Problems);
            Log($"bare+autocannon: valid={r.Valid} problems={probs}");
            Assert.That(r.Valid, Is.False, "a bare human can't shoulder a 120-mass autocannon (strength 100, max-item 50)");
            Assert.That(r.Problems, Is.Not.Empty, "and it says why");
        }

        [Test]
        [Description("G-D3 the gate UNLOCKS (the Space Marine story): the SAME human frame + power armour CAN mount the autocannon — the augment's +300 strength raises the carry budget (100 → 400) and the max-item limit, so the heavy weapon now fits and the unit's attack is the autocannon's.")]
        public void PowerArmour_UnlocksTheAutocannon_OnTheSameFrame()
        {
            _s = TestScenario.CreateWithColony();
            var frame = Part("default-design-human-frame");
            var parts = new List<(ComponentDesign, int)>
            {
                (Part("default-design-power-armor"), 1),
                (Part("default-design-ground-autocannon"), 1),
            };
            var r = GroundUnitAssembly.Compute(frame, parts);
            Log($"Marine: valid={r.Valid} cap={r.CarryCapacity:0} used={r.UsedCapacity:0} maxItem={r.MaxItemWeight:0} atk={r.Attack:0} hp={r.HitPoints:0}");

            Assert.That(r.CarryCapacity, Is.EqualTo(400), "power armour's +300 strength raised the budget from 100 to 400");
            Assert.That(r.Valid, Is.True, "so the SAME frame that couldn't carry the autocannon now can");
            Assert.That(r.Attack, Is.EqualTo(140), "and it hits with the autocannon (emergent)");
            Assert.That(r.HitPoints, Is.EqualTo(240), "HP = frame 200 × 1.2 toughness from the power armour");
        }

        [Test]
        [Description("G-D3b the SAME assembler builds a VEHICLE (essence-axis coverage): a Battle Tank = tracked vehicle frame + tank cannon + plating assembles legally, its big frame carries the heavy cannon a human never could, its stats emerge, and it's a Vehicle carry-class (hauled by a vehicle bay, not a troop bay).")]
        public void BattleTank_AssemblesOnAVehicleFrame_AndIsVehicleCarryClass()
        {
            _s = TestScenario.CreateWithColony();
            var frame = Part("default-design-vehicle-frame");
            var parts = new List<(ComponentDesign, int)>
            {
                (Part("default-design-ground-cannon"), 1),
                (Part("default-design-ground-plating"), 1),
            };
            var r = GroundUnitAssembly.Compute(frame, parts);
            Log($"Tank: valid={r.Valid} cap={r.CarryCapacity:0} used={r.UsedCapacity:0} atk={r.Attack:0} range={r.Range} hp={r.HitPoints:0} class={r.CarryClass}");

            Assert.That(r.Valid, Is.True, "the vehicle frame (strength 800) easily carries the 300-mass cannon");
            Assert.That(r.Attack, Is.EqualTo(220), "attack = the tank cannon (emergent)");
            Assert.That(r.Range, Is.EqualTo(3), "reach = the cannon");
            Assert.That(r.HitPoints, Is.EqualTo(1650), "HP = frame 1500 + plating 150");
            Assert.That(r.CarryClass, Is.EqualTo(GroundCarryClass.Vehicle), "a vehicle — hauled by a vehicle bay, not a troop bay");

            // and the cannon is beyond ANY infantry frame — the gate holds across scales
            var human = Part("default-design-human-frame");
            var onHuman = GroundUnitAssembly.Compute(human, new List<(ComponentDesign, int)> { (Part("default-design-ground-cannon"), 1) });
            Assert.That(onHuman.Valid, Is.False, "no human frame can shoulder a 300-mass tank cannon");
        }

        [Test]
        [Description("G-D3c the CONNECT: an assembly becomes a BUILDABLE unit and, when raised, the unit carries the EMERGENT stats. ToGroundUnitDesign(frame+parts) → a GroundUnitDesign with summed stats + costs → GroundForces.RaiseUnit → a GroundUnit whose attack/HP are the assembly's. The designer is now wired to a real unit on the ground.")]
        public void Assembly_BecomesABuildableUnit_ThatRaisesWithEmergentStats()
        {
            _s = TestScenario.CreateWithColony();
            var frame = Part("default-design-human-frame");
            var parts = new List<(ComponentDesign, int)>
            {
                (Part("default-design-ground-rifle"), 1),
                (Part("default-design-ground-plating"), 1),
            };

            var design = GroundUnitAssembly.ToGroundUnitDesign("test-guardsman", "Guardsman", frame, parts);
            Assert.That(design.Attack, Is.EqualTo(40), "the buildable design carries the emergent attack");
            Assert.That(design.HitPoints, Is.EqualTo(350), "…and the emergent HP");
            Assert.That(design.Range, Is.EqualTo(1), "…and reach");
            Assert.That(design.UnitType, Is.EqualTo(GroundUnitType.Infantry), "a foot/personnel frame → Infantry (triangle still works)");
            Assert.That(design.ResourceCosts, Is.Not.Empty, "cost = the sum of the parts' costs (frame + rifle + plating)");
            Assert.That(design.IndustryPointCosts, Is.GreaterThan(0), "and build points sum too");

            // the full chain: build it → a real unit on the ground with the assembled stats
            var unit = GroundForces.RaiseUnit(_s.StartingBody, design, _s.Faction.Id, 0, "1st Guards");
            Assert.That(unit.Attack, Is.EqualTo(40), "the RAISED unit fights with the assembly's attack");
            Assert.That(unit.MaxHealth, Is.EqualTo(350), "and the assembly's HP — parts → design → unit, end to end");

            // a vehicle assembly derives Armor
            var tank = GroundUnitAssembly.ToGroundUnitDesign("test-tank", "Tank", Part("default-design-vehicle-frame"),
                new List<(ComponentDesign, int)> { (Part("default-design-ground-cannon"), 1) });
            Assert.That(tank.UnitType, Is.EqualTo(GroundUnitType.Armor), "a vehicle frame → Armor");
        }
    }
}
