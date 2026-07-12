using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// F-C3d gauge (docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md §F): the graduated caught/suspicion model. Proves a low
    /// roll under low risk resolves Clean; a high roll or high risk trends Traced/Caught; agent skill lowers risk and
    /// counter-intel raises it; and suspicion rises most on Caught, some on Traced, none on Clean (clamped 0..100).
    /// Pure/deterministic (the roll is passed in) → byte-identical.
    /// </summary>
    [TestFixture]
    public class CovertRiskTests
    {
        [Test]
        [Description("Low roll + low risk = Clean; a high roll or high risk = Traced/Caught; skill helps, counter-intel hurts.")]
        public void Resolve_BandsByEffectiveRiskAndRoll()
        {
            // Low roll under low risk → Clean.
            Assert.That(CovertRisk.Resolve(0.10, 0.0, 0.0, 0.10), Is.EqualTo(CovertOutcome.Clean));

            // An unlucky (high) roll gets caught even at low risk — there's always some danger.
            Assert.That(CovertRisk.Resolve(0.10, 0.0, 0.0, 0.99), Is.EqualTo(CovertOutcome.Caught));

            // High risk + a middling roll → not Clean.
            Assert.That(CovertRisk.Resolve(0.80, 0.0, 0.0, 0.50), Is.Not.EqualTo(CovertOutcome.Clean));

            // A skilled agent lowers effective risk → the same middling roll comes back Clean.
            Assert.That(CovertRisk.Resolve(0.80, 0.9, 0.0, 0.50), Is.EqualTo(CovertOutcome.Clean),
                "a skilled agent slips through where a novice is traced/caught");

            // The target's counter-intel raises effective risk → worse than the same op with none.
            var noCounter = CovertRisk.Resolve(0.40, 0.0, 0.0, 0.75);
            var withCounter = CovertRisk.Resolve(0.40, 0.0, 1.0, 0.75);
            Assert.That((int)withCounter, Is.GreaterThanOrEqualTo((int)noCounter),
                "counter-intel makes the same op at least as likely to be caught");
        }

        [Test]
        [Description("Suspicion rises most on Caught, some on Traced, none on Clean; clamped at 100.")]
        public void SuspicionAfter_RisesByOutcome_Clamped()
        {
            Assert.That(CovertRisk.SuspicionAfter(0, CovertOutcome.Clean), Is.EqualTo(0.0));
            double traced = CovertRisk.SuspicionAfter(0, CovertOutcome.Traced);
            double caught = CovertRisk.SuspicionAfter(0, CovertOutcome.Caught);
            Assert.That(traced, Is.GreaterThan(0.0));
            Assert.That(caught, Is.GreaterThan(traced), "getting caught spikes suspicion more than being traced");

            Assert.That(CovertRisk.SuspicionAfter(95, CovertOutcome.Caught), Is.EqualTo(100.0), "suspicion clamps at 100");
        }
    }
}
