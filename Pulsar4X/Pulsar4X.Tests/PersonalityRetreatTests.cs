using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// M2-1b gauge (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II): the second personality→behaviour wire. A faction's
    /// Collectivism tilts how much of its fleet it will spend before breaking off. Proves a neutral (or absent)
    /// personality is byte-identical to the flat 0.5 threshold, a collectivist force fights on through heavier losses,
    /// and an individualist one flees early — personality now shapes the retreat decision.
    /// </summary>
    [TestFixture]
    public class PersonalityRetreatTests
    {
        [Test]
        [Description("Neutral/absent = the flat RetreatCasualtyThreshold (byte-identical); high Collectivism holds on longer, low breaks off sooner.")]
        public void RetreatThresholdFor_ShiftsByCollectivism_NeutralIsByteIdentical()
        {
            double flat = CombatEngagement.RetreatCasualtyThreshold; // 0.5

            Assert.That(CombatEngagement.RetreatThresholdFor(null), Is.EqualTo(flat).Within(1e-9),
                "no personality → the flat threshold");
            Assert.That(CombatEngagement.RetreatThresholdFor(new PersonalityDB()), Is.EqualTo(flat).Within(1e-9),
                "an all-neutral personality → the flat threshold (byte-identical)");

            var collectivist = new PersonalityDB();
            collectivist.SetTrait(PersonalityTrait.Collectivism, 1.0);
            double high = CombatEngagement.RetreatThresholdFor(collectivist);
            Assert.That(high, Is.GreaterThan(flat), "a collectivist force endures heavier losses before it breaks off");
            Assert.That(high, Is.EqualTo(flat + CombatEngagement.CollectivismRetreatSwing).Within(1e-9),
                "a maximal collectivist swings up by the full swing");

            var individualist = new PersonalityDB();
            individualist.SetTrait(PersonalityTrait.Collectivism, 0.0);
            double low = CombatEngagement.RetreatThresholdFor(individualist);
            Assert.That(low, Is.LessThan(flat), "an individualist breaks off to save the unit");
            Assert.That(low, Is.EqualTo(flat - CombatEngagement.CollectivismRetreatSwing).Within(1e-9),
                "a maximal individualist swings down by the full swing");

            // The clamp keeps the threshold inside (0,1) so a fleet can always both fight and eventually flee.
            Assert.That(high, Is.LessThanOrEqualTo(0.95));
            Assert.That(low, Is.GreaterThanOrEqualTo(0.05));
        }
    }
}
