using NUnit.Framework;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.People;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Espionage E3 — the COVERT OP end to end: task an operative, resolve the detection roll, land the effect or pay
    /// the price. Proves (a) <see cref="Espionage.TaskAgent"/> only tasks real operatives and won't double-book a busy
    /// one; (b) a CLEAN gather raises the actor's intel on the rival (Inferred→Confirmed) with no trace and the agent
    /// survives; (c) a CAUGHT op sours the target toward the actor, spikes their suspicion, and LOSES the agent (the
    /// grave rung). This is the live consumer that finally makes the covert-action catalog + risk resolver do something.
    /// </summary>
    [TestFixture]
    public class CovertOpTests
    {
        private static Entity MakeAgent(TestScenario s, int experienceCap)
        {
            var agentDB = CommanderFactory.CreateAgent(s.Game);
            agentDB.ExperienceCap = experienceCap;
            var agent = CommanderFactory.Create(s.StartingSystem, s.Faction.Id, agentDB);
            foreach (var b in CommanderBonuses.RollEspionageCompetence(experienceCap))
                agent.GetDataBlob<BonusesDB>().Bonuses.Add(b);
            return agent;
        }

        [Test]
        [Description("E3: only Intelligence operatives can be tasked; a busy agent can't be double-booked; self/neutral targets are rejected.")]
        public void TaskAgent_Guards_OnlyOperatives_NotDoubleBooked()
        {
            var s = TestScenario.CreateWithColony();
            var target = FactionFactory.CreateBasicFaction(s.Game, "Rival", "RIV", 1000);
            var now = s.Colony.StarSysDateTime;

            var navy = CommanderFactory.Create(s.StartingSystem, s.Faction.Id, new CommanderDB("Capt", 1, CommanderTypes.Navy));
            Assert.That(Espionage.TaskAgent(navy, target.Id, CovertAction.GatherIntel, IntelFacet.Military, now), Is.False,
                "a Navy commander is not an operative — can't be tasked");

            var agent = MakeAgent(s, 200);
            Assert.That(Espionage.TaskAgent(agent, target.Id, CovertAction.GatherIntel, IntelFacet.Military, now), Is.True,
                "an operative can be tasked on a rival");
            Assert.That(agent.HasDataBlob<CovertOpDB>(), Is.True, "tasking stamps the in-progress op");
            Assert.That(Espionage.TaskAgent(agent, target.Id, CovertAction.GatherIntel, IntelFacet.Military, now), Is.False,
                "a busy agent can't be double-booked");

            var idle = MakeAgent(s, 200);
            Assert.That(Espionage.TaskAgent(idle, s.Faction.Id, CovertAction.GatherIntel, IntelFacet.Military, now), Is.False,
                "you can't run a covert op against your own faction");
        }

        [Test]
        [Description("E3: a CLEAN gather op raises the actor's intel on the rival's facet to Confirmed, leaves no trace, and the agent survives.")]
        public void GatherOp_Clean_RaisesIntel_AgentSurvives_NoTrace()
        {
            var s = TestScenario.CreateWithColony();
            var target = FactionFactory.CreateBasicFaction(s.Game, "Rival", "RIV", 1000);
            var now = s.Colony.StarSysDateTime;
            var ledger = s.Faction.GetDataBlob<InformationLedgerDB>();
            var agent = MakeAgent(s, 200); // a master agent (skill 0.6) vs a target with no counter-intel

            Assert.That(ledger.LevelOf(target.Id, IntelFacet.Military), Is.EqualTo(IntelLevel.Inferred),
                "before the op, the rival's military is only inferred");

            Espionage.TaskAgent(agent, target.Id, CovertAction.GatherIntel, IntelFacet.Military, now);
            var op = agent.GetDataBlob<CovertOpDB>();
            var outcome = EspionageProcessor.ResolveOp(agent, op, s.Game, roll01: 0.01, now); // a lucky roll → clean

            Assert.That(outcome, Is.EqualTo(CovertOutcome.Clean), "a skilled agent on a soft target runs clean");
            Assert.That(ledger.LevelOf(target.Id, IntelFacet.Military), Is.EqualTo(IntelLevel.Confirmed),
                "a clean gather confirms the facet — the sharpened poker read");
            Assert.That(agent.HasDataBlob<CovertOpDB>(), Is.False, "the op is consumed");

            var relToActor = target.GetDataBlob<DiplomacyDB>().GetRelationship(s.Faction.Id);
            Assert.That(relToActor.RelationScore, Is.EqualTo(0), "a clean op leaves no trace — no souring");
            Assert.That(relToActor.Suspicion, Is.EqualTo(0.0), "no suspicion from a clean op");
            TestContext.Progress.WriteLine("[covert-op] clean gather: Military Inferred→Confirmed, agent intact");
        }

        [Test]
        [Description("E3: a CAUGHT op sours the target's relation toward the actor, spikes suspicion, and loses the agent (grave rung).")]
        public void GatherOp_Caught_SoursRelation_SpikesSuspicion_LosesAgent()
        {
            var s = TestScenario.CreateWithColony();
            var target = FactionFactory.CreateBasicFaction(s.Game, "Rival", "RIV", 1000);
            var now = s.Colony.StarSysDateTime;
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var agent = MakeAgent(s, 0); // a raw recruit — no tradecraft

            Espionage.TaskAgent(agent, target.Id, CovertAction.GatherIntel, IntelFacet.Military, now);
            var op = agent.GetDataBlob<CovertOpDB>();
            // Drive the CAUGHT outcome directly (deterministic — independent of the roll banding).
            EspionageProcessor.ApplyOutcome(agent, op, s.Game, CovertOutcome.Caught, now);

            var relToActor = target.GetDataBlob<DiplomacyDB>().GetRelationship(s.Faction.Id);
            Assert.That(relToActor.RelationScore, Is.EqualTo(EspionageProcessor.RelationHitCaught),
                "a caught op sours the target toward the exposed actor");
            Assert.That(relToActor.Suspicion, Is.GreaterThan(0.0), "the target's suspicion of the actor spikes");

            bool stillListed = false;
            foreach (var c in factionInfo.Commanders) if (c.Id == agent.Id) { stillListed = true; break; }
            Assert.That(stillListed, Is.False, "the caught agent is lost — captured/killed (the grave rung)");
            TestContext.Progress.WriteLine($"[covert-op] caught: relation {relToActor.RelationScore}, suspicion {relToActor.Suspicion}, agent lost");
        }
    }
}
