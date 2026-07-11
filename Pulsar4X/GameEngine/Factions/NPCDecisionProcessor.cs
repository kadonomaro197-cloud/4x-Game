using System;
using Pulsar4X.Engine;
using Pulsar4X.Industry;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Monthly decision loop for AI-controlled factions (the Organism brain, docs/AI-BRAIN-BUILD-TRACKER.md).
    /// Each cycle it runs reactive-diplomacy drift and settles a strategic objective: read the needs-ladder
    /// (<see cref="NeedsLadder"/>) → pick an objective from tier × doctrine × <see cref="PersonalityDB"/>
    /// (<see cref="ObjectiveSelector"/>) → commit it through the hysteresis engine (<see cref="ObjectiveTransition"/>)
    /// → store it on the faction's <see cref="StrategicObjectiveDB"/>.
    ///
    /// Faction entities live in the GlobalManager, which <see cref="Engine.MasterTimePulse"/> DOES iterate (keystone
    /// fixed 2026-06-30), so this fires on its schedule (proven by FactionEconomyTests). The decision loop is live;
    /// turning the stored objective into actual ORDERS (build / expand / attack) is the follow-on (Phase 2.4c+), so
    /// the brain currently DECIDES but does not yet ACT.
    /// </summary>
    public class NPCDecisionProcessor : IHotloopProcessor
    {
        public TimeSpan RunFrequency => TimeSpan.FromDays(30);
        public TimeSpan FirstRunOffset => TimeSpan.FromDays(5);
        public Type GetParameterType => typeof(FactionInfoDB);

        /// <summary>
        /// Liveness gauge: total faction entities processed across all ProcessManager calls. Climbs only once
        /// MasterTimePulse actually iterates the GlobalManager (where faction entities live) — before the
        /// keystone fix this stayed 0 forever (the processor was registered but never reached any faction).
        /// Read by tests to prove faction-level processors fire. Not serialized; resets each process start.
        /// </summary>
        public static int TickCount;

        /// <summary>
        /// Phase-2.4c gate: when true, the Tick doesn't just DECIDE — it ACTS, emitting real orders for the settled
        /// objective (the first behaviour-changing step). Defaults <b>false</b> so every existing test is
        /// byte-identical (the brain decides but stays hands-off); the client turns it on. Mirrors the combat/economy
        /// flag pattern (`RequireDetectionToEngage`, `TradeIncomeProcessor.EnablePayout`).
        /// </summary>
        public static bool EnableOrderEmission = false;

        private Game _game;

        public void Init(Game game)
        {
            _game = game;
        }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            if (!entity.TryGetDataBlob<FactionInfoDB>(out var factionInfoDB))
                return;
            if (!factionInfoDB.IsNPC)
                return;

            Tick(entity, factionInfoDB);
        }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            int count = 0;
            foreach (var entity in manager.GetAllEntitiesWithDataBlob<FactionInfoDB>())
            {
                ProcessEntity(entity, deltaSeconds);
                count++;
            }
            TickCount += count; // liveness gauge — proves the GlobalManager is now being iterated
            return count;
        }

        /// <summary>
        /// Core NPC decision step. Evaluates doctrine weights and selects the
        /// highest-priority goal for this faction this cycle.
        /// </summary>
        private static void Tick(Entity factionEntity, FactionInfoDB factionInfoDB)
        {
            // Reactive diplomacy (docs/DIPLOMACY-DESIGN.md "Are we good?"): a faction's feelings DRIFT each cycle
            // based on what it can read of its neighbours, turning the previously-dead ReactiveDiplomacy table into
            // a live loop. Runs before the doctrine step so it happens every cycle regardless of doctrine weights.
            RunDiplomaticDrift(factionEntity);

            // The Organism decision (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II Phase 2): read the needs-ladder, pick
            // an objective from tier × doctrine × personality, and commit it through the hysteresis engine. 2.4b
            // settles + STORES the objective (the DECISION).
            UpdateStrategicObjective(factionEntity, factionInfoDB);

            // ACT on it (Phase 2.4c) — but only behind the default-off gate, so the plan-only path stays
            // byte-identical until a client/test opts in.
            if (EnableOrderEmission)
                EmitOrders(factionEntity, factionInfoDB);
        }

        /// <summary>
        /// Phase-2.4c: turn the faction's settled <see cref="StrategicObjectiveDB"/> into real orders. This slice
        /// wires the first, safest objective — <see cref="StrategicObjective.GrowEconomy"/> → queue an industry job on
        /// a colony (the same lever a player pulls). The other objectives (Defend/Consolidate/Expand/Conquer) are the
        /// follow-on slices (2.4d+); they no-op here. Defensive/no-throw (runs in a hotloop). Internal for the gauge.
        /// </summary>
        internal static void EmitOrders(Entity factionEntity, FactionInfoDB factionInfo)
        {
            if (!factionEntity.TryGetDataBlob<StrategicObjectiveDB>(out var objective)) return;

            // Phase-2.8 P0-b: the means-ends PLANNER. Look up the resolver for the settled objective, snapshot the
            // faction's state, and let the resolver name the ONE step that advances the nearest unmet prerequisite;
            // the processor runs it. Objectives with no registered resolver no-op (Expand/Conquer/Defend land later).
            if (!ObjectiveResolvers.TryGet(objective.Objective, out var resolver)) return;

            FactionState state = FactionState.Snapshot(factionEntity);
            if (state == null) return;                       // defensive (hotloop)

            PlannerAction action = resolver.Resolve(state, objective);
            objective.LastActionKind = action.Kind;          // Visibility Gate: record what the planner decided…
            objective.LastActionDetail = action.Detail;      // …before acting, so a stuck NPC is never silent
            action.Execute?.Invoke();                        // the ONE step (the only side effect)
        }

        /// <summary>
        /// SUPERSEDED by <see cref="GrowEconomyResolver"/> (Phase-2.8 P0-b) — <c>EmitOrders</c> no longer calls this;
        /// the resolver's Rung C does the same selection AND routes the build through <c>AutoAddSubJobs</c> (which this
        /// bare version lacked — it's blind to its own inputs). Retained only as the direct-mechanic gauge in
        /// <c>NPCOrderEmissionTests</c>; delete with that test in a later cleanup slice.
        ///
        /// GrowEconomy's action: queue ONE buildable design onto the first free production line on the colony that can
        /// make it (repeat on). Returns true if a job was queued. No-op (false) on a colony with no industry or
        /// nothing buildable/free.
        /// </summary>
        internal static bool TryQueueEconomyJob(Entity colony, FactionInfoDB factionInfo)
        {
            if (colony == null || !colony.TryGetDataBlob<IndustryAbilityDB>(out var industry)) return false;

            foreach (var designKvp in factionInfo.IndustryDesigns)
            {
                IConstructableDesign design = designKvp.Value;
                if (design == null) continue;

                foreach (var lineKvp in industry.ProductionLines)
                {
                    if (!lineKvp.Value.IndustryTypeRates.ContainsKey(design.IndustryTypeID)) continue;
                    if (lineKvp.Value.Jobs.Count > 0) continue;   // that line is busy — try another design/line

                    var job = new IndustryJob(factionInfo, designKvp.Key);
                    job.InitialiseJob(1, true);                   // repeat: keep the line producing
                    IndustryTools.AddJob(colony, lineKvp.Key, job);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Phase-2.4b: settle this faction's <see cref="StrategicObjectiveDB"/> for the cycle. Assess the needs-tier
        /// (<see cref="NeedsLadder"/>), select the concrete objective (<see cref="ObjectiveSelector"/> over doctrine +
        /// personality), and commit it through the transition engine (<see cref="ObjectiveTransition"/>) so the plan
        /// doesn't thrash. The blob is created on first run. Uses GAME time (not wall-clock) so it stays deterministic.
        /// Defensive: a faction with no manager/game is skipped. Internal for the CI gauge.
        /// </summary>
        internal static void UpdateStrategicObjective(Entity factionEntity, FactionInfoDB factionInfoDB)
        {
            var game = factionEntity.Manager?.Game;
            if (game == null) return;
            DateTime now = game.TimePulse.GameGlobalDateTime;

            if (!factionEntity.TryGetDataBlob<StrategicObjectiveDB>(out var objective))
            {
                objective = new StrategicObjectiveDB();
                factionEntity.SetDataBlob(objective);
            }

            NeedTier tier = NeedsLadder.AssessTier(factionEntity);
            factionEntity.TryGetDataBlob<PersonalityDB>(out var personality);   // null → neutral in the selector
            StrategicObjective chosen = ObjectiveSelector.SelectObjective(tier, factionInfoDB.Doctrine, personality);

            // Target selection (which rival to Conquer) is the 2.4c refinement; keep -1 (none) for now.
            ObjectiveTransition.Advance(objective, tier, chosen, -1, now, ObjectiveTransition.DefaultCommitFor);
        }

        /// <summary>
        /// Reactive-diplomacy DRIFT: for each faction this one has met (a relationship row exists), nudge the
        /// relationship score by the effect of what's OBSERVABLE from existing state — a militarist neighbour
        /// sours the mood; a standing treaty warms it (kept faith). The magnitudes come straight from the locked
        /// <see cref="ReactiveDiplomacy.RelationDelta"/> table (no invented numbers); the monthly cadence is
        /// <see cref="RunFrequency"/>. Conservative on purpose — drift only, no auto-proposed treaties (that's a
        /// policy call, and border-proximity reactions need a territory model neither of which exists yet).
        ///
        /// Start-safe: a relationship row only exists after first contact, so a single-faction New Game has no
        /// rows and this is inert. War is skipped (its own latched track). Internal for the CI gauge.
        /// </summary>
        internal static void RunDiplomaticDrift(Entity factionEntity)
        {
            if (factionEntity == null || !factionEntity.TryGetDataBlob<FactionInfoDB>(out var dummy)) return;
            if (!factionEntity.TryGetDataBlob<DiplomacyDB>(out var dipDB)) return;
            var game = factionEntity.Manager?.Game;
            if (game == null) return;

            foreach (var rel in dipDB.Relationships.Values)
            {
                if (rel.AtWar) continue; // war is its own latched track; drift doesn't apply while shooting

                int delta = 0;

                // A militarist neighbour sours the mood (their hawks are loud).
                if (game.Factions.TryGetValue(rel.OtherFactionId, out var otherFaction)
                    && otherFaction.TryGetDataBlob<GovernmentDB>(out var otherGov)
                    && otherGov.Militarism == GovNotch.High)
                    delta += ReactiveDiplomacy.RelationDelta(ExternalStimulus.TheirMilitaristsRose);

                // Kept faith: while a treaty stands, trust slowly accrues (deals build on themselves).
                if (rel.NonAggressionPact || rel.TradeAgreement || rel.LogisticsAccess
                    || rel.MilitaryAccess || rel.DefensivePact)
                    delta += ReactiveDiplomacy.RelationDelta(ExternalStimulus.YouHonoredTreaties);

                if (delta != 0)
                    rel.AdjustScore(delta);
            }
        }
    }
}
