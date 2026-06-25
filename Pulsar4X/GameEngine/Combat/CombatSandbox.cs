using System.Linq;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// Dev / test utility: stand up a HOSTILE fleet to fight, so the auto-resolve engine can be exercised live.
    /// A fresh game has no enemy (just the player + an empty sky), so there is nothing for the combat trigger to
    /// engage — this builds the other side.
    ///
    /// <see cref="SpawnHostileFleet"/> creates a registered second faction, gives it a fleet of ships built from
    /// the player's designs, parked at the same body as the player — so <see cref="BattleTriggerProcessor"/>
    /// auto-engages them on the next tick. The DevTools "Spawn Hostile Fleet" button calls this.
    ///
    /// WHY THIS LIVES IN THE ENGINE (not the client): the test fixtures that drive combat
    /// (<c>BattleTriggerTests</c> etc.) deliberately call <c>CombatEngagement.Tick</c> DIRECTLY and never advance
    /// the game clock, because a *bare* enemy faction's owner-flipped ships "don't survive movement processing
    /// across a clock advance" — i.e. the full per-tick processor sweep dropped them before they could fight. That
    /// made "spawn an enemy and press play" an unproven, CI-blind path. Putting the spawn here lets
    /// <c>CombatSandboxTests</c> ADVANCE THE REAL CLOCK and assert the spawned enemy survives + engages — the CI
    /// gauge that the live button actually works. The faction is "set up" enough (registered in
    /// <c>game.Factions</c> by <see cref="FactionFactory.CreateBasicFaction"/>, told the system exists via
    /// <c>KnownSystems</c>, handed the player's <c>ShipDesigns</c>) that its ships persist like a real NPC's.
    /// </summary>
    public static class CombatSandbox
    {
        /// <summary>
        /// Spawn <paramref name="count"/> hostile ships of <paramref name="design"/> as a new fleet of a new
        /// faction, orbiting <paramref name="orbitBody"/>, hostile to <paramref name="playerFaction"/>. Returns the
        /// enemy fleet entity. Ships are built under the player faction (which has the unlocked components) and then
        /// owner-flipped to the enemy (combat only reads <c>FactionOwnerID</c>) and assigned via the order system.
        /// </summary>
        public static Entity SpawnHostileFleet(Game game, EntityManager system, Entity playerFaction,
            ShipDesign design, int count, Entity orbitBody, string factionName = "Hostiles")
        {
            // A registered enemy faction. CreateBasicFaction adds it to game.Factions and gives it a root FleetDB.
            var enemyFaction = FactionFactory.CreateBasicFaction(game, factionName, "FOE", 0);
            var enemyInfo = enemyFaction.GetDataBlob<FactionInfoDB>();
            var playerInfo = playerFaction.GetDataBlob<FactionInfoDB>();

            // "Set the faction up fully" so its ships persist through a clock advance (not just a direct Tick):
            // tell it this system exists, and hand it the player's ship designs (real NPC factions have both).
            if (!enemyInfo.KnownSystems.Contains(system.ManagerID))
                enemyInfo.KnownSystems.Add(system.ManagerID);
            foreach (var kv in playerInfo.ShipDesigns)
                enemyInfo.ShipDesigns[kv.Key] = kv.Value;

            var fleet = FleetFactory.Create(system, enemyFaction.Id, factionName + " Fleet");

            for (int i = 0; i < count; i++)
            {
                var ship = ShipFactory.CreateShip(design, playerFaction, orbitBody, $"{factionName} {i + 1}");
                ship.FactionOwnerID = enemyFaction.Id;
                game.OrderHandler.HandleOrder(FleetOrder.AssignShip(enemyFaction.Id, fleet, ship));
            }
            return fleet;
        }
    }
}
