using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Modding;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The DevTest game-start gauge (the "DevTest" button that replaces Quickstart). Proves the data-driven start
    /// stands up end-to-end through the WORKING pieces this branch built:
    ///   Sol via StarSystemFactory.LoadFromBlueprint  →  DevTestStartFactory.CreateDevTest  →
    ///   FactionFactory.LoadFromJson (design/species BY ID + the "startingItems" unlock + the inline colony parser).
    /// This first fixture loads the PLAYER faction alone, so a gotcha-#10 failure (a design/species/body id that
    /// doesn't resolve) is isolated to one file. UMF/Kithrin (war + strain + station) land in follow-up assertions.
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
    }
}
