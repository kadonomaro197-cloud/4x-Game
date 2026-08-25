using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// A component design attribute: COMMERCE — the MARKET side of the civic loop (the Commerce / market option of the
    /// Civic door, docs/Actual HTMLs Of designers/civicderived.html, which the engine previously LACKED — it was one of
    /// the door's mock-only jobs, "shelved / out of scope" until now). Markets, exchanges, trade halls and commercial
    /// districts that turn a colony's population into local trade revenue: their combined <see cref="TradeValue"/>
    /// (credits per month) is booked as INCOME on the owning faction's ledger by <see cref="ColonyEconomyProcessor"/>,
    /// a NEW money source distinct from population TAX (<see cref="ColonyEconomyDB"/>) and from the standing
    /// inter-faction trade-agreement income (<c>TransactionCategory.Trade</c>).
    ///
    /// One dial:
    ///  • <see cref="TradeValue"/> — the monthly trade revenue (credits) this installation generates. Summed across the
    ///    colony's commerce buildings (health-scaled) and booked as <c>TransactionCategory.ColonyCommerce</c> income, so
    ///    building markets pays the treasury. A bombarded exchange trades less (the grave rung); destroying it drops the
    ///    total and the income falls.
    ///
    /// Wired behind <see cref="ColonyEconomyProcessor.EnableCommerceIncome"/> (default OFF → byte-identical), so the
    /// existing suite is unchanged until a menu game / test opts in — the exact sibling of the medical / recreation /
    /// security civic buildings, except its output is MONEY on the ledger, not a morale term. Host-agnostic (colony or
    /// station). Summed on demand, so install/uninstall need no bookkeeping (the <see cref="MedicalAtbDB"/> pattern).
    /// Cradle-to-grave: designed in the component designer (Civic ▸ Commerce) → built from materials at a colony →
    /// earns credits each month → destroyed (bombardment) drops the income.
    /// </summary>
    public class CommerceAtbDB : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>The monthly trade revenue (credits) this installation generates, before health-scaling. Summed
        /// across the colony's commerce buildings and booked as colony-commerce income.</summary>
        [JsonProperty] public double TradeValue { get; internal set; }

        public CommerceAtbDB() { }

        public CommerceAtbDB(double tradeValue)
        {
            TradeValue = tradeValue < 0 ? 0 : tradeValue;
        }

        public override object Clone() => new CommerceAtbDB(TradeValue);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance component) { }

        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Commerce";

        public string AtbDescription() => "Generates local trade revenue — a colony's markets and exchanges earn credits for the treasury each month.";
    }
}
