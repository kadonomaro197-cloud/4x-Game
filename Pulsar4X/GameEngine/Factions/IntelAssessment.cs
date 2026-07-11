using Pulsar4X.Engine;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// F-C3b (docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md): the bridge between the Information Ledger and the eyes —
    /// what confirming intel actually BUYS you. A rival's estimated military strength is resolved through the ledger:
    /// with the Military facet only <b>Inferred</b> (or Stale, or no ledger) you get the fog-limited
    /// <see cref="ThreatAssessment.DetectedStrengthOf"/> — behaviour + a fuzzy estimate; once an agent raises it to
    /// <b>Confirmed</b> you get the TRUE number (<see cref="FactionRollup.MilitaryStrength"/>). So spending
    /// intelligence sharpens the poker read into the truth — the whole point of the ledger. Pure/read-only →
    /// byte-identical (nothing calls it yet; the AI's threat read + the intel UI are the consumers).
    /// </summary>
    public static class IntelAssessment
    {
        /// <summary>
        /// The observer's best estimate of <paramref name="rivalFactionId"/>'s military strength given its
        /// <paramref name="ledger"/>: the TRUE strength if Military intel is Confirmed, otherwise the fog-limited
        /// detected estimate.
        /// </summary>
        public static double EstimatedMilitaryStrength(Entity observer, int rivalFactionId, InformationLedgerDB ledger)
        {
            if (ledger != null && ledger.LevelOf(rivalFactionId, IntelFacet.Military) == IntelLevel.Confirmed)
            {
                var game = observer?.Manager?.Game;
                if (game != null && game.Factions.TryGetValue(rivalFactionId, out var rivalFaction))
                    return FactionRollup.MilitaryStrength(rivalFaction); // confirmed → the real number
            }

            // Inferred / Stale / no ledger → the fuzzy fog-limited read.
            return ThreatAssessment.DetectedStrengthOf(observer, rivalFactionId);
        }
    }
}
