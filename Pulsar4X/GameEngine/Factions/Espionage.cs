using System;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.People;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Espionage E3 — the entry point that TASKS an agent on a covert op, and the counter-intel read the detection roll
    /// uses. This is the seam the UI button (E4) and the NPC mirror (E5) both call: hand it an operative, a rival, and
    /// an action, and it stamps a <see cref="CovertOpDB"/> on the agent and schedules <see cref="EspionageProcessor"/>
    /// to resolve it. The op takes real time (<see cref="OpDurationDays"/>) — spying isn't instant.
    /// </summary>
    public static class Espionage
    {
        /// <summary>How long a covert op takes to run before it resolves. Tunable balance dial.</summary>
        public const int OpDurationDays = 90;

        /// <summary>The counter-intel rating (from directorates) that reads as a full 1.0 detection-risk doubling in the
        /// <see cref="CovertRisk.Resolve"/> roll. A faction with this much counter-intel maximally hardens its soil.</summary>
        public const double CounterIntelSaturation = 100.0;

        /// <summary>
        /// Task an <paramref name="agent"/> (a <see cref="CommanderTypes.Intelligence"/> operative) on a covert op
        /// against <paramref name="targetFactionId"/>. Attaches a <see cref="CovertOpDB"/> and schedules the resolver.
        /// Returns false (no-op) if the agent is invalid, is not an operative, is already on a job, or the target is
        /// itself/neutral — so a bad task never throws in the order/UI path.
        /// </summary>
        public static bool TaskAgent(Entity agent, int targetFactionId, CovertAction action, IntelFacet facet, DateTime now)
        {
            if (agent == null || !agent.IsValid) return false;
            if (agent.HasDataBlob<CovertOpDB>()) return false;                 // already busy on an op
            if (!agent.TryGetDataBlob<CommanderDB>(out var cdb) || cdb.Type != CommanderTypes.Intelligence) return false;
            if (targetFactionId == agent.FactionOwnerID || targetFactionId == Game.NeutralFactionId) return false;

            var resolveOn = now + TimeSpan.FromDays(OpDurationDays);
            agent.SetDataBlob(new CovertOpDB
            {
                TargetFactionId = targetFactionId,
                Action = action,
                TargetFacet = facet,
                ResolveOn = resolveOn
            });
            agent.Manager.ManagerSubpulses.AddEntityInterupt(resolveOn, nameof(EspionageProcessor), agent);
            return true;
        }

        /// <summary>
        /// A faction's counter-intelligence as a 0..1 fraction for the <see cref="CovertRisk.Resolve"/> roll: the total
        /// <see cref="IntelDirectorateDB.CounterIntelRating"/> across all its colonies, divided by
        /// <see cref="CounterIntelSaturation"/> and clamped to [0, 1]. 0 for a faction with no directorates (a soft
        /// target). Higher counter-intel raises the effective detection risk of anyone spying on it — the standing
        /// defensive decision (E5's mirror).
        /// </summary>
        public static double CounterIntelOf(Game game, int factionId)
        {
            if (game == null || !game.Factions.TryGetValue(factionId, out var faction)) return 0.0;
            if (!faction.TryGetDataBlob<FactionInfoDB>(out var factionInfo)) return 0.0;

            double rating = 0.0;
            foreach (var colony in factionInfo.Colonies)
                if (colony.TryGetDataBlob<IntelDirectorateDB>(out var directorate))
                    rating += directorate.CounterIntelRating;

            double frac = rating / CounterIntelSaturation;
            return frac < 0.0 ? 0.0 : (frac > 1.0 ? 1.0 : frac);
        }
    }
}
