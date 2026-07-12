using NUnit.Framework;
using Pulsar4X.People;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.7 gauge (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II — the Organism engine): officer character,
    /// tenure-blended. Proves a green officer executes the faction's doctrine, a seasoned one runs on their own
    /// character, tenure scales between them, and drift nudges a trait toward a target (clamped).
    /// </summary>
    [TestFixture]
    public class OfficerCharacterTests
    {
        [Test]
        [Description("TenureWeight: 0 green, 1 at cap, linear between; zero cap reads green.")]
        public void TenureWeight_RisesWithExperience()
        {
            Assert.That(OfficerCharacter.TenureWeight(0, 100), Is.EqualTo(0.0).Within(1e-9), "green officer");
            Assert.That(OfficerCharacter.TenureWeight(100, 100), Is.EqualTo(1.0).Within(1e-9), "fully seasoned");
            Assert.That(OfficerCharacter.TenureWeight(50, 100), Is.EqualTo(0.5).Within(1e-9), "halfway");
            Assert.That(OfficerCharacter.TenureWeight(200, 100), Is.EqualTo(1.0).Within(1e-9), "clamped at cap");
            Assert.That(OfficerCharacter.TenureWeight(50, 0), Is.EqualTo(0.0).Within(1e-9), "no cap → green");
        }

        [Test]
        [Description("Blend: green officer = faction doctrine, veteran = own character, tenure mixes.")]
        public void Blend_MixesOfficerAndFactionByTenure()
        {
            // officer is bold (0.9), faction is cautious (0.1)
            Assert.That(OfficerCharacter.Blend(0.9, 0.1, tenureWeight: 0.0), Is.EqualTo(0.1).Within(1e-9), "green → faction doctrine");
            Assert.That(OfficerCharacter.Blend(0.9, 0.1, tenureWeight: 1.0), Is.EqualTo(0.9).Within(1e-9), "veteran → own character");
            Assert.That(OfficerCharacter.Blend(0.9, 0.1, tenureWeight: 0.5), Is.EqualTo(0.5).Within(1e-9), "mid-career → a mix");
        }

        [Test]
        [Description("Drift nudges toward a target by rate, clamped to [0,1].")]
        public void Drift_MovesTowardTarget()
        {
            Assert.That(OfficerCharacter.Drift(0.2, 0.8, rate: 0.0), Is.EqualTo(0.2).Within(1e-9), "no drift");
            Assert.That(OfficerCharacter.Drift(0.2, 0.8, rate: 1.0), Is.EqualTo(0.8).Within(1e-9), "snap to target");
            Assert.That(OfficerCharacter.Drift(0.2, 0.8, rate: 0.5), Is.EqualTo(0.5).Within(1e-9), "halfway");
            Assert.That(OfficerCharacter.Drift(0.9, 2.0, rate: 1.0), Is.EqualTo(1.0).Within(1e-9), "result clamped to 1");
        }
    }
}
