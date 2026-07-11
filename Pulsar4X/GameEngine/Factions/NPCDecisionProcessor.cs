using System;
using Pulsar4X.Engine;
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
            // an objective from tier × doctrine × personality, and commit it through the hysteresis engine. This slice
            // (2.4b) settles + STORES the objective; the per-objective ORDER emission (build/expand/attack) is the
            // follow-on (2.4c+). Storing a plan the NPC brain hasn't acted on yet is byte-identical.
            UpdateStrategicObjective(factionEntity, factionInfoDB);
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
