using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;
using Pulsar4X.Damage;

namespace Pulsar4X.Hazards
{
    /// <summary>
    /// What a faction has LEARNED about space hazards — the set of damage SIGNATURES (flavours) it has
    /// encountered. This is the knowledge the research loop gates on: you can't research a counter to a flavour
    /// you've never met. It grows as the faction's ships fly through hazards (recorded by
    /// <see cref="HazardDiscovery"/>, called from <see cref="SpaceHazardProcessor"/>).
    ///
    /// Lives on the faction entity (created lazily on first discovery). Save/loaded — discovered knowledge persists.
    /// </summary>
    public class FactionHazardKnowledgeDB : BaseDataBlob
    {
        [JsonProperty] public HashSet<DamageSignature> DiscoveredSignatures { get; internal set; } = new HashSet<DamageSignature>();

        public FactionHazardKnowledgeDB() { }

        public FactionHazardKnowledgeDB(FactionHazardKnowledgeDB other)
        {
            DiscoveredSignatures = new HashSet<DamageSignature>(other.DiscoveredSignatures);
        }

        public override object Clone() => new FactionHazardKnowledgeDB(this);

        /// <summary>Records a signature as discovered; returns true if it was NEW (first time this faction met it).</summary>
        public bool Discover(DamageSignature sig) => DiscoveredSignatures.Add(sig);

        public bool Knows(DamageSignature sig) => DiscoveredSignatures.Contains(sig);
    }
}
