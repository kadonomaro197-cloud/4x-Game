using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Datablobs;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.People;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Espionage E5 — the always-on MIRROR: NPCs spy on their rivals, including the player. These exercise the
    /// mechanism (<see cref="NPCDecisionProcessor.RunEspionageMirror"/>) directly: an NPC with spy capacity (a built
    /// <see cref="IntelDirectorateDB"/>) and an idle operative tasks a covert op against its most-hostile met rival, and
    /// is gated on hostility (a friendly neighbour is left alone) and on capacity (no directorate → no op). The
    /// byte-identity guarantee is the default-off <see cref="NPCDecisionProcessor.EnableEspionageMirror"/> gate: the Tick
    /// only CALLS this when the flag is on, so with it off (the default) the whole existing NPC-tick suite is unchanged
    /// (those fixtures — NPCObjectiveTickTests etc. — run Tick without the flag and are the byte-identity tripwire).
    /// </summary>
    [TestFixture]
    public class NPCEspionageMirrorTests
    {
        // Build an NPC faction with spy capacity (a directorate) + an idle operative, and a relation of `score`
        // toward the player. Returns (npc, npcInfo, agent).
        private static (Entity npc, FactionInfoDB info, Entity agent) MakeNpcSpy(TestScenario s, int scoreTowardPlayer)
        {
            var npc = FactionFactory.CreateBasicFaction(s.Game, "Rival", "RIV", 1000);
            var info = npc.GetDataBlob<FactionInfoDB>();

            // Spy capacity: a colony-entity carrying a directorate.
            var colony = Entity.Create();
            colony.FactionOwnerID = npc.Id;
            s.StartingSystem.AddEntity(colony, new List<BaseDataBlob>
            {
                new IntelDirectorateDB { OpCapacity = 2, CounterIntelRating = 20 }
            });
            info.Colonies.Add(colony);

            // An idle operative with real tradecraft.
            var agentDB = CommanderFactory.CreateAgent(s.Game);
            agentDB.ExperienceCap = 150;
            var agent = CommanderFactory.Create(s.StartingSystem, npc.Id, agentDB);
            foreach (var b in CommanderBonuses.RollEspionageCompetence(150))
                agent.GetDataBlob<BonusesDB>().Bonuses.Add(b);

            // The NPC's standing toward the player.
            npc.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(s.Faction.Id).AdjustScore(scoreTowardPlayer);

            return (npc, info, agent);
        }

        [Test]
        [Description("E5: the mirror gate defaults off (byte-identical) — the existing NPC-tick suite proves the gated call site is inert.")]
        public void MirrorGate_DefaultsOff()
        {
            Assert.That(NPCDecisionProcessor.EnableEspionageMirror, Is.False,
                "the espionage mirror is opt-in — default off keeps every existing NPC tick byte-identical");
        }

        [Test]
        [Description("E5: a hostile NPC with capacity + an idle agent tasks a low-risk gather op against the player (its most-hostile met rival).")]
        public void HostileNpc_SpiesOnThePlayer()
        {
            var s = TestScenario.CreateWithColony();
            var (npc, info, agent) = MakeNpcSpy(s, scoreTowardPlayer: -100); // fully hostile toward the player

            NPCDecisionProcessor.RunEspionageMirror(npc, info);

            Assert.That(agent.HasDataBlob<CovertOpDB>(), Is.True, "the NPC tasks a covert op against the hostile player");
            var op = agent.GetDataBlob<CovertOpDB>();
            Assert.That(op.TargetFactionId, Is.EqualTo(s.Faction.Id), "the target is the player — its most-hostile met rival");
            Assert.That(op.Action, Is.EqualTo(CovertAction.GatherIntel), "the mirror runs the safe baseline (gather) — tuned LOW");
            TestContext.Progress.WriteLine("[npc-mirror] hostile NPC tasked a gather op on the player");
        }

        [Test]
        [Description("E5: a FRIENDLY NPC does not spy — the mirror is gated on hostility (a friendly neighbour is left alone).")]
        public void FriendlyNpc_DoesNotSpy()
        {
            var s = TestScenario.CreateWithColony();
            var (npc, info, agent) = MakeNpcSpy(s, scoreTowardPlayer: 50); // friendly toward the player

            NPCDecisionProcessor.RunEspionageMirror(npc, info);

            Assert.That(agent.HasDataBlob<CovertOpDB>(), Is.False,
                "a friendly NPC does not spy — no rival at or below the hostile threshold");
            TestContext.Progress.WriteLine("[npc-mirror] friendly NPC left the player alone (hostility gate)");
        }

        [Test]
        [Description("E5: an NPC with NO directorate (no spy capacity) can't run the mirror even when hostile.")]
        public void NpcWithoutCapacity_CannotSpy()
        {
            var s = TestScenario.CreateWithColony();
            var (npc, info, agent) = MakeNpcSpy(s, scoreTowardPlayer: -100);

            // Strip the directorate (no capacity) — keep the hostile agent.
            info.Colonies.Clear();

            NPCDecisionProcessor.RunEspionageMirror(npc, info);

            Assert.That(agent.HasDataBlob<CovertOpDB>(), Is.False,
                "no built directorate → no spy capacity → no op (the E1 gear gate holds)");
        }
    }
}
