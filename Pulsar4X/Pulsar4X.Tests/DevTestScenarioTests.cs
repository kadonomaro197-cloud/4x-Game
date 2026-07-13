using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
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
    }
}
