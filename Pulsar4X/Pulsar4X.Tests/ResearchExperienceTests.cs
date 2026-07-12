using System.Linq;
using NUnit.Framework;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.People;
using Pulsar4X.Technology;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Exploration X.0-3 gauge — a scientist IMPROVES as they work ("ceiling now + bonus on top"). Proves
    /// (a) working a lab grows a scientist's Experience and earns a SEPARATE, growing research bonus in the category
    /// they research, (b) that growth stops at the school-set ExperienceCap — a career can't exceed the school's
    /// potential, (c) the graduation "Research Aptitude" competence (the school-set floor) is NEVER disturbed and the
    /// experience bonus is replaced not duplicated, and (d) the grown competence actually folds through the real
    /// ResearchProcessor reader into more points/day. The engine wire (ResearchProcessor.DoResearch) only calls this
    /// for an assigned scientist ENTITY (ScientistId >= 0); the default start uses the legacy scientist path
    /// (ScientistId == -1), so nothing grows at game start — the smoke/loop tests are the byte-identity tripwire.
    /// </summary>
    [TestFixture]
    public class ResearchExperienceTests
    {
        const string Cat = "test-cat";

        [Test]
        [Description("Working a lab grows Experience and a separate experience bonus, capped at the school ceiling, leaving the graduation bonus intact.")]
        public void GrowScientistExperience_RaisesBonusUpToTheSchoolCeiling_LeavingGraduationBonusIntact()
        {
            var s = TestScenario.CreateWithColony();
            var mgr = s.Game.GlobalManager;

            var scientist = Entity.Create();
            mgr.AddEntity(scientist);
            // A school-set ceiling of 20 (a modest academy) with no experience yet.
            var cmdr = new CommanderDB { Name = "Test Sci", Experience = 0, ExperienceCap = 20 };
            scientist.SetDataBlob(cmdr);
            var bonuses = new BonusesDB();
            // The graduation competence (the school-set FLOOR), rolled from the cap in the specialty.
            bonuses.Bonuses.AddRange(CommanderBonuses.RollResearchCompetence(experienceCap: 20, techCategoryId: Cat));
            scientist.SetDataBlob(bonuses);

            double GradBonus() => bonuses.Bonuses
                .Where(b => b.Name == "Research Aptitude").Sum(b => b.Value);
            double ExpBonus() => bonuses.Bonuses
                .Where(b => b.Name == CommanderBonuses.ResearchExperienceBonusName && b.FilterId == Cat).Sum(b => b.Value);
            int ExpBonusCount() => bonuses.Bonuses
                .Count(b => b.Name == CommanderBonuses.ResearchExperienceBonusName && b.FilterId == Cat);

            double gradFloor = GradBonus();
            Assert.That(gradFloor, Is.GreaterThan(0), "the scientist starts with a school-set graduation competence");
            Assert.That(ExpBonus(), Is.EqualTo(0), "and no experience bonus before any work");

            // Work: experience rises, and a growing experience bonus appears on top.
            Assert.That(ResearchProcessor.GrowScientistExperience(scientist, Cat, 5), Is.True);
            Assert.That(cmdr.Experience, Is.EqualTo(5));
            double afterFirst = ExpBonus();
            Assert.That(afterFirst, Is.GreaterThan(0), "experience earns a bonus on top of the school floor");

            Assert.That(ResearchProcessor.GrowScientistExperience(scientist, Cat, 5), Is.True);
            Assert.That(cmdr.Experience, Is.EqualTo(10));
            Assert.That(ExpBonus(), Is.GreaterThan(afterFirst), "more experience → a bigger bonus");
            Assert.That(ExpBonusCount(), Is.EqualTo(1), "the experience bonus is replaced each step, never duplicated");

            // The school-set graduation competence is never disturbed by growth.
            Assert.That(GradBonus(), Is.EqualTo(gradFloor), "the graduation 'Research Aptitude' bonus is left alone");

            // Cannot grow past the school-set ceiling — the school bounds ultimate potential.
            ResearchProcessor.GrowScientistExperience(scientist, Cat, 5);  // 15
            Assert.That(ResearchProcessor.GrowScientistExperience(scientist, Cat, 5), Is.True); // 15 -> 20 (capped)
            Assert.That(cmdr.Experience, Is.EqualTo(20));
            Assert.That(ResearchProcessor.GrowScientistExperience(scientist, Cat, 5), Is.False,
                "a scientist at their school-set ceiling cannot grow further");
            Assert.That(cmdr.Experience, Is.EqualTo(20), "experience is clamped at ExperienceCap");
        }

        [Test]
        [Description("Grown experience folds through the real ResearchProcessor reader into more points/day on the category.")]
        public void GrownExperience_FoldsThroughTheRealReader_IntoMorePointsPerDay()
        {
            var s = TestScenario.CreateWithColony();
            var mgr = s.Game.GlobalManager;

            var scientist = Entity.Create();
            mgr.AddEntity(scientist);
            var cmdr = new CommanderDB { Name = "Test Sci", Experience = 0, ExperienceCap = 200 };
            scientist.SetDataBlob(cmdr);
            scientist.SetDataBlob(new BonusesDB());   // no graduation bonus — isolate the experience contribution

            const int baseline = 100;
            var researcher = new ResearcherDB(null) { PointsPerDay = new ModifiableValue<int>(baseline) };

            // Before any work, an empty scientist changes nothing (the default path stays byte-identical).
            ResearchProcessor.RefreshPointModifiers(researcher, new Tech { Category = Cat }, scientist);
            Assert.That(researcher.PointsPerDay.GetValue(), Is.EqualTo(baseline),
                "a scientist with no experience folds nothing");

            // Earn a lot of experience, then re-fold: points/day rises on the worked category.
            for (int i = 0; i < 20; i++)
                ResearchProcessor.GrowScientistExperience(scientist, Cat, 10);   // climb toward the cap
            ResearchProcessor.RefreshPointModifiers(researcher, new Tech { Category = Cat }, scientist);
            Assert.That(researcher.PointsPerDay.GetValue(), Is.GreaterThan(baseline),
                "an experienced scientist researches their worked category faster");

            // But not on a mismatched category — the FilterId gate holds.
            ResearchProcessor.RefreshPointModifiers(researcher, new Tech { Category = "other-cat" }, scientist);
            Assert.That(researcher.PointsPerDay.GetValue(), Is.EqualTo(baseline),
                "a mismatched category folds nothing — experience is category-specific");
        }
    }
}
