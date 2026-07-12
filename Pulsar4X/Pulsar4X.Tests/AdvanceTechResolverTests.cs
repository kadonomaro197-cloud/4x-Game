using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Industry;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.8 gauge (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md — the means-ends planner): the ADVANCE-TECH resolver
    /// (Thrive tier, tech-led). Proves that when pursuing AdvanceTech the NPC builds research capacity — queues a
    /// research-lab design (one carrying ResearchPointsAtbDB) on a free line — and that AdvanceTech is now registered.
    /// Resolve is a pure decision (no side effect until Execute), the same convention as the other resolvers.
    /// </summary>
    [TestFixture]
    public class AdvanceTechResolverTests
    {
        private static int TotalJobs(Entity colony)
        {
            var ind = colony.GetDataBlob<IndustryAbilityDB>();
            int n = 0;
            foreach (var line in ind.ProductionLines.Values) n += line.Jobs.Count;
            return n;
        }

        [Test]
        [Description("Under AdvanceTech the NPC queues a research lab (research infrastructure); Execute queues it; Resolve is pure.")]
        public void AdvanceTech_QueuesAResearchLab()
        {
            var s = TestScenario.CreateWithColony();
            var state = FactionState.Snapshot(s.Faction);
            var objective = new StrategicObjectiveDB { Objective = StrategicObjective.AdvanceTech };

            int before = TotalJobs(s.Colony);
            var action = new AdvanceTechResolver().Resolve(state, objective);

            Assert.That(action.Kind, Is.EqualTo("QueueResearchLab"), "AdvanceTech builds research capacity (a lab)");
            Assert.That(TotalJobs(s.Colony), Is.EqualTo(before), "Resolve is a pure decision — nothing queued until Execute");

            action.Execute();
            Assert.That(TotalJobs(s.Colony), Is.GreaterThan(before), "Execute queues the lab build (+ any sub-jobs)");
        }

        [Test]
        [Description("The registry now resolves AdvanceTech — the last Thrive-tier no-op is closed.")]
        public void Registry_ResolvesAdvanceTech()
        {
            Assert.That(ObjectiveResolvers.TryGet(StrategicObjective.AdvanceTech, out var r), Is.True);
            Assert.That(r.Handles, Is.EqualTo(StrategicObjective.AdvanceTech));
        }
    }
}
