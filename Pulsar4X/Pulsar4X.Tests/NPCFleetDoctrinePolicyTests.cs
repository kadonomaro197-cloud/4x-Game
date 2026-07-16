using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;   // FleetDoctrineDB, EngagementPosture
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;   // FleetDB

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase B-1 gauge — the AI puts its hands on the FLEET-DOCTRINE levers it never touched. `RunFleetDoctrinePolicy`
    /// scores the moddable combat-doctrine catalog through the shared `DecisionScorer` (the 2nd live caller after the
    /// warship pick) and applies the winner + a matching engagement posture to every owned fleet. Proves the personality
    /// fingerprint reaches the fleet's fighting stance: a BOLD, warlike faction runs an OFFENSIVE doctrine + Weapons-Free,
    /// a CAUTIOUS one a DEFENSIVE line + Return-Fire — from the dials alone. Drives the pass directly (no clock advance);
    /// the default game is byte-identical (the call site is gated on EnableOrderEmission).
    /// </summary>
    [TestFixture]
    public class NPCFleetDoctrinePolicyTests
    {
        private static void SetPersonality(Entity faction, double aggression, double risk)
        {
            var p = new PersonalityDB();
            p.SetTrait(PersonalityTrait.Aggression, aggression);
            p.SetTrait(PersonalityTrait.Risk, risk);
            faction.SetDataBlob(p);
        }

        // TOP-LEVEL owned fleets with doctrine — EXCLUDING role sub-fleets (which carry a FleetRoleDB and their own
        // ROLE doctrine, assigned by B-2c2's ApplyRoleDoctrines). This policy sets the faction-wide doctrine on the
        // top-level fleets; the sub-fleets' role doctrines are a separate concern (see NPCRoleDoctrineTests).
        private static System.Collections.Generic.List<Entity> DoctrinedFleets(TestScenario s)
            => s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>()
                .Where(f => f.FactionOwnerID == s.Faction.Id
                            && f.HasDataBlob<FleetDoctrineDB>()
                            && !f.HasDataBlob<FleetRoleDB>()).ToList();

        [Test]
        [Description("A bold, warlike faction (Aggr .9, Risk .9) sets every fleet to an OFFENSIVE doctrine + Weapons-Free — the AI using the doctrine levers by personality.")]
        public void AggressiveFaction_RunsOffensiveDoctrine_WeaponsFree()
        {
            var s = TestScenario.CreateWithColony();
            SetPersonality(s.Faction, aggression: 0.9, risk: 0.9);

            NPCDecisionProcessor.RunFleetDoctrinePolicy(s.Faction);

            var fleets = DoctrinedFleets(s);
            Assert.That(fleets, Is.Not.Empty, "the AI set doctrine on its fleets (the start faction has fleets)");
            foreach (var f in fleets)
            {
                var d = f.GetDataBlob<FleetDoctrineDB>();
                Assert.That(d.Family, Is.EqualTo("Offensive"), "an aggressive/bold faction runs an offensive doctrine");
                Assert.That(d.Posture, Is.EqualTo(EngagementPosture.WeaponsFree), "and fights weapons-free (starts battles)");
            }
        }

        [Test]
        [Description("A cautious faction (Aggr .3, Risk .1) sets every fleet to a DEFENSIVE doctrine + Return-Fire — the opposite pick from the same scorer.")]
        public void CautiousFaction_RunsDefensiveDoctrine_ReturnFire()
        {
            var s = TestScenario.CreateWithColony();
            SetPersonality(s.Faction, aggression: 0.3, risk: 0.1);

            NPCDecisionProcessor.RunFleetDoctrinePolicy(s.Faction);

            var fleets = DoctrinedFleets(s);
            Assert.That(fleets, Is.Not.Empty, "the AI set doctrine on its fleets");
            foreach (var f in fleets)
            {
                var d = f.GetDataBlob<FleetDoctrineDB>();
                Assert.That(d.Family, Is.EqualTo("Defensive"), "a cautious faction runs a defensive doctrine");
                Assert.That(d.Posture, Is.EqualTo(EngagementPosture.ReturnFire), "and holds fire until fired upon");
            }
        }
    }
}
