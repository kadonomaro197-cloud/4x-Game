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
        [Description("RunFleetDoctrinePolicy sets the faction doctrine on the TOP fleet but leaves each sub-fleet untouched — so it can't stomp a role doctrine.")]
        public void RunFleetDoctrinePolicy_SkipsSubFleets()
        {
            var s = TestScenario.CreateWithColony();
            var p = new PersonalityDB();
            p.SetTrait(PersonalityTrait.Aggression, 0.9);
            p.SetTrait(PersonalityTrait.Risk, 0.9);
            s.Faction.SetDataBlob(p);

            var fleet = StartFleet(s);
            var formed = FleetRoleComposer.FormRoleSubFleets(fleet);
            Assert.That(formed, Is.Not.Empty);

            NPCDecisionProcessor.RunFleetDoctrinePolicy(s.Faction);

            Assert.That(fleet.HasDataBlob<FleetDoctrineDB>(), Is.True, "the top-level fleet got the faction doctrine");
            foreach (var sub in formed.Values)
                Assert.That(sub.HasDataBlob<FleetDoctrineDB>(), Is.False, "the policy skipped the sub-fleet — no role doctrine stomped");
        }
    }
}
