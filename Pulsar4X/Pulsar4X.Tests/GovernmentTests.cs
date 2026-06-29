using NUnit.Framework;
using Pulsar4X.Factions;
using Pulsar4X.Colonies;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the government substrate (docs/GOVERNMENT-AND-POLITICS-DESIGN.md): the four dials derive the
    /// coefficient/rule overrides the rest of the engine reads, and the live classifier names the regime. This
    /// is the "build the dials, ship the menu" substrate — processors wiring to it comes later.
    /// </summary>
    [TestFixture]
    public class GovernmentTests
    {
        [Test]
        [Description("Iconic dial combos name themselves; the default (all Mid) is a Federal Republic.")]
        public void Classifier_NamesIconicCombos()
        {
            Assert.That(new GovernmentDB().Name(), Is.EqualTo("Federal Republic"), "default = all Mid");

            // People / Free Market / Open / Pacifist
            Assert.That(new GovernmentDB(GovNotch.Low, GovNotch.Low, GovNotch.High, GovNotch.Low).Name(),
                Is.EqualTo("Liberal Democracy"));
            // One Ruler / Command / Closed / Militarist
            Assert.That(new GovernmentDB(GovNotch.High, GovNotch.High, GovNotch.Low, GovNotch.High).Name(),
                Is.EqualTo("Totalitarian War-State"));
            // One Ruler / Free Market / Open / Pacifist
            Assert.That(new GovernmentDB(GovNotch.High, GovNotch.Low, GovNotch.High, GovNotch.Low).Name(),
                Is.EqualTo("Corporate Plutocracy"));
        }

        [Test]
        [Description("Any un-named combo still produces a non-empty name and description (never blank).")]
        public void Classifier_FallsBackForUnnamedCombos()
        {
            // An unlisted combo (Mid authority / High economy / Low openness / High militarism).
            var gov = new GovernmentDB(GovNotch.Mid, GovNotch.High, GovNotch.Low, GovNotch.High);
            Assert.That(gov.Name(), Is.Not.Empty);
            Assert.That(gov.Description(), Is.Not.Empty);
            Assert.That(gov.Description(), Does.Contain("command"));   // economy High
            Assert.That(gov.Description(), Does.Contain("closed"));    // openness Low
            Assert.That(gov.Description(), Does.Contain("militarist")); // militarism High
        }

        [Test]
        [Description("AUTHORITY sets the crew-shortage rule (M3-2): only a high-authority regime conscripts.")]
        public void Authority_SetsCrewPolicy()
        {
            Assert.That(new GovernmentDB { Authority = GovNotch.High }.CrewPolicy(), Is.EqualTo(CrewShortagePolicy.BuildUnderstaffed));
            Assert.That(new GovernmentDB { Authority = GovNotch.Mid }.CrewPolicy(), Is.EqualTo(CrewShortagePolicy.Block));
            Assert.That(new GovernmentDB { Authority = GovNotch.Low }.CrewPolicy(), Is.EqualTo(CrewShortagePolicy.Block));
        }

        [Test]
        [Description("AUTHORITY raises the tax ceiling and damps morale's pull; OPENNESS scales research.")]
        public void Dials_DeriveCoefficients()
        {
            Assert.That(new GovernmentDB { Authority = GovNotch.High }.TaxCeiling(),
                Is.GreaterThan(new GovernmentDB { Authority = GovNotch.Low }.TaxCeiling()), "authority raises the tax ceiling");

            // The People-end amplifies morale's effect (both ways); One Ruler damps it.
            Assert.That(new GovernmentDB { Authority = GovNotch.Low }.MoraleWeight(),
                Is.GreaterThan(new GovernmentDB { Authority = GovNotch.High }.MoraleWeight()));

            Assert.That(new GovernmentDB { Openness = GovNotch.High }.ResearchMultiplier(),
                Is.GreaterThan(new GovernmentDB { Openness = GovNotch.Low }.ResearchMultiplier()), "open society out-researches a closed one");
        }

        [Test]
        [Description("Discontent vents as emigration in an open/consent regime, unrest under authority or closed borders.")]
        public void Discontent_DependsOnRegime()
        {
            Assert.That(new GovernmentDB(GovNotch.Low, GovNotch.Mid, GovNotch.High, GovNotch.Mid).Discontent(),
                Is.EqualTo(DiscontentResponse.Emigration), "open & low-authority = people leave");
            Assert.That(new GovernmentDB(GovNotch.High, GovNotch.Mid, GovNotch.High, GovNotch.Mid).Discontent(),
                Is.EqualTo(DiscontentResponse.Unrest), "high authority = unrest");
            Assert.That(new GovernmentDB(GovNotch.Low, GovNotch.Mid, GovNotch.Low, GovNotch.Mid).Discontent(),
                Is.EqualTo(DiscontentResponse.Unrest), "closed borders = unrest");
        }

        [Test]
        [Description("GovernmentDB clones deeply (survives save/load).")]
        public void GovernmentDB_ClonesDeeply()
        {
            var original = new GovernmentDB(GovNotch.High, GovNotch.Low, GovNotch.High, GovNotch.Mid);
            var clone = (GovernmentDB)original.Clone();
            Assert.That(clone.Authority, Is.EqualTo(GovNotch.High));
            Assert.That(clone.Economy, Is.EqualTo(GovNotch.Low));
            clone.Authority = GovNotch.Low;
            Assert.That(original.Authority, Is.EqualTo(GovNotch.High), "clone shares no state with the original");
        }
    }
}
