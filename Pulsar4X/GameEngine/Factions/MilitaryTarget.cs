using Pulsar4X.Colonies;
using Pulsar4X.Engine;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.8 P-3 — the deferred military REACH, first helper (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md +
    /// AI-BRAIN-BUILD-TRACKER.md). This is the SEEING half of turning "an NPC built a war fleet" into "an NPC sails
    /// that fleet at the enemy." <see cref="ConquerResolver"/> named three deferred helpers — MilitaryTarget /
    /// MilitaryComposition / MilitaryReach — and this is MilitaryTarget: the "which enemy world do I aim at?"
    /// perception.
    ///
    /// Pure read, no side effects, no warp/order surface touched (the movement-order emission that USES this target is
    /// the next slice — it touches the landmine-dense warp path, so it is kept separate and small). It names an enemy
    /// COLONY BODY to strike: a colony owned by a faction this one holds an <see cref="RelationshipState.AtWar"/> latch
    /// toward — the exact latch a Phase-3.4b coalition sets when it joins a war. So the chain composes: 3.4b DECLARES
    /// the war → this NAMES the target world → the reach slice SAILS the fleet there.
    ///
    /// v1 returns the FIRST at-war rival's first valid colony body. Ranking by real jump-route distance / reachability
    /// is the MilitaryReach slice (needs the pathfinder); here "nearest" is a v1 stub for "a target at all." No war on
    /// record, or no at-war rival that owns a colony → <see cref="Entity.InvalidEntity"/> (nothing to hit — the caller
    /// falls back to massing more fleet). Defensive/no-throw — safe to call every monthly cycle.
    /// </summary>
    public static class MilitaryTarget
    {
        /// <summary>
        /// Name an enemy colony's BODY to strike — the <see cref="ColonyInfoDB.PlanetEntity"/> of a colony owned by a
        /// faction <paramref name="faction"/> holds an AtWar latch toward. Returns <see cref="Entity.InvalidEntity"/>
        /// when there is no war on record or no at-war rival owns a valid colony. Only a STORED, AtWar relationship
        /// counts, so a faction at peace with everyone names no target (byte-identical — nothing consumes this yet).
        /// </summary>
        public static Entity NearestEnemyColonyBody(Entity faction)
        {
            if (faction == null || faction.Manager?.Game == null) return Entity.InvalidEntity;
            if (!faction.TryGetDataBlob<DiplomacyDB>(out var dip)) return Entity.InvalidEntity;
            var game = faction.Manager.Game;

            foreach (var rel in dip.Relationships.Values)
            {
                if (!rel.AtWar) continue;                                        // strike only a faction we're at war with
                int enemyId = rel.OtherFactionId;
                if (enemyId == faction.Id || enemyId == Game.NeutralFactionId) continue;
                if (!game.Factions.TryGetValue(enemyId, out var enemy) || enemy == null) continue;
                if (!enemy.TryGetDataBlob<FactionInfoDB>(out var enemyInfo)) continue;

                foreach (var colony in enemyInfo.Colonies)
                {
                    if (colony == null || !colony.IsValid) continue;
                    if (!colony.TryGetDataBlob<ColonyInfoDB>(out var colonyInfo)) continue;
                    var body = colonyInfo.PlanetEntity;
                    if (body != null && body.IsValid) return body;              // the world to sail at
                }
            }
            return Entity.InvalidEntity;
        }
    }
}
