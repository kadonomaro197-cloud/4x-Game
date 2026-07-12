using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.People;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Exploration X.0-2 — the base-mod Research Academy, through the REAL data path. The starting colony now lists a
    /// buildable <c>default-design-research-academy</c>; this proves it loads onto the faction and binds its
    /// <see cref="ResearchAcademyAtb"/> from JSON via the ComponentDesigner (template → NCalc → atb, gotcha #10 — the
    /// six-point registration). This is the sensor that a mis-ordered/mis-counted <c>AtbConstrArgs</c>, a wrong
    /// AttributeType namespace, or a bad ctor fails HERE in CI, not in a player's New Game (the client is CI-blind).
    /// The <see cref="Modding.BaseModIntegrityTests"/> material/unlock check rides alongside; this adds the
    /// does-it-actually-instantiate half that check can't see.
    /// </summary>
    [TestFixture]
    public class ResearchAcademyBaseModTests
    {
        [Test]
        [Description("X.0-2: the research academy loads onto the start faction and binds its ResearchAcademyAtb from JSON with the school-quality dials (class size / training length / specialty).")]
        public void ResearchAcademy_LoadsFromJson_BindsItsAtb_WithSchoolQualityDials()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            Assert.That(designs.ContainsKey("default-design-research-academy"), Is.True,
                "the research academy loads onto the faction — the six-point registration is wired (template in " +
                "StartingItems, design in ComponentDesigns, materials stocked)");

            var design = designs["default-design-research-academy"] as ComponentDesign;
            Assert.That(design, Is.Not.Null, "default-design-research-academy is a ComponentDesign");

            Assert.That(design.HasAttribute<ResearchAcademyAtb>(), Is.True,
                "the design binds a ResearchAcademyAtb — the AttributeType FQN resolved and the ctor args matched");

            var atb = design.GetAttribute<ResearchAcademyAtb>();
            TestContext.Progress.WriteLine(
                $"[research-academy] class={atb.ClassSize} length={atb.TrainingPeriodInMonths}mo specialty={atb.SpecialtyCategory}");

            Assert.That(atb.ClassSize, Is.EqualTo(10), "class size bound from the template default");
            Assert.That(atb.TrainingPeriodInMonths, Is.EqualTo(24), "training length bound from the template default");
            Assert.That(atb.SpecialtyCategory, Is.EqualTo("tech-category-power-propulsion"),
                "the specialty tech-category string threaded through AtbConstrArgs into the atb — the school's focus");
        }
    }
}
