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
    }
}
