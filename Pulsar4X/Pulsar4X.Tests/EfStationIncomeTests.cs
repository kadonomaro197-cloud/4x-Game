using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;      // ColonyInfoDB, ColonyMoraleDB, ColonyEconomyDB (the shared tax model)
using Pulsar4X.Components;    // ComponentDesign
using Pulsar4X.Engine;        // Entity
using Pulsar4X.Factions;      // FactionInfoDB, TransactionCategory
using Pulsar4X.Stations;      // StationFactory, StationEconomyDB, StationInfoDB

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Operation Earthfall D2.1 (a) — END THE STRUCTURAL BANKRUPTCY: a populated station now EARNS.
    ///
    /// Plain English: A6 found the Kithrin outpost was structurally bankrupt — its Titan station billed ~6,880/month
    /// of upkeep but collected ZERO income, so the treasury only ever fell (a monotonic drain that emptied it in a bit
    /// over a month). There was no station-income processor at all. This slice folds an INCOME half into the existing
    /// <see cref="StationUpkeepProcessor"/> (the net-operating pass) so a populated station collects population tax
    /// through the SAME model a colony uses (<see cref="ColonyEconomyDB.MonthlyTaxIncome"/>). The gauge below proves a
    /// populated industrial station is net-POSITIVE (income &gt; upkeep) — i.e. its faction balance no longer only
    /// decays — and that the income actually reaches the faction ledger, while an UNMANNED station still earns nothing
    /// (the inert case that keeps every existing station test byte-identical).
    /// </summary>
    [TestFixture]
    public class EfStationIncomeTests
    {
        // A populated industrial station carries a habitat (life support) + a factory (the "industrial" in "populated
        // industrial station"). Tax rate is set explicitly (below the Mid 0.5 government ceiling → un-capped) so the
        // gauge is robust to any tuning of the modest DEFAULT rate.
        private const long StationPop = 5_000_000;   // Kithrin-scale (Titan is 6M) — a real tax base
        private const double SetTaxRate = 0.25;      // explicit, below the 0.5 ceiling — not the default

        private static (Entity station, FactionInfoDB info) BuildPopulatedIndustrialStation(TestScenario s)
        {
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var planet = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;

            var habDesign = (ComponentDesign)factionInfo.IndustryDesigns["default-design-space-habitat"];
            var factoryDesign = (ComponentDesign)factionInfo.IndustryDesigns["default-design-factory"];

            var station = StationFactory.CreateStation(s.Faction, planet, StationPop, s.Species);
            station.AddComponent(habDesign);      // life support — a manned station is a sealed habitat
            station.AddComponent(factoryDesign);  // industry — a productive station

            station.GetDataBlob<StationEconomyDB>().TaxRate = SetTaxRate;
            return (station, factionInfo);
        }

        [Test]
        [Description("The headline (net-positive): a POPULATED industrial station's monthly INCOME exceeds its upkeep, "
                     + "so its faction balance no longer only decays. Income flows through the shared colony tax model. "
                     + "Deterministic (no sim): read at the station's creation morale.")]
        public void PopulatedStation_IsNetPositive_NotAMonotonicDrain()
        {
            var s = TestScenario.CreateWithColony();
            var (station, _) = BuildPopulatedIndustrialStation(s);

            double morale = station.GetDataBlob<ColonyMoraleDB>().Morale;

            decimal income = StationEconomyDB.MonthlyIncome(station);
            decimal cost = StationEconomyDB.OperatingCost(station);

            // Income is the SHARED colony tax model — population x the station rate x a morale multiplier.
            decimal expected = ColonyEconomyDB.MonthlyTaxIncome(StationPop, SetTaxRate, morale);
            Assert.That(income, Is.EqualTo(expected),
                "station income must route through the shared ColonyEconomyDB.MonthlyTaxIncome model");
            Assert.That(income, Is.GreaterThan(0m), "a populated station has a tax base");

            // The refutation of the A6 bankruptcy: a productive populated station is net-POSITIVE, so its balance grows
            // instead of only decaying. (Upkeep still exceeds income for a big IDLE unmanned platform — see below.)
            Assert.That(income, Is.GreaterThan(cost),
                $"a populated industrial station should be net-positive (income {income:N0} > upkeep {cost:N0})");
        }

        [Test]
        [Description("Inert case (byte-identity): an UNMANNED station has no tax base -> ZERO income, so every existing "
                     + "unmanned-station test (which asserts a monotonic upkeep drain) is unaffected.")]
        public void UnmannedStation_EarnsNothing()
        {
            var s = TestScenario.CreateWithColony();
            var planet = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;

            var station = StationFactory.CreateStation(s.Faction, planet); // default population 0 = unmanned
            Assert.That(StationEconomyDB.MonthlyIncome(station), Is.EqualTo(0m),
                "an unmanned station has no population to tax — the income pass is inert");
        }

        [Test]
        [Description("The wire, end-to-end: advancing the clock past a monthly cycle, the net-operating pass books "
                     + "station tax INCOME (the income category) into the faction ledger ALONGSIDE the upkeep expense. "
                     + "Before A6's fix the ledger had ONLY the upkeep drain (no station-income processor existed at "
                     + "all) — a monotonic bleed; now the income category is present against it.")]
        public void PopulatedStation_BooksIncomeToLedger_AlongsideUpkeep()
        {
            var s = TestScenario.CreateWithColony();
            var (_, factionInfo) = BuildPopulatedIndustrialStation(s);

            s.AdvanceTime(TimeSpan.FromDays(35)); // past the first monthly net-operating pass (billed at 30d)

            // The colony is UNTAXED by default (strain nodes are DevTest-only), so every ColonyTax entry here is the
            // STATION income the net-operating pass collected — "the ledger shows the income category". (Booked under
            // ColonyTax pending the requested dedicated StationIncome category — see LANE-DEV-NOTES.)
            decimal stationIncome = factionInfo.Money
                .GetTransactionsByCategory(TransactionCategory.ColonyTax).Sum(t => t.Amount);
            decimal upkeep = factionInfo.Money
                .GetTransactionsByCategory(TransactionCategory.StationUpkeep).Sum(t => t.Amount);

            // The A6 fix: a station-owning faction's ledger now shows a POSITIVE income category, not upkeep alone.
            // Robust to population dynamics (a 5M-pop first billing produces substantial income even if a later tick
            // crowds the station); the deterministic net-positive proof is the pure test above.
            Assert.That(stationIncome, Is.GreaterThan(0m),
                "the net-operating pass should have booked station tax INCOME into the ledger (the income category)");
            Assert.That(upkeep, Is.LessThan(0m),
                "the net-operating pass should also have billed the station's upkeep (an expense)");
        }
    }
}
