using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// ONE entry in an NPC's decision tape — the fog-limited picture it acted on (SENSED), the objective it settled
    /// and why (DECIDED), and the step its planner named (ACTED), stamped with the game time it happened. A plain
    /// serialisable value the recorder appends each daily Tick; the readout renders it as an <c>[AI]</c> line.
    /// </summary>
    public class AIDecisionRecord
    {
        [JsonProperty] public DateTime When { get; set; }

        // DECIDED — what the brain settled this cycle (mirrors StrategicObjectiveDB after UpdateStrategicObjective).
        [JsonProperty] public NeedTier Tier { get; set; }
        [JsonProperty] public StrategicObjective Objective { get; set; }
        [JsonProperty] public string Reason { get; set; } = "";

        // ACTED — the planner's step (empty / "(gated)" when the order gate is off, so the tape shows the brain
        // deciding even before it's turned loose — the whole point of watching before you turn the AI on).
        [JsonProperty] public string ActionKind { get; set; } = "";
        [JsonProperty] public string ActionDetail { get; set; } = "";

        // SENSED — the cheap perception snapshot (own-side gauges + the fog-limited greatest threat) the decision
        // was made against. This is the half StrategicObjectiveDB never captured; it's what makes a decision
        // reviewable ("it chose Defend because it saw a rival 2× its strength").
        [JsonProperty] public double OwnStrength { get; set; }
        [JsonProperty] public int ThreatFactionId { get; set; } = -1;
        [JsonProperty] public double ThreatStrength { get; set; }
        [JsonProperty] public double Morale { get; set; }
        [JsonProperty] public double Legitimacy { get; set; }
        [JsonProperty] public double Balance { get; set; }
        [JsonProperty] public int ColonyCount { get; set; }
        // Station hosts owned alongside colonies. Recorded separately so a station-only faction (the Kithrin outpost)
        // no longer tapes "colonies 0" while owning Titan — ColonyCount counts FactionInfoDB.Colonies only. Save-safe:
        // an appended [JsonProperty], so an older save with no value deserialises it to 0 (Newtonsoft matches by name).
        [JsonProperty] public int StationCount { get; set; }
        [JsonProperty] public int Contacts { get; set; }

        public AIDecisionRecord() { }

        public AIDecisionRecord(AIDecisionRecord o)
        {
            When = o.When; Tier = o.Tier; Objective = o.Objective; Reason = o.Reason;
            ActionKind = o.ActionKind; ActionDetail = o.ActionDetail;
            OwnStrength = o.OwnStrength; ThreatFactionId = o.ThreatFactionId; ThreatStrength = o.ThreatStrength;
            Morale = o.Morale; Legitimacy = o.Legitimacy; Balance = o.Balance;
            ColonyCount = o.ColonyCount; StationCount = o.StationCount; Contacts = o.Contacts;
        }
    }

    /// <summary>
    /// The AI FLIGHT RECORDER (docs/ai/DEVTEST-CONQUEST-SANDBOX-DESIGN.md §4 — the observability SPINE). A per-faction
    /// ring buffer of the last <see cref="Capacity"/> <see cref="AIDecisionRecord"/>s. <see cref="StrategicObjectiveDB"/>
    /// holds only the CURRENT cycle (each Tick overwrites it); this keeps the TAPE, so a whole game's brain reasoning is
    /// reviewable after the fact — as <c>[AI]</c> lines in the rolling <c>game_logs/</c> pages (readable remotely, client
    /// closed) and in the live AI Inspector window (both surfaces read this ONE record).
    ///
    /// Pure observability: the recorder never changes a sim outcome, so it runs ALWAYS-ON (not behind an action gate) —
    /// you watch the brain decide, THEN flip the order gates and watch it act. Bounded by construction (the ring buffer
    /// trims the oldest), so it can't grow without limit over a long game.
    /// </summary>
    public class AIDecisionRecordDB : BaseDataBlob
    {
        /// <summary>Max entries kept — ~2 game-months of daily decisions, plenty for recent review; oldest trims off.</summary>
        public const int Capacity = 60;

        [JsonProperty] public List<AIDecisionRecord> Records { get; internal set; } = new();

        public AIDecisionRecordDB() { }

        public AIDecisionRecordDB(AIDecisionRecordDB other)
        {
            Records = new List<AIDecisionRecord>(other.Records.Count);
            foreach (var r in other.Records)
                Records.Add(new AIDecisionRecord(r));
        }

        /// <summary>Append a decision to the tape, trimming the oldest once over <see cref="Capacity"/>.</summary>
        public void Append(AIDecisionRecord record)
        {
            if (record == null) return;
            Records.Add(record);
            while (Records.Count > Capacity)
                Records.RemoveAt(0);
        }

        /// <summary>The most recent decision, or null on an empty tape.</summary>
        public AIDecisionRecord Latest => Records.Count > 0 ? Records[Records.Count - 1] : null;

        public override object Clone() => new AIDecisionRecordDB(this);
    }
}
