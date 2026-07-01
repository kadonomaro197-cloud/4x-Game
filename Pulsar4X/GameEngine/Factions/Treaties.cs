using System;
using Pulsar4X.Engine;

namespace Pulsar4X.Factions
{
    /// <summary>The five treaty levers (docs/DIPLOMACY-DESIGN.md "Treaties — the levers"). Each is a real,
    /// costed decision: it sets a flag on the relationship, nudges the score, and (later) ripples into the
    /// signer's INTERNAL politics.</summary>
    public enum TreatyType
    {
        NonAggression,   // a standing promise not to attack; breaking it is the betrayal penalty
        TradeAgreement,  // opens inter-faction commerce (both profit)
        LogisticsAccess, // their ships may use your bases / supply network
        MilitaryAccess,  // their warships may transit your space without it being an act of war
        DefensivePact,   // an attack on one drags in the other — the deepest entanglement
        Peace            // end a declared war (un-latches AtWar)
    }

    /// <summary>
    /// The treaty engine — the "teeth" that turn a relationship SCORE into player MOVES
    /// (docs/DIPLOMACY-DESIGN.md, task #33). A treaty is proposed → considered → accepted/refused: the target
    /// decides from its own view of the proposer (relation score vs. a per-treaty trust threshold — the deeper the
    /// entanglement, the more trust required). On acceptance the flag is set on BOTH sides (a treaty is mutual) and
    /// both scores tick up (signing warms relations). This is the costed-lever SUBSTRATE the negotiation "scene"
    /// (C2) and the commitment model (#35) build on.
    ///
    /// v1 scope: the accept decision is relation-score-gated. The design's "their ARCHETYPE also decides" (a
    /// rival's diplomatic personality) folds in once NPC doctrine/personality data exists — a flagged follow-up,
    /// NOT a rebuild (it just adjusts the threshold per rival). Breaking a treaty = the betrayal penalty is its own
    /// later slice; this slice records the pacts. Nothing calls Propose yet, so adding it changes no behavior.
    /// </summary>
    public static class Treaties
    {
        /// <summary>The minimum relation score (on the DECIDER's view of the proposer) to accept this treaty. The
        /// deeper the entanglement, the more trust it takes: a non-aggression pact calms a tense border; a
        /// defensive pact needs a full alliance.</summary>
        public static int RequiredScore(TreatyType t) => t switch
        {
            TreatyType.NonAggression   => RelationshipState.HostileThreshold, // -25: even wary neighbours de-escalate
            TreatyType.TradeAgreement  => 0,                                  // Neutral+: mutual profit
            TreatyType.LogisticsAccess => RelationshipState.FriendlyThreshold,// +25: foreigners in your supply net
            TreatyType.MilitaryAccess  => RelationshipState.FriendlyThreshold,// +25: foreign warships in your space
            TreatyType.DefensivePact   => RelationshipState.AlliedThreshold,  // +75: a real entanglement
            TreatyType.Peace           => RelationshipState.MinScore,         // any war can be ended
            _ => RelationshipState.AlliedThreshold
        };

        /// <summary>How many points signing this treaty warms the relationship (both sides). Deeper commitments
        /// build more trust.</summary>
        public static int SigningBonus(TreatyType t) => t switch
        {
            TreatyType.NonAggression   => 10,
            TreatyType.TradeAgreement  => 10,
            TreatyType.LogisticsAccess => 10,
            TreatyType.MilitaryAccess  => 15,
            TreatyType.DefensivePact   => 20,
            TreatyType.Peace           => 5,
            _ => 5
        };

        /// <summary>
        /// Would the faction holding <paramref name="targetViewOfProposer"/> accept the proposed treaty? Peace is
        /// accepted only while actually at war; every other treaty needs peace first (you don't sign a trade deal
        /// mid-war) and a relation score at or above the treaty's trust threshold.
        /// </summary>
        public static bool WouldAccept(RelationshipState targetViewOfProposer, TreatyType t)
        {
            if (targetViewOfProposer == null) return false;
            if (t == TreatyType.Peace)
                return targetViewOfProposer.AtWar;                 // only meaningful during a war; always acceptable
            if (targetViewOfProposer.AtWar) return false;          // no ordinary treaties while shooting
            return targetViewOfProposer.RelationScore >= RequiredScore(t);
        }

        /// <summary>Apply a treaty's effect to ONE side's relationship row (the flag + the score warm-up). Called
        /// for both signers so the pact is mutual.</summary>
        private static void ApplyToRow(RelationshipState rel, TreatyType t)
        {
            switch (t)
            {
                case TreatyType.NonAggression:   rel.NonAggressionPact = true; break;
                case TreatyType.TradeAgreement:  rel.TradeAgreement = true;    break;
                case TreatyType.LogisticsAccess: rel.LogisticsAccess = true;   break;
                case TreatyType.MilitaryAccess:  rel.MilitaryAccess = true;    break;
                case TreatyType.DefensivePact:   rel.DefensivePact = true;     break;
                case TreatyType.Peace:           rel.MakePeace();              break;
            }
            rel.AdjustScore(SigningBonus(t));
        }

        /// <summary>
        /// Propose a treaty from <paramref name="proposerId"/> to <paramref name="targetId"/> using the two
        /// factions' ledgers. If the target accepts (see <see cref="WouldAccept"/>), the treaty is signed on BOTH
        /// sides (flag set + score warmed + LastContact stamped) and this returns true; otherwise it returns false
        /// and nothing changes. Pure on the two DataBlobs — the entity-level overload resolves them.
        /// </summary>
        public static bool Propose(DiplomacyDB proposerDip, int proposerId, DiplomacyDB targetDip, int targetId, TreatyType t, DateTime when)
        {
            if (proposerDip == null || targetDip == null) return false;

            var targetView = targetDip.GetOrCreateRelationship(proposerId);
            if (!WouldAccept(targetView, t)) return false;

            var proposerView = proposerDip.GetOrCreateRelationship(targetId);
            ApplyToRow(proposerView, t); proposerView.LastContact = when;
            ApplyToRow(targetView, t);   targetView.LastContact = when;
            return true;
        }

        /// <summary>Entity-level convenience: resolve both factions' <see cref="DiplomacyDB"/> and propose. Returns
        /// false (no-op) if either faction lacks a ledger.</summary>
        public static bool Propose(Entity proposerFaction, Entity targetFaction, TreatyType t, DateTime when)
        {
            if (proposerFaction == null || targetFaction == null) return false;
            if (!proposerFaction.TryGetDataBlob<DiplomacyDB>(out var pDip)) return false;
            if (!targetFaction.TryGetDataBlob<DiplomacyDB>(out var tDip)) return false;
            return Propose(pDip, proposerFaction.Id, tDip, targetFaction.Id, t, when);
        }
    }
}
