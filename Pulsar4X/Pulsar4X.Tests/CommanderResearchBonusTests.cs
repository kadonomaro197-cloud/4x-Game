using NUnit.Framework;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.People;
using Pulsar4X.Technology;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// F-A2 gauge (docs/AI-BRAIN-BUILD-TRACKER.md, Movement I): scientist RESEARCH competence is now EARNABLE — the
    /// research-side twin of the combat-competence generator. Proves (a) the generator scales with the cap and is
    /// shaped for the live reader (FilterId = tech category, Type = Percentage), (b) a scientist carrying rolled
    /// competence actually researches FASTER through the real ResearchProcessor.RefreshPointModifiers on the matching
    /// category — and NOT on a mismatched one (the FilterId gate), and (c) an empty-BonusesDB scientist is unchanged,
    /// so the default path stays byte-identical (this slice only makes competence available, it doesn't grant it).
    /// </summary>
    [TestFixture]
    public class CommanderResearchBonusTests
    {
        [Test]
        [Description("The generator scales with ExperienceCap and shapes the bonus exactly how the reader matches it.")]
        public void RollResearchCompetence_ScalesWithCap_AndIsShapedForTheReader()
        {
            var full = CommanderBonuses.RollResearchCompetence(experienceCap: 200, techCategoryId: "propulsion");
            Assert.That(full, Has.Count.EqualTo(1));
            var b = full[0];
            Assert.That(b.FilterId, Is.EqualTo("propulsion"),
                "FilterId MUST be the tech category — that is what RefreshPointModifiers matches a scientist bonus on");
            Assert.That(b.Type, Is.EqualTo(BonusType.Perentage), "folded as a percentage increase to points/day");
            Assert.That(b.Category, Is.EqualTo(BonusCategory.ResearchPoints));
            Assert.That(b.Value, Is.EqualTo(CommanderBonuses.MaxResearchCompetenceBonus).Within(1e-9),
                "cap 200 → the full competence bonus");

            var half = CommanderBonuses.RollResearchCompetence(100, "propulsion");
            Assert.That(half[0].Value, Is.EqualTo(CommanderBonuses.MaxResearchCompetenceBonus / 2).Within(1e-9),
                "cap 100 → half");

            Assert.That(CommanderBonuses.RollResearchCompetence(0, "propulsion"), Is.Empty, "no cap → no competence");
            Assert.That(CommanderBonuses.RollResearchCompetence(200, ""), Is.Empty, "no category to specialise in → nothing");
        }

        [Test]
        [Description("A scientist carrying rolled competence researches faster on the matching category, not a mismatched one; an empty scientist is unchanged (default path byte-identical).")]
        public void RolledCompetence_FoldsThroughTheRealReader_OnlyOnTheMatchingCategory()
        {
            var s = TestScenario.CreateWithColony();
            var mgr = s.Game.GlobalManager;

            // A scientist ENTITY carrying the rolled research competence in "test-cat".
            var rolled = CommanderBonuses.RollResearchCompetence(experienceCap: 200, techCategoryId: "test-cat");
            var scientist = Entity.Create();
            mgr.AddEntity(scientist);
            var bonuses = new BonusesDB();
            bonuses.Bonuses.AddRange(rolled);
            scientist.SetDataBlob(bonuses);

            // A lab with a real, non-zero base output (a percentage bonus on a zero base would move nothing).
            const int baseline = 100;
            var researcher = new ResearcherDB(null) { PointsPerDay = new ModifiableValue<int>(baseline) };

            // Matching tech category → the competence folds → more points/day.
            ResearchProcessor.RefreshPointModifiers(researcher, new Tech { Category = "test-cat" }, scientist);
            Assert.That(researcher.PointsPerDay.GetValue(), Is.GreaterThan(baseline),
                "rolled research competence raises the scientist's points/day on their specialty");

            // Mismatched tech category → the FilterId gate blocks it → back to baseline.
            ResearchProcessor.RefreshPointModifiers(researcher, new Tech { Category = "other-cat" }, scientist);
            Assert.That(researcher.PointsPerDay.GetValue(), Is.EqualTo(baseline),
                "a mismatched tech category folds nothing — the FilterId gate");

            // An empty-BonusesDB scientist (today's default) → unchanged → the default path is byte-identical.
            var emptyScientist = Entity.Create();
            mgr.AddEntity(emptyScientist);
            emptyScientist.SetDataBlob(new BonusesDB());
            ResearchProcessor.RefreshPointModifiers(researcher, new Tech { Category = "test-cat" }, emptyScientist);
            Assert.That(researcher.PointsPerDay.GetValue(), Is.EqualTo(baseline),
                "an empty-competence scientist changes nothing — this slice only makes competence available");
        }
    }
}
