using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Industry;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.8 P-3 gauge (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md — the means-ends planner): the CONQUER resolver
    /// (Ambition tier), the SIXTH and last objective. v1 proves the NPC MASSES a strike fleet — queues an armed hull
    /// on a free ship line — when pursuing Conquer, and that Conquer is now registered (so every objective resolves).
    /// The actual attack (target-selection / reach / fuel / strike) is the deferred P-3 military sub-subsystem.
    /// Resolve is a pure decision (no side effect until Execute).
    /// </summary>
    [TestFixture]
    public class ConquerResolverTests
    {
        private static int TotalJobs(Entity colony)
        {
            var ind = colony.GetDataBlob<IndustryAbilityDB>();
            int n = 0;
            foreach (var line in ind.ProductionLines.Values) n += line.Jobs.Count;
            return n;
        }

        [Test]
        [Description("Under Conquer the NPC masses a strike fleet — queues a warship; Execute queues it; Resolve is pure.")]
        public void Conquer_MassesAStrikeFleet()
        {
            var s = TestScenario.CreateWithColony();
            var state = FactionState.Snapshot(s.Faction);
            var objective = new StrategicObjectiveDB { Objective = StrategicObjective.Conquer };

            int before = TotalJobs(s.Colony);
            var action = new ConquerResolver().Resolve(state, objective);

            Assert.That(action.Kind, Is.EqualTo("QueueWarship"), "Conquer builds a warship to mass a strike fleet");
            Assert.That(TotalJobs(s.Colony), Is.EqualTo(before), "Resolve is a pure decision — nothing queued until Execute");

            action.Execute();
            Assert.That(TotalJobs(s.Colony), Is.GreaterThan(before), "Execute queues the warship build (+ any sub-jobs)");
        }

        [Test]
        [Description("The registry now resolves Conquer — the sixth and final objective; the planner is complete.")]
        public void Registry_ResolvesConquer()
        {
            Assert.That(ObjectiveResolvers.TryGet(StrategicObjective.Conquer, out var r), Is.True);
            Assert.That(r.Handles, Is.EqualTo(StrategicObjective.Conquer));
        }
    }
}
