using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;    // FleetCombat, FleetDoctrineDB, EngagementPosture, FleetCombatStateDB
using Pulsar4X.Engine;
using Pulsar4X.Factions;  // PersonalityDB, NPCDecisionProcessor
using Pulsar4X.Fleets;    // FleetDB, FleetRole, FleetRoleDB

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase B-2c2 gauge — the AI now ORGANISES its fleets for battle (Q7): `RunFleetDoctrinePolicy` decomposes each
    /// multi-ship fleet into role sub-fleets and gives EACH the fighting stance its job wants — the fast Screen goes
    /// all-out-attack + weapons-free, the Line balanced, long-reach Artillery a defensive (survive-at-range) stance
    /// that still fires, and Support tenders defensive + HOLD FIRE (kept out of the shooting). Proves the whole chain
    /// (classify → form → per-role doctrine) runs end-to-end off a real start fleet, conserves ships, and skips a
    /// fleet mid-battle. Gated by EnableOrderEmission at the call site, so a default game is byte-identical; drives the
    /// method directly (no clock advance — combat-interrupt fine-steps hang CI).
    /// </summary>
    [TestFixture]
    public class NPCRoleDoctrineTests
    {
        private static readonly Dictionary<FleetRole, (string family, EngagementPosture posture)> Expected = new()
        {
            { FleetRole.Screen,    ("Offensive",   EngagementPosture.WeaponsFree) },
            { FleetRole.Line,      ("Utilitarian", EngagementPosture.WeaponsFree) },
            { FleetRole.Artillery, ("Defensive",   EngagementPosture.WeaponsFree) },
            { FleetRole.Support,   ("Defensive",   EngagementPosture.WeaponsHold) },
        };

        private static Entity BiggestOwnedFleet(TestScenario s)
            => s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>()
                .Where(f => f.FactionOwnerID == s.Faction.Id)
                .OrderByDescending(f => FleetCombat.Ships(f).Count).First();

        [Test]
        [Description("A multi-ship fleet is decomposed into role sub-fleets, each carrying the doctrine + posture its role wants; no ship is lost.")]
        public void RunFleetDoctrinePolicy_FormsRoleSubFleets_WithRoleDoctrine()
        {
            var s = TestScenario.CreateWithColony();
            var p = new PersonalityDB();
            p.SetTrait(PersonalityTrait.Aggression, 0.9);
            p.SetTrait(PersonalityTrait.Risk, 0.9);
            s.Faction.SetDataBlob(p);

            var fleet = BiggestOwnedFleet(s);
            Assert.That(FleetCombat.Ships(fleet).Count, Is.GreaterThanOrEqualTo(2), "need a multi-ship fleet to decompose");
            var shipsBefore = FleetCombat.Ships(fleet);

            NPCDecisionProcessor.RunFleetDoctrinePolicy(s.Faction);

            var subs = fleet.GetDataBlob<FleetDB>().GetChildren().Where(c => c.HasDataBlob<FleetRoleDB>()).ToList();
            Assert.That(subs, Is.Not.Empty, "the multi-ship fleet was organised into role sub-fleets");

            foreach (var sub in subs)
            {
                var role = sub.GetDataBlob<FleetRoleDB>().Role;
                Assert.That(sub.HasDataBlob<FleetDoctrineDB>(), Is.True, $"the {role} sub-fleet got a doctrine");
                var d = sub.GetDataBlob<FleetDoctrineDB>();
                var (family, posture) = Expected[role];
                Assert.That(d.Family, Is.EqualTo(family), $"{role} runs the {family} doctrine");
                Assert.That(d.Posture, Is.EqualTo(posture), $"{role} holds the {posture} posture");
            }

            CollectionAssert.AreEquivalent(shipsBefore, FleetCombat.Ships(fleet), "no ship lost in the reorganisation");
        }

        [Test]
        [Description("A fleet already IN BATTLE (FleetCombatStateDB) is NOT restructured — reorganising the tree mid-fight would disrupt the resolver.")]
        public void RunFleetDoctrinePolicy_SkipsReorg_ForFleetInBattle()
        {
            var s = TestScenario.CreateWithColony();
            s.Faction.SetDataBlob(new PersonalityDB());

            var fleet = BiggestOwnedFleet(s);
            Assert.That(FleetCombat.Ships(fleet).Count, Is.GreaterThanOrEqualTo(2));
            fleet.SetDataBlob(new FleetCombatStateDB(-1, FleetCombat.Ships(fleet).Count));  // pretend it's mid-battle

            NPCDecisionProcessor.RunFleetDoctrinePolicy(s.Faction);

            var subs = fleet.GetDataBlob<FleetDB>().GetChildren().Where(c => c.HasDataBlob<FleetRoleDB>()).ToList();
            Assert.That(subs, Is.Empty, "a fighting fleet keeps its shape — no role sub-fleets formed");
        }
    }
}
