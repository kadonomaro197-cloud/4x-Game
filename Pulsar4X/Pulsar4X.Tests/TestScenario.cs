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
    /// The scenario harness — a reusable engine-level mid-game state that feature tests build on.
    ///
    /// It stands up a REAL start the way the live game does (GameFactory.CreateGame →
    /// StarSystemFactory.LoadFromBlueprint → FactionFactory/SpeciesFactory/ColonyFactory.CreateFromBlueprint,
    /// using the New Game wizard's default selection), hands back the key entities, and can advance the
    /// simulation clock. Other tests (economy, combat, …) set their preconditions on top of this instead of
    /// re-deriving the start each time.
    ///
    /// Engine-only, so it runs in CI — it is the engine's gauge board: build a known state, advance the
    /// clock, then read/assert. Self-tests live in ScenarioHarnessTests; a build failure logs the full
    /// exception to the test output.
    /// </summary>
    public sealed class TestScenario
    {
        public Game Game { get; private set; } = null!;
        public Entity Faction { get; private set; } = null!;
        public Entity Species { get; private set; } = null!;
        public StarSystem StartingSystem { get; private set; } = null!;
        public Entity StartingBody { get; private set; } = null!;
        public Entity Colony { get; private set; } = null!;

        private TestScenario() { }

        /// <summary>
        /// Build a default mid-game start. With no args, loads the base mod and uses the New Game wizard's
        /// default selection (first species/colony, first CanStartHere body). Throws — with the full
        /// exception written to the test output — if the build fails.
        /// </summary>
        public static TestScenario CreateWithColony(params string[] modManifestPaths)
        {
            if (modManifestPaths == null || modManifestPaths.Length == 0)
                modManifestPaths = new[] { "Data/basemod/modInfo.json" };

            var s = new TestScenario();
            try
            {
                var modDataStore = new ModDataStore();
                var modLoader = new ModLoader();
                foreach (var path in modManifestPaths)
                    modLoader.LoadModManifest(path, modDataStore);

                // --- selection: mirror the New Game wizard defaults ---
                var enabledSystems = modDataStore.Systems
                    .Where(sys => modDataStore.SystemBodies.Any(b => b.Value.CanStartHere && sys.Value.Bodies.Contains(b.Key)))
                    .Select(sys => sys.Key)
                    .ToList();
                Assert.That(enabledSystems, Is.Not.Empty, "No system has a CanStartHere body.");

                string systemId = enabledSystems.First();
                var systemBlueprint = modDataStore.Systems[systemId];
                string bodyId = modDataStore.SystemBodies
                    .First(b => b.Value.CanStartHere && systemBlueprint.Bodies.Contains(b.Key)).Key;
                string speciesId = modDataStore.Species.First().Key;
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

                StarSystem startingSystem = null!;
                foreach (var id in enabledSystems)
                {
                    var loaded = StarSystemFactory.LoadFromBlueprint(game, modDataStore.Systems[id]);
                    if (id == systemId)
                        startingSystem = loaded;
                }
                Assert.That(startingSystem, Is.Not.Null, $"Loaded starting system '{systemId}' came back null.");

                var startingBodyBlueprint = modDataStore.SystemBodies[bodyId];
                Entity startingBody = null!;
                foreach (var sb in startingSystem.GetAllDataBlobsOfType<SystemBodyInfoDB>())
                {
                    if (sb.OwningEntity?.GetDefaultName()?.Equals(startingBodyBlueprint.Name) == true)
                        startingBody = sb.OwningEntity!;
                }
                Assert.That(startingBody, Is.Not.Null,
                    $"Starting body '{startingBodyBlueprint.Name}' (id '{bodyId}') not found in system '{systemId}'.");

                var faction = FactionFactory.CreateBasicFaction(game, "Test Faction", "TST", 100_000_000);
                faction.FactionOwnerID = faction.Id;
                faction.GetDataBlob<FactionInfoDB>().KnownSystems.Add(startingSystem.ID);

                var species = SpeciesFactory.CreateFromBlueprint(startingSystem, modDataStore.Species[speciesId]);
                species.FactionOwnerID = faction.Id;
                faction.GetDataBlob<FactionInfoDB>().Species.Add(species);

                var colony = ColonyFactory.CreateFromBlueprint(game, faction, species, startingSystem, startingBody,
                    modDataStore.Colonies[colonyId]);

                s.Game = game;
                s.Faction = faction;
                s.Species = species;
                s.StartingSystem = startingSystem;
                s.StartingBody = startingBody;
                s.Colony = colony;
            }
            catch (Exception ex) when (ex is not AssertionException)
            {
                TestContext.WriteLine("=== TestScenario.CreateWithColony FAILED ===");
                TestContext.WriteLine($"Mods: {string.Join(", ", modManifestPaths)}");
                TestContext.WriteLine(ex.ToString());
                throw;
            }
            return s;
        }

        /// <summary>
        /// Advance the simulation clock by <paramref name="total"/> game-time, stepping at
        /// <paramref name="step"/> granularity (default 1 game-day — the frequency the economy processors run
        /// at; the per-system scheduler still catches up any sub-daily processors within each step). Runs
        /// single-threaded so a throw in any processor surfaces on this thread instead of the thread pool.
        /// </summary>
        public void AdvanceTime(TimeSpan total, TimeSpan? step = null)
        {
            var tick = step ?? TimeSpan.FromDays(1);
            Game.Settings.EnforceSingleThread = true;
            Game.TimePulse.Ticklength = tick;

            long steps = (long)Math.Ceiling(total.TotalSeconds / tick.TotalSeconds);
            for (long i = 0; i < steps; i++)
                Game.TimePulse.TimeStep();
        }
    }
}
