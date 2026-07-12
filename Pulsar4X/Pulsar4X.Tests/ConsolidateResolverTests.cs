using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.8 gauge (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md — the means-ends planner): the CONSOLIDATE resolver,
    /// the crisis brain for the Stabilize tier (parking-lot gap G1 — the NPC used to FREEZE in a crisis because
    /// Consolidate had no resolver). Proves the resolver eases tax on a restless, over-taxed colony (Execute lowers
    /// the rate), leaves a content colony alone, and is now registered. Resolve is a pure decision (no side effect
    /// until Execute) — the same convention as the GrowEconomy gauge.
    /// </summary>
    [TestFixture]
    public class ConsolidateResolverTests
    {
        [Test]
        [Description("A restless, over-taxed colony → the planner eases its tax; Execute lowers the rate; Resolve alone is side-effect-free.")]
        public void Consolidate_RestlessColony_EasesTax()
        {
            var s = TestScenario.CreateWithColony();

            // Make the colony restless and taxing at the ceiling: morale well below the neutral 50.
            s.Colony.GetDataBlob<ColonyMoraleDB>().Morale = 20.0;   // deep unrest (internal set via InternalsVisibleTo)
            var econ = s.Colony.GetDataBlob<ColonyEconomyDB>();
            econ.TaxRate = 0.5;                                     // taxing hard

            var state = FactionState.Snapshot(s.Faction);
            var objective = new StrategicObjectiveDB { Objective = StrategicObjective.Consolidate };

            double before = econ.TaxRate;
            var action = new ConsolidateResolver().Resolve(state, objective);

            Assert.That(action.Kind, Is.EqualTo("EaseTax"), "a restless, over-taxed colony gets tax relief");
            Assert.That(econ.TaxRate, Is.EqualTo(before), "Resolve is a pure decision — no change until Execute");

            action.Execute();
            Assert.That(econ.TaxRate, Is.LessThan(before), "Execute eases the tax to quell unrest");
        }

        [Test]
        [Description("A content colony (morale at/above neutral) has no unrest to consolidate → None.")]
        public void Consolidate_ContentColony_DoesNothing()
        {
            var s = TestScenario.CreateWithColony();
            s.Colony.GetDataBlob<ColonyMoraleDB>().Morale = 70.0;   // content
            s.Colony.GetDataBlob<ColonyEconomyDB>().TaxRate = 0.5;

            var state = FactionState.Snapshot(s.Faction);
            var action = new ConsolidateResolver().Resolve(state, new StrategicObjectiveDB { Objective = StrategicObjective.Consolidate });

            Assert.That(action.Kind, Is.EqualTo("None"), "a happy colony needs no consolidation");
        }

        [Test]
        [Description("The registry now resolves Consolidate to the crisis resolver (gap G1 closed).")]
        public void Registry_ResolvesConsolidate()
        {
            Assert.That(ObjectiveResolvers.TryGet(StrategicObjective.Consolidate, out var r), Is.True);
            Assert.That(r.Handles, Is.EqualTo(StrategicObjective.Consolidate));
        }
    }
}
