using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.2 gauge (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II — the Organism engine): the needs-ladder read.
    /// Proves the pure tier assessment climbs correctly across its boundaries (rebellion/collapse/losing-war →
    /// Survive; internal trouble or any war → Stabilize; dominant-and-secure → Ambition; healthy → Thrive), and that
    /// a fresh, healthy start faction reads Thrive end-to-end.
    /// </summary>
    [TestFixture]
    public class NeedsLadderTests
    {
        [Test]
        [Description("The pure AssessTier climbs the ladder: Survive < Stabilize < Thrive < Ambition by the gauges.")]
        public void AssessTier_PureBoundaries()
        {
            // Healthy baseline: peace, content, legitimate, solvent, secure → Thrive.
            Assert.That(NeedsLadder.AssessTier(false, 100, 100, 55, 55, 5000m, false),
                Is.EqualTo(NeedTier.Thrive), "a healthy-but-not-dominant faction thrives");

            // Survive — any single existential trigger.
            Assert.That(NeedsLadder.AssessTier(false, 100, 0, 55, 55, 5000m, inRebellion: true),
                Is.EqualTo(NeedTier.Survive), "open rebellion is existential");
            Assert.That(NeedsLadder.AssessTier(false, 100, 0, 15, 55, 5000m, false),
                Is.EqualTo(NeedTier.Survive), "collapsed morale is existential");
            Assert.That(NeedsLadder.AssessTier(false, 100, 0, 55, 15, 5000m, false),
                Is.EqualTo(NeedTier.Survive), "collapsed legitimacy is existential");
            Assert.That(NeedsLadder.AssessTier(true, 40, 100, 55, 55, 5000m, false),
                Is.EqualTo(NeedTier.Survive), "a war being lost badly (own 40 < enemy 100 × 0.5) is existential");

            // Stabilize — internal trouble or an active war not being lost.
            Assert.That(NeedsLadder.AssessTier(true, 100, 100, 55, 55, 5000m, false),
                Is.EqualTo(NeedTier.Stabilize), "an even war still demands attention");
            Assert.That(NeedsLadder.AssessTier(false, 100, 0, 40, 55, 5000m, false),
                Is.EqualTo(NeedTier.Stabilize), "unhealthy (not collapsed) morale");
            Assert.That(NeedsLadder.AssessTier(false, 100, 0, 55, 55, -100m, false),
                Is.EqualTo(NeedTier.Stabilize), "a treasury in the red");

            // Ambition — dominant and secure on every axis.
            Assert.That(NeedsLadder.AssessTier(false, 100, 50, 70, 70, 200000m, false),
                Is.EqualTo(NeedTier.Ambition), "content, legitimate, rich, and militarily ahead → reach for the grand aim");
            // …but drop ANY axis below its bar and it falls back to Thrive.
            Assert.That(NeedsLadder.AssessTier(false, 100, 50, 70, 70, 50000m, false),
                Is.EqualTo(NeedTier.Thrive), "not wealthy enough for ambition");
            Assert.That(NeedsLadder.AssessTier(false, 40, 50, 70, 70, 200000m, false),
                Is.EqualTo(NeedTier.Thrive), "not militarily ahead → thrive, don't overreach");
        }

        [Test]
        [Description("A fresh, healthy start faction reads Thrive through the entity gatherer.")]
        public void AssessTier_FreshStartFaction_Thrives()
        {
            var s = TestScenario.CreateWithColony();
            // Peace, a neutral-morale/legitimacy start colony, positive funds, no rebellion, no rival → not in crisis,
            // not dominant.
            Assert.That(NeedsLadder.AssessTier(s.Faction), Is.EqualTo(NeedTier.Thrive));
        }
    }
}
