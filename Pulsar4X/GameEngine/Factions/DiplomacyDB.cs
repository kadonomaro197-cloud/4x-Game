using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// A faction's whole diplomatic standing toward every OTHER faction it knows about — its "ledger of
    /// relationships", one row per other faction. This is the substrate the entire external-politics layer hangs
    /// on (docs/DIPLOMACY-DESIGN.md): treaties, first-contact, IFF/hostility, casus belli, and the reactive
    /// "Are we good?" engine all read and write the per-pair <see cref="RelationshipState"/> rows in here.
    ///
    /// Attached to every faction entity by <see cref="FactionFactory"/>. Each faction keeps its OWN view — the
    /// relationship is stored once per side (A's table holds how A feels about B; B's table holds how B feels
    /// about A), so the two can legitimately disagree (A thinks they're Friendly; B is quietly Hostile). A
    /// processor that wants a symmetric fact (war) writes both sides.
    ///
    /// SUBSTRATE step: this is the data store + safe accessor only. No processor reads it for behavior yet, so
    /// adding it changes nothing in the running game — the IFF/combat/first-contact wiring is a later slice.
    /// </summary>
    public class DiplomacyDB : BaseDataBlob
    {
        /// <summary>
        /// other-faction entity Id → this faction's standing toward it. A faction NOT in this table is an
        /// unmet stranger; <see cref="GetRelationship"/> returns a fresh Neutral record for it (and does NOT
        /// store it — "looking" doesn't create a relationship; only an actual event does, via
        /// <see cref="SetRelationship"/>).
        /// </summary>
        [JsonProperty] public Dictionary<int, RelationshipState> Relationships { get; internal set; } = new();

        public DiplomacyDB() { }

        public DiplomacyDB(DiplomacyDB other)
        {
            Relationships = new Dictionary<int, RelationshipState>(other.Relationships.Count);
            foreach (var kvp in other.Relationships)
                Relationships[kvp.Key] = kvp.Value.Copy();
        }

        public override object Clone() => new DiplomacyDB(this);

        /// <summary>
        /// This faction's standing toward <paramref name="otherFactionId"/>. If the two have never interacted,
        /// returns a fresh Neutral (score 0) record WITHOUT storing it — reading a relationship that doesn't
        /// exist must not silently create one. Use <see cref="GetOrCreateRelationship"/> when you are about to
        /// mutate (an actual diplomatic event) and want the row persisted.
        /// </summary>
        public RelationshipState GetRelationship(int otherFactionId)
        {
            if (Relationships.TryGetValue(otherFactionId, out var rel))
                return rel;
            return new RelationshipState(otherFactionId);
        }

        /// <summary>
        /// Get the stored relationship row, creating and storing a fresh Neutral one if this is the first
        /// interaction. Call this when an actual diplomatic event happens (contact, treaty, incident) — the row
        /// is now part of the faction's permanent ledger.
        /// </summary>
        public RelationshipState GetOrCreateRelationship(int otherFactionId)
        {
            if (!Relationships.TryGetValue(otherFactionId, out var rel))
            {
                rel = new RelationshipState(otherFactionId);
                Relationships[otherFactionId] = rel;
            }
            return rel;
        }

        /// <summary>Store (or replace) a relationship row. Used by load/migration and explicit setters.</summary>
        public void SetRelationship(RelationshipState rel)
        {
            Relationships[rel.OtherFactionId] = rel;
        }

        /// <summary>True if a relationship row already exists for the other faction (they have met / interacted).</summary>
        public bool HasMet(int otherFactionId) => Relationships.ContainsKey(otherFactionId);
    }
}
