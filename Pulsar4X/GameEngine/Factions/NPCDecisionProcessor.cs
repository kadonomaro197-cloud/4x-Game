using System;
using Pulsar4X.Engine;

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
            return count;
        }

        /// <summary>
        /// Core NPC decision step. Evaluates doctrine weights and selects the
        /// highest-priority goal for this faction this cycle.
        /// </summary>
        private static void Tick(Entity factionEntity, FactionInfoDB factionInfoDB)
        {
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
    }
}
