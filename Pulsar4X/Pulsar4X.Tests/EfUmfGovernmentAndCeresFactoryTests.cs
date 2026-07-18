using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Engine;
using Pulsar4X.Extensions;   // GetDefaultName()
using Pulsar4X.Factions;
using Pulsar4X.Industry;
using Pulsar4X.Modding;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Operation Earthfall P3.1 — the data-driven gauge for the UMF's authored GOVERNMENT + the Ceres factory.
    ///
    /// TWO fixes ride this fixture, both authored in umf.json and read through the real load path
    /// (<see cref="DevTestStartFactory.CreateDevTest"/> → <see cref="FactionFactory.LoadFromJson"/>):
    ///
    ///  (a) A3 fix seam 1 — the UMF now carries a "government" node with **Militarism High** (plus a fitting
    ///      authoritarian **Authority High**). Before, the UMF had NO government node, so it ran the neutral all-Mid
    ///      default whose <see cref="GovernmentDB.WarMoraleFactor"/> is −0.25 → a flat −5 legitimacy war term all game.
    ///      During a hostile-world morale trough that −5 dropped legitimacy below the collapse line and triggered a
    ///      phantom one-month rebellion that locked the AI into Defend for 180 days (findings/A3-objective-flip.md).
    ///      Militarism High makes <see cref="GovernmentDB.WarMoraleFactor"/> +0.5 → a +10 war term, so a
    ///      militarist-at-war takes PRIDE in the fight (the war term is POSITIVE, not negative) and the trough no
    ///      longer collapses the province.
    ///
    ///  (b) A6 data gap — Ceres had "default-design-factory" amount 0 (it could mine+refine but NOT manufacture,
    ///      findings/A6-faction-development.md, umf.json:511-513). It now hosts one factory → a manufacturing-capable
    ///      industry line (component/installation construction), so the outer UMF world isn't a dead-end.
    ///
    /// Load-only (no clock advance) → lands in the CI "rest" shard.
    /// </summary>
    [TestFixture]
    public class EfUmfGovernmentAndCeresFactoryTests
    {
        private const string ScenarioDir = "Data/basemod/ScenarioFiles";

        private Game _game;
        private FactionInfoDB _umf;
        private Entity _umfEntity;

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

        [OneTimeSetUp]
        public void LoadSandbox()
        {
            _game = NewGame();

            // Load the whole conquest sandbox exactly as the client / the sibling DevTest tests do — the second pass
            // inside CreateDevTest applies umf.json's openingRelations (so the UMF opens the game AT WAR with the player).
            DevTestStartFactory.CreateDevTest(
                _game, ScenarioDir, new List<string> { "uef-devtest.json", "umf.json", "kithrin.json" });

            // The UMF is the NPC with the inner-system colony cluster (matches the sibling DevTestScenarioTests shape check).
            _umf = _game.Factions.Values
                .Where(f => f != null && f.IsValid && f.HasDataBlob<FactionInfoDB>())
                .Select(f => f.GetDataBlob<FactionInfoDB>())
                .FirstOrDefault(i => i.IsNPC && i.Colonies.Count >= 4);

            Assert.That(_umf, Is.Not.Null,
                "the United Martian Federation (an NPC with its four inner-system colonies) did not load.");
            _umfEntity = _umf.OwningEntity;
            Assert.That(_umfEntity, Is.Not.Null, "UMF FactionInfoDB has no owning entity.");
        }

        [Test]
        [Description("The UMF loads its authored government: Militarism High (the load-bearing dial that flips the "
                     + "legitimacy war term positive) and a fitting authoritarian Authority High. The 'government' node "
                     + "parser landed the dials off umf.json — a faction with no node would read the neutral all-Mid default.")]
        public void Umf_LoadsWith_MilitarismHigh_AndAuthorityHigh()
        {
            var gov = _umfEntity.GetDataBlob<GovernmentDB>();
            Assert.That(gov, Is.Not.Null, "the UMF carries no GovernmentDB (CreateFaction attaches one to every faction).");

            Assert.That(gov.Militarism, Is.EqualTo(GovNotch.High),
                "the UMF should load Militarism High from its authored 'government' node — the parser didn't apply it.");
            Assert.That(gov.Authority, Is.EqualTo(GovNotch.High),
                "the UMF should load the fitting authoritarian Authority High from its 'government' node.");

            // The load-bearing consequence: a militarist regime takes pride in war (a POSITIVE morale factor), where the
            // all-Mid default the UMF used to run yields a negative one (the A3 collapse driver).
            Assert.That(gov.WarMoraleFactor(), Is.GreaterThan(0.0),
                "a Militarism-High regime must yield a positive war-morale factor.");
            Assert.That(new GovernmentDB().WarMoraleFactor(), Is.LessThan(0.0),
                "sanity: the neutral all-Mid default (what the UMF ran before this fix) yields a NEGATIVE war factor — "
                + "the sign the government node flips.");
        }

        [Test]
        [Description("A militarist-at-war UMF province computes a POSITIVE legitimacy war term. Drives the real "
                     + "LegitimacyProcessor.RecalcLegitimacy on a UMF colony: the UMF opened the game at war (openingRelations), "
                     + "so its Militarism-High regime feeds a +10 war term (WarMoraleFactor 0.5 x MaxWarSwing 20) instead of "
                     + "the all-Mid default's -5 — the A3 fix that stops the phantom rebellion.")]
        public void Umf_MilitaristAtWar_LegitimacyWarTerm_IsPositive()
        {
            // Precondition — the UMF really is at war (openingRelations applied), else the war term would be a peacetime 0.
            var dip = _umfEntity.GetDataBlob<DiplomacyDB>();
            Assert.That(dip.IsAtWarWithAnyone(), Is.True,
                "the UMF should have opened the game at war (openingRelations) — otherwise there is no war term to test.");

            var gov = _umfEntity.GetDataBlob<GovernmentDB>();

            // Pick any UMF province and recompute its legitimacy the way the monthly processor does.
            var province = _umf.Colonies.First(c => c != null && c.IsValid && c.HasDataBlob<LegitimacyDB>());
            LegitimacyProcessor.RecalcLegitimacy(province);

            var legit = province.GetDataBlob<LegitimacyDB>();
            Assert.That(legit.Factors.ContainsKey("war"), Is.True, "the legitimacy breakdown has no 'war' factor.");

            double warFactor = legit.Factors["war"];
            Assert.That(warFactor, Is.GreaterThan(0.0),
                "a militarist-at-war UMF province must read a POSITIVE legitimacy war term (pride in the fight), "
                + "not the negative term that collapsed the province in A3.");

            // Exactly the militarism-gated swing: WarMoraleFactor (0.5 for High) x MaxWarSwing (20) = +10.
            Assert.That(warFactor,
                Is.EqualTo(gov.WarMoraleFactor() * LegitimacyDB.MaxWarSwing).Within(0.0001),
                "the war term should equal the government's WarMoraleFactor scaled by MaxWarSwing.");
        }

        [Test]
        [Description("Ceres now hosts a manufacturing-capable production line. With the authored factory (amount 0 -> 1) "
                     + "the outer UMF world gains a component/installation-construction industry line — where before it "
                     + "could only mine + refine (findings/A6). A BaseModIntegrity-style scenario check: the line falls out "
                     + "of the real AddComponent -> IndustryAtb install path at load.")]
        public void Ceres_HostsAManufacturingCapableLine()
        {
            var ceres = _umf.Colonies.FirstOrDefault(c =>
                c != null && c.IsValid
                && c.HasDataBlob<ColonyInfoDB>()
                && c.GetDataBlob<ColonyInfoDB>().PlanetEntity.IsValid
                && c.GetDataBlob<ColonyInfoDB>().PlanetEntity.GetDefaultName().Contains("Ceres"));

            Assert.That(ceres, Is.Not.Null, "the UMF's Ceres colony did not load.");

            Assert.That(ceres.HasDataBlob<IndustryAbilityDB>(), Is.True,
                "Ceres carries no IndustryAbilityDB — the factory (and its industry line) did not install.");

            var industry = ceres.GetDataBlob<IndustryAbilityDB>();

            // A "manufacturing-capable" line makes COMPONENTS or INSTALLATIONS (the factory's industry types) — not just
            // refining (the refinery's only type). So this distinguishes the added factory from Ceres's pre-existing refinery.
            bool canManufacture = industry.ProductionLines.Values.Any(line =>
                line.IndustryTypeRates.TryGetValue("component-construction", out var comp) && comp > 0
                || line.IndustryTypeRates.TryGetValue("installation-construction", out var inst) && inst > 0);

            Assert.That(canManufacture, Is.True,
                "Ceres has no production line offering component/installation construction — the authored factory "
                + "(default-design-factory amount 1) didn't grant a manufacturing line.");
        }
    }
}
