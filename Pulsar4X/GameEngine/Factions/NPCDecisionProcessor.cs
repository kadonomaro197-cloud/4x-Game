using System;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Monthly decision loop for AI-controlled factions.
    /// Uses DoctrineVector weights to pick the highest-priority goal each cycle.
    ///
    /// NOTE: Faction entities live in the GlobalManager, which is currently not
    /// iterated by MasterTimePulse's per-system loop. This processor skeleton
    /// is wired and structurally correct; a future session must either register
    /// the GlobalManager with MasterTimePulse or trigger Tick() directly from
    /// a game-level monthly event.
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

            var doc = factionInfoDB.Doctrine;

            // Determine the dominant doctrine axis this cycle.
            float maxWeight = Math.Max(doc.Economic, Math.Max(doc.Military, Math.Max(doc.Tech, doc.Expansion)));

            if (maxWeight <= 0f)
                return;

            // TODO: translate dominant axis into actual orders:
            //   Economic  -> prioritize colony construction / refinery queues
            //   Military  -> prioritize ship builds / weapon research
            //   Tech      -> prioritize research lab staffing / funding
            //   Expansion -> prioritize survey ships / colonization orders
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
