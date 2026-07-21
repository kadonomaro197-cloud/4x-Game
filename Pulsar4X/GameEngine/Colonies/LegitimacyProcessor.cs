using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Phase gate (docs/GOVERNMENT-AND-POLITICS-DESIGN.md §Demands — the popular-demands pillar): when true, a
        /// province's UNANSWERED political demands actually erode its legitimacy each cycle. This is the LIVE CONSUMER
        /// for the previously-dark <see cref="Pulsar4X.Factions.DemandEngine"/> / <see cref="Pulsar4X.Factions.DemandResolution"/>
        /// logic (built with zero callers): the processor surfaces the province's demands and applies their resolution
        /// delta. Defaults <b>false</b> so the whole existing suite stays byte-identical until a client/test opts in —
        /// a SIBLING of <see cref="Pulsar4X.Factions.NPCDecisionProcessor.EnableIntelLedger"/> (its own flag, flippable
        /// alone). The v1 response model treats every surfaced demand as REFUSED (there is no minister/player-response
        /// wiring yet — nobody enacted it), so demands are a standing legitimacy DRAG, not a swing.
        /// </summary>
        public static bool EnablePopularDemands = false;

        /// <summary>
        /// Kill the ONE-CYCLE STALE-MORALE ECHO (Operation Earthfall A3, findings/A3-objective-flip.md seam 3). By
        /// default <see cref="RecalcLegitimacy"/> reads the sibling <see cref="ColonyMoraleDB.Morale"/> field that
        /// <see cref="PopulationProcessor"/> writes — but that field is ONE CYCLE STALE whenever this monthly hotloop
        /// happens to fire BEFORE PopulationProcessor's on the shared 30-day boundary. VERIFIED: the two are keyed to
        /// different DataBlobs (LegitimacyDB vs ColonyInfoDB) at the same frequency, and their run-order on that shared
        /// boundary derives from <c>Assembly.GetTypes()</c> (ProcessorManager.CreateProcessors → the
        /// HotLoopProcessorsNextRun iteration in ManagerSubPulse.ProcessToNextInterupt) — an order the CLR leaves
        /// UNSPECIFIED, so it cannot be relied on to put legitimacy after population (and those files are outside this
        /// slice's fence anyway). When TRUE, legitimacy instead recomputes THIS cycle's morale from the same live
        /// inputs via <see cref="PopulationProcessor.ComputeCurrentMorale"/>, so it is ORDER-INDEPENDENT and never
        /// echoes a stale trough — a transient morale dip can no longer produce a legit cliff a month late. Defaults
        /// <b>false</b> so every existing test stays byte-identical (a sibling of <see cref="EnablePopularDemands"/>,
        /// flippable alone); a later integration/client slice turns it on.
        /// </summary>
        public static bool ReadCurrentMorale = false;

        /// <summary>
        /// Debounce the rebellion trigger (Operation Earthfall A3 seam 2): by default <see cref="UpdateRebellion"/>
        /// begins a rebellion on a SINGLE collapsing read, so one transient legitimacy trough starts a revolt. When
        /// TRUE, a rebellion needs <see cref="RebellionDebounceReads"/> CONSECUTIVE monthly collapsing reads
        /// (persisted on <see cref="LegitimacyDB.ConsecutiveCollapsingReads"/>, reset by any non-collapsing read), so
        /// a one-month dip is ignored while a sustained collapse still rebels. Defaults <b>false</b> → byte-identical
        /// (existing RebellionTests keep their single-sample trigger); a later slice turns it on.
        /// </summary>
        public static bool EnableRebellionDebounce = false;

        /// <summary>Consecutive monthly collapsing reads a rebellion needs to fire under
        /// <see cref="EnableRebellionDebounce"/>. // FLAGGED balance value — the developer sets the debounce depth.</summary>
        public const int RebellionDebounceReads = 2;

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

            // The v1 driver: the province's morale. By DEFAULT read the sibling ColonyMoraleDB.Morale field that
            // PopulationProcessor writes — but that field is ONE CYCLE STALE when this hotloop fires before
            // PopulationProcessor's on the shared 30-day boundary (their order is Assembly.GetTypes()-derived, i.e.
            // unspecified — see the ReadCurrentMorale doc). ReadCurrentMorale recomputes THIS cycle's morale from the
            // same live inputs, so legitimacy is order-independent and the stale echo can't occur. Default off →
            // byte-identical to the field read (and to every existing test).
            double morale;
            if (ReadCurrentMorale)
            {
                morale = PopulationProcessor.ComputeCurrentMorale(province);
            }
            else
            {
                morale = ColonyMoraleDB.Neutral;
                if (province.TryGetDataBlob<ColonyMoraleDB>(out var moraleDB))
                    morale = moraleDB.Morale;
            }

            var inputs = LegitimacyInputs.FromMorale(morale);
            inputs.WarOutcome = WarTermFor(province);   // 0 in peace; while at war, gated by militarism
            legitimacy.Legitimacy = LegitimacyDB.ComputeLegitimacy(inputs, legitimacy.Factors);

            // Popular-demands pillar (F-C2, docs/GOVERNMENT-AND-POLITICS-DESIGN.md): the demand engine's UNANSWERED
            // demands drag legitimacy down. This is the LIVE WIRE for DemandEngine + DemandResolution (built with no
            // callers). Gated so it's byte-identical until opted in; when on, the delta is applied on top of the
            // morale/war baseline and re-clamped, with the total recorded in the Factors gauge (why, not just the number).
            if (EnablePopularDemands)
            {
                double demandDelta = DemandLegitimacyDelta(province);
                legitimacy.Legitimacy = ClampLegitimacy(legitimacy.Legitimacy + demandDelta);
                legitimacy.Factors["popular_demands"] = demandDelta;
            }

            // Legitimacy collapse drives the REBELLION state (#38): begin a rebellion (start the reaction window)
            // when it falls into the collapse band, quell it when legitimacy is restored. The window-expiry
            // resolution (secession/defection) is a later slice — this lights up the collapse hook.
            if (province.TryGetDataBlob<RebellionDB>(out var rebellion))
                UpdateRebellion(rebellion, legitimacy, legitimacy.Legitimacy, province.StarSysDateTime);
        }

        /// <summary>
        /// Drive one province's rebellion state from its current legitimacy. BEGIN a rebellion (open the reaction
        /// window) when legitimacy is in the collapse band and none is active; QUELL it when legitimacy has climbed
        /// back to <see cref="RebellionDB.RecoveryThreshold"/> (hysteresis above the collapse line). A rebellion in
        /// progress that hasn't recovered simply keeps running toward its window-expiry (the later resolution slice
        /// reads <see cref="RebellionDB.WindowExpired"/>).
        /// </summary>
        internal static void UpdateRebellion(RebellionDB rebellion, double legitimacy, DateTime now)
            => UpdateRebellion(rebellion, null, legitimacy, now);

        /// <summary>
        /// The debounce-aware form: <paramref name="legitDb"/> carries the persisted consecutive-collapsing counter.
        /// With <see cref="EnableRebellionDebounce"/> OFF (or a null <paramref name="legitDb"/>) this is byte-identical
        /// to the single-sample trigger above. With it ON, a rebellion only BEGINS once legitimacy has read collapsing
        /// for <see cref="RebellionDebounceReads"/> consecutive monthly cycles — any non-collapsing read resets the
        /// counter, so a one-month transient dip is ignored while a sustained collapse still rebels.
        /// </summary>
        internal static void UpdateRebellion(RebellionDB rebellion, LegitimacyDB legitDb, double legitimacy, DateTime now)
        {
            bool collapsing = LegitimacyDB.IsCollapsing(legitimacy);

            // Accumulate/reset the debounce counter (only when armed AND there's a blob to persist it on).
            if (EnableRebellionDebounce && legitDb != null)
            {
                if (collapsing) legitDb.ConsecutiveCollapsingReads++;
                else legitDb.ConsecutiveCollapsingReads = 0;
            }

            if (!rebellion.IsRebelling)
            {
                bool trigger = collapsing;
                if (EnableRebellionDebounce && legitDb != null)
                    trigger = legitDb.ConsecutiveCollapsingReads >= RebellionDebounceReads;
                if (trigger)
                {
                    rebellion.IsRebelling = true;
                    rebellion.StartDate = now;
                    rebellion.ReactionWindowEnds = now + TimeSpan.FromDays(RebellionDB.ReactionWindowDays);
                }
            }
            else if (legitimacy >= RebellionDB.RecoveryThreshold)
            {
                rebellion.IsRebelling = false;   // quelled — legitimacy restored within the window
                if (EnableRebellionDebounce && legitDb != null)
                    legitDb.ConsecutiveCollapsingReads = 0;
            }
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

        /// <summary>
        /// The legitimacy delta from this province's UNANSWERED popular demands (F-C2). Surfaces the province's
        /// political demands with <see cref="Pulsar4X.Factions.DemandEngine.SurfaceDemands"/> — which reads the SAME
        /// <see cref="ColonyMoraleDB.Factors"/> breakdown the morale system already computes (tax/employment/
        /// conditions/crowding), under the owning faction's government and war status — then treats each as REFUSED
        /// (no minister/player response is wired yet) and sums <see cref="Pulsar4X.Factions.DemandResolution.LegitimacyDelta"/>.
        /// Returns 0 when nothing organises a demand (a content province, or no morale blob). Defensive/no-throw
        /// (runs in the monthly hotloop, gotcha L4). Internal for the CI gauge.
        /// </summary>
        internal static double DemandLegitimacyDelta(Entity province)
        {
            var government = Pulsar4X.Factions.GovernmentTools.OwnerOf(province);

            Dictionary<string, double> moraleFactors = null;
            if (province.TryGetDataBlob<ColonyMoraleDB>(out var moraleDB))
                moraleFactors = moraleDB.Factors;

            var demands = Pulsar4X.Factions.DemandEngine.SurfaceDemands(moraleFactors, government, IsOwnerAtWar(province));

            double delta = 0.0;
            foreach (var demand in demands)
                delta += Pulsar4X.Factions.DemandResolution.LegitimacyDelta(
                    demand, Pulsar4X.Factions.DemandResponse.Refuse, government);
            return delta;
        }

        /// <summary>True if this province's owning faction is at war with anyone (the war-demand flavour switch).
        /// Defensive: any missing game / faction / ledger reads as peace.</summary>
        private static bool IsOwnerAtWar(Entity province)
        {
            var game = province.Manager?.Game;
            if (game == null) return false;
            if (!game.Factions.TryGetValue(province.FactionOwnerID, out var faction) || faction == null || !faction.IsValid)
                return false;
            return faction.TryGetDataBlob<Pulsar4X.Factions.DiplomacyDB>(out var dip) && dip.IsAtWarWithAnyone();
        }

        /// <summary>Clamp a legitimacy value into its 0..100 band (LegitimacyDB's own clamp is private).</summary>
        private static double ClampLegitimacy(double v) => v < 0.0 ? 0.0 : (v > 100.0 ? 100.0 : v);
    }
}
