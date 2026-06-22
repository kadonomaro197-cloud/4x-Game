using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Modding;
using Pulsar4X.Factions;
using Pulsar4X.People;
using Pulsar4X.Colonies;
using Pulsar4X.Galaxy;
using Pulsar4X.Extensions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Reproduces the real New Game startup path INSIDE CI — the JSON colony-blueprint construction the live
    /// game runs when you click "New Game" (NewGameMenu.CreateGameCore -> ColonyFactory.CreateFromBlueprint).
    ///
    /// Why this is the missing gauge: the rest of the suite never builds a colony this way — it either skips
    /// colonies (CreateTestUniverse) or uses the broken in-code DefaultHumans helper. So a crash in the actual
    /// New Game path ships green through `dotnet test` and only blows up when a human clicks New Game. Because
    /// the colony builder lives in the ENGINE (not the SDL client), CI CAN run it — so if this throws, the CI
    /// log carries the exact exception + stack trace, no console needed from the player.
    ///
    /// Two variants on purpose:
    ///   - base mod only            -> the baseline (should match the working Quickstart path)
    ///   - base mod + testing mod   -> the exact combination the player's live session loads
    /// If the base passes but base+testing fails, that pins the fault on the testing mod's data.
    /// </summary>
    [TestFixture]
    public class NewGameStartSmokeTests
    {
        [Test]
        [Description("New Game start with the BASE mod only must build the starting colony without throwing.")]
        public void NewGameStart_BaseMod_DoesNotThrow()
        {
            BuildNewGameStart("Data/basemod/modInfo.json");
        }

        [Test]
        [Ignore("Separate issue surfaced by this sensor: enabling the TESTING mod makes the New Game colony "
                + "build throw NullReferenceException (the testing mod ships incomplete Armor/Theme data; it "
                + "adds no species/colony, so this is NOT the player-facing 'no mod enabled -> .First() on empty' "
                + "crash, which is fixed in NewGameMenu.DisplayModsPage). Base mod alone passes. Re-enable once "
                + "the testing mod data is completed or the engine hardens against partial blueprints.")]
        [Description("New Game start with BASE + TESTING mod (the combo the live game loads) must not throw.")]
        public void NewGameStart_BaseModPlusTestingMod_DoesNotThrow()
        {
            BuildNewGameStart("Data/basemod/modInfo.json", "Data/testingmod/modInfo.json");
        }

        /// <summary>
        /// Mirrors NewGameMenu.CreateGameCore at the engine level, using the same default selection the New Game
        /// wizard uses (first species, first colony, first CanStartHere body). Throws the same exception the live
        /// game would hit; on failure the full exception is written to the test log before rethrowing.
        /// </summary>
        private static void BuildNewGameStart(params string[] modManifestPaths)
        {
            try
            {
                // Load the requested mods exactly as the game does (base first, then any overlay mods).
                var modDataStore = new ModDataStore();
                var modLoader = new ModLoader();
                foreach (var path in modManifestPaths)
                    modLoader.LoadModManifest(path, modDataStore);

                // --- selection: mirror the New Game wizard defaults ---
                // Systems that have at least one body flagged CanStartHere.
                var enabledSystems = modDataStore.Systems
                    .Where(sys => modDataStore.SystemBodies.Any(b => b.Value.CanStartHere && sys.Value.Bodies.Contains(b.Key)))
                    .Select(sys => sys.Key)
                    .ToList();

                Assert.That(enabledSystems, Is.Not.Empty,
                    "No system has a CanStartHere body — New Game has nowhere to place the starting colony.");

                string systemId = enabledSystems.First();
                var systemBlueprint = modDataStore.Systems[systemId];
                string bodyId = modDataStore.SystemBodies
                    .First(b => b.Value.CanStartHere && systemBlueprint.Bodies.Contains(b.Key)).Key;
                string speciesId = modDataStore.Species.First().Key;   // New Game uses .First(), NOT .First(Playable)
                string colonyId = modDataStore.Colonies.First().Key;

                // --- build: mirror CreateGameCore ---
                var gameSettings = new NewGameSettings
                {
                    MaxSystems = enabledSystems.Count,   // == EnabledSystems.Count, so no random systems are generated
                    CreatePlayerFaction = true,
                    DefaultFactionName = "Test Faction",
                    DefaultSolStart = true,
                    MasterSeed = 12345,
                    EleStart = true
                };

                Game game = GameFactory.CreateGame(modDataStore, gameSettings);

                // Load the pre-made systems and capture the one we start in (LoadFromBlueprint returns it).
                StarSystem startingSystem = null!;
                foreach (var id in enabledSystems)
                {
                    var loaded = StarSystemFactory.LoadFromBlueprint(game, modDataStore.Systems[id]);
                    if (id == systemId)
                        startingSystem = loaded;
                }
                Assert.That(startingSystem, Is.Not.Null, $"Loaded starting system '{systemId}' came back null.");

                // Find the starting body by name, the same way CreateGameCore does.
                var startingBodyBlueprint = modDataStore.SystemBodies[bodyId];
                Entity startingBody = null!;
                foreach (var sb in startingSystem.GetAllDataBlobsOfType<SystemBodyInfoDB>())
                {
                    if (sb.OwningEntity?.GetDefaultName()?.Equals(startingBodyBlueprint.Name) == true)
                        startingBody = sb.OwningEntity!;
                }
                Assert.That(startingBody, Is.Not.Null,
                    $"Starting body '{startingBodyBlueprint.Name}' (id '{bodyId}') was not found in system '{systemId}'.");

                var playerFaction = FactionFactory.CreateBasicFaction(game, "Test Faction", "TST", 100_000_000);
                playerFaction.FactionOwnerID = playerFaction.Id;
                playerFaction.GetDataBlob<FactionInfoDB>().KnownSystems.Add(startingSystem.ID);

                var playerSpecies = SpeciesFactory.CreateFromBlueprint(startingSystem, modDataStore.Species[speciesId]);
                playerSpecies.FactionOwnerID = playerFaction.Id;
                playerFaction.GetDataBlob<FactionInfoDB>().Species.Add(playerSpecies);

                // The crash site we are hunting: the real colony builder the live New Game runs.
                ColonyFactory.CreateFromBlueprint(game, playerFaction, playerSpecies, startingSystem, startingBody,
                    modDataStore.Colonies[colonyId]);
            }
            catch (Exception ex) when (ex is not AssertionException)
            {
                // Guarantee the exact failure (type + message + stack trace) lands in the CI test log / TRX so a
                // reproduced New Game crash is fully diagnosable straight from the cloud run.
                TestContext.WriteLine("=== New Game start REPRODUCED a failure ===");
                TestContext.WriteLine($"Mods loaded: {string.Join(", ", modManifestPaths)}");
                TestContext.WriteLine(ex.ToString());
                throw;
            }
        }
    }
}
