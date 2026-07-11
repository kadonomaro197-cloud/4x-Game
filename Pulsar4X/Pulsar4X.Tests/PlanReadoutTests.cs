using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// P1 Visibility-Gate gauge (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md): the plan readout. Proves the readout surfaces a
    /// faction's objective + its last planner step, and that `EmitOrders` records what the planner did into the
    /// objective — so a stuck NPC is never a silent black box.
    /// </summary>
    [TestFixture]
    public class PlanReadoutTests
    {
        [Test]
        [Description("The readout line shows the objective/tier and the last planner action.")]
        public void Faction_ShowsObjectiveAndLastAction()
        {
            var s = TestScenario.CreateWithColony();
            var obj = new StrategicObjectiveDB
            {
                Objective = StrategicObjective.GrowEconomy,
                LastActionKind = "QueueMine",
                LastActionDetail = "build a Mine on colony 7 to feed the stalled 'iron'",
            };
            s.Faction.SetDataBlob(obj);

            string line = PlanReadout.Faction(s.Faction);
            Assert.That(line, Does.Contain("GrowEconomy"), "the objective is shown");
            Assert.That(line, Does.Contain("QueueMine"), "the last action kind is shown");
            Assert.That(line, Does.Contain("iron"), "the human-readable detail is shown");
        }

        [Test]
        [Description("EmitOrders records the planner's step into the objective (the Visibility Gate) — not silent.")]
        public void EmitOrders_RecordsTheAction()
        {
            var s = TestScenario.CreateWithColony();
            var info = s.Faction.GetDataBlob<FactionInfoDB>();
            var obj = new StrategicObjectiveDB { Objective = StrategicObjective.GrowEconomy };
            s.Faction.SetDataBlob(obj);

            NPCDecisionProcessor.EmitOrders(s.Faction, info);

            Assert.That(obj.LastActionKind, Is.Not.Empty,
                "the planner records what it decided — a healthy GrowEconomy faction starts a build");
            Assert.That(PlanReadout.Faction(s.Faction), Does.Contain(obj.LastActionKind), "the readout reflects it");
        }
    }
}
