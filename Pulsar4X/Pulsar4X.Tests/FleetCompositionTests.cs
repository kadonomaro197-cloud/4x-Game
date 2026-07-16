using NUnit.Framework;
using Pulsar4X.Fleets;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Fleet-composition ladder (2026-07-16, slice 1) — the growth tiers a HOLISTIC fleet climbs. Pure data/logic:
    /// the size thresholds (min-to-deploy / ideal / perfect) and the tier read. The AI (later slices) uses this to
    /// decide when a forming fleet is deployable, when to grow it to ideal + reorganise into role sub-fleets, and
    /// when to push to perfect + sub-sub-fleets — always as ONE holistic fleet, never a single-role fleet.
    /// </summary>
    [TestFixture]
    public class FleetCompositionTests
    {
        [Test]
        [Description("The default strike-fleet ladder (3/8/18) reads the right tier across the whole count range.")]
        public void TierFor_ClimbsThroughTheLadder()
        {
            var t = FleetCompositionTemplate.DefaultStrikeFleet;   // 3 / 8 / 18

            Assert.That(t.TierFor(0), Is.EqualTo(FleetCompositionTier.Forming), "empty → Forming");
            Assert.That(t.TierFor(2), Is.EqualTo(FleetCompositionTier.Forming), "below min → Forming");
            Assert.That(t.TierFor(3), Is.EqualTo(FleetCompositionTier.Deployable), "at min → Deployable");
            Assert.That(t.TierFor(7), Is.EqualTo(FleetCompositionTier.Deployable), "below ideal → Deployable");
            Assert.That(t.TierFor(8), Is.EqualTo(FleetCompositionTier.Ideal), "at ideal → Ideal");
            Assert.That(t.TierFor(17), Is.EqualTo(FleetCompositionTier.Ideal), "below perfect → Ideal");
            Assert.That(t.TierFor(18), Is.EqualTo(FleetCompositionTier.Perfect), "at perfect → Perfect");
            Assert.That(t.TierFor(30), Is.EqualTo(FleetCompositionTier.Perfect), "above perfect → Perfect");
        }

        [Test]
        [Description("TargetCountFor returns the size the AI grows toward for each aspiration.")]
        public void TargetCountFor_MapsAspirationToSize()
        {
            var t = FleetCompositionTemplate.DefaultStrikeFleet;
            Assert.That(t.TargetCountFor(FleetCompositionTier.Deployable), Is.EqualTo(3));
            Assert.That(t.TargetCountFor(FleetCompositionTier.Ideal), Is.EqualTo(8));
            Assert.That(t.TargetCountFor(FleetCompositionTier.Perfect), Is.EqualTo(18));
            Assert.That(t.TargetCountFor(FleetCompositionTier.Forming), Is.EqualTo(0), "no target below the deploy floor");
        }

        [Test]
        [Description("A faction can define its own ladder (per-faction compositions) — the thresholds are honoured.")]
        public void CustomTemplate_HonoursItsOwnThresholds()
        {
            var heavy = new FleetCompositionTemplate("Battle Fleet", minToDeploy: 5, idealSize: 12, perfectSize: 30);
            Assert.That(heavy.TierFor(4), Is.EqualTo(FleetCompositionTier.Forming));
            Assert.That(heavy.TierFor(5), Is.EqualTo(FleetCompositionTier.Deployable));
            Assert.That(heavy.TierFor(12), Is.EqualTo(FleetCompositionTier.Ideal));
            Assert.That(heavy.TierFor(30), Is.EqualTo(FleetCompositionTier.Perfect));
        }
    }
}
