using Pulsar4X.Engine;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Resolves the <see cref="GovernmentDB"/> that governs an entity, and is the single lookup the economy /
    /// population / research processors call to read a regime's coefficient dials (docs/GOVERNMENT-AND-POLITICS-
    /// DESIGN.md, task #30 — "wire the substrate into processors"). The government is a MODULATOR: its dials are
    /// coefficients the rest of the engine multiplies by. This is the wiring that makes those dials real.
    ///
    /// Every faction is attached a `GovernmentDB` at creation (default all-Mid = the neutral middle), so the
    /// coefficients are 1.0/neutral until a player picks a non-Mid regime — which is why wiring these in changes
    /// no current behavior. A missing government (older save, bare faction) falls back to the shared neutral
    /// default, so a read never throws or shifts the balance.
    /// </summary>
    public static class GovernmentTools
    {
        /// <summary>The neutral (all-Mid) government used when a faction has none. Read-only — never mutated.</summary>
        private static readonly GovernmentDB Neutral = new GovernmentDB();

        /// <summary>The faction's government, or the neutral default if it has none.</summary>
        public static GovernmentDB Of(Entity faction)
            => faction != null && faction.TryGetDataBlob<GovernmentDB>(out var gov) ? gov : Neutral;

        /// <summary>The government of the faction that OWNS this entity (colony / ship / station), or the neutral
        /// default. This is the call a per-colony processor makes: resolve the owning faction from
        /// <see cref="Entity.FactionOwnerID"/>, then read its dials.</summary>
        public static GovernmentDB OwnerOf(Entity entity)
        {
            var game = entity?.Manager?.Game;
            if (game != null && game.Factions.TryGetValue(entity.FactionOwnerID, out var faction))
                return Of(faction);
            return Neutral;
        }
    }
}
