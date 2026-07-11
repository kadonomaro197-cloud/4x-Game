using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;
using Pulsar4X.GeoSurveys;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.8 P-2 gauge (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md — the means-ends planner): the EXPAND resolver
    /// (Ambition tier). Proves that when a colonizeable, surveyed, uncolonized world is available near home, the NPC
    /// decides to FOUND a colony there — using the SAME signal the player's colonize UI reads (a `ColonizeableDB`
    /// body whose geo-survey is complete). Resolve is a pure decision (the CreateColonyOrder rides the Execute
    /// closure; EmitOrders runs it — we gauge the decision, not the hotloop-driven colony creation).
    /// </summary>
    [TestFixture]
    public class ExpandResolverTests
    {
        [Test]
        [Description("With a surveyed, colonizeable, uncolonized world available (Sol has Mars/Mercury/Luna), Expand decides to Found a colony.")]
        public void Expand_FoundsASurveyedColonizeableWorld()
        {
            var s = TestScenario.CreateWithColony();
            int factionId = s.Faction.Id;

            // Bodies begin as fog to explore; mark every uncolonized colonizeable body in the home system as surveyed
            // for this faction so the FOUND leg has a ready target (the survey→move leg that clears it is a later slice).
            int candidates = 0;
            foreach (var body in s.StartingSystem.GetAllEntitiesWithDataBlob<ColonizeableDB>())
            {
                if (body.IsOrHasColony().Item1) continue;   // skip Earth (our home) and any settled world
                candidates++;
                if (body.TryGetDataBlob<GeoSurveyableDB>(out var gsd))
                    gsd.GeoSurveyStatus[factionId] = 0;     // survey complete for us
            }
            Assert.That(candidates, Is.GreaterThan(0), "Sol has uncolonized colonizeable bodies (Mars/Mercury/Luna) to settle");

            var state = FactionState.Snapshot(s.Faction);
            var action = new ExpandResolver().Resolve(state, new StrategicObjectiveDB { Objective = StrategicObjective.Expand });

            Assert.That(action.Kind, Is.EqualTo("Found"), "with a surveyed colonizeable world available, the NPC settles it");
            Assert.That(action.Execute, Is.Not.Null, "the Found step carries the CreateColonyOrder to run on Execute");
        }

        [Test]
        [Description("The registry now resolves Expand to the P-2 colonization resolver.")]
        public void Registry_ResolvesExpand()
        {
            Assert.That(ObjectiveResolvers.TryGet(StrategicObjective.Expand, out var r), Is.True);
            Assert.That(r.Handles, Is.EqualTo(StrategicObjective.Expand));
        }
    }
}
