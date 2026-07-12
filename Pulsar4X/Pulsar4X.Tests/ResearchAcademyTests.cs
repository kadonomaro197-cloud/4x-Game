using System.Linq;
using NUnit.Framework;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.People;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Exploration X.0 gauge — the RESEARCH ACADEMY graduates competent scientists, the scientist twin of the naval
    /// academy. Proves (a) a graduate is a real scientist entity with a <c>BonusesDB</c> carrying rolled RESEARCH
    /// competence in the school's specialty tech category (the same shape <c>ResearchProcessor.RefreshPointModifiers</c>
    /// reads), (b) the competence lands on the specialty category and nowhere else (the FilterId gate), and (c) a longer
    /// program (a better school) produces higher-ceiling graduates on average — "build a better research academy" is a
    /// real decision. The engine slice is byte-identical until a component schedules the processor (X.0-2); this gauge
    /// exercises the static graduation directly.
    /// </summary>
    [TestFixture]
    public class ResearchAcademyTests
    {
        const string Specialty = "tech-category-power-propulsion";

        [Test]
        [Description("A research academy graduate is a scientist entity carrying rolled research competence in the school's specialty.")]
        public void GraduateScientist_ProducesAScientistWithResearchCompetenceInTheSpecialty()
        {
            var s = TestScenario.CreateWithColony();

            // Graduate a class from a good (48-month) school. The competence ceiling is a random bell-curve roll, so
            // graduate several and assert the school reliably produces at least one competent scientist.
            bool anyCompetent = false;
            for (int i = 0; i < 8 && !anyCompetent; i++)
            {
                var grad = ResearchAcademyProcessor.GraduateScientist(s.Colony, 48, Specialty);

                Assert.That(grad.TryGetDataBlob<BonusesDB>(out var bonuses), Is.True,
                    "a graduate is created through CommanderFactory.Create, which attaches a BonusesDB");

                var research = bonuses.Bonuses.Where(b => b.Category == BonusCategory.ResearchPoints).ToList();
                foreach (var b in research)
                {
                    Assert.That(b.FilterId, Is.EqualTo(Specialty),
                        "research competence is stamped ONLY in the school's specialty — the FilterId the reader gates on");
                    Assert.That(b.Type, Is.EqualTo(BonusType.Perentage));
                    Assert.That(b.Value, Is.GreaterThan(0));
                    anyCompetent = true;
                }
            }

            Assert.That(anyCompetent, Is.True,
                "across a class, a 48-month research academy graduates at least one scientist with research competence");
        }

        [Test]
        [Description("A longer training program (a better school) yields higher-ceiling graduates on average — the school-quality decision.")]
        public void GraduateScientist_LongerProgram_YieldsStrongerGraduatesOnAverage()
        {
            var s = TestScenario.CreateWithColony();

            // The rolled research bonus scales with ExperienceCap, whose mean shifts with training length
            // (mean = 100 + (months - 24)). Averaged over a class, a 48-month school beats a 1-month school.
            double AverageBonus(int months)
            {
                double total = 0;
                const int n = 40;
                for (int i = 0; i < n; i++)
                {
                    var grad = ResearchAcademyProcessor.GraduateScientist(s.Colony, months, Specialty);
                    grad.TryGetDataBlob<BonusesDB>(out var bonuses);
                    total += bonuses.Bonuses
                        .Where(b => b.Category == BonusCategory.ResearchPoints && b.FilterId == Specialty)
                        .Sum(b => b.Value);
                }
                return total / n;
            }

            double shortSchool = AverageBonus(1);
            double longSchool = AverageBonus(48);

            Assert.That(longSchool, Is.GreaterThan(shortSchool),
                "a longer program produces stronger scientists on average — a better academy is worth building");
        }
    }
}
