using System;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// F-C1b (docs/AI-BRAIN-BUILD-TRACKER.md, trade pillar): the monthly TRADE PAYOUT — the wire that turns the
    /// F-C1a value (<see cref="TradeIncome.MonthlyIncomeFor"/>) into real money by booking it into each faction's
    /// ledger under <see cref="TransactionCategory.Trade"/>. This is what finally gives the Trade Minister role and
    /// commerce diplomacy something to manage.
    ///
    /// Keyed on <c>DiplomacyDB</c> (no other hotloop owns that blob — landmine L9) and runs in the GlobalManager
    /// where faction entities live (now iterated, keystone #34). Auto-discovered by reflection, so — per landmine L4
    /// — the ctor is trivial and <see cref="ProcessEntity"/> never throws.
    ///
    /// GATED behind <see cref="EnablePayout"/> (default OFF) → wholly inert → byte-identical: no ledger moves until
    /// it's switched on (the client/game turns it on alongside the other economy levers, the same pattern as the
    /// combat/detection flags). When on, a faction earns <see cref="TradeIncome.PerAgreementMonthly"/> per standing
    /// trade agreement, each month.
    /// </summary>
    public class TradeIncomeProcessor : IHotloopProcessor
    {
        /// <summary>Default OFF → byte-identical. Turn on to actually pay trade income (the economy lever).</summary>
        public static bool EnablePayout = false;

        public TimeSpan RunFrequency => TimeSpan.FromDays(30);
        public TimeSpan FirstRunOffset => TimeSpan.FromDays(5);
        public Type GetParameterType => typeof(DiplomacyDB);

        private Game _game;

        public void Init(Game game) => _game = game;

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            if (!EnablePayout)
                return;
            if (entity == null || !entity.TryGetDataBlob<FactionInfoDB>(out var factionInfo))
                return;

            decimal income = TradeIncome.MonthlyIncomeFor(entity);
            if (income <= 0m)
                return;

            DateTime when = _game?.TimePulse?.GameGlobalDateTime ?? entity.Manager?.StarSysDateTime ?? default;
            factionInfo.Money.AddIncome(when, TransactionCategory.Trade, "Trade agreements", income);
        }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            int count = 0;
            foreach (var entity in manager.GetAllEntitiesWithDataBlob<DiplomacyDB>())
            {
                ProcessEntity(entity, deltaSeconds);
                count++;
            }
            return count;
        }
    }
}
