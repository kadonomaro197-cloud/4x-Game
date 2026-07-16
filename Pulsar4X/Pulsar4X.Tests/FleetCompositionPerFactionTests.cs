using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Modding;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Per-faction FLEET COMPOSITION (slice B) — a faction grows its fleets to ITS OWN ladder (a Martian battle-line vs
    /// a Kithrin raid-swarm), not the shared 3/8/18. Gauges: (1) `FleetAssembly.TemplateFor` reads the faction's authored
    /// numbers off `FactionInfoDB` (falls back to the default when absent → the engine tests stay byte-identical);
    /// (2) the DevTest UMF/Kithrin load their authored ladders from the `fleetComposition` JSON node; (3)
    /// `AssembleBuiltWarships` stamps the faction's ladder onto the forming fleet's `FleetCompositionDB`.
    /// </summary>
    [TestFixture]
    public class FleetCompositionPerFactionTests
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

        private static ShipDesign FirstWarshipDesign(Entity faction)
        {
            foreach (var d in faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values)
                if (d.TryGetComponentsByAttribute<Pulsar4X.Weapons.GenericBeamWeaponAtb>(out _)
                    || d.TryGetComponentsByAttribute<Pulsar4X.Weapons.RailgunWeaponAtb>(out _))
                    return d;
            return null;
        }

        [Test]
        [Description("TemplateFor reads the faction's own authored ladder off FactionInfoDB; a default faction / null → the shared 3/8/18.")]
        public void TemplateFor_ReadsTheFactionsOwnLadder_ElseDefault()
        {
            var s = TestScenario.CreateWithColony();

            var def = FleetAssembly.TemplateFor(s.Faction);   // FactionInfoDB defaults
            Assert.That(def.MinToDeploy, Is.EqualTo(3));
            Assert.That(def.IdealSize, Is.EqualTo(8));
            Assert.That(def.PerfectSize, Is.EqualTo(18));

            var info = s.Faction.GetDataBlob<FactionInfoDB>();
            info.FleetTemplateName = "Test Line";
            info.FleetMinToDeploy = 5; info.FleetIdealSize = 12; info.FleetPerfectSize = 30;

            var t = FleetAssembly.TemplateFor(s.Faction);
            Assert.That(t.Name, Is.EqualTo("Test Line"));
            Assert.That(t.MinToDeploy, Is.EqualTo(5));
            Assert.That(t.IdealSize, Is.EqualTo(12));
            Assert.That(t.PerfectSize, Is.EqualTo(30));

            var d2 = FleetAssembly.TemplateFor(null);
            Assert.That(d2.MinToDeploy, Is.EqualTo(FleetCompositionTemplate.DefaultStrikeFleet.MinToDeploy),
                "a null faction falls back to the shared default ladder");
        }

        [Test]
        [Description("The DevTest UMF loads a bigger Battle-Line ladder and the Kithrin a smaller Raid-Swarm; the player keeps the default.")]
        public void DevTestFactions_LoadTheirAuthoredCompositions()
        {
            var game = NewGame();
            var (player, _) = DevTestStartFactory.CreateDevTest(
                game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            var infos = game.Factions.Values
                .Where(f => f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>())
                .Select(f => f.GetDataBlob<FactionInfoDB>()).ToList();

            var umf = infos.First(i => i.IsNPC && i.Colonies.Count >= 4);
            Assert.That(umf.FleetTemplateName, Is.EqualTo("Martian Battle Line"));
            Assert.That(umf.FleetMinToDeploy, Is.EqualTo(4));
            Assert.That(umf.FleetIdealSize, Is.EqualTo(10));
            Assert.That(umf.FleetPerfectSize, Is.EqualTo(20));

            var kith = infos.First(i => i.IsNPC && i.Stations.Count > 0);
            Assert.That(kith.FleetTemplateName, Is.EqualTo("Kithrin Raid Swarm"));
            Assert.That(kith.FleetMinToDeploy, Is.EqualTo(3));
            Assert.That(kith.FleetIdealSize, Is.EqualTo(6));
            Assert.That(kith.FleetPerfectSize, Is.EqualTo(12));

            // The player authored no fleetComposition node → the default ladder stands (byte-identical).
            Assert.That(player.GetDataBlob<FactionInfoDB>().FleetIdealSize, Is.EqualTo(8),
                "a faction with no fleetComposition node keeps the default 3/8/18");
        }

        [Test]
        [Description("AssembleBuiltWarships stamps the faction's OWN ladder onto the forming fleet, not the default.")]
        public void AssembleBuiltWarships_StampsTheFactionsLadderOnTheFormingFleet()
        {
            var s = TestScenario.CreateWithColony();
            var info = s.Faction.GetDataBlob<FactionInfoDB>();
            info.FleetTemplateName = "Battle Line";
            info.FleetMinToDeploy = 4; info.FleetIdealSize = 10; info.FleetPerfectSize = 20;

            var design = FirstWarshipDesign(s.Faction);
            Assert.That(design, Is.Not.Null);
            for (int i = 0; i < 4; i++)
                s.Faction.GetDataBlob<FleetDB>().AddChild(ShipFactory.CreateShip(design, s.Faction, s.StartingBody));

            FleetAssembly.AssembleBuiltWarships(s.Faction);

            var fleet = s.StartingSystem.GetAllEntitiesWithDataBlob<FleetCompositionDB>()
                .First(f => f.FactionOwnerID == s.Faction.Id);
            var comp = fleet.GetDataBlob<FleetCompositionDB>();
            Assert.That(comp.MinToDeploy, Is.EqualTo(4), "the forming fleet uses the FACTION's ladder, not the default 3");
            Assert.That(comp.IdealSize, Is.EqualTo(10));
            Assert.That(comp.PerfectSize, Is.EqualTo(20));
            Assert.That(comp.TemplateName, Is.EqualTo("Battle Line"));
        }
    }
}
