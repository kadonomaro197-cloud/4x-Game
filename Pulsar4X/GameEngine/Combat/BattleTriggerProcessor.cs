using System;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// The hotloop processor that drives the auto-resolve combat engine: every few game-seconds it scans each
    /// star system for hostile fleets in range and steps any battles forward (see <see cref="CombatEngagement"/>).
    ///
    /// Keyed to <see cref="StarInfoDB"/>, NOT FleetDB — FleetDB is already claimed by FleetOrderProcessor, and the
    /// processor registry allows exactly one processor per DataBlob type. Every star system has a star, so this
    /// runs once per system per tick; it returns >= 1 so the per-system scheduler never puts it to sleep (the same
    /// trick FleetOrderProcessor uses). All real work is in CombatEngagement.Tick; this class is only the hook.
    ///
    /// Auto-discovered by ProcessorManager (any IHotloopProcessor in the assembly registers itself), so the
    /// constructor is trivial — a throwing constructor would crash startup.
    /// </summary>
    public class BattleTriggerProcessor : IHotloopProcessor
    {
        public void Init(Game game) { }

        public TimeSpan RunFrequency => TimeSpan.FromSeconds(5);
        public TimeSpan FirstRunOffset => TimeSpan.FromSeconds(0);
        public Type GetParameterType => typeof(StarInfoDB);

        // All work is done per-manager in ProcessManager; nothing to do per-entity.
        public void ProcessEntity(Entity entity, int deltaSeconds) { }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            CombatEngagement.Tick(manager, deltaSeconds);
            return 1; // always >= 1 so this processor never sleeps on a system that has a star
        }
    }
}
