using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.GroundCombat;
using Pulsar4X.Modding;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Per-faction GROUND GARRISON (the ground echo of per-faction fleet composition). A faction's home worlds start
    /// with ITS authored combined-arms mix, not the shared 3/2/1 — so the militarist UMF garrisons a heavier Martian
    /// legion than the default light watch. Gauges: (1) `GroundStartGarrison.CompositionFor` reads the faction's mix off
    /// `FactionInfoDB.GarrisonComposition` (default when absent); (2) the DevTest UMF actually raises its authored 4/3/2
    /// on its worlds while the player keeps the default 3/2/1.
    /// </summary>
    [TestFixture]
    public class GroundGarrisonPerFactionTests
    {
        private const string ScenarioDir = "Data/basemod/ScenarioFiles";

        private static Game NewGame()
        {
            var modDataStore = new ModDataStore();
            new ModLoader().LoadModManifest("Data/basemod/modInfo.json", modDataStore);
            return GameFactory.CreateGame(modDataStore, new NewGameSettings
            {
                MaxSystems = 1, CreatePlayerFaction = false, DefaultSolStart = true, MasterSeed = 12345, EleStart = true
            });
        }

        [Test]
        [Description("CompositionFor reads the faction's authored garrison mix; an unset faction falls back to the engine default (3/2/1).")]
        public void CompositionFor_ReadsTheFactionsGarrison_ElseDefault()
        {
            var s = TestScenario.CreateWithColony();
            var fi = s.Faction.GetDataBlob<FactionInfoDB>();

            var def = GroundStartGarrison.CompositionFor(fi);
            Assert.That(def.Sum(x => x.count), Is.EqualTo(6), "no authored garrison → the default 3 Inf + 2 Armor + 1 Arty");

            fi.GarrisonComposition = new Dictionary<string, int> { { "Infantry", 4 }, { "Armor", 3 }, { "Artillery", 2 } };
            var comp = GroundStartGarrison.CompositionFor(fi);
            Assert.That(comp.Sum(x => x.count), Is.EqualTo(9), "the authored 4/3/2 = a heavier 9-unit legion");
            Assert.That(comp.First(x => x.type == GroundUnitType.Infantry).count, Is.EqualTo(4));
            Assert.That(comp.First(x => x.type == GroundUnitType.Armor).count, Is.EqualTo(3));
            Assert.That(comp.First(x => x.type == GroundUnitType.Artillery).count, Is.EqualTo(2));
        }

        [Test]
        [Description("The DevTest UMF raises its authored heavier garrison (4/3/2) on its worlds; the player's Earth starts UNGARRISONED (they design + build their own).")]
        public void DevTestUMF_RaisesItsAuthoredHeavierGarrison_PlayerStartsUngarrisoned()
        {
            var game = NewGame();
            var (player, _) = DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            var infos = game.Factions.Values
                .Where(f => f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>())
                .Select(f => f.GetDataBlob<FactionInfoDB>()).ToList();
            var umf = infos.First(i => i.IsNPC && i.Colonies.Count >= 4);
            int umfId = umf.OwningEntity.Id;

            var umfBody = umf.Colonies
                .Where(c => c != null && c.IsValid)
                .Select(c => c.GetDataBlob<ColonyInfoDB>().PlanetEntity)
                .First(b => b != null && b.IsValid && b.HasDataBlob<GroundForcesDB>());
            var umfUnits = umfBody.GetDataBlob<GroundForcesDB>().Units.Where(u => u.FactionOwnerID == umfId).ToList();

            Assert.That(umfUnits.Count(u => u.UnitType == GroundUnitType.Infantry), Is.EqualTo(4), "UMF authored 4 infantry");
            Assert.That(umfUnits.Count(u => u.UnitType == GroundUnitType.Armor), Is.EqualTo(3), "UMF authored 3 armor");
            Assert.That(umfUnits.Count(u => u.UnitType == GroundUnitType.Artillery), Is.EqualTo(2), "UMF authored 2 artillery");

            // The player (UEF) starts UNGARRISONED — the DevTest no longer raises a code-built home garrison for the
            // player (the developer's "nothing pre-made through non-designer paths"; you design + build your own). So NO
            // player colony body carries a player-owned ground unit. (A body may still carry an ENEMY unit later, hence
            // the FactionOwnerID == player.Id filter.)
            var playerBodies = player.GetDataBlob<FactionInfoDB>().Colonies
                .Where(c => c != null && c.IsValid)
                .Select(c => c.GetDataBlob<ColonyInfoDB>().PlanetEntity)
                .Where(b => b != null && b.IsValid);
            int playerUnitCount = playerBodies.Sum(b =>
                b.TryGetDataBlob<GroundForcesDB>(out var f) ? f.Units.Count(u => u.FactionOwnerID == player.Id) : 0);
            Assert.That(playerUnitCount, Is.EqualTo(0), "the player starts with NO pre-made garrison — designer-built only");
        }
    }
}
