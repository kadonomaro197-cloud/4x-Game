namespace Pulsar4X.Factions
{
    /// <summary>
    /// Something one faction OBSERVES about another (through its own sensor fog) that makes it act
    /// (docs/DIPLOMACY-DESIGN.md "Reactive diplomacy — the world acts on its own"). The external mirror of an
    /// INTERNAL sim-pressure: an observation, not a scripted event.
    /// </summary>
    public enum ExternalStimulus
    {
        FleetNearBorder,          // your ships near their space — the developer's "Are we good?" example
        AtWarWithTheirEnemy,      // you fight someone they hate → a chance for alliance
        AtWarWithTheirFriend,     // you fight someone they like → a warning
        YouAppearWeak,            // you lost a battle / have unrest → they press the advantage
        YouAppearStrong,          // you're winning → they seek your favor
        CrisisOnTheirBorder,      // a pirate/crisis threat → they ask for a defense fleet (the commitment)
        TheyLackAResourceYouHold, // a shortage you could fill → a trade proposal
        YouBrokeATreaty,          // you broke a pact (with anyone) → distrust
        YouHonoredTreaties,       // a track record of kept deals → trust
        TheirMilitaristsRose      // their OWN internal politics turned hawkish → more aggressive overtures
    }

    /// <summary>What a faction GENERATES in response to an <see cref="ExternalStimulus"/> — the overture/query/demand
    /// it sends you. You then respond (reassure / dodge / concede / threaten), and the response nudges the relation.</summary>
    public enum DiplomaticOverture
    {
        None,                 // no reaction (e.g. an ally is unbothered by your fleet)
        AreWeGoodProbe,       // the low-stakes intent probe — "are we good?"
        AllianceOffer,        // "we share an enemy — let's ally"
        WarningToStop,        // "back off / stop that"
        PressTheAdvantage,    // you look weak — a demand, vassalization push, or war threat
        SeekFavor,            // you look strong — tribute / a protection deal
        RequestDefenseFleet,  // "post a fleet at my world against this crisis" (→ the commitment that sails your ships)
        TradeProposal,        // "you have what I lack — let's trade"
        DistrustGuardRises,   // you broke faith — deals dry up
        DeeperDealOffer,      // you've proven reliable — they offer more
        AggressiveOverture    // their hawks are loud — a harder line
    }

    /// <summary>
    /// The "Are we good?" ENGINE (task #35): the world acts on what it SEES, generating overtures instead of
    /// waiting on a hidden timer — built as the INTERNAL emergent-demand engine pointed outward (big reuse, not a
    /// new engine). Pure and stateless: an observation + the observer's current view of you → the overture it
    /// generates, plus (for the trust-accrual stimuli) a direct nudge to the relationship needle.
    ///
    /// Fog cuts both ways: the caller only feeds stimuli the observer can actually DETECT of you, so EMCON-dark
    /// movement can dodge the "Are we good?" probe entirely (sneak the fleet) or you move loud to intimidate. This
    /// slice is the decision table; the loop that feeds it observations each cycle (reading fleet positions vs. the
    /// sensor contact table) is the wiring step, and a generated overture becoming a real message/commitment is the
    /// commitment-model step. Nothing calls this yet → no behavior change.
    /// </summary>
    public static class ReactiveDiplomacy
    {
        /// <summary>
        /// The overture a faction generates from an observation, gated (where it matters) by its current stance
        /// toward you — a neutral stranger probes ("are we good?"), a hostile reads the same fleet as a threat, an
        /// ally shrugs. Mirrors the design's observation→overture table.
        /// </summary>
        public static DiplomaticOverture Overture(ExternalStimulus obs, RelationshipState theirViewOfYou)
        {
            var stance = theirViewOfYou?.CurrentStance() ?? DiplomaticStance.Neutral;

            switch (obs)
            {
                case ExternalStimulus.FleetNearBorder:
                    // An ally/friend is unbothered; a hostile reads a threat; a neutral runs the intent probe.
                    if (stance == DiplomaticStance.Allied || stance == DiplomaticStance.Friendly)
                        return DiplomaticOverture.None;
                    if (stance == DiplomaticStance.Hostile || stance == DiplomaticStance.War)
                        return DiplomaticOverture.WarningToStop;
                    return DiplomaticOverture.AreWeGoodProbe;

                case ExternalStimulus.AtWarWithTheirEnemy:    return DiplomaticOverture.AllianceOffer;
                case ExternalStimulus.AtWarWithTheirFriend:   return DiplomaticOverture.WarningToStop;
                case ExternalStimulus.YouAppearWeak:          return DiplomaticOverture.PressTheAdvantage;
                case ExternalStimulus.YouAppearStrong:        return DiplomaticOverture.SeekFavor;
                case ExternalStimulus.CrisisOnTheirBorder:    return DiplomaticOverture.RequestDefenseFleet;
                case ExternalStimulus.TheyLackAResourceYouHold: return DiplomaticOverture.TradeProposal;
                case ExternalStimulus.YouBrokeATreaty:        return DiplomaticOverture.DistrustGuardRises;
                case ExternalStimulus.YouHonoredTreaties:     return DiplomaticOverture.DeeperDealOffer;
                case ExternalStimulus.TheirMilitaristsRose:   return DiplomaticOverture.AggressiveOverture;
                default:                                       return DiplomaticOverture.None;
            }
        }

        /// <summary>
        /// The direct relationship-needle nudge an observation carries (trust accrues, distrust bites). Most
        /// stimuli produce an OVERTURE (a probe/offer) rather than an automatic score change and return 0; the
        /// track-record ones move the needle on their own — breaking faith is the sharpest.
        /// </summary>
        public static int RelationDelta(ExternalStimulus obs) => obs switch
        {
            ExternalStimulus.YouBrokeATreaty     => -15,  // distrust — the betrayal signal
            ExternalStimulus.YouHonoredTreaties  => 5,    // trust accrues slowly
            ExternalStimulus.TheirMilitaristsRose => -5,  // their hawks sour the mood
            _ => 0
        };
    }
}
