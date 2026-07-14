using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// SYSTEM ③ — sub-formation NESTING (the sub-fleet tree, ported from space). A formation can now parent
    /// sub-formations — a "Battle Group" holding a "Front Line" and an "Artillery" sub-group, each with its OWN stance,
    /// all commanded as one block. This is the ground echo of `FleetDB` nesting via `TreeHierarchyDB` with per-sub-fleet
    /// doctrine, and the structural half of Combined Arms. Engine-only → runs in CI.
    /// Design: docs/economy/UNIVERSAL-ASSEMBLY-DESIGN.md §6d.
    /// </summary>
    [TestFixture]
    public class GroundSubFormationTests
    {
        private static GroundUnitDesign Rifleman() => new GroundUnitDesign
        { UniqueID = "t-inf", Name = "Rifleman", UnitType = GroundUnitType.Infantry, Attack = 10, HitPoints = 100 };

        [Test]
        [Description("System ③: a Battle Group parents a Front Line + an Artillery sub-formation; the tree reads correctly (children, subtree formations, subtree units), the sub-groups hold INDEPENDENT stances, and re-parenting refuses a cycle.")]
        public void BattleGroup_NestsSubFormations_WithIndependentStances_AndNoCycles()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            int fac = s.Faction.Id;

            var group = GroundForces.CreateFormation(body, fac, "Battle Group");
            var front = GroundForces.CreateFormation(body, fac, "Front Line", parentFormationId: group.FormationId);
            var arty  = GroundForces.CreateFormation(body, fac, "Artillery",  parentFormationId: group.FormationId);
            var forces = body.GetDataBlob<GroundForcesDB>();

            // populate: 2 up front, 1 in the battery
            var a = GroundForces.RaiseUnit(body, Rifleman(), fac, 0);
            var b = GroundForces.RaiseUnit(body, Rifleman(), fac, 0);
            var c = GroundForces.RaiseUnit(body, Rifleman(), fac, 0);
            GroundForces.AssignUnit(front, a);
            GroundForces.AssignUnit(front, b);
            GroundForces.AssignUnit(arty, c);

            // the tree reads
            var children = GroundFormationTools.ChildFormations(forces, group);
            Assert.That(children.Select(f => f.FormationId), Is.EquivalentTo(new[] { front.FormationId, arty.FormationId }),
                "the battle group's direct children are the two sub-formations");
            Assert.That(GroundFormationTools.ChildFormations(forces, front), Is.Empty, "the front line is a leaf");

            var subtree = GroundFormationTools.SubtreeFormations(forces, group);
            Assert.That(subtree.Count, Is.EqualTo(3), "subtree = the group itself + its two sub-formations");

            var subtreeUnits = GroundFormationTools.SubtreeUnits(forces, group);
            Assert.That(subtreeUnits.Count, Is.EqualTo(3), "a battle-group command touches every unit in the whole tree");
            Assert.That(GroundFormationTools.SubtreeUnits(forces, front).Count, Is.EqualTo(2), "…and a sub-group command touches only its own");

            // independent stances — the whole point of sub-fleets: different parts fight with different postures
            front.StanceId = "offensive"; front.AttackMult = 1.25;
            arty.StanceId = "defensive";  arty.DamageTakenMult = 0.75;
            Assert.That(front.AttackMult, Is.Not.EqualTo(arty.AttackMult), "front line and artillery hold DIFFERENT stances in one force");
            Assert.That(group.ParentFormationId, Is.EqualTo(-1), "the battle group is top-level");
            Assert.That(front.ParentFormationId, Is.EqualTo(group.FormationId), "the front line nests under it");

            // re-parenting refuses a cycle (can't make the parent a child of its own descendant)
            Assert.That(GroundForces.SetParentFormation(forces, group, front.FormationId), Is.False,
                "parenting the group under its own sub-formation would be a cycle — refused");
            Assert.That(group.ParentFormationId, Is.EqualTo(-1), "so the group stays top-level");

            // a legal re-parent works (Artillery under Front Line)
            Assert.That(GroundForces.SetParentFormation(forces, arty, front.FormationId), Is.True, "a non-cyclic re-parent is allowed");
            Assert.That(GroundFormationTools.SubtreeFormations(forces, front).Count, Is.EqualTo(2), "front line now has the artillery beneath it");
        }
    }
}
