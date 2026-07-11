using Pulsar4X.Engine;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// F-C1a (docs/AI-BRAIN-BUILD-TRACKER.md, Movement I — the trade-money pillar): the value of a faction's standing
    /// TRADE AGREEMENTS, expressed as monthly income. Until now trade earned nothing — a signed `TradeAgreement`
    /// warmed relations but moved no money, so the Trade Minister role (and commerce diplomacy) had nothing real to
    /// manage. This is the missing money side, as a pure read.
    ///
    /// v1 is deliberately simple: a flat value per standing agreement (a flagged balance dial). Scaling by the
    /// relationship score, each partner's economy (<see cref="FactionRollup"/>), or a real goods flow is a later
    /// refinement. Pure/read-only → byte-identical; nothing PAYS it yet (the gated trade-payout processor is F-C1b),
    /// so no ledger changes until that wire lands.
    /// </summary>
    public static class TradeIncome
    {
        /// <summary>What one standing trade agreement is worth per month, to each partner. Flagged balance dial.</summary>
        public const decimal PerAgreementMonthly = 1000m;

        /// <summary>
        /// The monthly trade income a faction earns from every standing <see cref="RelationshipState.TradeAgreement"/>
        /// in its diplomatic ledger. 0 for a faction with no diplomacy blob or no trade agreements.
        /// </summary>
        public static decimal MonthlyIncomeFor(Entity faction)
        {
            if (faction == null || !faction.TryGetDataBlob<DiplomacyDB>(out var diplomacy))
                return 0m;

            decimal total = 0m;
            foreach (var relationship in diplomacy.Relationships.Values)
            {
                if (relationship != null && relationship.TradeAgreement)
                    total += PerAgreementMonthly;
            }
            return total;
        }
    }
}
