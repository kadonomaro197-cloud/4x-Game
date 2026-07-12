using System;
using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.3 gauge (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II — the Organism engine): the transition engine's
    /// hysteresis. Proves the brain holds a committed objective under noise (no thrash), re-plans once the commitment
    /// expires, and drops everything to preempt a more urgent need (an emergency doesn't wait for the clock).
    /// </summary>
    [TestFixture]
    public class ObjectiveTransitionTests
    {
        private static readonly DateTime T0 = new DateTime(2050, 1, 1);

        [Test]
        [Description("ShouldReplan: a more-urgent tier preempts; an expired commitment re-plans; otherwise hold.")]
        public void ShouldReplan_UrgencyOrExpiry()
        {
            var committedUntil = T0.AddDays(180);

            // Within the commitment, a same or less-urgent proposal → hold.
            Assert.That(ObjectiveTransition.ShouldReplan(NeedTier.Thrive, committedUntil, NeedTier.Thrive, T0.AddDays(30)), Is.False);
            Assert.That(ObjectiveTransition.ShouldReplan(NeedTier.Thrive, committedUntil, NeedTier.Ambition, T0.AddDays(30)), Is.False,
                "a LESS urgent proposal (Ambition) never preempts a commitment");

            // A more-urgent proposal preempts immediately, mid-commitment.
            Assert.That(ObjectiveTransition.ShouldReplan(NeedTier.Thrive, committedUntil, NeedTier.Survive, T0.AddDays(30)), Is.True,
                "an emergency (Survive) preempts the commitment");

            // Once the commitment expires, re-plan even for a same-tier proposal.
            Assert.That(ObjectiveTransition.ShouldReplan(NeedTier.Thrive, committedUntil, NeedTier.Thrive, T0.AddDays(200)), Is.True);
        }

        [Test]
        [Description("Advance holds under noise, re-plans on expiry, and preempts on an emergency.")]
        public void Advance_NoThrash_ThenReplan_ThenPreempt()
        {
            var obj = new StrategicObjectiveDB();
            var commit = TimeSpan.FromDays(180);

            // First commit: a fresh objective (CommittedUntil = MinValue) always adopts.
            Assert.That(ObjectiveTransition.Advance(obj, NeedTier.Thrive, StrategicObjective.GrowEconomy, -1, T0, commit), Is.True);
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.GrowEconomy));
            Assert.That(obj.CommittedUntil, Is.EqualTo(T0 + commit));

            // Noise mid-commitment: a lateral re-proposal is HELD (no thrash).
            Assert.That(ObjectiveTransition.Advance(obj, NeedTier.Thrive, StrategicObjective.AdvanceTech, -1, T0.AddDays(30), commit), Is.False);
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.GrowEconomy), "held the plan");

            // Emergency: a Survive need preempts the commitment right now.
            Assert.That(ObjectiveTransition.Advance(obj, NeedTier.Survive, StrategicObjective.Defend, 7, T0.AddDays(60), commit), Is.True);
            Assert.That(obj.Tier, Is.EqualTo(NeedTier.Survive));
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.Defend));
            Assert.That(obj.TargetFactionId, Is.EqualTo(7));

            // After the (new) commitment expires, it re-plans to whatever the ladder now says.
            Assert.That(ObjectiveTransition.Advance(obj, NeedTier.Thrive, StrategicObjective.GrowEconomy, -1, T0.AddDays(60) + commit, commit), Is.True);
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.GrowEconomy));
        }
    }
}
