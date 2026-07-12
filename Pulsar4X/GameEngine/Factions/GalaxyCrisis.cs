using Pulsar4X.Engine;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase 4 — 🌌 The Galaxy + Crisis (docs/AI-BRAIN-BUILD-TRACKER.md). The late-game endgame: a faction that
    /// reaches a transcendent CAPABILITY — an "ascension," a Stellaris-crisis-style existential leap — becomes a
    /// galaxy-wide threat the others must unite against. The ascension is a real CAPABILITY (F-D2:
    /// <see cref="FactionDataStore.Capabilities"/>), granted by RESEARCHING a transcendent tech (a tech whose
    /// <c>Unlocks</c> names a <c>capability-</c> id → <see cref="FactionDataStore.Unlock"/> routes it to the capability
    /// set). So it is the concrete, meaningful capability the Phase-4.1 "a tech grants a capability, not a component"
    /// concept exists for — cradle-to-grave: research the tech → hold the flag → the galaxy reacts.
    ///
    /// THIS slice is the CRISIS DETECTOR (a pure read): who, if anyone, has ascended. The crisis EVENT and the
    /// coalition RESPONSE — every other faction treats the ascendant as the shared threat and declares war on it,
    /// reusing the Phase-3.4 coalition machinery — are the next slice (4.2b). Pure/no side effects → byte-identical
    /// (nothing consumes it yet).
    /// </summary>
    public static class GalaxyCrisis
    {
        /// <summary>The capability whose acquisition makes a faction a GALAXY CRISIS — the "ascension" flag. FLAGGED id.
        /// Granted by researching a transcendent tech (the F-D2 capability-unlock path).</summary>
        public const string AscensionCapability = "capability-ascension";

        /// <summary>
        /// The faction that has ASCENDED (holds <see cref="AscensionCapability"/>), or <see cref="Entity.InvalidEntity"/>
        /// if none — the galaxy is not in crisis. First holder wins (v1; simultaneous ascension by two factions is a
        /// later refinement). Defensive/no-throw — safe to poll.
        /// </summary>
        public static Entity Ascendant(Game game)
        {
            if (game == null) return Entity.InvalidEntity;
            foreach (var kvp in game.Factions)
            {
                if (kvp.Key == Game.NeutralFactionId) continue;
                var faction = kvp.Value;
                if (faction == null || !faction.IsValid) continue;
                if (!faction.TryGetDataBlob<FactionInfoDB>(out var info) || info.Data == null) continue;
                if (info.Data.HasCapability(AscensionCapability))
                    return faction;
            }
            return Entity.InvalidEntity;
        }

        /// <summary>True if some faction has ascended — the galaxy crisis is live.</summary>
        public static bool IsCrisisActive(Game game) => Ascendant(game).IsValid;
    }
}
