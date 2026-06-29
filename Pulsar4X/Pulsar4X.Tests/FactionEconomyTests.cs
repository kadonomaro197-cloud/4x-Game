using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// M4 gauge: the colony economy → faction money lever (docs/MORALE-AND-POPULATION-DESIGN.md). Proves the
    /// pure tax math, the tax-as-morale penalty, and — the integration gauge the Prime-Directive pass flagged
    /// as missing — that colony tax actually reaches the faction Ledger over time.
    /// </summary>
    [TestFixture]
    public class FactionEconomyTests
    {
        [Test]
        [Description("Monthly tax income scales up with population, tax rate, and morale; zero tax or zero pop = nothing.")]
        public void MonthlyTaxIncome_ScalesWithPopTaxMorale()
        {
            decimal baseInc = ColonyEconomyDB.MonthlyTaxIncome(1_000_000, 0.1, ColonyMoraleDB.Neutral);
            Assert.That(baseInc, Is.GreaterThan(0m));

            Assert.That(ColonyEconomyDB.MonthlyTaxIncome(2_000_000, 0.1, ColonyMoraleDB.Neutral), Is.GreaterThan(baseInc), "more people = more tax");
            Assert.That(ColonyEconomyDB.MonthlyTaxIncome(1_000_000, 0.2, ColonyMoraleDB.Neutral), Is.GreaterThan(baseInc), "higher tax = more tax");
            Assert.That(ColonyEconomyDB.MonthlyTaxIncome(1_000_000, 0.1, 100.0), Is.GreaterThan(baseInc), "a happy colony pays more willingly");

            Assert.That(ColonyEconomyDB.MonthlyTaxIncome(1_000_000, 0.0, 50.0), Is.EqualTo(0m), "no tax = no income");
            Assert.That(ColonyEconomyDB.MonthlyTaxIncome(0, 0.1, 50.0), Is.EqualTo(0m), "no people = no income");
        }

        [Test]
        [Description("Tax is a morale penalty that scales with the rate; zero tax = no morale hit.")]
        public void Tax_IsAMoralePenalty()
        {
            double noTax = ColonyMoraleDB.ComputeMorale(0.0, 0.0, -1.0, 0.0, 0.0, null);
            double someTax = ColonyMoraleDB.ComputeMorale(0.0, 0.0, -1.0, 0.0, 0.5, null);
            Assert.That(someTax, Is.LessThan(noTax), "tax should lower morale");
            Assert.That(noTax, Is.EqualTo(ColonyMoraleDB.Neutral), "zero tax (and no other factor) = neutral");

            double fullTax = ColonyMoraleDB.ComputeMorale(0.0, 0.0, -1.0, 0.0, 1.0, null);
            Assert.That(fullTax, Is.EqualTo(ColonyMoraleDB.Neutral - ColonyMoraleDB.MaxTaxPenalty).Within(0.001));
        }

        [Test]
        [Description("ColonyEconomyDB clones deeply (survives entity transfer / save-load).")]
        public void ColonyEconomyDB_ClonesDeeply()
        {
            var original = new ColonyEconomyDB { TaxRate = 0.33 };
            var clone = (ColonyEconomyDB)original.Clone();
            Assert.That(clone.TaxRate, Is.EqualTo(0.33));
            clone.TaxRate = 0.5;
            Assert.That(original.TaxRate, Is.EqualTo(0.33), "clone shares no state with the original");
        }

        [Test]
        [Description("Colony tax flows into the faction's Ledger over time (the money circuit, finally plugged in).")]
        public void Tax_FlowsColonyOutputToFactionLedger()
        {
            var s = TestScenario.CreateWithColony();
            Assert.That(s.Colony.HasDataBlob<ColonyEconomyDB>(), Is.True, "a colony should be born with a ColonyEconomyDB");
            s.Colony.GetDataBlob<ColonyEconomyDB>().TaxRate = 0.2;

            var money = s.Faction.GetDataBlob<FactionInfoDB>().Money;
            decimal taxBefore = money.GetTransactionsByCategory(TransactionCategory.ColonyTax).Sum(t => t.Amount);

            s.AdvanceTime(TimeSpan.FromDays(60)); // ~2 monthly economy ticks

            decimal taxAfter = money.GetTransactionsByCategory(TransactionCategory.ColonyTax).Sum(t => t.Amount);
            Assert.That(taxAfter, Is.GreaterThan(taxBefore), "colony tax should have added income to the faction ledger");
        }
    }
}
