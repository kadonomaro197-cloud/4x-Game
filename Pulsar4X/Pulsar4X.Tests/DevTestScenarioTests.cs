using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Combat;      // FleetCombat.Ships
using Pulsar4X.Energy;      // EnergyGenAbilityDB (ground the fleet for the BuildTransport gauge)
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.GroundCombat;
using Pulsar4X.Modding;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The DevTest game-start gauge (the "DevTest" button that replaces Quickstart). Proves the data-driven start
    /// stands up end-to-end through the WORKING pieces this branch built:
    ///   Sol via StarSystemFactory.LoadFromBlueprint  →  DevTestStartFactory.CreateDevTest  →
    ///   FactionFactory.LoadFromJson (design/species BY ID + the "startingItems" unlock + the inline colony/station parser).
    /// The first fixture loads the PLAYER faction alone, so a gotcha-#10 failure (a design/species/body id that
    /// doesn't resolve) is isolated to one file. The second loads the WHOLE conquest sandbox (UEF + United Martian
    /// Federation + Kithrin Collective) and asserts the scenario's shape: an inner-system war, war-strain on the
    /// aggressor's colonies, and the Kithrin's outer-system station.
    /// </summary>
    [TestFixture]
    public class DevTestScenarioTests
    {
        private const string ScenarioDir = "Data/basemod/ScenarioFiles";

        private static Game NewGame()
        {
            var modDataStore = new ModDataStore();
            var modLoader = new ModLoader();
            modLoader.LoadModManifest("Data/basemod/modInfo.json", modDataStore);

            var gameSettings = new NewGameSettings
            {
                MaxSystems = 1,
                CreatePlayerFaction = false,   // DevTest authors its own factions from JSON
                DefaultSolStart = true,
                MasterSeed = 12345,
                EleStart = true
            };
            return GameFactory.CreateGame(modDataStore, gameSettings);
        }

        [Test]
        [Description("The DevTest player faction (UEF) loads from JSON with its Earth colony and its full startingItems "
                     + "unlock — everything ENABLED to design/build, nothing pre-built. Exercises the modernized "
                     + "FactionFactory.LoadFromJson (designs by id, startingItems unlock, inline colony parser).")]
        public void DevTest_PlayerFaction_LoadsWithColonyAndUnlocks()
        {
            var game = NewGame();

            var (player, startingSystemId) = DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json" });

            Assert.That(player, Is.Not.Null, "DevTest returned no player faction.");
            Assert.That(player.IsValid, Is.True, "player faction is not valid.");
            Assert.That(startingSystemId, Is.Not.Null.And.Not.Empty, "no starting system id returned.");

            var info = player.GetDataBlob<FactionInfoDB>();
            Assert.That(info, Is.Not.Null, "player faction has no FactionInfoDB.");
            Assert.That(info.Colonies.Count, Is.GreaterThan(0), "player faction has no colony (Earth).");

            // The "startingItems" unlock ran: a listed material was unlocked into CargoGoods AND synced into
            // IndustryDesigns (what makes it buildable). If this is empty, the unlock pass didn't run.
            Assert.That(info.IndustryDesigns.Count, Is.GreaterThan(0),
                "startingItems unlock produced no buildable IndustryDesigns.");
            Assert.That(info.IndustryDesigns.ContainsKey("stainless-steel"), Is.True,
                "a startingItems material (stainless-steel) was not unlocked into IndustryDesigns — the unlock pass "
                + "or the material sync didn't run.");
        }

        [Test]
        [Description("The WHOLE DevTest conquest sandbox loads: UEF (player) + United Martian Federation (NPC, inner-system "
                     + "war economy) + Kithrin Collective (NPC, outer-system developed station). Asserts the scenario's "
                     + "shape — three factions, the UMF authored as an NPC at war with the player with war-strain on its "
                     + "colonies, and the Kithrin holding an outer-system station. This is the gotcha-#10 sensor for the "
                     + "NPC files (war/strain/station parsing) the way the player test is for the player file.")]
        public void DevTest_FullSandbox_ThreeFactionsWarStrainAndStation()
        {
            var game = NewGame();

            var (player, startingSystemId) = DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            Assert.That(player, Is.Not.Null, "DevTest returned no player faction.");
            var playerInfo = player.GetDataBlob<FactionInfoDB>();
            Assert.That(playerInfo.IsNPC, Is.False, "the player faction (first file) should not be an NPC.");

            // Collect every loaded faction's info blob. Classify the two NPCs by their authored shape rather than by
            // name: the UMF is the NPC with the inner-system colony cluster; the Kithrin is the NPC with a station.
            var infos = game.Factions.Values
                .Where(f => f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>())
                .Select(f => f.GetDataBlob<FactionInfoDB>())
                .ToList();

            Assert.That(infos.Count(i => i.IsNPC), Is.GreaterThanOrEqualTo(2),
                "expected at least two NPC factions (UMF + Kithrin) loaded from JSON.");

            var umf = infos.FirstOrDefault(i => i.IsNPC && i.Colonies.Count >= 4);
            Assert.That(umf, Is.Not.Null,
                "the United Martian Federation (an NPC with its four inner-system colonies) did not load.");

            var kithrin = infos.FirstOrDefault(i => i.IsNPC && i.Stations.Count > 0);
            Assert.That(kithrin, Is.Not.Null,
                "the Kithrin Collective (an NPC with an outer-system station) did not load — the 'stations' parser "
                + "or the station's modules didn't resolve.");

            // The UMF opened the game already at war with the player (openingRelations, applied second-pass).
            var umfEntity = umf.OwningEntity;
            Assert.That(umfEntity, Is.Not.Null, "UMF FactionInfoDB has no owning entity.");
            var umfDiplomacy = umfEntity.GetDataBlob<DiplomacyDB>();
            Assert.That(umfDiplomacy.GetRelationship(player.Id).AtWar, Is.True,
                "the UMF should have opened the game at war with the player (openingRelations atWar).");

            // The war-strain landed: the UMF's colonies carry the authored high war-tax (ApplyOpeningStrain sets the
            // INPUT the economy processor reads, so the strain sticks and degrades morale over time).
            var strainedColony = umf.Colonies.FirstOrDefault(c =>
                c != null && c.IsValid && c.HasDataBlob<ColonyEconomyDB>()
                && c.GetDataBlob<ColonyEconomyDB>().TaxRate > 0.0);
            Assert.That(strainedColony, Is.Not.Null,
                "no UMF colony carries the authored war-tax strain — ApplyOpeningStrain didn't run or found no economy blob.");
        }

        [Test]
        [Description("DevTest colony worlds start DEFENDED: after the full sandbox loads, the UMF's colony worlds (and the "
                     + "player's Earth) carry a GroundForcesDB with their owner's home garrison — so an invasion is a real "
                     + "fight, not an unopposed capture, and the AI's own worlds aren't free for the taking. The garrison "
                     + "is the ground echo of the authored fleets, raised for every DevTest faction (unlike the barebones "
                     + "New Game). Also the gauge that the DevTest Sol generates the region maps the garrison needs.")]
        public void DevTest_ColonyWorlds_StartWithAHomeGarrison()
        {
            var game = NewGame();
            var (player, _) = DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            var infos = game.Factions.Values
                .Where(f => f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>())
                .Select(f => f.GetDataBlob<FactionInfoDB>()).ToList();
            var umf = infos.First(i => i.IsNPC && i.Colonies.Count >= 4);
            int umfId = umf.OwningEntity.Id;

            // Every UMF colony body should carry a GroundForcesDB with UMF-owned units (its home garrison).
            int garrisonedBodies = 0, umfUnits = 0;
            foreach (var colony in umf.Colonies)
            {
                if (colony == null || !colony.IsValid) continue;
                var body = colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;
                if (body != null && body.IsValid && body.TryGetDataBlob<GroundForcesDB>(out var forces))
                {
                    int mine = forces.Units.Count(u => u.FactionOwnerID == umfId);
                    if (mine > 0) { garrisonedBodies++; umfUnits += mine; }
                }
            }
            Assert.That(garrisonedBodies, Is.GreaterThan(0),
                "no UMF colony world carries a home garrison — RaiseForFactionColonies didn't run or the bodies have no region map.");
            Assert.That(umfUnits, Is.GreaterThanOrEqualTo(6),
                "expected at least one UMF world's combined-arms garrison (3 inf + 2 armor + 1 arty = 6).");

            // The player's Earth is defended too, so a UMF invasion (once the AI can land troops) meets resistance.
            var earthBody = player.GetDataBlob<FactionInfoDB>().Colonies
                .Where(c => c != null && c.IsValid)
                .Select(c => c.GetDataBlob<ColonyInfoDB>().PlanetEntity)
                .FirstOrDefault(b => b != null && b.IsValid);
            Assert.That(earthBody, Is.Not.Null, "player has no colony body.");
            Assert.That(earthBody.TryGetDataBlob<GroundForcesDB>(out var earthForces)
                && earthForces.Units.Any(u => u.FactionOwnerID == player.Id), Is.True,
                "the player's Earth should start with a home garrison in the DevTest.");
        }

        [Test]
        [Description("B5-1: the UMF can build a TROOP TRANSPORT — the ship that lifts ground units to an invasion. The "
                     + "base mod had no ship mounting a troop-bay, so the AI (or player) could never carry troops off-world. "
                     + "Asserts UMF's ShipDesigns holds 'default-ship-design-trooper' (so every component id resolved — the "
                     + "gotcha-#10 sensor for a scenario ship design) AND that it mounts a GroundBayAtb (Personnel carry room) "
                     + "so it can actually lift infantry. The prerequisite for the B5 conquest loop's load/land step.")]
        public void DevTest_UMF_CanBuildATroopTransport_ThatCarriesABay()
        {
            var game = NewGame();
            DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            var umf = game.Factions.Values
                .Where(f => f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>())
                .Select(f => f.GetDataBlob<FactionInfoDB>())
                .First(i => i.IsNPC && i.Colonies.Count >= 4);

            Assert.That(umf.ShipDesigns.ContainsKey("default-ship-design-trooper"), Is.True,
                "UMF can't build the troop transport — the ship design didn't load (a component id didn't resolve, "
                + "or it's missing from UMF's shipDesigns list).");

            var trooper = umf.ShipDesigns["default-ship-design-trooper"];
            Assert.That(trooper.TryGetComponentsByAttribute<GroundBayAtb>(out var bays) && bays.Count > 0, Is.True,
                "the troop transport mounts no GroundBayAtb — it can't actually carry ground units.");
        }

        [Test]
        [Description("B5-2 helpers: ConquerResolver.IsTroopTransport recognises the trooper (mounts a bay) and rejects the "
                     + "gunship (no bay); and FactionOwnsTransport is FALSE for UMF right after load — it has the trooper "
                     + "DESIGN but no transport SHIP yet (its start fleet is gunships). These are the two reads the "
                     + "BuildTransport rung turns on.")]
        public void DevTest_ConquerResolver_TransportDetection_Helpers()
        {
            var game = NewGame();
            DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            var umfEntity = game.Factions.Values.First(f =>
                f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>()
                && f.GetDataBlob<FactionInfoDB>().IsNPC
                && f.GetDataBlob<FactionInfoDB>().Colonies.Count >= 4);
            var umf = umfEntity.GetDataBlob<FactionInfoDB>();

            Assert.That(ConquerResolver.IsTroopTransport(umf.ShipDesigns["default-ship-design-trooper"]), Is.True,
                "the trooper mounts a troop bay → IsTroopTransport true");
            Assert.That(ConquerResolver.IsTroopTransport(umf.ShipDesigns["default-ship-design-gunship"]), Is.False,
                "the gunship has no troop bay → IsTroopTransport false");

            var state = FactionState.Snapshot(umfEntity);
            Assert.That(ConquerResolver.FactionOwnsTransport(state), Is.False,
                "UMF owns the trooper DESIGN but no transport SHIP yet (its start fleet is gunships) → false");
        }

        [Test]
        [Description("B5-2: an at-war UMF that owns NO troop transport (only the design) and can't sail its fleet yet "
                     + "decides to BUILD one. We ground UMF's strike fleet (clear its stored warp energy) so the sail rung "
                     + "can't fire, then resolve Conquer — with a war target, a shipyard line, the trooper design, and no "
                     + "transport ship, ConquerResolver's new Rung 2 returns 'BuildTransport'. The prerequisite for the "
                     + "load/land keystone (B5-3).")]
        public void DevTest_UMF_AtWar_GroundedFleet_BuildsATransport()
        {
            var game = NewGame();
            DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            var umfEntity = game.Factions.Values.First(f =>
                f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>()
                && f.GetDataBlob<FactionInfoDB>().IsNPC
                && f.GetDataBlob<FactionInfoDB>().Colonies.Count >= 4);

            // Ground the strike fleet: clear each ship's stored warp energy so MilitaryReach.HasRange is false and the
            // sail rung (Rung 1) can't fire — forcing the resolver to the BuildTransport rung.
            foreach (var fleet in FactionState.Snapshot(umfEntity).OwnedFleets())
                foreach (var ship in FleetCombat.Ships(fleet))
                    if (ship != null && ship.IsValid && ship.TryGetDataBlob<EnergyGenAbilityDB>(out var egen))
                        egen.EnergyStored.Clear();

            var state = FactionState.Snapshot(umfEntity);
            var action = new ConquerResolver().Resolve(state,
                new StrategicObjectiveDB { Objective = StrategicObjective.Conquer });

            Assert.That(action.Kind, Is.EqualTo("BuildTransport"),
                "a belligerent UMF that can't sail yet and owns no transport should BUILD one (Rung 2). "
                + $"Got '{action.Kind}': {action.Detail}");
        }
    }
}
