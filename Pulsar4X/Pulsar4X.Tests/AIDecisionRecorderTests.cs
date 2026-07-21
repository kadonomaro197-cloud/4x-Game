using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// B4 — the AI FLIGHT RECORDER gauge (docs/ai/DEVTEST-CONQUEST-SANDBOX-DESIGN.md §4, the observability SPINE).
    /// Proves every monthly NPC decision cycle TAPES a reviewable record: the objective it settled matches the tape,
    /// the tape carries the fog-limited SENSED picture (own strength / threat / morale), the tape GROWS across cycles
    /// (the history StrategicObjectiveDB — a single snapshot — never kept), the readout renders it as an `[AI]` line,
    /// and a PLAYER faction tapes nothing (the recorder rides the IsNPC-gated Tick). The "no AI slice ships without
    /// its explain output" rule made testable.
    /// </summary>
    [TestFixture]
    public class AIDecisionRecorderTests
    {
        [Test]
        [Description("One NPC decision cycle tapes a record whose decision matches the settled objective, carries the "
                     + "sensed perception, and renders as an [AI] line; the tape then grows each cycle.")]
        public void Tick_TapesTheDecision_WithSensedContext_AndGrows()
        {
            var s = TestScenario.CreateWithColony();

            var npc = FactionFactory.CreateBasicFaction(s.Game, "Martian Federation", "MAR", 100000);
            var info = npc.GetDataBlob<FactionInfoDB>();
            info.IsNPC = true;
            info.Doctrine = new DoctrineVector { Economic = 1f };

            // P0.4 — the Kithrin scenario: a station-only faction (no colonies, one station). The recorder must tape
            // BOTH counts so the tape reads the faction's real footprint instead of "colonies 0" while it owns a station.
            info.Stations.Add(Entity.Create());

            var proc = new NPCDecisionProcessor();
            proc.Init(s.Game);

            // One decision cycle → the flight recorder taped it.
            proc.ProcessEntity(npc, 0);

            Assert.That(npc.TryGetDataBlob<AIDecisionRecordDB>(out var tape), Is.True, "the NPC now holds a decision tape");
            Assert.That(tape.Records.Count, Is.EqualTo(1), "one cycle -> one taped record");

            var rec = tape.Latest;
            var obj = npc.GetDataBlob<StrategicObjectiveDB>();
            Assert.That(rec.Objective, Is.EqualTo(obj.Objective), "the taped objective matches what the brain settled");
            Assert.That(rec.Tier, Is.EqualTo(obj.Tier), "the taped tier matches what the brain settled");
            // The SENSED half is present. Morale reads the neutral 50 midpoint when no data, never below 0. A bare
            // Economic-led faction is at peace and holds no sensor contacts, so it senses no greater threat.
            Assert.That(rec.Morale, Is.GreaterThanOrEqualTo(0.0), "morale sensed");
            Assert.That(rec.ThreatFactionId, Is.EqualTo(-1), "a faction at peace with no contacts senses no greater threat");
            // The station-aware tape: this faction owns no colonies but one station, and the record captures both —
            // the Kithrin "colonies 0 while owning Titan" honesty fix (P0.4).
            Assert.That(rec.ColonyCount, Is.EqualTo(0), "a station-only faction taped its true colony count (0)");
            Assert.That(rec.StationCount, Is.EqualTo(1), "the station it owns is taped, not silently dropped");

            // The readout renders the tape as an [AI] line naming the faction, including the station count.
            string line = PlanReadout.DecisionTape(npc);
            Assert.That(line, Does.Contain("[AI]"), "the tape renders as an [AI] line");
            Assert.That(line, Does.Contain("Martian Federation"), "the line names the faction");
            Assert.That(line, Does.Contain("colonies 0"), "the readout shows the true colony count");
            Assert.That(line, Does.Contain("stations 1"), "the readout shows the station count alongside colonies");

            // A PLAYER faction rides the IsNPC guard — no tape.
            proc.ProcessEntity(s.Faction, 0);
            Assert.That(s.Faction.HasDataBlob<AIDecisionRecordDB>(), Is.False, "a player faction tapes no decisions");

            // The tape GROWS across cycles — the history the recorder adds over the single-snapshot StrategicObjectiveDB.
            proc.ProcessEntity(npc, 0);
            proc.ProcessEntity(npc, 0);
            Assert.That(npc.GetDataBlob<AIDecisionRecordDB>().Records.Count, Is.EqualTo(3), "each cycle appends a record");
        }

        [Test]
        [Description("The tape is a bounded ring buffer — it never grows past AIDecisionRecordDB.Capacity.")]
        public void Tape_IsBounded_ToCapacity()
        {
            var tape = new AIDecisionRecordDB();
            for (int i = 0; i < AIDecisionRecordDB.Capacity + 25; i++)
                tape.Append(new AIDecisionRecord { Objective = StrategicObjective.GrowEconomy });

            Assert.That(tape.Records.Count, Is.EqualTo(AIDecisionRecordDB.Capacity),
                "the ring buffer trims the oldest and never exceeds Capacity");
            Assert.That(tape.Latest, Is.Not.Null, "the newest record is retained");
        }
    }
}
