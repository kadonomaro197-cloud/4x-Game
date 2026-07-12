using System;
using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.5 gauge (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II — the Organism engine): the AMBITION CADENCE.
    /// Ambition already steers WHICH objective a faction picks; this proves it now also drives HOW OFTEN a faction
    /// renews an expansion push. <see cref="ObjectiveTransition.CommitFor"/> shortens the commit dwell for a
    /// high-Ambition faction (it re-commits to Expand/Conquer more often) and lengthens it for a low-Ambition one —
    /// while staying byte-identical at the neutral (0.5) trait every existing test faction has.
    /// </summary>
    [TestFixture]
    public class AmbitionCadenceTests
    {
        private static PersonalityDB WithAmbition(double value)
        {
            var p = new PersonalityDB();
            p.SetTrait(PersonalityTrait.Ambition, value);
            return p;
        }

        [Test]
        [Description("Byte-identity tripwire: neutral Ambition (or no personality) → exactly the pre-2.5 fixed dwell, "
            + "and a non-expansion objective is never scaled at all.")]
        public void NeutralAmbition_IsByteIdenticalToDefault()
        {
            // No personality at all → neutral fallback → the old fixed dwell.
            Assert.That(ObjectiveTransition.CommitFor(StrategicObjective.Expand, null),
                Is.EqualTo(ObjectiveTransition.DefaultCommitFor), "absent personality holds the default dwell");

            // An authored-but-neutral (0.5) Ambition → still exactly the default (the multiply-by-1.0 is exact).
            var neutral = WithAmbition(PersonalityDB.Neutral);
            Assert.That(ObjectiveTransition.CommitFor(StrategicObjective.Expand, neutral),
                Is.EqualTo(ObjectiveTransition.DefaultCommitFor), "neutral Ambition holds the default dwell");
            Assert.That(ObjectiveTransition.CommitFor(StrategicObjective.Conquer, neutral),
                Is.EqualTo(ObjectiveTransition.DefaultCommitFor), "neutral Ambition holds the default for Conquer too");

            // A non-expansion objective is never scaled, even at an extreme Ambition — only the expansion push moves.
            var extreme = WithAmbition(1.0);
            Assert.That(ObjectiveTransition.CommitFor(StrategicObjective.GrowEconomy, extreme),
                Is.EqualTo(ObjectiveTransition.DefaultCommitFor), "a peaceful growth aim keeps the fixed dwell");
            Assert.That(ObjectiveTransition.CommitFor(StrategicObjective.Defend, extreme),
                Is.EqualTo(ObjectiveTransition.DefaultCommitFor), "Defend keeps the fixed dwell");
            Assert.That(ObjectiveTransition.CommitFor(StrategicObjective.None, extreme),
                Is.EqualTo(ObjectiveTransition.DefaultCommitFor), "None keeps the fixed dwell");
        }

        [Test]
        [Description("High Ambition → strictly SHORTER expansion dwell (push more often); low Ambition → strictly LONGER.")]
        public void HighAmbitionShortens_LowAmbitionLengthens()
        {
            var high = ObjectiveTransition.CommitFor(StrategicObjective.Expand, WithAmbition(1.0));
            var low = ObjectiveTransition.CommitFor(StrategicObjective.Expand, WithAmbition(0.0));

            Assert.That(high, Is.LessThan(ObjectiveTransition.DefaultCommitFor), "an ambitious faction re-commits sooner");
            Assert.That(low, Is.GreaterThan(ObjectiveTransition.DefaultCommitFor), "a low-drive faction dwells longer");

            // Conquer (the aggressive form of the expansion push) scales the same way.
            var highConquer = ObjectiveTransition.CommitFor(StrategicObjective.Conquer, WithAmbition(1.0));
            Assert.That(highConquer, Is.LessThan(ObjectiveTransition.DefaultCommitFor), "Conquer scales with Ambition too");
        }

        [Test]
        [Description("Monotonic: the expansion dwell only ever DECREASES as Ambition rises across the whole 0..1 range.")]
        public void ExpansionDwell_IsMonotonicDecreasingInAmbition()
        {
            TimeSpan previous = TimeSpan.MaxValue;
            for (int step = 0; step <= 10; step++)
            {
                double ambition = step / 10.0;                       // 0.0, 0.1, … 1.0
                var span = ObjectiveTransition.CommitFor(StrategicObjective.Expand, WithAmbition(ambition));
                Assert.That(span, Is.LessThan(previous),
                    $"dwell must strictly shrink as Ambition rises (broke at Ambition {ambition:0.0})");
                previous = span;
            }
        }
    }
}
