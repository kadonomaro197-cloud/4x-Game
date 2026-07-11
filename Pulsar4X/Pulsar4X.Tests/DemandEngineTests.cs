using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// F-C2b gauge (docs/GOVERNMENT-AND-POLITICS-DESIGN.md §Demands): the demand engine surfaces emergent demands
    /// from the morale-factor breakdown. Proves (a) a factor bad past the threshold surfaces the right bloc's demand
    /// with pressure = badness × bloc loudness, (b) a mild factor surfaces nothing, and (c) war surfaces a political
    /// demand whose flavour flips on the regime's militarism (hawks: Confront Rival; otherwise: End the War). Pure →
    /// byte-identical.
    /// </summary>
    [TestFixture]
    public class DemandEngineTests
    {
        [Test]
        [Description("Economic grievances surface the owning bloc's demand; pressure = badness × loudness; a mild factor surfaces nothing.")]
        public void SurfaceDemands_FromMoraleFactors()
        {
            var gov = new GovernmentDB(); // all Mid → loudness 1.0 everywhere
            var factors = new Dictionary<string, double>
            {
                { "baseline", 50.0 },
                { "tax", -20.0 },        // bad → Merchants: Lower Taxes
                { "employment", -15.0 }, // bad → Labor: Create Jobs
                { "conditions", -5.0 },  // mild (above threshold) → nothing
            };

            var demands = DemandEngine.SurfaceDemands(factors, gov, atWar: false);
            Assert.That(demands, Has.Count.EqualTo(2), "only the two factors past the threshold surface demands");

            var tax = demands.Single(d => d.Kind == DemandKind.LowerTaxes);
            Assert.That(tax.Bloc, Is.EqualTo(PoliticalBloc.Merchants));
            Assert.That(tax.Pressure, Is.EqualTo(20.0).Within(1e-9), "pressure = |factor| × Mid loudness (1.0)");

            var jobs = demands.Single(d => d.Kind == DemandKind.CreateJobs);
            Assert.That(jobs.Bloc, Is.EqualTo(PoliticalBloc.Labor));
            Assert.That(jobs.Pressure, Is.EqualTo(15.0).Within(1e-9));

            // A mild grievance (above the threshold) organises nobody.
            var mild = DemandEngine.SurfaceDemands(new Dictionary<string, double> { { "tax", -5.0 } }, gov, atWar: false);
            Assert.That(mild, Is.Empty);
        }

        [Test]
        [Description("War surfaces a political demand: hawks demand Confront Rival (louder), otherwise the public demands End the War.")]
        public void SurfaceDemands_War_FlipsOnMilitarism()
        {
            var moderate = new GovernmentDB(); // Mid militarism
            var pacifistWar = DemandEngine.SurfaceDemands(null, moderate, atWar: true);
            Assert.That(pacifistWar, Has.Count.EqualTo(1));
            Assert.That(pacifistWar[0].Kind, Is.EqualTo(DemandKind.EndTheWar));
            Assert.That(pacifistWar[0].Bloc, Is.EqualTo(PoliticalBloc.Liberty));

            var hawk = new GovernmentDB { Militarism = GovNotch.High };
            var hawkWar = DemandEngine.SurfaceDemands(null, hawk, atWar: true);
            Assert.That(hawkWar, Has.Count.EqualTo(1));
            Assert.That(hawkWar[0].Kind, Is.EqualTo(DemandKind.ConfrontRival));
            Assert.That(hawkWar[0].Bloc, Is.EqualTo(PoliticalBloc.Militarists));
            Assert.That(hawkWar[0].Pressure, Is.EqualTo(1.5).Within(1e-9), "a militarist regime's hawks push harder");

            // Not at war → no political demand.
            Assert.That(DemandEngine.SurfaceDemands(null, moderate, atWar: false), Is.Empty);
        }
    }
}
