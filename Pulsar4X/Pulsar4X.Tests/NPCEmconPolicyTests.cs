using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;   // PersonalityDB, NPCDecisionProcessor
using Pulsar4X.Fleets;     // FleetDB, FleetRoleDB
using Pulsar4X.Sensors;    // FleetEmcon, EmconPosture

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase D gauge — the AI deploys the EMCON lever as a scored TOOL. `RunEmconPolicy` scores the three postures
    /// (Full/Cruise/Silent) through the shared `DecisionScorer` and sets each owned fleet's dark-vs-loud stance by
    /// personality: a BOLD, aggressive faction runs Full (doesn't care who sees it coming), a GUILEFUL, cautious one
    /// goes Silent (hide, strike from the dark). That feeds straight into the detection/fog math — a Silent fleet is
    /// seen from far closer — so the same fingerprint that picks the fighting doctrine also decides whether the fleet
    /// sneaks. Drives the method directly (no clock advance); byte-identical off (gated on EnableOrderEmission).
    /// </summary>
    [TestFixture]
    public class NPCEmconPolicyTests
    {
        private static void SetPersonality(Entity faction, double aggression, double risk, double guile)
        {
            var p = new PersonalityDB();
            p.SetTrait(PersonalityTrait.Aggression, aggression);
            p.SetTrait(PersonalityTrait.Risk, risk);
            p.SetTrait(PersonalityTrait.Guile, guile);
            faction.SetDataBlob(p);
        }

        private static System.Collections.Generic.List<Entity> OwnedTopFleets(TestScenario s)
            => s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>()
                .Where(f => f.FactionOwnerID == s.Faction.Id && !f.HasDataBlob<FleetRoleDB>()).ToList();

        [Test]
        [Description("A bold, aggressive faction (Aggr .9, Risk .9) runs its fleets LOUD (Full EMCON) — it doesn't care who sees it coming.")]
        public void AggressiveFaction_RunsFullEmcon()
        {
            var s = TestScenario.CreateWithColony();
            SetPersonality(s.Faction, aggression: 0.9, risk: 0.9, guile: 0.5);

            NPCDecisionProcessor.RunEmconPolicy(s.Faction);

            var fleets = OwnedTopFleets(s);
            Assert.That(fleets, Is.Not.Empty, "the start faction has fleets to set");
            foreach (var f in fleets)
                Assert.That(FleetEmcon.PostureOf(f), Is.EqualTo(EmconPosture.Full), "a bold aggressor runs hot");
        }

        [Test]
        [Description("A guileful, cautious faction (Guile .9, Aggr .3, Risk .1) goes DARK (Silent EMCON) — hide and strike from the shadows.")]
        public void GuilefulCautiousFaction_GoesSilent()
        {
            var s = TestScenario.CreateWithColony();
            SetPersonality(s.Faction, aggression: 0.3, risk: 0.1, guile: 0.9);

            NPCDecisionProcessor.RunEmconPolicy(s.Faction);

            var fleets = OwnedTopFleets(s);
            Assert.That(fleets, Is.Not.Empty);
            foreach (var f in fleets)
                Assert.That(FleetEmcon.PostureOf(f), Is.EqualTo(EmconPosture.Silent), "a guileful/cautious faction hides");
        }
    }
}
