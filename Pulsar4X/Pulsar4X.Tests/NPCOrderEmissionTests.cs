using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Industry;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.4c gauge (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II — the Organism engine): the brain's first ACT.
    /// A GrowEconomy objective now queues a real industry job (the same lever a player pulls). Proves the emission
    /// gate defaults OFF (byte-identical), the GrowEconomy action queues exactly one job on a free line, and
    /// `EmitOrders` routes by objective (a routed objective acts; an UNROUTED one — `None` — queues nothing).
    /// </summary>
    [TestFixture]
    public class NPCOrderEmissionTests
    {
        private static int TotalJobs(Entity colony)
        {
            var ind = colony.GetDataBlob<IndustryAbilityDB>();
            int n = 0;
            foreach (var line in ind.ProductionLines.Values) n += line.Jobs.Count;
            return n;
        }

        [Test]
        [Description("The order-emission gate defaults OFF (byte-identical); the GrowEconomy action queues one job on a free line.")]
        public void EmitGate_DefaultsOff_And_GrowEconomyQueuesAJob()
        {
            Assert.That(NPCDecisionProcessor.EnableOrderEmission, Is.False,
                "the brain decides but does not act until a client/test opts in — keeps every existing test byte-identical");

            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();

            int before = TotalJobs(s.Colony);
            bool queued = NPCDecisionProcessor.TryQueueEconomyJob(s.Colony, factionInfo);
            Assert.That(queued, Is.True, "a colony with industry + buildable designs queues an economy job");
            Assert.That(TotalJobs(s.Colony), Is.EqualTo(before + 1), "exactly one job queued");
        }

        [Test]
        [Description("EmitOrders routes by objective: an UNROUTED objective (None) queues nothing; a routed one (GrowEconomy) queues a job.")]
        public void EmitOrders_RoutesByObjective()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();

            // An UNROUTED objective emits no order. Most concrete objectives now DO act (GrowEconomy / Consolidate /
            // Defend / Expand all have resolvers), so `None` — the sentinel that never has a resolver — is the stable
            // no-op case here (not a not-yet-built one). Defend, for instance, now queues a warship.
            var obj = new StrategicObjectiveDB { Objective = StrategicObjective.None };
            s.Faction.SetDataBlob(obj);
            int before = TotalJobs(s.Colony);
            NPCDecisionProcessor.EmitOrders(s.Faction, factionInfo);
            Assert.That(TotalJobs(s.Colony), Is.EqualTo(before), "an unrouted objective (None) queues nothing");

            // Flip the same objective to GrowEconomy → a job is queued (routing reaches the resolver).
            obj.Objective = StrategicObjective.GrowEconomy;
            NPCDecisionProcessor.EmitOrders(s.Faction, factionInfo);
            Assert.That(TotalJobs(s.Colony), Is.GreaterThan(before), "GrowEconomy queues an industry job");
        }
    }
}
