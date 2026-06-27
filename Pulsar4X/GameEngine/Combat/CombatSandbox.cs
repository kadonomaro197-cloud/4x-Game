using System.Collections.Generic;
using System.Linq;
using Pulsar4X.Engine;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Galaxy;
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
                // Fill tanks BEFORE the owner flip: fuel resolves through the ship's faction library, and only
                // the player has the fuel unlocked (the bare enemy faction's CargoGoods is empty).
                ShipFactory.FillFuelTanks(ship, playerInfo);
                // Charge the reactor too (energy is the ship's own, faction-independent): a 0-charge ship can't
                // fire (weapons draw stored energy) or warp — spawn it ready to fight.
                ShipFactory.ChargeReactors(ship);
                ship.FactionOwnerID = enemyFaction.Id;
                game.OrderHandler.HandleOrder(FleetOrder.AssignShip(enemyFaction.Id, fleet, ship));
            }
            return fleet;
        }

        /// <summary>The "well-rounded" design set for a good combat-data fleet: BEAM (Aegis) + RAILGUN (Lancer) +
        /// FLAK (Bulwark) + two FIGHTERS (Wasp). Deliberately NO Leviathan capital — it carries all three weapon
        /// flavors (the whole triangle) and a range spread (long beam / mid railgun / short flak), so a fight gives
        /// rich closing/dodge data. Pulls the design objects from a faction that has them in <c>ShipDesigns</c>.</summary>
        public static List<ShipDesign> WellRoundedDesignSet(FactionInfoDB info)
        {
            string[] ids =
            {
                "default-ship-design-test-warship",   // Aegis — 4 beams (long range)
                "default-ship-design-test-railgun",   // Lancer — 4 railguns (mid)
                "default-ship-design-test-flak",      // Bulwark — 4 flak (short, anti-fighter)
                "default-ship-design-test-fighter",   // Wasp — fighter (evasive screen)
                "default-ship-design-test-fighter",   // Wasp — a second fighter
            };
            var set = new List<ShipDesign>();
            foreach (var id in ids)
                if (info.ShipDesigns.TryGetValue(id, out var d)) set.Add(d);
            return set;
        }

        /// <summary>Spawn ONE fleet of the given designs (one ship each), owned by <paramref name="owningFaction"/>,
        /// parked at <paramref name="body"/>, fuelled + charged (ready to fly + fire). Ships are built under
        /// <paramref name="playerFaction"/> (which has the unlocked components/fuel) then owner-flipped — the same
        /// recipe as <see cref="SpawnHostileFleet"/>, generalised to a mixed design list and any owner.</summary>
        public static Entity SpawnMixedFleet(Game game, EntityManager system, Entity owningFaction, Entity playerFaction,
            List<ShipDesign> designs, Entity body, string fleetName)
        {
            var playerInfo = playerFaction.GetDataBlob<FactionInfoDB>();
            var fleet = FleetFactory.Create(system, owningFaction.Id, fleetName);
            int i = 0;
            foreach (var design in designs)
            {
                i++;
                var ship = ShipFactory.CreateShip(design, playerFaction, body, $"{fleetName} {design.Name} {i}");
                ShipFactory.FillFuelTanks(ship, playerInfo);   // BEFORE the flip — fuel resolves via the player's library
                ShipFactory.ChargeReactors(ship);
                ship.FactionOwnerID = owningFaction.Id;
                game.OrderHandler.HandleOrder(FleetOrder.AssignShip(owningFaction.Id, fleet, ship));
            }
            return fleet;
        }

        /// <summary>Find a body in the system by its default name (e.g. "Earth", "Luna", "Mars"), or null.</summary>
        public static Entity FindBody(EntityManager system, string name)
        {
            foreach (var e in system.GetAllEntitiesWithDataBlob<SystemBodyInfoDB>())
                if (e.GetDefaultName() == name)
                    return e;
            return null;
        }

        /// <summary>Stand up a ready-to-watch COMBAT SCENARIO: two well-rounded PLAYER task forces at Earth, and
        /// well-rounded HOSTILE squadrons at Luna, Venus, Mercury, and Mars. Luna is inside the auto-engage range, so
        /// that fight starts at once (instant data); Venus/Mercury/Mars are far, so the player sails a task force out
        /// to them (closing data). Returns the hostile faction. The DevTools "Spawn Combat Scenario" button calls this.</summary>
        public static Entity SpawnCombatScenario(Game game, EntityManager system, Entity playerFaction)
        {
            var playerInfo = playerFaction.GetDataBlob<FactionInfoDB>();
            List<ShipDesign> RoundSet() => WellRoundedDesignSet(playerInfo);

            // Player task forces at Earth — the home base; send them out to the enemies.
            var earth = FindBody(system, "Earth");
            if (earth != null)
            {
                SpawnMixedFleet(game, system, playerFaction, playerFaction, RoundSet(), earth, "1st Task Force");
                SpawnMixedFleet(game, system, playerFaction, playerFaction, RoundSet(), earth, "2nd Task Force");
            }

            // One hostile faction (set up to persist like a real NPC: knows the system, holds the designs), with a
            // well-rounded squadron at each of the four bodies.
            var enemy = FactionFactory.CreateBasicFaction(game, "Hostiles", "FOE", 0);
            var enemyInfo = enemy.GetDataBlob<FactionInfoDB>();
            if (!enemyInfo.KnownSystems.Contains(system.ManagerID))
                enemyInfo.KnownSystems.Add(system.ManagerID);
            foreach (var kv in playerInfo.ShipDesigns)
                enemyInfo.ShipDesigns[kv.Key] = kv.Value;

            foreach (var bodyName in new[] { "Luna", "Venus", "Mercury", "Mars" })
            {
                var body = FindBody(system, bodyName);
                if (body != null)
                    SpawnMixedFleet(game, system, enemy, playerFaction, RoundSet(), body, $"Hostile {bodyName} Squadron");
            }
            return enemy;
        }
    }
}
