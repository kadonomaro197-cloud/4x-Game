using System.Collections.Generic;
using System.Linq;
using Pulsar4X.Engine;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Galaxy;
using Pulsar4X.Ships;
using Pulsar4X.Colonies;
using Pulsar4X.GroundCombat;

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
            // Attach the new fleet to the faction's fleet tree (the same call FleetOrder.Create makes). FleetFactory
            // .Create alone leaves the fleet an ORPHAN — owned by the faction but not a child of its root FleetDB —
            // and the Fleet window only lists factionRoot.Children, so an un-parented fleet never shows up there.
            fleet.GetDataBlob<FleetDB>().SetParent(enemyFaction);

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
        /// rich closing/dodge data. Pulls the design objects from a faction that has them in <c>ShipDesigns</c>.
        /// (This is the PLAYER task-force set; the enemy uses the beefier <see cref="HostileSquadronSet"/>.)</summary>
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
            return ResolveDesigns(info, ids);
        }

        /// <summary>A BEEFED-UP hostile squadron (the developer's "beef up the enemy fleets"): the well-rounded
        /// weapon spread PLUS a Leviathan CAPITAL and a doubled beam line — a genuine threat, not a token picket.
        /// Seven ships: capital + 2 beam + railgun + flak + 2 fighters. Used by <see cref="SpawnCombatScenario"/>
        /// so each rival faction fields a real fleet. Pulls design objects from a faction that holds them.</summary>
        public static List<ShipDesign> HostileSquadronSet(FactionInfoDB info)
        {
            string[] ids =
            {
                "default-ship-design-test-capital",   // Leviathan — capital ship (the beef)
                "default-ship-design-test-warship",   // Aegis — beams
                "default-ship-design-test-warship",   // a second Aegis
                "default-ship-design-test-railgun",   // Lancer — railguns
                "default-ship-design-test-flak",      // Bulwark — flak screen
                "default-ship-design-test-fighter",   // Wasp — fighter
                "default-ship-design-test-fighter",   // Wasp — a second fighter
            };
            return ResolveDesigns(info, ids);
        }

        // Look each design id up in a faction's ShipDesigns; skip any it doesn't hold (defensive — never throws).
        private static List<ShipDesign> ResolveDesigns(FactionInfoDB info, string[] ids)
        {
            var set = new List<ShipDesign>();
            foreach (var id in ids)
                if (info.ShipDesigns.TryGetValue(id, out var d)) set.Add(d);
            return set;
        }

        /// <summary>Create + register a hostile NPC faction set up to PERSIST like a real one: it's added to
        /// <c>game.Factions</c> (with a root FleetDB) by <see cref="FactionFactory.CreateBasicFaction"/>, told the
        /// system exists (<c>KnownSystems</c>), and handed the player's ship designs — the recipe that makes its
        /// owner-flipped ships survive a clock advance. The shared setup behind every enemy the sandbox stands up.</summary>
        public static Entity SetupHostileFaction(Game game, string name, string abbr, EntityManager system, FactionInfoDB playerInfo)
        {
            var faction = FactionFactory.CreateBasicFaction(game, name, abbr, 0);
            var info = faction.GetDataBlob<FactionInfoDB>();
            if (!info.KnownSystems.Contains(system.ManagerID))
                info.KnownSystems.Add(system.ManagerID);
            foreach (var kv in playerInfo.ShipDesigns)
                info.ShipDesigns[kv.Key] = kv.Value;
            return faction;
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
            // Attach to the owning faction's fleet tree so it appears in the Fleet window (FleetFactory.Create alone
            // leaves it an orphan; the window lists only factionRoot.Children). Same call FleetOrder.Create makes.
            fleet.GetDataBlob<FleetDB>().SetParent(owningFaction);
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

        /// <summary>Starting population of an NPC beachhead colony. FLAGGED number (balance) — big enough to be a real
        /// capture prize, small enough to fall to an invasion.</summary>
        private const long MarsColonyPopulation = 500_000_000L;

        /// <summary>Whether the combat scenario gives Mars a GROUND BEACHHEAD (enemy colony + garrison = a world you can
        /// invade and TAKE). **Default OFF** — a normal New Game has the fleet-only Mars rival, no Earth-Mars ground war;
        /// flip this on (DevTools / a test) to stage the take-Mars playtest. Kept off by the developer's "hold off on the
        /// Earth-Mars war" call while the work stays available.</summary>
        public static bool SpawnMarsBeachhead = false;

        /// <summary>
        /// Give an NPC faction a holdable GROUND BEACHHEAD on a body: a minimal colony (a capture TARGET — clearing its
        /// garrison flips ownership) plus a defending home garrison (region 0). This is what turns a fleet-only rival
        /// into a world you can TAKE. Reuses the passed species (an NPC "human" colony — fine for a playtest; flagged).
        ///
        /// Fog: <see cref="ColonyFactory.CreateColony"/> reveals the world on colony creation (survey is world-level in
        /// v1). We RE-FOG it afterward if it was fogged before, so the player still has to send a survey ship to see the
        /// surface (the "only your home starts surveyed" rule). The enemy garrison mustered fine before the re-fog;
        /// re-fogging only flips the per-region 'known' bool the player reads. Defensive — never throws.
        /// </summary>
        public static Entity SetupGroundBeachhead(Game game, Entity faction, Entity species, Entity body, long population)
        {
            if (game == null || faction == null || species == null || body == null) return Entity.InvalidEntity;

            // Was this world fogged (unsurveyed) before we colonised it? Only the player's home starts surveyed.
            bool wasFogged = body.TryGetDataBlob<PlanetRegionsDB>(out var regions)
                             && regions.Regions.Any(r => !r.Surveyed);

            var colony = ColonyFactory.CreateColony(faction, species, body, population);  // a capturable colony (reveals the world)
            GroundStartGarrison.RaiseHomeGarrison(body, faction.Id);                      // a defending garrison in region 0

            if (wasFogged && regions != null)                                            // re-fog: the player must scan it themselves
                foreach (var r in regions.Regions)
                    r.Surveyed = false;

            return colony;
        }

        /// <summary>Stand up a ready-to-watch COMBAT SCENARIO: two well-rounded PLAYER task forces at Earth, and a
        /// beefed-up HOSTILE squadron — each its OWN rival faction — at Luna, Venus, Mercury, and Mars. FOUR distinct
        /// enemy factions (2026-07-03, the developer's "beef up the enemy fleets and make them different factions"),
        /// so this also exercises MULTI-FACTION combat / IFF, each fielding a capital-led squadron
        /// (<see cref="HostileSquadronSet"/>). Luna is inside the auto-engage range, so that fight starts at once
        /// (instant data); Venus/Mercury/Mars are far, so the player sails a task force out to them (closing data).
        /// Because they're different factions they're hostile to the player AND each other, but they sit at separate
        /// bodies, so each only fights whoever comes to it. Returns the list of hostile factions (one per body found).
        /// The DevTools "Spawn Combat Scenario" button calls this.</summary>
        public static List<Entity> SpawnCombatScenario(Game game, EntityManager system, Entity playerFaction)
        {
            var playerInfo = playerFaction.GetDataBlob<FactionInfoDB>();
            List<ShipDesign> RoundSet() => WellRoundedDesignSet(playerInfo);   // player task-force set
            List<ShipDesign> BeefSet()  => HostileSquadronSet(playerInfo);     // beefier enemy set (capital-led)

            // Player task forces at Earth — the home base; send them out to the enemies. (Unchanged — the scenario's
            // own ships stay the well-rounded set; only the ENEMY was beefed up.)
            var earth = FindBody(system, "Earth");
            if (earth != null)
            {
                SpawnMixedFleet(game, system, playerFaction, playerFaction, RoundSet(), earth, "1st Task Force");
                SpawnMixedFleet(game, system, playerFaction, playerFaction, RoundSet(), earth, "2nd Task Force");
            }

            // One DISTINCT rival faction per body, each fielding a beefed-up squadron, with a MIX of engagement
            // postures so the first-shot / standoff mechanic has something to show: some attack on sight, one only
            // returns fire, one holds fire entirely. (Postures only BITE when the first-shot trigger flag —
            // RequireWeaponsReleaseToEngage — is on; with it off, everyone fights on proximity.)
            var enemyPlan = new[]
            {
                ("Luna",    "Lunar Free State",    "LFS", EngagementPosture.WeaponsFree),  // attack first — in auto-engage range, opens the fight
                ("Venus",   "Venusian Compact",    "VNC", EngagementPosture.ReturnFire),   // only shoots if shot at
                ("Mercury", "Mercury Combine",     "MRC", EngagementPosture.WeaponsHold),  // won't attack — a passive picket (standoff unless you fire)
                ("Mars",    "Martian Directorate", "MRD", EngagementPosture.WeaponsFree),  // attack first
            };
            var factions = new List<Entity>();
            foreach (var (bodyName, factionName, abbr, posture) in enemyPlan)
            {
                var body = FindBody(system, bodyName);
                if (body == null) continue;
                var enemy = SetupHostileFaction(game, factionName, abbr, system, playerInfo);
                factions.Add(enemy);
                var squadron = SpawnMixedFleet(game, system, enemy, playerFaction, BeefSet(), body,
                    $"{factionName} {bodyName} Squadron ({PostureLabel(posture)})");
                FleetDoctrine.SetEngagementPosture(squadron, posture);

                // MARS is the GROUND front: give the Martian Directorate a holdable colony + garrison, so Mars is a
                // world you can actually invade and TAKE (survey -> beat the squadron -> bombard -> land -> capture),
                // not just a fleet to shoot. Gated OFF by default (SpawnMarsBeachhead) — no Earth-Mars ground war in a
                // normal New Game; flip it on to stage the take-Mars playtest. The other rivals stay fleet-only.
                if (bodyName == "Mars" && SpawnMarsBeachhead)
                    SetupGroundBeachhead(game, enemy, playerInfo.Species.FirstOrDefault(), body, MarsColonyPopulation);
            }
            return factions;
        }

        private static string PostureLabel(EngagementPosture p) => p switch
        {
            EngagementPosture.WeaponsFree => "Weapons Free",
            EngagementPosture.WeaponsHold => "Hold Fire",
            EngagementPosture.ReturnFire => "Return Fire",
            _ => p.ToString(),
        };
    }
}
