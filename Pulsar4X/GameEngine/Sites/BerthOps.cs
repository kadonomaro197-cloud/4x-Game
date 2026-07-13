using System;
using Pulsar4X.Engine;
using Pulsar4X.People;

namespace Pulsar4X.Sites
{
    /// <summary>
    /// Site Engine SE-2b — seating a leader in a Command Berth and reading their competence. A seated leader is what
    /// makes a berth actually work a field-site (and work it faster); an empty berth is just gear. Seating sets the
    /// berth's <see cref="CommandBerth.CommanderID"/> AND the leader's own <c>CommanderDB.AssignedTo</c> back-reference
    /// (the same shared convention the admin-seat and scientist assignments use), so the two views agree.
    ///
    /// This is the engine-level mechanism (a helper an order/UI or an NPC delegate calls); the full player order + the
    /// type-vs-role check are a later refinement. Pure-ish (mutates only the berth roster + the commander's blob).
    /// </summary>
    public static class BerthOps
    {
        /// <summary>The experience value that reads as full skill (the academy's 0–200 bell-curve ceiling). A leader's
        /// competence contribution to site work is Experience / this, clamped to 0..1.</summary>
        public const double MaxLeaderExperience = 200.0;

        /// <summary>
        /// Seat <paramref name="commander"/> in the best empty berth on <paramref name="host"/> that matches
        /// <paramref name="role"/>. Vacates any berth this leader already held on the host first (no double-seating),
        /// then sets the berth's occupant and the leader's <c>AssignedTo</c> back-reference. Returns false if the host
        /// has no roster or no empty matching berth.
        /// </summary>
        public static bool SeatLeader(Entity host, Entity commander, SiteRole role)
        {
            if (host == null || commander == null) return false;
            if (!host.TryGetDataBlob<CommandBerthDB>(out var roster)) return false;
            if (!commander.TryGetDataBlob<CommanderDB>(out var commanderDB)) return false;

            // If this leader is already in a berth on this host, free it before re-seating.
            var existing = roster.FindBerthOfCommander(commander.Id);
            if (existing != null) existing.CommanderID = -1;

            var berth = roster.BestEmptyBerthFor(role);
            if (berth == null) return false;

            berth.CommanderID = commander.Id;
            commanderDB.AssignedTo = host.Id;
            return true;
        }

        /// <summary>
        /// Free whatever berth on <paramref name="host"/> holds <paramref name="commanderId"/> (reassignment or the
        /// grave rung), clearing the leader's <c>AssignedTo</c> if it still points here. Returns true if a berth was
        /// vacated.
        /// </summary>
        public static bool VacateBerth(Entity host, int commanderId)
        {
            if (host == null || commanderId < 0) return false;
            if (!host.TryGetDataBlob<CommandBerthDB>(out var roster)) return false;

            var berth = roster.FindBerthOfCommander(commanderId);
            if (berth == null) return false;

            berth.CommanderID = -1;

            if (host.Manager != null
                && host.Manager.TryGetGlobalEntityById(commanderId, out var commander)
                && commander.TryGetDataBlob<CommanderDB>(out var commanderDB)
                && commanderDB.AssignedTo == host.Id)
            {
                commanderDB.AssignedTo = -1;
            }

            return true;
        }

        /// <summary>
        /// The seated leader's competence contribution, 0..1 — Experience / <see cref="MaxLeaderExperience"/>, clamped.
        /// A missing/unresolvable commander reads 0 (no bonus). This is the SE-2b proxy for site-work skill; a dedicated
        /// Site-work BonusCategory (the scientist/espionage-competence pattern) is a later refinement.
        /// </summary>
        public static double LeaderSkill01(EntityManager manager, int commanderId)
        {
            if (manager == null || commanderId < 0) return 0.0;
            if (!manager.TryGetGlobalEntityById(commanderId, out var commander)) return 0.0;
            if (!commander.TryGetDataBlob<CommanderDB>(out var commanderDB)) return 0.0;

            return Math.Clamp(commanderDB.Experience / MaxLeaderExperience, 0.0, 1.0);
        }
    }
}
