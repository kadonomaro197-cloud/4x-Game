using System;
using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// F-C1a gauge (docs/AI-BRAIN-BUILD-TRACKER.md, the trade-money pillar): a standing trade agreement now has a
    /// monetary VALUE. Proves the income is the per-agreement value times the number of standing TradeAgreements the
    /// faction holds, and that a faction with none earns nothing. (Nothing PAYS it yet — the gated payout processor
    /// is F-C1b — so this is a pure read; the ledger is untouched until that wire lands.)
    /// </summary>
    [TestFixture]
    public class TradeIncomeTests
    {
        [Test]
        [Description("Trade income = PerAgreementMonthly × the number of standing trade agreements; none → 0.")]
        public void MonthlyIncome_CountsStandingTradeAgreements()
        {
            var s = TestScenario.CreateWithColony();
            var diplomacy = s.Faction.GetDataBlob<DiplomacyDB>();

            decimal before = TradeIncome.MonthlyIncomeFor(s.Faction);

            // Two standing trade agreements + one relationship WITHOUT one (must not count).
            diplomacy.GetOrCreateRelationship(111).TradeAgreement = true;
            diplomacy.GetOrCreateRelationship(222).TradeAgreement = true;
            diplomacy.GetOrCreateRelationship(333); // met, but no trade agreement

            Assert.That(TradeIncome.MonthlyIncomeFor(s.Faction),
                Is.EqualTo(before + 2 * TradeIncome.PerAgreementMonthly),
                "each standing trade agreement adds its monthly value; a plain relationship adds nothing");
        }

        [Test]
        [Description("F-C1b: the payout processor books trade income into the ledger only when EnablePayout is on; off → no Trade transaction (byte-identical).")]
        public void PayoutProcessor_BooksTradeIncome_OnlyWhenEnabled()
        {
            var s = TestScenario.CreateWithColony();
            var info = s.Faction.GetDataBlob<FactionInfoDB>();
            var diplomacy = s.Faction.GetDataBlob<DiplomacyDB>();
            diplomacy.GetOrCreateRelationship(111).TradeAgreement = true;
            diplomacy.GetOrCreateRelationship(222).TradeAgreement = true;

            bool wasEnabled = TradeIncomeProcessor.EnablePayout;
            try
            {
                // OFF (default): advancing past monthly cycles books NO trade income → byte-identical.
                TradeIncomeProcessor.EnablePayout = false;
                s.AdvanceTime(TimeSpan.FromDays(70));
                Assert.That(info.Money.GetTransactionsByCategory(TransactionCategory.Trade), Is.Empty,
                    "payout off → the trade agreements move no money");

                // ON: a monthly cycle books the trade income (2 agreements × the per-agreement value).
                TradeIncomeProcessor.EnablePayout = true;
                s.AdvanceTime(TimeSpan.FromDays(35));
                var tradeTxns = info.Money.GetTransactionsByCategory(TransactionCategory.Trade);
                Assert.That(tradeTxns, Is.Not.Empty, "payout on → trade income is booked");
                Assert.That(tradeTxns[0].Amount, Is.EqualTo(2 * TradeIncome.PerAgreementMonthly),
                    "each booked payout = the standing agreements × the per-agreement value");
            }
            finally
            {
                TradeIncomeProcessor.EnablePayout = wasEnabled; // restore the global so other tests stay byte-identical
            }
        }
    }
}
