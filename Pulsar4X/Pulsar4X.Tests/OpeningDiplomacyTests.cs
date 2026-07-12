using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Pulsar4X.Engine;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase 5.1b gauge (docs/AI-BRAIN-BUILD-TRACKER.md — 🪐 The Brane, authoring). Proves a scenario can AUTHOR a
    /// faction's OPENING DIPLOMACY from data: <see cref="FactionFactory.ApplyOpeningRelations"/> reads an
    /// <c>"openingRelations"</c> JSON array (target by name/abbreviation → score + opening war) and applies it through
    /// the real DiplomacyDB / Diplomacy.DeclareWar machinery. Targets resolve by name or abbreviation; an unknown
    /// target is skipped (never thrown on). This is the second half of the north-star authoring rig: hand a faction a
    /// starting stance — including a war already underway — in JSON, not in C#.
    /// </summary>
    [TestFixture]
    public class OpeningDiplomacyTests
    {
        [Test]
        [Description("openingRelations sets score + latches war, resolving the target by abbreviation; symmetric on both ledgers.")]
        public void ApplyOpeningRelations_SetsScoreAndWar_ByAbbreviation()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Red Star Collective", "RED", 0);
            var blues = FactionFactory.CreateBasicFaction(s.Game, "Blue Republic", "BLU", 0);
            var when = s.Game.TimePulse.GameGlobalDateTime;

            // Author RED's opening stance: at war with BLU (named by abbreviation), hostile score.
            var node = JArray.Parse(@"[ { ""target"": ""BLU"", ""atWar"": true, ""score"": -80 } ]");
            FactionFactory.ApplyOpeningRelations(s.Game, reds, node, when);

            Assert.That(reds.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(blues.Id).AtWar, Is.True,
                "RED opened the game at war with BLU");
            Assert.That(blues.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(reds.Id).AtWar, Is.True,
                "war is symmetric — BLU is at war with RED too");
            Assert.That(reds.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(blues.Id).RelationScore, Is.LessThan(0),
                "the authored hostile score is applied");
            Assert.That(blues.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(reds.Id).RelationScore, Is.LessThan(0),
                "the score is set on both ledgers");
        }

        [Test]
        [Description("A target resolvable by full name works; an unknown target is skipped without throwing.")]
        public void ApplyOpeningRelations_ByName_AndSkipsUnknown()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Red Star Collective", "RED", 0);
            var blues = FactionFactory.CreateBasicFaction(s.Game, "Blue Republic", "BLU", 0);
            var when = s.Game.TimePulse.GameGlobalDateTime;

            // Resolve by full NAME, plus an unknown target that must be silently skipped.
            var node = JArray.Parse(@"[
                { ""target"": ""Blue Republic"", ""score"": 60 },
                { ""target"": ""No Such Faction"", ""atWar"": true }
            ]");
            Assert.DoesNotThrow(() => FactionFactory.ApplyOpeningRelations(s.Game, reds, node, when),
                "an unknown target is skipped, not thrown on");

            Assert.That(reds.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(blues.Id).RelationScore, Is.GreaterThan(0),
                "a friendly opening score resolved by full name");
            Assert.That(reds.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(blues.Id).AtWar, Is.False,
                "no war was declared (the war entry named an unknown faction and was skipped)");
        }
    }
}
