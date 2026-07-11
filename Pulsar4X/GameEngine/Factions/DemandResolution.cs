namespace Pulsar4X.Factions
{
    /// <summary>The binary response to a demand (v1 — partial/negotiated responses are later). The "teeth."</summary>
    public enum DemandResponse
    {
        /// <summary>Do the ask: the bloc's loyalty + legitimacy rise (but it costs money/policy and angers a rival bloc).</summary>
        Enact,
        /// <summary>Ignore/suppress it: loyalty ↓, unrest ↑, legitimacy ↓ — how much depends on the regime.</summary>
        Refuse,
    }

    /// <summary>
    /// F-C2c (docs/GOVERNMENT-AND-POLITICS-DESIGN.md §"The decision — enact / refuse"): the teeth. Turns a response
    /// to a demand into a LEGITIMACY delta — the regime's health bar (<see cref="Pulsar4X.Colonies.LegitimacyDB"/>)
    /// is what mishandled politics actually costs you.
    ///
    /// Enact → a fixed legitimacy gain (you delivered). Refuse → a loss that grows with the demand's PRESSURE and the
    /// regime's REFUSAL HARDNESS: a CONSENT regime (low Authority) bleeds more legitimacy from refusing — people
    /// emigrate / vote you out; a COMMAND regime (high Authority) can suppress, so the immediate legitimacy hit is
    /// smaller (the resentment instead stacks toward a coup — a later slice). Pure/derived → byte-identical (nothing
    /// applies the delta yet; the Interior-Minister / player-response wire that feeds it into LegitimacyDB is next).
    /// </summary>
    public static class DemandResolution
    {
        /// <summary>Legitimacy gained by enacting a demand (you kept faith with the bloc). Flagged balance dial.</summary>
        public const double EnactLegitimacyGain = 5.0;

        /// <summary>The base legitimacy lost by refusing ANY demand, before pressure/regime scaling. Flagged dial.</summary>
        public const double RefuseLegitimacyBase = 5.0;

        /// <summary>
        /// The legitimacy delta of <paramref name="response"/> to <paramref name="demand"/> under
        /// <paramref name="government"/>. Positive = the regime is shored up; negative = it's eroded.
        /// </summary>
        public static double LegitimacyDelta(PoliticalDemand demand, DemandResponse response, GovernmentDB government)
        {
            if (response == DemandResponse.Enact)
                return EnactLegitimacyGain;

            // Refuse: base + the demand's loudness, scaled by how much refusal costs THIS regime.
            return -(RefuseLegitimacyBase + demand.Pressure * RefusalHardness(government));
        }

        /// <summary>
        /// How much refusing a demand costs a regime, by government type (the consent-vs-command split): a consent
        /// regime (Authority Low) 1.5× — refusal is expensive (emigration / lost elections); a command regime
        /// (Authority High) 0.5× — it can suppress, so the immediate legitimacy cost is lower. Null/Mid → 1.0.
        /// </summary>
        public static double RefusalHardness(GovernmentDB government)
        {
            if (government == null)
                return 1.0;

            return government.Authority switch
            {
                GovNotch.Low => 1.5,
                GovNotch.High => 0.5,
                _ => 1.0,
            };
        }
    }
}
