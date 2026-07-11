using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Industry;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.8 P0-b gauge (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md — the means-ends planner): the GrowEconomy resolver
    /// (Rung C) + the registry. Proves the resolver names a QueueBuild step for a colony with a free line, that
    /// executing it queues the build (routed through AutoAddSubJobs, the fix the blind 2.4c emitter lacked), and that
    /// the registry only knows GrowEconomy so far. Resolve is a pure decision (no side effect until Execute).
    /// </summary>
    [TestFixture]
    public class GrowEconomyResolverTests
    {
        private static int TotalJobs(Entity colony)
        {
            var ind = colony.GetDataBlob<IndustryAbilityDB>();
            int n = 0;
            foreach (var line in ind.ProductionLines.Values) n += line.Jobs.Count;
            return n;
        }

        [Test]
        [Description("Resolve names a QueueBuild for a free-line colony; Execute queues the build; Resolve alone is side-effect-free.")]
        public void GrowEconomy_ReturnsQueueBuild_ExecuteQueuesTheBuild()
        {
            var s = TestScenario.CreateWithColony();
            var state = FactionState.Snapshot(s.Faction);
            var objective = new StrategicObjectiveDB { Objective = StrategicObjective.GrowEconomy };

            int before = TotalJobs(s.Colony);
            var action = new GrowEconomyResolver().Resolve(state, objective);

            Assert.That(action.Kind, Is.EqualTo("QueueBuild"), "a colony with a free line has a growth build to start");
            Assert.That(TotalJobs(s.Colony), Is.EqualTo(before), "Resolve is a pure decision — no job queued until Execute");

            action.Execute();
            Assert.That(TotalJobs(s.Colony), Is.GreaterThan(before), "Execute queues the build (+ any auto-added sub-jobs)");
        }

        [Test]
        [Description("The registry knows GrowEconomy and reports its Handles; unregistered objectives (Conquer) miss.")]
        public void Registry_ResolvesGrowEconomyOnly()
        {
            Assert.That(ObjectiveResolvers.TryGet(StrategicObjective.GrowEconomy, out var r), Is.True);
            Assert.That(r.Handles, Is.EqualTo(StrategicObjective.GrowEconomy));
            Assert.That(ObjectiveResolvers.TryGet(StrategicObjective.Conquer, out _), Is.False, "no Conquer resolver yet");
        }
    }
}
