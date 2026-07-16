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

        [Test]
        [Description("WAR FOOTING: a Stabilize-tier faction AT WAR and WINNING presses the offensive (Conquer) if it's " +
                     "military-led or aggressive — the link that turns a declared, winnable war into an invasion for a " +
                     "recovering-but-dominant power (the DevTest UMF). Without it Conquer is Ambition-only.")]
        public void WarFooting_StabilizeAtWarAndWinning_PressesTheOffensive()
        {
            var warlike = Doctrine(0, 1, 0, 0);      // military-led
            var peaceful = Doctrine(1, 0, 0, 0);     // economy-led, no aggression

            // Military-led + at war + winning → Conquer from Stabilize (not just Consolidate).
            Assert.That(ObjectiveSelector.SelectWithReason(NeedTier.Stabilize, warlike, null, atWarAndWinning: true).objective,
                Is.EqualTo(StrategicObjective.Conquer), "a strong military belligerent presses its winnable war");

            // Aggressive personality behind a peaceful doctrine also presses.
            var aggressive = new PersonalityDB();
            aggressive.SetTrait(PersonalityTrait.Aggression, 1.0);
            Assert.That(ObjectiveSelector.SelectWithReason(NeedTier.Stabilize, peaceful, aggressive, atWarAndWinning: true).objective,
                Is.EqualTo(StrategicObjective.Conquer), "aggression drives the offensive even behind a peaceful doctrine");

            // NOT winning → no offensive; consolidate/recover instead.
            Assert.That(ObjectiveSelector.SelectWithReason(NeedTier.Stabilize, warlike, null, atWarAndWinning: false).objective,
                Is.EqualTo(StrategicObjective.Consolidate), "a belligerent that can't win doesn't attack — it consolidates");

            // Winning but PEACEFUL (no military weight, no aggression) → still consolidate; war footing needs a warlike bent.
            Assert.That(ObjectiveSelector.SelectWithReason(NeedTier.Stabilize, peaceful, null, atWarAndWinning: true).objective,
                Is.EqualTo(StrategicObjective.Consolidate), "a peaceful faction doesn't press even a winnable war");

            // SURVIVE tier + winning + warlike + homeland NOT in rebellion → presses the offensive. A hostile-world
            // faction (Mars/Venus) is pinned at Survive by its conditions morale penalty; gating Conquer above Survive
            // would mean it could never invade (the DevTest UMF). A dominant, warlike, WINNING power presses its war.
            Assert.That(ObjectiveSelector.SelectWithReason(NeedTier.Survive, warlike, null, atWarAndWinning: true, homelandInRebellion: false).objective,
                Is.EqualTo(StrategicObjective.Conquer), "a winning warlike power presses its war even from Survive when its homeland isn't in revolt");

            // ...but SURVIVE + open REBELLION at home → Defend (recover first; don't invade while the capital revolts).
            Assert.That(ObjectiveSelector.SelectWithReason(NeedTier.Survive, warlike, null, atWarAndWinning: true, homelandInRebellion: true).objective,
                Is.EqualTo(StrategicObjective.Defend), "a rebellion at home is attended before any offensive");
        }

        [Test]
        [Description("Byte-identity: the war-footing overload with atWarAndWinning=false equals the 3-arg call across all tiers.")]
        public void WarFootingOverload_NotAtWarOrWinning_IsByteIdentical()
        {
            var doctrines = new[] { Doctrine(1, 0, 0, 0), Doctrine(0, 1, 0, 0), Doctrine(0, 0, 1, 0), Doctrine(0, 0, 0, 1), Doctrine(0, 0, 0, 0) };
            foreach (NeedTier tier in System.Enum.GetValues(typeof(NeedTier)))
                foreach (var d in doctrines)
                    Assert.That(ObjectiveSelector.SelectWithReason(tier, d, null, atWarAndWinning: false).objective,
                        Is.EqualTo(ObjectiveSelector.SelectWithReason(tier, d, null).objective),
                        $"tier {tier} must be unchanged when not (at war and winning)");
        }
    }
}
