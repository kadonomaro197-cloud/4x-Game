using System;
using NUnit.Framework;
using Pulsar4X.Fleets;       // RefuelAction, ResupplyAction, ServeyAnomalyAction
using Pulsar4X.Logistics;    // ShipLogisticsOrders

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL A4 — the four "issue-and-do-nothing" order stubs are DE-FANGED (no wedge, no crash).
    ///
    /// The four (RefuelAction / ResupplyAction / ServeyAnomalyAction / ShipLogisticsOrders) are surfaced-or-surfaceable
    /// orders whose acting method was dead — and two carried real LANDMINES: RefuelAction/ResupplyAction had an EMPTY
    /// Execute AND an IsFinished that never flipped true, so once a standing "refuel when low" order fired one onto the
    /// fleet's blocking order lane it JAMMED that lane forever (IsBlocking + never-finished, and FleetOrderProcessor
    /// won't add further standing actions while the lane is non-empty); ServeyAnomalyAction and ShipLogisticsOrders threw
    /// NotImplementedException in Clone (and ServeyAnomaly in Execute/IsValidCommand too), which — because
    /// FleetOrderProcessor clones standing actions on the background sim thread — would land as a clock-killing [FATAL].
    ///
    /// FINISHING their real behavior needs developer design decisions (what a "supply source" is + transfer range for
    /// refuel; what "resupply" even means; finish-vs-delete the JPSurveyOrder-duplicate survey; drive the per-ship
    /// logistics state machine or leave it a display shim) — all parked in the campaign log ADJUDICATION QUEUE. This
    /// slice removes the landmines so the current surfaced state can never wedge or crash. These gauges pin exactly that.
    /// </summary>
    [TestFixture]
    public class OrderStubSafetyTests
    {
        [Test]
        [Description("RefuelAction + ResupplyAction no longer WEDGE the fleet's blocking order lane: Execute completes "
                   + "the order (IsFinished flips true) so OrderableProcessor removes it, instead of jamming forever.")]
        public void RefuelResupply_Execute_CompletesInsteadOfWedging()
        {
            var refuel = new RefuelAction();
            Assert.That(refuel.IsFinished(), Is.False, "not finished before Execute");
            refuel.Execute(DateTime.MinValue);
            Assert.That(refuel.IsFinished(), Is.True, "RefuelAction completes after Execute (de-wedged — leaves the lane)");

            var resupply = new ResupplyAction();
            Assert.That(resupply.IsFinished(), Is.False, "not finished before Execute");
            resupply.Execute(DateTime.MinValue);
            Assert.That(resupply.IsFinished(), Is.True, "ResupplyAction completes after Execute (de-wedged)");
        }

        [Test]
        [Description("ServeyAnomalyAction + ShipLogisticsOrders no longer THROW in Clone — the old NotImplementedException "
                   + "would land on the sim thread (FleetOrderProcessor clones standing actions there) as a [FATAL].")]
        public void SurveyAnomaly_And_ShipLogistics_Clone_DoesNotThrow()
        {
            Assert.That(() => new ServeyAnomalyAction().Clone(), Throws.Nothing, "ServeyAnomalyAction.Clone is safe");
            Assert.That(() => new ShipLogisticsOrders().Clone(), Throws.Nothing, "ShipLogisticsOrders.Clone is safe");
        }

        [Test]
        [Description("ServeyAnomalyAction is inert (unregistered, behavior parked): Execute/IsFinished are a safe no-op "
                   + "completion and never throw.")]
        public void SurveyAnomaly_IsAnInertSafeShell()
        {
            var survey = new ServeyAnomalyAction();
            Assert.That(() => survey.Execute(DateTime.MinValue), Throws.Nothing, "Execute is a safe no-op");
            Assert.That(survey.IsFinished(), Is.True, "the no-op survey completes rather than lingering");
        }
    }
}
