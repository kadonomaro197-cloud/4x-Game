using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Colonies;
using Pulsar4X.Components;   // ComponentInstance
using Pulsar4X.Datablobs;   // ComponentInstancesDB
using Pulsar4X.Factions;    // FactionInfoDB, TransactionCategory
using Pulsar4X.Extensions;  // GetTotalCommerce

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The COMMERCE civic dial — a colony's MARKETS (components carrying <see cref="CommerceAtbDB"/>) earn trade revenue
    /// for the treasury each month (docs/Actual HTMLs Of designers/civicderived.html — the Commerce option, previously a
    /// mock-only "shelved" job). The MONEY sibling of the Medical/Recreation morale buildings and the Security legitimacy
    /// building: a NEW component attribute summed into a NEW, flag-gated INCOME wire on <see cref="ColonyEconomyProcessor"/>
    /// (booked as <see cref="TransactionCategory.ColonyCommerce"/>, distinct from population TAX and from inter-faction
    /// Trade). Flag OFF (the engine default) → byte-identical (no colony ships a market, and a market-less colony earns 0).
    /// Cradle-to-grave: designed/built → earns credits → destroyed (bombardment) drops the income. Calibration-independent
    /// — asserts RELATIONSHIPS against the market's own trade value, so a re-tune can't make it lie.
    /// </summary>
    [TestFixture]
    public class CommerceIncomeTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[commerce] " + m);

        // The flag is a process-global static — always restore it so it can't leak into the rest of the shard.
        [TearDown]
        public void ResetFlag() => ColonyEconomyProcessor.EnableCommerceIncome = false;

        [Test]
        [Description("The commerce helper is defensive — null components read 0 without throwing.")]
        public void CommerceIncome_NullComponents_IsZero()
        {
            Assert.That(ColonyEconomyProcessor.CommerceIncome(null), Is.EqualTo(0m));
        }

        [Test]
        [Description("CRADLE-TO-GRAVE: the market is a registered buildable start design (six-point registration); installing one makes GetTotalCommerce read its trade value; with the flag ON, ColonyEconomyProcessor books that as ColonyCommerce INCOME on the owning faction's ledger; with the flag OFF it books nothing (byte-identical). THE GRAVE RUNG: destroying the market drops the trade value to 0.")]
        public void Market_EarnsIncome_FlagGated_CradleToGrave()
        {
            var s = TestScenario.CreateWithColony();
            var colony = s.Colony;
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var comps = colony.GetDataBlob<ComponentInstancesDB>();

            // Registration (L6 six-point): the market is a registered buildable design on the start faction.
            Assert.That(factionInfo.ComponentDesigns.ContainsKey("default-design-market"), Is.True,
                "the market is a registered buildable start design");
            var marketDesign = factionInfo.ComponentDesigns["default-design-market"];

            // The start colony ships NO market → zero commerce (byte-identical baseline).
            Assert.That(comps.GetTotalCommerce(), Is.EqualTo(0.0), "the start colony ships no market → no commerce");

            // Install a market → GetTotalCommerce reads its trade value (1000) at full health.
            var instance = new ComponentInstance(marketDesign);
            colony.AddComponent(instance);
            Assert.That(comps.GetTotalCommerce(), Is.EqualTo(1000.0).Within(0.01), "the installed market provides its trade value");
            decimal expected = ColonyEconomyProcessor.CommerceIncome(comps);
            Log($"commerce income = {expected:N2} credits/month");
            Assert.That(expected, Is.EqualTo(1000m), "the commerce income equals the installed market's trade value");

            // Flag OFF → billing books nothing (the byte-identical default).
            ColonyEconomyProcessor.EnableCommerceIncome = false;
            ColonyEconomyProcessor.BillCommerceIncome(colony);
            Assert.That(factionInfo.Money.GetTransactionsByCategory(TransactionCategory.ColonyCommerce), Is.Empty,
                "flag off → no commerce transaction booked");

            // Flag ON → books exactly one income equal to the derived commerce.
            ColonyEconomyProcessor.EnableCommerceIncome = true;
            ColonyEconomyProcessor.BillCommerceIncome(colony);
            var billed = factionInfo.Money.GetTransactionsByCategory(TransactionCategory.ColonyCommerce);
            Assert.That(billed.Count, Is.EqualTo(1), "flag on → one commerce transaction booked");
            Assert.That(billed[0].Amount, Is.EqualTo(expected), "booked as a POSITIVE income equal to the derived commerce");
            Assert.That(billed[0].IsIncome, Is.True);

            // THE GRAVE RUNG: destroy the market (an orbital-bombardment installation-kill) → trade value drops to 0.
            // Commerce is a real, LOSABLE money source.
            comps.RemoveComponentInstance(instance);
            Assert.That(comps.GetTotalCommerce(), Is.EqualTo(0.0), "destroyed market → no commerce (the grave rung)");
            Assert.That(ColonyEconomyProcessor.CommerceIncome(comps), Is.EqualTo(0m), "no market → the treasury earns nothing");
        }
    }
}
