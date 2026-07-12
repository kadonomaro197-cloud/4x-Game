using System;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Espionage E3 — an IN-PROGRESS covert operation, attached to the AGENT entity while it runs its job. This is the
    /// "task the agent" rung made concrete: tasking an operative (<see cref="Espionage.TaskAgent"/>) stamps this blob
    /// onto them and schedules <see cref="EspionageProcessor"/> to resolve it at <see cref="ResolveOn"/>. While an agent
    /// carries one they are BUSY (can't be re-tasked). On resolution the detection roll decides the outcome, the effect
    /// lands (or doesn't), and the blob is consumed. An agent with no <see cref="CovertOpDB"/> is idle/available.
    /// </summary>
    public class CovertOpDB : BaseDataBlob
    {
        /// <summary>The rival faction this op is run AGAINST.</summary>
        [JsonProperty] public int TargetFactionId { get; set; }

        /// <summary>Which covert action from the catalog (gather / steal-tech / sabotage / …).</summary>
        [JsonProperty] public CovertAction Action { get; set; }

        /// <summary>The intel facet the op bears on (for GatherIntel, the facet raised to Confirmed on success).</summary>
        [JsonProperty] public IntelFacet TargetFacet { get; set; }

        /// <summary>When the op resolves — the scheduled <see cref="EspionageProcessor"/> instant.</summary>
        [JsonProperty] public DateTime ResolveOn { get; set; }

        public CovertOpDB() { }

        public CovertOpDB(CovertOpDB other)
        {
            TargetFactionId = other.TargetFactionId;
            Action = other.Action;
            TargetFacet = other.TargetFacet;
            ResolveOn = other.ResolveOn;
        }

        public override object Clone() => new CovertOpDB(this);
    }
}
