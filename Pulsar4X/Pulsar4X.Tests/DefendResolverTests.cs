using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Industry;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.8 gauge (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md — the means-ends planner): the DEFEND resolver, the
    /// Survive-tier crisis brain (the other half of parking-lot gap G1 — the NPC used to FREEZE while being attacked
    /// because Defend had no resolver). Proves that under Defend the NPC takes a real defensive action — builds a
    /// warship at a colony with a free yard (Rung A) or, failing that, postures an owned fleet (Rung B) — never a
    /// no-op, and that Defend is now registered. Resolve is a pure decision (no side effect until Execute).
    /// </summary>
    [TestFixture]
    public class DefendResolverTests
    {
        private static int TotalJobs(Entity colony)
        {
            var ind = colony.GetDataBlob<IndustryAbilityDB>();
            int n = 0;
            foreach (var line in ind.ProductionLines.Values) n += line.Jobs.Count;
            return n;
        }

        [Test]
        [Description("Under Defend the NPC ACTS (G1): builds a warship on a free yard, or (fallback) postures a fleet — never None.")]
        public void Defend_TakesADefensiveAction()
        {
            var s = TestScenario.CreateWithColony();
            var state = FactionState.Snapshot(s.Faction);
            var objective = new StrategicObjectiveDB { Objective = StrategicObjective.Defend };

            int before = TotalJobs(s.Colony);
            var action = new DefendResolver().Resolve(state, objective);

            // The start faction has armed hulls (gunship / test-warship) registered + a free shipyard line, so Rung A
            // builds a warship. (If no yard were free, Rung B would posture an owned start fleet — either is a real
            // Defend action.) The point of G1: it is never "None".
            Assert.That(action.Kind, Is.EqualTo("QueueWarship").Or.EqualTo("SetDefensivePosture"),
                "under threat the NPC builds a warship or postures a fleet — it no longer freezes");
            Assert.That(TotalJobs(s.Colony), Is.EqualTo(before), "Resolve is a pure decision — nothing queued until Execute");

            if (action.Kind == "QueueWarship")
            {
                action.Execute();
                Assert.That(TotalJobs(s.Colony), Is.GreaterThan(before), "Execute queues the warship build (+ any sub-jobs)");
            }
        }

        [Test]
        [Description("The registry now resolves Defend to the crisis resolver (gap G1 fully closed alongside Consolidate).")]
        public void Registry_ResolvesDefend()
        {
            Assert.That(ObjectiveResolvers.TryGet(StrategicObjective.Defend, out var r), Is.True);
            Assert.That(r.Handles, Is.EqualTo(StrategicObjective.Defend));
        }
    }
}
