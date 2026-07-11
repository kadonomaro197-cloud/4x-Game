using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// M2-1d gauge (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II): the fourth (last Phase-1) personality→behaviour
    /// wire — Authoritarianism → tax-under-unrest. Proves that with no unrest every faction taxes at the ceiling
    /// (byte-identical), and under unrest an authoritarian faction holds taxes high (suppress) while a permissive one
    /// cuts them to appease — the personality now shapes the tax response.
    /// </summary>
    [TestFixture]
    public class PersonalityTaxTests
    {
        [Test]
        [Description("No unrest = the ceiling for everyone; under unrest, high Authoritarianism holds, low cuts to appease.")]
        public void TaxRateUnderUnrest_AuthoritarianismDecidesTheResponse()
        {
            const double ceiling = 0.5;

            var authoritarian = new PersonalityDB();
            authoritarian.SetTrait(PersonalityTrait.Authoritarianism, 1.0);
            var permissive = new PersonalityDB();
            permissive.SetTrait(PersonalityTrait.Authoritarianism, 0.0);

            // No unrest → the ceiling regardless of personality (byte-identical to "tax at the ceiling").
            Assert.That(TaxPolicy.TaxRateUnderUnrest(authoritarian, ceiling, 0.0), Is.EqualTo(ceiling).Within(1e-9));
            Assert.That(TaxPolicy.TaxRateUnderUnrest(permissive, ceiling, 0.0), Is.EqualTo(ceiling).Within(1e-9));
            Assert.That(TaxPolicy.TaxRateUnderUnrest(null, ceiling, 0.0), Is.EqualTo(ceiling).Within(1e-9));

            // Full unrest: the authoritarian holds at the ceiling, the permissive cuts all the way, neutral halves.
            Assert.That(TaxPolicy.TaxRateUnderUnrest(authoritarian, ceiling, 1.0), Is.EqualTo(ceiling).Within(1e-9),
                "a full authoritarian gives up nothing — suppress, don't appease");
            Assert.That(TaxPolicy.TaxRateUnderUnrest(permissive, ceiling, 1.0), Is.EqualTo(0.0).Within(1e-9),
                "a fully permissive faction cuts taxes to nothing to appease");
            Assert.That(TaxPolicy.TaxRateUnderUnrest(null, ceiling, 1.0), Is.EqualTo(ceiling * 0.5).Within(1e-9),
                "a neutral faction meets in the middle");

            // The ordering holds at partial unrest too: authoritarian always taxes at least as hard as permissive.
            Assert.That(TaxPolicy.TaxRateUnderUnrest(authoritarian, ceiling, 0.5),
                Is.GreaterThan(TaxPolicy.TaxRateUnderUnrest(permissive, ceiling, 0.5)));
        }
    }
}
