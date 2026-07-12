using System;
using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.1 gauge (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II — the Organism engine): the faction goal-slot
    /// data model. Proves the defaults are the neutral "no plan settled" state (Thrive tier, no objective, no target,
    /// not committed) and that the blob clones deeply (save/load, entity transfer) with no shared state.
    /// </summary>
    [TestFixture]
    public class StrategicObjectiveTests
    {
        [Test]
        [Description("Neutral defaults; write/read; deep Clone shares no state.")]
        public void Defaults_Write_Clone()
        {
            var obj = new StrategicObjectiveDB();
            Assert.That(obj.Tier, Is.EqualTo(NeedTier.Thrive), "the default tier is the healthy 'good times' rung");
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.None), "no objective settled by default");
            Assert.That(obj.TargetFactionId, Is.EqualTo(-1), "no target by default");
            Assert.That(obj.CommittedUntil, Is.EqualTo(DateTime.MinValue), "not committed by default (free to plan)");

            obj.Tier = NeedTier.Survive;
            obj.Objective = StrategicObjective.Defend;
            obj.TargetFactionId = 42;
            var committed = new DateTime(2050, 1, 1);
            obj.CommittedUntil = committed;

            var clone = (StrategicObjectiveDB)obj.Clone();
            Assert.That(clone.Tier, Is.EqualTo(NeedTier.Survive));
            Assert.That(clone.Objective, Is.EqualTo(StrategicObjective.Defend));
            Assert.That(clone.TargetFactionId, Is.EqualTo(42));
            Assert.That(clone.CommittedUntil, Is.EqualTo(committed));

            clone.Objective = StrategicObjective.Conquer;
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.Defend), "the clone shares no state with the original");
        }
    }
}
