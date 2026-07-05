using System;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// The C-track economy-wire reconciler — an hourly hotloop that drains the body's <see cref="GroundBuildQueueDB"/>:
    /// as each tile-targeted industry build finishes, it lays the freshly-built footprint building on the reserved
    /// mini-hex tile (via <see cref="GroundBuild.ReconcileBody"/>). Keyed on its OWN blob (<see cref="GroundBuildQueueDB"/> —
    /// no other processor owns it, landmine L9), so it processes only bodies with pending builds and sleeps otherwise.
    /// Trivial ctor + a try/catch body: a hotloop must never throw or it crashes the whole game loop (landmine L4).
    /// </summary>
    public class GroundBuildQueueProcessor : IHotloopProcessor
    {
        public TimeSpan RunFrequency { get; } = TimeSpan.FromHours(1);
        public TimeSpan FirstRunOffset { get; } = TimeSpan.FromHours(1);
        public Type GetParameterType { get; } = typeof(GroundBuildQueueDB);

        public void Init(Game game) { }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            try { GroundBuild.ReconcileBody(entity); }
            catch { /* never throw in a hotloop (L4) — a bad body is skipped, the sim keeps running */ }
        }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var bodies = manager.GetAllEntitiesWithDataBlob<GroundBuildQueueDB>();
            foreach (var body in bodies)
                ProcessEntity(body, deltaSeconds);
            return bodies.Count;
        }
    }
}
