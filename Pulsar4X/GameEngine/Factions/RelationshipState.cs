using System;
using Newtonsoft.Json;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Where one faction stands toward another, on a five-step ladder from open war to alliance. This is the
    /// "headline" of a relationship — the single word a player sees ("we are Hostile toward the Vega Combine") —
    /// derived from the underlying <see cref="RelationshipState.RelationScore"/> the way a temperature gauge's
    /// COLD/WARM/HOT band is derived from the actual degrees. See docs/DIPLOMACY-DESIGN.md.
    /// </summary>
    public enum DiplomaticStance
    {
        War,        // shooting on sight — IFF hostile, combat engages
        Hostile,    // not shooting yet, but no cooperation and a hair-trigger
        Neutral,    // the default with a stranger: no deals, no hostility
        Friendly,   // cooperative — trade/logistics deals are on the table
        Allied      // bound by treaty — mutual access, mutual defense
    }

    /// <summary>
    /// One faction's standing toward ONE other faction — the per-pair record that lives inside a
    /// <see cref="DiplomacyDB"/>. It is deliberately a plain value object (not a DataBlob): a faction owns a
    /// whole table of these, one per other faction it knows about.
    ///
    /// The load-bearing idea (docs/DIPLOMACY-DESIGN.md, the relationship TRACK): a relationship is a NUMBER on a
    /// track (<see cref="RelationScore"/>, −100..+100) that events nudge up and down, and the <see cref="DiplomaticStance"/>
    /// the rest of the game reads is DERIVED from that number by fixed thresholds. So a treaty signing, a border
    /// incident, or a caught spy each move one dial — the score — and the stance follows. War is a latched
    /// override (<see cref="AtWar"/>) so a declared war doesn't silently lapse just because the score drifts up.
    ///
    /// This is the SUBSTRATE step: the data + the derivation. Nothing reads it for hostility/IFF yet — that
    /// wiring is a deliberate later slice so adding this changes no current behavior.
    /// </summary>
    public class RelationshipState
    {
        // --- Score thresholds (named, not magic numbers — the bands on the gauge) ---
        /// <summary>At or above this score the stance reads Allied.</summary>
        public const int AlliedThreshold = 75;
        /// <summary>At or above this score (and below Allied) the stance reads Friendly.</summary>
        public const int FriendlyThreshold = 25;
        /// <summary>At or below this score the stance reads Hostile (War is a separate latched flag).</summary>
        public const int HostileThreshold = -25;
        /// <summary>The track's hard limits. Score is always clamped into this band.</summary>
        public const int MinScore = -100;
        public const int MaxScore = 100;

        /// <summary>The faction this record is ABOUT (the other party). The owner is the faction holding the table.</summary>
        [JsonProperty] public int OtherFactionId { get; internal set; }

        /// <summary>The relationship score on the −100..+100 track. 0 = a stranger at neutral.</summary>
        [JsonProperty] public int RelationScore { get; internal set; } = 0;

        /// <summary>
        /// A latched state of declared war. When true the stance is forced to <see cref="DiplomaticStance.War"/>
        /// regardless of score — a war is ended by an explicit peace, not by the score drifting upward.
        /// </summary>
        [JsonProperty] public bool AtWar { get; internal set; } = false;

        // --- Treaty flags (the concrete deals; each gates a real capability when wired) ---
        /// <summary>A non-aggression pact: a standing promise not to attack. Breaking it is the betrayal penalty.</summary>
        [JsonProperty] public bool NonAggressionPact { get; internal set; } = false;
        /// <summary>A standing trade agreement (commerce between the two factions is permitted).</summary>
        [JsonProperty] public bool TradeAgreement { get; internal set; } = false;
        /// <summary>Logistics access: this faction's supply network may route through the other's territory.</summary>
        [JsonProperty] public bool LogisticsAccess { get; internal set; } = false;
        /// <summary>Military access: this faction's warships may transit the other's territory without it being an act of war.</summary>
        [JsonProperty] public bool MilitaryAccess { get; internal set; } = false;
        /// <summary>A defensive pact: an attack on one drags in the other. The deepest, most entangling treaty.</summary>
        [JsonProperty] public bool DefensivePact { get; internal set; } = false;

        /// <summary>When the two factions last made contact (first-contact / last diplomatic exchange). Null = never met.</summary>
        [JsonProperty] public DateTime? LastContact { get; internal set; } = null;

        public RelationshipState() { }

        public RelationshipState(int otherFactionId)
        {
            OtherFactionId = otherFactionId;
        }

        public RelationshipState(RelationshipState other)
        {
            OtherFactionId = other.OtherFactionId;
            RelationScore = other.RelationScore;
            AtWar = other.AtWar;
            NonAggressionPact = other.NonAggressionPact;
            TradeAgreement = other.TradeAgreement;
            LogisticsAccess = other.LogisticsAccess;
            MilitaryAccess = other.MilitaryAccess;
            DefensivePact = other.DefensivePact;
            LastContact = other.LastContact;
        }

        public RelationshipState Copy() => new RelationshipState(this);

        /// <summary>
        /// The headline stance, derived from the score (with War as a latched override). This is the single
        /// method the rest of the game asks "how do these two feel about each other?" — keep the derivation
        /// here so every reader agrees.
        /// </summary>
        public DiplomaticStance CurrentStance()
        {
            if (AtWar) return DiplomaticStance.War;
            if (RelationScore >= AlliedThreshold) return DiplomaticStance.Allied;
            if (RelationScore >= FriendlyThreshold) return DiplomaticStance.Friendly;
            if (RelationScore <= HostileThreshold) return DiplomaticStance.Hostile;
            return DiplomaticStance.Neutral;
        }

        /// <summary>
        /// Nudge the relationship score by <paramref name="delta"/> (can be negative), clamped to the track's
        /// limits. This is the ONE mutation every diplomatic event funnels through — a treaty signing adds, a
        /// border incident or caught spy subtracts. Returns the new clamped score.
        /// </summary>
        public int AdjustScore(int delta)
        {
            RelationScore = Math.Clamp(RelationScore + delta, MinScore, MaxScore);
            return RelationScore;
        }

        /// <summary>Latch a declared war (and drop the score to fully hostile so a later peace starts from the bottom).</summary>
        public void DeclareWar()
        {
            AtWar = true;
            RelationScore = MinScore;
        }

        /// <summary>End a war. The score is left where it is (at the floor) — peace is fragile by default.</summary>
        public void MakePeace()
        {
            AtWar = false;
        }
    }
}
