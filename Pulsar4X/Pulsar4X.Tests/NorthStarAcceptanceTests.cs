using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Pulsar4X.Engine;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase 5.3 — THE NORTH-STAR ACCEPTANCE TEST (docs/AI-BRAIN-BUILD-TRACKER.md — 🪐 The Brane; docs/NORTH-STAR-VISION.md).
    /// "Stage one aspect of a franchise; it plays believably." This composes the WHOLE brain stack in one staged galaxy
    /// and asserts the emergent behaviour is both BELIEVABLE and TRACEABLE to the authored inputs:
    ///
    ///   • AUTHORING (Phase 5.1): factions get distinct characters (doctrine) + an opening stance (opening war) from data.
    ///   • THE BRAIN (Phase 2.x): each NPC reads its needs-ladder and settles a character-appropriate objective.
    ///   • EMERGENCE IS CHECKABLE (Phase 5.2): every decision carries a reason tracing it to the input that drove it.
    ///   • THE CRISIS (Phase 4): a faction that researches the transcendent tech ASCENDS and the galaxy unites against
    ///     it through the LIVE decision tick — while the player keeps their agency.
    ///
    /// If this reads believably and every "why" traces to an authored input, the engine is doing what the vision asks:
    /// distinct powers behaving like themselves, legibly, in a galaxy that reacts.
    /// </summary>
    [TestFixture]
    public class NorthStarAcceptanceTests
    {
        [Test]
        [Description("Three authored powers settle DISTINCT, character-appropriate objectives whose reasons trace to their doctrine; the player keeps agency.")]
        public void AuthoredPowers_BehaveLikeThemselves_AndTheReasonsTraceToTheirCharacter()
        {
            var s = TestScenario.CreateWithColony();   // s.Faction is the human player

            // Three rival powers, each authored with a distinct priority (the "character").
            var directorate = Npc(s, "The Directorate", "DIR", new DoctrineVector { Economic = 1f });   // a mercantile power
            var ascendancy  = Npc(s, "The Ascendancy",  "ASC", new DoctrineVector { Tech = 1f });        // a research power
            var frontier    = Npc(s, "The Frontier",    "FRO", new DoctrineVector { Expansion = 1f });   // an expansionist power

            var brain = new NPCDecisionProcessor();
            brain.Init(s.Game);

            // A decision cycle for each — the real brain, not a stub.
            brain.ProcessEntity(directorate, 0);
            brain.ProcessEntity(ascendancy, 0);
            brain.ProcessEntity(frontier, 0);

            // Each behaves like ITSELF — a distinct, character-appropriate objective.
            var dir = directorate.GetDataBlob<StrategicObjectiveDB>();
            var asc = ascendancy.GetDataBlob<StrategicObjectiveDB>();
            var fro = frontier.GetDataBlob<StrategicObjectiveDB>();
            Assert.That(dir.Objective, Is.EqualTo(StrategicObjective.GrowEconomy), "the mercantile power grows its economy");
            Assert.That(asc.Objective, Is.EqualTo(StrategicObjective.AdvanceTech), "the research power pushes tech");
            Assert.That(fro.Objective, Is.EqualTo(StrategicObjective.Expand),      "the expansionist power expands");

            // And you can READ WHY — every decision traces to the authored input (Phase 5.2 decision-log).
            Assert.That(dir.DecisionReason, Does.Contain("Economic"),  "the Directorate's reason names Economic doctrine");
            Assert.That(asc.DecisionReason, Does.Contain("Tech"),      "the Ascendancy's reason names Tech doctrine");
            Assert.That(fro.DecisionReason, Does.Contain("Expansion"), "the Frontier's reason names Expansion doctrine");

            // The player is not steered by the brain — they keep their agency.
            brain.ProcessEntity(s.Faction, 0);
            Assert.That(s.Faction.HasDataBlob<StrategicObjectiveDB>(), Is.False, "the player settles no objective — they choose");
        }

        [Test]
        [Description("An authored opening war persists; and when a rival ASCENDS the galaxy unites against it through the live tick, the player left to choose.")]
        public void AnAuthoredWarPersists_AndAnAscensionUnitesTheGalaxy_WhileThePlayerChooses()
        {
            var s = TestScenario.CreateWithColony();
            var alliance  = Npc(s, "The Alliance",  "ALL", new DoctrineVector { Economic = 1f });
            var hegemony   = Npc(s, "The Hegemony",  "HEG", new DoctrineVector { Military = 1f });
            var transcendent = Npc(s, "The Transcendent", "TRA", new DoctrineVector { Tech = 1f });
            var when = s.Game.TimePulse.GameGlobalDateTime;

            // AUTHORING (5.1b): the Alliance and the Hegemony start the game already at war.
            FactionFactory.ApplyOpeningRelations(s.Game, alliance,
                JArray.Parse(@"[ { ""target"": ""HEG"", ""atWar"": true } ]"), when);
            Assert.That(alliance.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(hegemony.Id).AtWar, Is.True,
                "the authored opening war stands");
            Assert.That(hegemony.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(alliance.Id).AtWar, Is.True,
                "and it is symmetric");

            // THE CRISIS (Phase 4): the Transcendent researches the transcendent tech and ASCENDS.
            var data = transcendent.GetDataBlob<FactionInfoDB>().Data;
            data.Unlock("tech-ascension");
            data.IncrementTechLevel("tech-ascension");
            Assert.That(GalaxyCrisis.Ascendant(s.Game), Is.EqualTo(transcendent), "the ascended power is the galaxy crisis");

            // The galaxy unites — through the LIVE decision tick (EnableGalaxyCrisis is on since the Phase-4 finish),
            // running any NPC's cycle forms the coalition (it is galaxy-global + idempotent).
            var brain = new NPCDecisionProcessor();
            brain.Init(s.Game);
            brain.ProcessEntity(alliance, 0);

            Assert.That(alliance.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(transcendent.Id).AtWar, Is.True,
                "the Alliance joins the coalition against the ascendant");
            Assert.That(hegemony.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(transcendent.Id).AtWar, Is.True,
                "so does the Hegemony — old enemies unite against the greater threat");
            // The player keeps agency — not dragged into the galaxy's war automatically.
            Assert.That(s.Faction.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(transcendent.Id).AtWar, Is.False,
                "the player chooses whether to join — they are not auto-committed");
        }

        /// <summary>Create a named NPC faction with an authored doctrine, a war chest, and IsNPC set.</summary>
        private static Entity Npc(TestScenario s, string name, string abbr, DoctrineVector doctrine)
        {
            var f = FactionFactory.CreateBasicFaction(s.Game, name, abbr, 100000);
            var info = f.GetDataBlob<FactionInfoDB>();
            info.IsNPC = true;
            info.Doctrine = doctrine;
            return f;
        }
    }
}
