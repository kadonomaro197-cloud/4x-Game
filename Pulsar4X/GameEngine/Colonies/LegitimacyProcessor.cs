using System;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// Recomputes each province's LEGITIMACY every month from its local contentment (docs/GOVERNMENT-AND-POLITICS-
    /// DESIGN.md, task #31). This is the live-wiring of <see cref="LegitimacyDB"/>: legitimacy is DERIVED, not a
    /// parallel resource, so — like tax collection — it runs colony-side on a hotloop and reads the sibling
    /// <see cref="ColonyMoraleDB"/>. A content province is a loyal one; a miserable one drifts toward the collapse
    /// band (<see cref="LegitimacyDB.IsCollapsing"/>), the rebellion trigger (#38).
    ///
    /// NOTE (the one-hotloop-per-blob rule, gotcha L9): keyed on <see cref="LegitimacyDB"/>, NOT ColonyInfoDB
    /// (PopulationProcessor owns that) nor ColonyMoraleDB — its own blob, so no other processor is displaced. Every
    /// colony/station carries a LegitimacyDB, so this processes them all. Host-agnostic: it runs on any entity with
    /// the blob (colonies now, stations too), reading whatever morale that host has.
    ///
    /// v1 scope: the morale-only driver (the other inputs — demand track-record, war outcomes, governor competence,
    /// connectivity — stay at their neutral sentinels until each is wired). Government re-weighting of the inputs is
    /// the flagged follow-up. Defensive: never throws (a throwing hotloop crashes the game loop, gotcha L4).
    /// </summary>
    public class LegitimacyProcessor : IHotloopProcessor
    {
        public TimeSpan RunFrequency { get; } = TimeSpan.FromDays(30);
        public TimeSpan FirstRunOffset { get; } = TimeSpan.FromDays(30);
        public Type GetParameterType { get; } = typeof(LegitimacyDB);

        public void Init(Game game) { }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            RecalcLegitimacy(entity);
        }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var provinces = manager.GetAllEntitiesWithDataBlob<LegitimacyDB>();
            foreach (var province in provinces)
                RecalcLegitimacy(province);
            return provinces.Count;
        }

        /// <summary>Recompute one province's legitimacy from its current morale (the v1 driver). No-ops safely if
        /// the blob is missing; a host with no morale blob reads the neutral midpoint.</summary>
        internal static void RecalcLegitimacy(Entity province)
        {
            if (!province.TryGetDataBlob<LegitimacyDB>(out var legitimacy)) return;

            double morale = ColonyMoraleDB.Neutral;
            if (province.TryGetDataBlob<ColonyMoraleDB>(out var moraleDB))
                morale = moraleDB.Morale;

            var inputs = LegitimacyInputs.FromMorale(morale);
            inputs.WarOutcome = WarTermFor(province);   // 0 in peace; while at war, gated by militarism
            legitimacy.Legitimacy = LegitimacyDB.ComputeLegitimacy(inputs, legitimacy.Factors);
        }

        /// <summary>
        /// The war term feeding legitimacy: 0 in peacetime; while the province's OWNING FACTION is at war with
        /// anyone it's the government's <see cref="Pulsar4X.Factions.GovernmentDB.WarMoraleFactor"/> — a militarist
        /// regime takes PRIDE in war (a legitimacy bonus), a pacifist one tires of it (a penalty). This is the
        /// casus-belli militarism gate made LIVE on the province: war has a domestic price set by who you are. A
        /// faction with no <see cref="Pulsar4X.Factions.GovernmentDB"/> reads the neutral (Mid) default. Defensive:
        /// any missing game / faction / ledger reads as peace (0).
        /// </summary>
        private static double WarTermFor(Entity province)
        {
            var game = province.Manager?.Game;
            if (game == null) return 0.0;
            if (!game.Factions.TryGetValue(province.FactionOwnerID, out var faction) || faction == null || !faction.IsValid)
                return 0.0;
            if (!faction.TryGetDataBlob<Pulsar4X.Factions.DiplomacyDB>(out var dip) || !dip.IsAtWarWithAnyone())
                return 0.0;
            if (faction.TryGetDataBlob<Pulsar4X.Factions.GovernmentDB>(out var gov))
                return gov.WarMoraleFactor();
            return new Pulsar4X.Factions.GovernmentDB().WarMoraleFactor();   // no regime set → neutral Mid default
        }
    }
}
