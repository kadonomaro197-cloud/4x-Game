using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.4a gauge (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II — the Organism engine): the objective selector.
    /// Proves the tier picks the family (survive→defend, stabilize→consolidate) and, at Thrive/Ambition, doctrine +
    /// personality pick the specific aim — including that an Aggressive faction reaches for Conquer even behind a
    /// peaceful doctrine.
    /// </summary>
    [TestFixture]
    public class ObjectiveSelectorTests
    {
        private static DoctrineVector Doctrine(float econ, float mil, float tech, float expand)
            => new DoctrineVector { Economic = econ, Military = mil, Tech = tech, Expansion = expand };

        [Test]
        [Description("Survive→Defend and Stabilize→Consolidate regardless of doctrine/personality.")]
        public void CrisisTiers_ForceDefendAndConsolidate()
        {
            var warlike = Doctrine(0, 1, 0, 0);
            Assert.That(ObjectiveSelector.SelectObjective(NeedTier.Survive, warlike, null), Is.EqualTo(StrategicObjective.Defend));
            Assert.That(ObjectiveSelector.SelectObjective(NeedTier.Stabilize, warlike, null), Is.EqualTo(StrategicObjective.Consolidate));
        }

        [Test]
        [Description("Thrive presses the strongest peaceful growth axis; Military doesn't buy a war from Thrive.")]
        public void Thrive_PicksTheDominantGrowthAxis()
        {
            Assert.That(ObjectiveSelector.SelectObjective(NeedTier.Thrive, Doctrine(1, 0, 0, 0), null), Is.EqualTo(StrategicObjective.GrowEconomy));
            Assert.That(ObjectiveSelector.SelectObjective(NeedTier.Thrive, Doctrine(0, 0, 1, 0), null), Is.EqualTo(StrategicObjective.AdvanceTech));
            Assert.That(ObjectiveSelector.SelectObjective(NeedTier.Thrive, Doctrine(0, 0, 0, 1), null), Is.EqualTo(StrategicObjective.Expand));
            Assert.That(ObjectiveSelector.SelectObjective(NeedTier.Thrive, Doctrine(0, 1, 0, 0), null), Is.EqualTo(StrategicObjective.GrowEconomy),
                "a Military-led faction at Thrive builds its economy, not a war");
            Assert.That(ObjectiveSelector.SelectObjective(NeedTier.Thrive, Doctrine(0, 0, 0, 0), null), Is.EqualTo(StrategicObjective.GrowEconomy),
                "all-zero doctrine → the economy base");
        }

        [Test]
        [Description("Ambition → Conquer for a Military-led OR Aggressive faction; else the strongest peaceful axis.")]
        public void Ambition_ConquerOrPressTheAxis()
        {
            Assert.That(ObjectiveSelector.SelectObjective(NeedTier.Ambition, Doctrine(0, 1, 0, 0), null), Is.EqualTo(StrategicObjective.Conquer),
                "a Military-led ambition goes to war");

            // A peaceful (expansion-led) doctrine but an AGGRESSIVE personality still reaches for Conquer.
            var aggressive = new PersonalityDB();
            aggressive.SetTrait(PersonalityTrait.Aggression, 1.0);
            Assert.That(ObjectiveSelector.SelectObjective(NeedTier.Ambition, Doctrine(0, 0, 0, 1), aggressive), Is.EqualTo(StrategicObjective.Conquer),
                "aggression overrides a peaceful doctrine at the ambition tier");

            // Peaceful doctrine + a non-aggressive (or absent) personality → press the strongest peaceful axis.
            Assert.That(ObjectiveSelector.SelectObjective(NeedTier.Ambition, Doctrine(0, 0, 0, 1), null), Is.EqualTo(StrategicObjective.Expand),
                "a peaceful, unaggressive ambition expands rather than conquers");
        }
    }
}
