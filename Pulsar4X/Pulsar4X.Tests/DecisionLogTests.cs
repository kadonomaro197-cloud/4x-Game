using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase 5.2 gauge (docs/AI-BRAIN-BUILD-TRACKER.md — 🪐 The Brane, decision-log). Proves a run's decisions are
    /// LEGIBLE and TRACE TO INPUTS: <see cref="ObjectiveSelector.SelectWithReason"/> returns not just the objective but
    /// a reason naming the driving input (the tier, and the winning doctrine axis / personality trait), and
    /// <see cref="PlanReadout"/> surfaces it. So an authored personality's fingerprint is checkable in what its brain
    /// decides — the "emergence is checkable" requirement. Byte-identical: `SelectObjective` still returns the same
    /// objective (it just discards the reason), guarded by the existing `ObjectiveSelectorTests`.
    /// </summary>
    [TestFixture]
    public class DecisionLogTests
    {
        [Test]
        [Description("Each objective choice carries a reason naming the input that drove it (tier / doctrine axis / trait).")]
        public void SelectWithReason_TracesTheChoiceToTheDrivingInput()
        {
            // Survive tier → Defend; the reason names the tier.
            var (obj, reason) = ObjectiveSelector.SelectWithReason(NeedTier.Survive, new DoctrineVector(), null);
            Assert.That(obj, Is.EqualTo(StrategicObjective.Defend));
            Assert.That(reason, Does.Contain("Survive"), "the reason traces to the tier");

            // Thrive + expansion-led doctrine → Expand; the reason names Expansion (the winning growth axis).
            var expansionist = new DoctrineVector { Economic = 0.2f, Military = 0.1f, Tech = 0.2f, Expansion = 0.6f };
            (obj, reason) = ObjectiveSelector.SelectWithReason(NeedTier.Thrive, expansionist, null);
            Assert.That(obj, Is.EqualTo(StrategicObjective.Expand));
            Assert.That(reason, Does.Contain("Expansion"), "the reason names the doctrine axis that led growth");

            // Ambition + an AGGRESSIVE personality → Conquer; the reason names Aggression (the trait fingerprint).
            var aggressive = new PersonalityDB();
            aggressive.SetTrait(PersonalityTrait.Aggression, 0.9);
            (obj, reason) = ObjectiveSelector.SelectWithReason(NeedTier.Ambition, new DoctrineVector { Economic = 0.5f }, aggressive);
            Assert.That(obj, Is.EqualTo(StrategicObjective.Conquer));
            Assert.That(reason, Does.Contain("Aggression"), "an authored aggressive trait is visible in the decision");

            // Ambition + a MILITARY-led doctrine → Conquer; the reason names Military.
            var militarist = new DoctrineVector { Economic = 0.1f, Military = 0.7f, Tech = 0.1f, Expansion = 0.1f };
            (obj, reason) = ObjectiveSelector.SelectWithReason(NeedTier.Ambition, militarist, null);
            Assert.That(obj, Is.EqualTo(StrategicObjective.Conquer));
            Assert.That(reason, Does.Contain("Military"), "a military-led doctrine is visible in the decision");
        }

        [Test]
        [Description("PlanReadout surfaces the decision reason (the 'why') so a run's plan is legible.")]
        public void PlanReadout_SurfacesTheDecisionReason()
        {
            var s = TestScenario.CreateWithColony();
            s.Faction.SetDataBlob(new StrategicObjectiveDB
            {
                Objective = StrategicObjective.Conquer,
                Tier = NeedTier.Ambition,
                DecisionReason = "Ambition tier: Aggression 0.90 > neutral → Conquer",
            });

            string line = PlanReadout.Faction(s.Faction);
            Assert.That(line, Does.Contain("why:"), "the readout has a why field");
            Assert.That(line, Does.Contain("Aggression"), "the readout surfaces the traced reason");
            Assert.That(line, Does.Contain("Conquer"), "and the objective");
        }
    }
}
