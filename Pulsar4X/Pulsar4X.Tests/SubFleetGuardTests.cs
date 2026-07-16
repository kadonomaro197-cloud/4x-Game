using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;    // CombatEngagement, FleetCombat, FleetDoctrineDB
using Pulsar4X.Engine;
using Pulsar4X.Factions;  // PersonalityDB, NPCDecisionProcessor
using Pulsar4X.Fleets;    // FleetDB, FleetRoleComposer, FleetTools

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase B-2c1 gauge — the THREE guards that let a role sub-fleet be LIVE without breaking anything, each proven
    /// additive/byte-identical for a default (flat-fleet) game:
    ///   1. movement recurses (`FleetTools.AllShipsRecursive`) so a fleet decomposed into role sub-fleets still moves
    ///      ALL its ships instead of leaving the now-nested ones behind;
    ///   2. combat enrols TOP-LEVEL fleets only (`CombatEngagement.IsSubFleet`) so a sub-fleet's ships aren't
    ///      double-counted (the parent's recursive resolve already fights them);
    ///   3. the AI's whole-faction doctrine policy skips sub-fleets so it won't stomp their (future) role doctrine.
    /// No sub-fleet is ever formed in a default game, so all three read inert until B-2c2 wires the AI to form them.
    /// Drives the methods directly (no clock advance — the combat-interrupt fine-step hangs CI in a war).
    /// </summary>
    [TestFixture]
    public class SubFleetGuardTests
    {
        private static Entity StartFleet(TestScenario s)
            => s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>()
                .First(f => f.FactionOwnerID == s.Faction.Id && FleetCombat.Ships(f).Count > 0);

        private static System.Collections.Generic.List<Entity> DirectShips(Entity fleet)
            => fleet.GetDataBlob<FleetDB>().GetChildren()
                .Where(c => c.HasDataBlob<Pulsar4X.Ships.ShipInfoDB>()).ToList();

        // --- Guard 1: movement recursion (a decomposed fleet still moves as one) ----------------------------

        [Test]
        [Description("On a flat fleet, AllShipsRecursive returns exactly the direct ships — byte-identical to the old direct-children walk the move orders used.")]
        public void AllShipsRecursive_FlatFleet_EqualsDirectShips()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = StartFleet(s);
            CollectionAssert.AreEquivalent(DirectShips(fleet), FleetTools.AllShipsRecursive(fleet));
        }

        [Test]
        [Description("After decomposing into sub-fleets, the direct-children walk finds ZERO ships but AllShipsRecursive still finds them all — the move-as-one fix.")]
        public void AllShipsRecursive_AfterForming_ReturnsAllNestedShips()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = StartFleet(s);
            var before = FleetTools.AllShipsRecursive(fleet);
            Assert.That(before, Is.Not.Empty);

            FleetRoleComposer.FormRoleSubFleets(fleet);

            Assert.That(DirectShips(fleet), Is.Empty, "the flat fleet decomposed — the old direct walk would move nothing");
            CollectionAssert.AreEquivalent(before, FleetTools.AllShipsRecursive(fleet), "the recursive walk still reaches every ship");
        }

        // --- Guard 2: combat enrols top-level fleets only ---------------------------------------------------

        [Test]
        [Description("A normal fleet (parent is the faction) reads NOT a sub-fleet; a formed role sub-fleet (parent is a fleet) reads IS one — the combat-enrol discriminator.")]
        public void IsSubFleet_TrueForRoleSubFleet_FalseForTopLevel()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = StartFleet(s);
            Assert.That(CombatEngagement.IsSubFleet(fleet), Is.False, "a top-level fleet is not a sub-fleet");

            var formed = FleetRoleComposer.FormRoleSubFleets(fleet);
            Assert.That(formed, Is.Not.Empty);
            foreach (var sub in formed.Values)
                Assert.That(CombatEngagement.IsSubFleet(sub), Is.True, "a formed role sub-fleet is a sub-fleet");
        }

        // --- Guard 3: the whole-faction doctrine policy skips sub-fleets ------------------------------------

        [Test]
        [Description("The faction-wide doctrine loop skips sub-fleets: after the policy, the TOP fleet carries the faction doctrine (Offensive for an aggressive faction) while each sub-fleet keeps its own ROLE doctrine — NOT stomped to the faction pick.")]
        public void RunFleetDoctrinePolicy_LeavesSubFleetsTheirRoleDoctrine_NotTheFactionPick()
        {
            var s = TestScenario.CreateWithColony();
            var p = new PersonalityDB();
            p.SetTrait(PersonalityTrait.Aggression, 0.9);   // → the faction picks the Offensive doctrine
            p.SetTrait(PersonalityTrait.Risk, 0.9);
            s.Faction.SetDataBlob(p);

            // Pick the BIGGEST owned fleet (the start has 5 ships across 3 fleets → the largest has ≥2), so it
            // actually decomposes — ApplyRoleDoctrines only forms sub-fleets for a 2+-ship fleet.
            var fleet = s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>()
                .Where(f => f.FactionOwnerID == s.Faction.Id)
                .OrderByDescending(f => FleetCombat.Ships(f).Count).First();
            Assert.That(FleetCombat.Ships(fleet).Count, Is.GreaterThanOrEqualTo(2), "need a multi-ship fleet to decompose");

            NPCDecisionProcessor.RunFleetDoctrinePolicy(s.Faction);

            Assert.That(fleet.HasDataBlob<FleetDoctrineDB>(), Is.True, "the top-level fleet got the faction doctrine");
            Assert.That(fleet.GetDataBlob<FleetDoctrineDB>().Family, Is.EqualTo("Offensive"), "aggressive faction → Offensive on the top fleet");

            // The role sub-fleets each carry the family their JOB wants — the guard kept the whole-faction loop from
            // overwriting them with the faction's Offensive pick (Line→Utilitarian, Artillery/Support→Defensive).
            var expected = new System.Collections.Generic.Dictionary<FleetRole, string>
            {
                { FleetRole.Screen, "Offensive" }, { FleetRole.Line, "Utilitarian" },
                { FleetRole.Artillery, "Defensive" }, { FleetRole.Support, "Defensive" },
            };
            var subs = fleet.GetDataBlob<FleetDB>().GetChildren().Where(c => c.HasDataBlob<FleetRoleDB>()).ToList();
            Assert.That(subs, Is.Not.Empty, "the multi-ship fleet decomposed into role sub-fleets");
            foreach (var sub in subs)
            {
                var role = sub.GetDataBlob<FleetRoleDB>().Role;
                Assert.That(sub.HasDataBlob<FleetDoctrineDB>(), Is.True, $"the {role} sub-fleet has its role doctrine");
                Assert.That(sub.GetDataBlob<FleetDoctrineDB>().Family, Is.EqualTo(expected[role]),
                    $"the {role} sub-fleet kept its role family, not the faction's Offensive pick");
            }
        }
    }
}
