using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// F-C2c gauge (docs/GOVERNMENT-AND-POLITICS-DESIGN.md §enact/refuse): the teeth. Proves enacting a demand is a
    /// fixed legitimacy gain; refusing is a legitimacy LOSS that grows with the demand's pressure AND is harsher for
    /// a CONSENT regime (low Authority) than a COMMAND one (high Authority, which can suppress) — the consent-vs-
    /// command refusal split. Pure → byte-identical.
    /// </summary>
    [TestFixture]
    public class DemandResolutionTests
    {
        [Test]
        [Description("Enact = fixed gain; Refuse = a loss that scales with pressure and is worse under a consent regime.")]
        public void LegitimacyDelta_EnactGains_RefuseCosts_ByRegime()
        {
            var demand = new PoliticalDemand(PoliticalBloc.Labor, DemandKind.CreateJobs, pressure: 20.0);
            var mid = new GovernmentDB(); // Mid authority → hardness 1.0

            // Enact → the fixed gain, regardless of government.
            Assert.That(DemandResolution.LegitimacyDelta(demand, DemandResponse.Enact, mid),
                Is.EqualTo(DemandResolution.EnactLegitimacyGain).Within(1e-9));

            // Refuse under Mid → -(base + pressure × 1.0) = -25.
            Assert.That(DemandResolution.LegitimacyDelta(demand, DemandResponse.Refuse, mid),
                Is.EqualTo(-(DemandResolution.RefuseLegitimacyBase + 20.0 * 1.0)).Within(1e-9));

            // A CONSENT regime (Low Authority) bleeds MORE from refusing than a COMMAND regime (High Authority).
            var consent = new GovernmentDB { Authority = GovNotch.Low };
            var command = new GovernmentDB { Authority = GovNotch.High };
            double refuseConsent = DemandResolution.LegitimacyDelta(demand, DemandResponse.Refuse, consent);
            double refuseCommand = DemandResolution.LegitimacyDelta(demand, DemandResponse.Refuse, command);

            Assert.That(refuseConsent, Is.EqualTo(-(5.0 + 20.0 * 1.5)).Within(1e-9), "consent: hardness 1.5 → -35");
            Assert.That(refuseCommand, Is.EqualTo(-(5.0 + 20.0 * 0.5)).Within(1e-9), "command: hardness 0.5 → -15");
            Assert.That(refuseConsent, Is.LessThan(refuseCommand),
                "a consent regime loses more legitimacy refusing (a command regime can suppress)");
        }

        [Test]
        [Description("A louder demand costs more legitimacy to refuse than a quiet one.")]
        public void LegitimacyDelta_RefusingALouderDemand_CostsMore()
        {
            var mid = new GovernmentDB();
            var loud = new PoliticalDemand(PoliticalBloc.Militarists, DemandKind.ConfrontRival, pressure: 30.0);
            var quiet = new PoliticalDemand(PoliticalBloc.Militarists, DemandKind.ConfrontRival, pressure: 5.0);

            Assert.That(DemandResolution.LegitimacyDelta(loud, DemandResponse.Refuse, mid),
                Is.LessThan(DemandResolution.LegitimacyDelta(quiet, DemandResponse.Refuse, mid)),
                "refusing a louder demand is a bigger legitimacy hit");
        }
    }
}
