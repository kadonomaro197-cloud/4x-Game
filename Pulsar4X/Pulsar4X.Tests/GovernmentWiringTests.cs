using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for wiring the government MODULATOR into the processors (docs/GOVERNMENT-AND-POLITICS-DESIGN.md, task
    /// #30): every faction carries a `GovernmentDB` (default all-Mid = neutral), and `GovernmentTools` is the
    /// lookup the economy/population/research processors use to read its coefficient dials. Proves the factory
    /// attaches it, the lookup resolves the owning faction's regime, and the **TaxCeiling** dial actually caps
    /// billed income end-to-end (the money wire). At the default Mid regime every coefficient is neutral, so New
    /// Game behaviour is unchanged — the dials only bite once a non-Mid regime is chosen (that FEEL is a PC-test).
    /// </summary>
    [TestFixture]
    public class GovernmentWiringTests
    {
        [Test]
        [Description("Every faction is built with a GovernmentDB (default all-Mid), and OwnerOf resolves it from a colony.")]
        public void Faction_HasGovernment_AndOwnerOfResolvesIt()
        {
            var s = TestScenario.CreateWithColony();
            Assert.That(s.Faction.HasDataBlob<GovernmentDB>(), Is.True, "the factory attaches GovernmentDB");
            var gov = GovernmentTools.OwnerOf(s.Colony);
            Assert.That(gov, Is.Not.Null);
            Assert.That(gov.Name(), Is.EqualTo("Federal Republic"), "the default all-Mid regime");
        }

        [Test]
        [Description("A missing government resolves to the neutral (all-Mid) default — coefficients are all neutral.")]
        public void MissingGovernment_ResolvesNeutralDefault()
        {
            var gov = GovernmentTools.Of(null);
            Assert.That(gov.MoraleWeight(), Is.EqualTo(1.0));
            Assert.That(gov.ResearchMultiplier(), Is.EqualTo(1.0));
            Assert.That(gov.TaxCeiling(), Is.EqualTo(0.5));
        }

        [Test]
        [Description("The TaxCeiling dial caps billed income: a low-authority regime (ceiling 0.3) bills a colony taxed at 0.8 as if it were taxed at 0.3.")]
        public void TaxCeiling_CapsBilledIncome()
        {
            var s = TestScenario.CreateWithColony();

            // Low-authority regime → TaxCeiling 0.3 (vs the Mid default 0.5).
            s.Faction.SetDataBlob(new GovernmentDB(GovNotch.Low, GovNotch.Mid, GovNotch.Mid, GovNotch.Mid));
            Assert.That(GovernmentTools.OwnerOf(s.Colony).TaxCeiling(), Is.EqualTo(0.3));

            var econ = s.Colony.GetDataBlob<ColonyEconomyDB>();
            econ.TaxRate = 0.8;   // player tries to over-tax past the ceiling

            long pop = 0;
            foreach (var kvp in s.Colony.GetDataBlob<ColonyInfoDB>().Population) pop += kvp.Value;
            double morale = s.Colony.GetDataBlob<ColonyMoraleDB>().Morale;

            var money = s.Faction.GetDataBlob<FactionInfoDB>().Money;
            decimal before = money.GetCurrentFunds();
            ColonyEconomyProcessor.CollectTax(s.Colony);
            decimal gained = money.GetCurrentFunds() - before;

            decimal expectedCapped = ColonyEconomyDB.MonthlyTaxIncome(pop, 0.3, morale);
            decimal expectedUncapped = ColonyEconomyDB.MonthlyTaxIncome(pop, 0.8, morale);
            Assert.That(gained, Is.EqualTo(expectedCapped), "billed at the capped rate");
            Assert.That(gained, Is.LessThan(expectedUncapped), "and that's less than the uncapped rate");
        }
    }
}
