using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the LIVE CONSUMER of the popular-demands pillar (docs/GOVERNMENT-AND-POLITICS-DESIGN.md §Demands).
    /// The demand logic — <see cref="DemandEngine"/> / <see cref="DemandResolution"/> — was built as pure math with
    /// ZERO callers (a dark socket). This wires it into <see cref="LegitimacyProcessor"/>: each cycle a province's
    /// UNANSWERED demands (surfaced from its morale-factor breakdown) resolve to a legitimacy delta that is applied.
    ///
    /// Proves (a) with the gate OFF the wire is inert — legitimacy is the morale/war baseline only (byte-identical);
    /// (b) with the gate ON a province with a grievance loud enough to organise a demand takes the expected legitimacy
    /// hit; and (c) the applied delta matches <see cref="DemandResolution"/> exactly (the same demand→delta math).
    /// </summary>
    [TestFixture]
    public class PopularDemandsLegitimacyTests
    {
        [Test]
        [Description("Gate OFF → demands are inert: legitimacy is morale-only, no demand factor recorded (byte-identical).")]
        public void GateOff_DemandsAreInert()
        {
            var s = TestScenario.CreateWithColony();
            var morale = s.Colony.GetDataBlob<ColonyMoraleDB>();
            var leg = s.Colony.GetDataBlob<LegitimacyDB>();

            morale.Morale = 60.0;
            morale.Factors.Clear();
            morale.Factors["tax"] = -30.0;   // a grievance far past the demand threshold (-10)

            LegitimacyProcessor.EnablePopularDemands = false;
            LegitimacyProcessor.RecalcLegitimacy(s.Colony);

            Assert.That(leg.Legitimacy, Is.EqualTo(60.0).Within(1e-6), "gate off → legitimacy tracks morale only");
            Assert.That(leg.Factors.ContainsKey("popular_demands"), Is.False, "gate off → no demand factor recorded");
        }

        [Test]
        [Description("Gate ON → an unmet demand erodes legitimacy by exactly the DemandResolution refusal delta.")]
        public void GateOn_UnmetDemand_ErodesLegitimacy_ByTheResolutionMath()
        {
            var s = TestScenario.CreateWithColony();
            var morale = s.Colony.GetDataBlob<ColonyMoraleDB>();
            var leg = s.Colony.GetDataBlob<LegitimacyDB>();
            var gov = GovernmentTools.OwnerOf(s.Colony);   // the start faction's (all-Mid) regime

            morale.Morale = 60.0;
            morale.Factors.Clear();
            morale.Factors["tax"] = -30.0;   // Merchants organise a "Lower Taxes" demand, pressure = 30 × Mid loudness

            // Independently compute what the pillar SHOULD apply: surface the demands, refuse each. This is (c) — the
            // test's own copy of the demand→delta math, asserted against the processor's applied value below.
            var demands = DemandEngine.SurfaceDemands(morale.Factors, gov, atWar: false);
            double expectedDelta = 0.0;
            foreach (var d in demands)
                expectedDelta += DemandResolution.LegitimacyDelta(d, DemandResponse.Refuse, gov);

            Assert.That(demands, Has.Count.EqualTo(1), "the bad tax factor organises exactly one demand");
            Assert.That(expectedDelta, Is.EqualTo(-35.0).Within(1e-6),
                "refuse a pressure-30 demand under a Mid regime = -(5 base + 30 × 1.0 hardness)");

            LegitimacyProcessor.EnablePopularDemands = true;
            try
            {
                LegitimacyProcessor.RecalcLegitimacy(s.Colony);
            }
            finally
            {
                LegitimacyProcessor.EnablePopularDemands = false;   // never leak the static flag to the rest of the suite
            }

            // (b) the province takes the expected legitimacy delta from the unmet demand.
            Assert.That(leg.Legitimacy, Is.EqualTo(60.0 + expectedDelta).Within(1e-6),
                "legitimacy = morale baseline + the demand refusal delta");
            // (c) the applied delta matches DemandResolution exactly (recorded in the gauge factor).
            Assert.That(leg.Factors["popular_demands"], Is.EqualTo(expectedDelta).Within(1e-6),
                "the applied demand delta is the sum of DemandResolution.LegitimacyDelta");
        }
    }
}
