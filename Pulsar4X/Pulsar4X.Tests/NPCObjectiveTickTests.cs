using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.4b gauge (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II — the Organism engine): the Tick now SETTLES an
    /// objective. Proves an NPC faction, after a decision cycle, holds a `StrategicObjectiveDB` whose tier + objective
    /// match its gauges and doctrine (a healthy Economic-led NPC → Thrive / GrowEconomy), that a player faction gets
    /// none (the IsNPC guard), and that re-running the cycle HOLDS the committed plan (hysteresis, no thrash). The
    /// objective is stored but not yet acted on, so live behaviour is unchanged.
    /// </summary>
    [TestFixture]
    public class NPCObjectiveTickTests
    {
        [Test]
        [Description("A healthy Economic-led NPC settles Thrive/GrowEconomy; a player faction settles nothing; the plan then holds.")]
        public void Tick_SettlesAndCommitsAnObjective_ForNPCsOnly()
        {
            var s = TestScenario.CreateWithColony();

            var npc = FactionFactory.CreateBasicFaction(s.Game, "Directorate", "DIR", 100000);
            var npcInfo = npc.GetDataBlob<FactionInfoDB>();
            npcInfo.IsNPC = true;
            npcInfo.Doctrine = new DoctrineVector { Economic = 1f };

            var proc = new NPCDecisionProcessor();
            proc.Init(s.Game);

            // NPC: a decision cycle settles a sensible objective.
            proc.ProcessEntity(npc, 0);
            Assert.That(npc.TryGetDataBlob<StrategicObjectiveDB>(out var obj), Is.True, "the NPC now holds a strategic objective");
            Assert.That(obj.Tier, Is.EqualTo(NeedTier.Thrive), "a healthy bare faction is at the Thrive tier");
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.GrowEconomy), "Economic-led → grow the economy");
            var committedUntil = obj.CommittedUntil;

            // Player faction (IsNPC == false): the processor no-ops, so no objective is stored.
            proc.ProcessEntity(s.Faction, 0);
            Assert.That(s.Faction.HasDataBlob<StrategicObjectiveDB>(), Is.False, "a player faction settles no objective");

            // Re-running the NPC cycle holds the committed plan (hysteresis — the commitment clock hasn't moved).
            proc.ProcessEntity(npc, 0);
            Assert.That(npc.GetDataBlob<StrategicObjectiveDB>().CommittedUntil, Is.EqualTo(committedUntil),
                "the plan is held, not re-committed, within its commitment window");
        }
    }
}
