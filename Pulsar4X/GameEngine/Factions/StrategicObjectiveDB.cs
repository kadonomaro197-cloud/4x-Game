using System;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// The faction's NEEDS LADDER (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II Phase 2 — the Organism engine).
    /// A Maslow-style hierarchy: a faction attends to a lower rung before it reaches for a higher one. The brain
    /// reads its own gauges (war standing, morale/legitimacy, money, strength) each cycle and settles on the
    /// LOWEST unmet tier — you don't chase a grand ambition while a colony starves.
    /// </summary>
    public enum NeedTier
    {
        /// <summary>Existential: at war and losing, a colony starving, open rebellion. Everything bends to not dying.</summary>
        Survive,
        /// <summary>Internal health: unrest, low legitimacy, a bleeding treasury. Fix the house before growing it.</summary>
        Stabilize,
        /// <summary>Healthy: grow the economy and tech, build up. The default "good times" tier.</summary>
        Thrive,
        /// <summary>Dominant and secure: reach for the grand aim — expand, out-tech, or conquer, as personality dictates.</summary>
        Ambition,
    }

    /// <summary>
    /// The concrete STRATEGY a faction commits to for a cycle — what it actually spends its effort on. Chosen from
    /// the faction's current <see cref="NeedTier"/>, its doctrine weights, and its personality (the Phase-2.4 Tick
    /// turns this into orders). Deliberately a small, legible set — one clear intent per cycle.
    /// </summary>
    public enum StrategicObjective
    {
        /// <summary>No objective settled (the neutral default — an all-zero doctrine, or a brand-new faction).</summary>
        None,
        /// <summary>Survive: pour into defense — warships, fortification, hold the line.</summary>
        Defend,
        /// <summary>Stabilize: quell unrest — ease taxes, raise morale, restore legitimacy.</summary>
        Consolidate,
        /// <summary>Thrive: build the economy — colonies, industry, refineries.</summary>
        GrowEconomy,
        /// <summary>Thrive: push research — labs, scientists, tech.</summary>
        AdvanceTech,
        /// <summary>Ambition: expand — survey and settle new worlds.</summary>
        Expand,
        /// <summary>Ambition: go to war for gain — build a fleet and take from a rival.</summary>
        Conquer,
    }

    /// <summary>
    /// Phase-2.1 (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II): the faction's GOAL SLOT — the single objective its
    /// brain is committed to this cycle, plus the needs-tier it came from and a commitment clock (so the plan
    /// doesn't thrash every month — Phase 2.3 reads <see cref="CommittedUntil"/> for hysteresis). This slice is the
    /// DATA MODEL only: a new blob NOT yet attached to any faction or written by anything → byte-identical. The
    /// needs-ladder read (2.2) fills <see cref="Tier"/>, the transition engine (2.3) manages the commitment, and the
    /// Tick (2.4) reads <see cref="Objective"/> to emit orders.
    /// </summary>
    public class StrategicObjectiveDB : BaseDataBlob
    {
        /// <summary>The needs-ladder rung the faction is currently attending to.</summary>
        [JsonProperty] public NeedTier Tier { get; internal set; } = NeedTier.Thrive;

        /// <summary>The concrete strategy chosen from that tier this cycle.</summary>
        [JsonProperty] public StrategicObjective Objective { get; internal set; } = StrategicObjective.None;

        /// <summary>The faction this objective targets (a rival to Conquer, an ally to defend), or -1 if none.</summary>
        [JsonProperty] public int TargetFactionId { get; internal set; } = -1;

        /// <summary>Game time before which the brain holds this objective rather than re-planning (hysteresis, 2.3).
        /// Default <see cref="DateTime.MinValue"/> = "not committed", so a fresh objective is always free to change.</summary>
        [JsonProperty] public DateTime CommittedUntil { get; internal set; } = DateTime.MinValue;

        /// <summary>P1 Visibility Gate: the KIND of the last step the planner emitted (QueueBuild / QueueMine / None …).
        /// Empty until the planner acts. Recorded because every planner failure is otherwise SILENT.</summary>
        [JsonProperty] public string LastActionKind { get; internal set; } = "";

        /// <summary>P1 Visibility Gate: the human-readable line for the last step (what it did, or why it's stuck) —
        /// the `PlannerAction.Detail`, surfaced by <see cref="PlanReadout"/>.</summary>
        [JsonProperty] public string LastActionDetail { get; internal set; } = "";

        public StrategicObjectiveDB() { }

        public StrategicObjectiveDB(StrategicObjectiveDB other)
        {
            Tier = other.Tier;
            Objective = other.Objective;
            TargetFactionId = other.TargetFactionId;
            CommittedUntil = other.CommittedUntil;
            LastActionKind = other.LastActionKind;
            LastActionDetail = other.LastActionDetail;
        }

        public override object Clone() => new StrategicObjectiveDB(this);
    }
}
