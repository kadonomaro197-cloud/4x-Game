using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Factions
{
    /// <summary>The facets you can hold intel on about a rival — the picture is sharp on some, blank on others.</summary>
    public enum IntelFacet
    {
        /// <summary>Their stance/intent toward you (are those massing fleets a threat or a bluff?).</summary>
        Disposition,
        /// <summary>Their military strength (the Inferred band = the fog-limited ThreatAssessment read).</summary>
        Military,
        /// <summary>Their economy / production.</summary>
        Economy,
        /// <summary>Their internal politics (unrest, blocs, a wavering province to exploit).</summary>
        InternalPolitics,
        /// <summary>Their secrets (tech, plans, ascension breakthroughs).</summary>
        Secrets,
    }

    /// <summary>How good your picture of a facet is (docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md §D2).</summary>
    public enum IntelLevel
    {
        /// <summary>The default: you see only behaviour + a fuzzy estimate (the poker default).</summary>
        Inferred,
        /// <summary>You raised intel on it: the estimate sharpens / the truth is revealed.</summary>
        Confirmed,
        /// <summary>Confirmed intel that has decayed — you must refresh it (you can't know a rival forever).</summary>
        Stale,
    }

    /// <summary>One facet's intel: its current level + when it was last confirmed (for decay).</summary>
    public class IntelRecord
    {
        [JsonProperty] public IntelLevel Level { get; set; } = IntelLevel.Inferred;
        [JsonProperty] public DateTime LastConfirmed { get; set; }

        public IntelRecord() { }
        public IntelRecord(IntelRecord other)
        {
            Level = other.Level;
            LastConfirmed = other.LastConfirmed;
        }
    }

    /// <summary>
    /// F-C3a (docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md — the Information Ledger, "the load-bearing new concept"):
    /// per-rival, per-facet intel level — fog-of-war for POLITICS, on the detection substrate. For every rival you've
    /// met you hold an <see cref="IntelLevel"/> on each <see cref="IntelFacet"/>: Inferred (behaviour + fuzzy
    /// estimate) → Confirmed (raised by an ambassador/agent → sharp) → Stale (decays; refresh or lose the picture).
    ///
    /// This slice is the DATA MODEL + the level/confirm/decay logic only — a new blob NOT yet attached to factions
    /// (so it's byte-identical / no save-format change to faction creation), NOT yet fed by anything. Slice F-C3b
    /// attaches it and sets the Military facet's baseline from <see cref="ThreatAssessment.DetectedStrengthOf"/>;
    /// later slices raise facets via agents and decay them over time.
    /// </summary>
    public class InformationLedgerDB : BaseDataBlob
    {
        /// <summary>rival faction id → (facet → intel record). Absent = Inferred (the default picture).</summary>
        [JsonProperty]
        public Dictionary<int, Dictionary<IntelFacet, IntelRecord>> Ledger { get; internal set; } = new();

        public InformationLedgerDB() { }

        public InformationLedgerDB(InformationLedgerDB other)
        {
            Ledger = new Dictionary<int, Dictionary<IntelFacet, IntelRecord>>();
            foreach (var kvp in other.Ledger)
            {
                var facets = new Dictionary<IntelFacet, IntelRecord>();
                foreach (var facetKvp in kvp.Value)
                    facets[facetKvp.Key] = new IntelRecord(facetKvp.Value);
                Ledger[kvp.Key] = facets;
            }
        }

        public override object Clone() => new InformationLedgerDB(this);

        /// <summary>Your intel level on a rival's facet — <see cref="IntelLevel.Inferred"/> if you hold nothing.</summary>
        public IntelLevel LevelOf(int rivalFactionId, IntelFacet facet)
        {
            if (Ledger.TryGetValue(rivalFactionId, out var facets) && facets.TryGetValue(facet, out var record))
                return record.Level;
            return IntelLevel.Inferred;
        }

        /// <summary>Raise a facet to <see cref="IntelLevel.Confirmed"/>, stamping <paramref name="when"/> for decay.</summary>
        public void Confirm(int rivalFactionId, IntelFacet facet, DateTime when)
        {
            if (!Ledger.TryGetValue(rivalFactionId, out var facets))
            {
                facets = new Dictionary<IntelFacet, IntelRecord>();
                Ledger[rivalFactionId] = facets;
            }
            if (!facets.TryGetValue(facet, out var record))
            {
                record = new IntelRecord();
                facets[facet] = record;
            }
            record.Level = IntelLevel.Confirmed;
            record.LastConfirmed = when;
        }

        /// <summary>Every Confirmed record not refreshed within <paramref name="staleAfter"/> drops to Stale.</summary>
        public void DecayStale(DateTime now, TimeSpan staleAfter)
        {
            foreach (var facets in Ledger.Values)
                foreach (var record in facets.Values)
                    if (record.Level == IntelLevel.Confirmed && now - record.LastConfirmed > staleAfter)
                        record.Level = IntelLevel.Stale;
        }
    }
}
