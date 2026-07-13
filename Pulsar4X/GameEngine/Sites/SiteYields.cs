using System;
using System.Linq;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Technology;

namespace Pulsar4X.Sites
{
    /// <summary>
    /// Site Engine SE-1c — the YIELD ROUTER (docs/SITE-ENGINE-DESIGN.md §3 Yield dial). When a site RESOLVES, its
    /// banked <see cref="FieldSiteDB.Progress"/> is paid out into the consumer system its Yield names — the "connect"
    /// that turns the located episode into a real reward the player feels in an existing system (research here; the
    /// other routes — blueprint / resource / population / leader / strategic-asset / network-route — are later slices).
    ///
    /// Pure over game state (no clock/RNG), so it's exactly testable and the processor calls it as a one-liner.
    /// </summary>
    public static class SiteYields
    {
        /// <summary>
        /// Pay <paramref name="points"/> research points to the working faction's NEAREST BREAKTHROUGH — the
        /// researchable tech with the most existing progress (an anomaly's data accelerates what you're closest to
        /// finishing), ties broken deterministically by tech id. Returns true if it landed on a tech. A faction with
        /// no researchable tech (all maxed / none present) simply banks nothing — the data had no project to help.
        /// </summary>
        public static bool DeliverResearch(Game game, int factionId, int points)
        {
            if (game == null || points <= 0) return false;
            if (!game.Factions.TryGetValue(factionId, out var faction)) return false;
            if (!faction.TryGetDataBlob<FactionInfoDB>(out var info)) return false;

            var store = info.Data;

            Tech target = null;
            foreach (var tech in store.Techs.Values)
            {
                if (tech.Level >= tech.MaxLevel) continue; // already maxed — not researchable

                if (target == null
                    || tech.ResearchProgress > target.ResearchProgress
                    || (tech.ResearchProgress == target.ResearchProgress
                        && string.CompareOrdinal(tech.UniqueID, target.UniqueID) < 0))
                {
                    target = tech;
                }
            }

            if (target == null) return false;

            store.AddTechPoints(target, points);
            return true;
        }

        /// <summary>
        /// SE-5e — the DIPLOMACY yield: a resolved diplomatic site is diplomatic capital, so it WARMS the working
        /// faction's standing with every faction it has met (a goodwill outcome). Nudges the relationship score on both
        /// ledgers (relations are stored per-side) by a delta scaled from the site's magnitude. Returns true if any
        /// relationship was warmed. A faction that has met nobody has no relations to warm → no-op (returns false).
        /// Pure over game state; fully defensive (no faction / no ledger → no-op).
        /// </summary>
        public static bool DeliverDiplomacy(Game game, int factionId, double magnitude)
        {
            if (game == null || magnitude <= 0) return false;
            if (!game.Factions.TryGetValue(factionId, out var faction)) return false;
            if (!faction.TryGetDataBlob<DiplomacyDB>(out var diplo)) return false;

            int delta = Math.Clamp((int)(magnitude / 20.0), 1, 25); // a modest, bounded goodwill nudge
            bool any = false;

            foreach (var otherId in diplo.Relationships.Keys.ToList()) // snapshot: we mutate the rows as we go
            {
                diplo.GetOrCreateRelationship(otherId).AdjustScore(delta);

                // Relations are stored per-side, so warm the OTHER faction's view of us too (symmetric goodwill).
                if (game.Factions.TryGetValue(otherId, out var other) && other.TryGetDataBlob<DiplomacyDB>(out var otherDiplo))
                    otherDiplo.GetOrCreateRelationship(factionId).AdjustScore(delta);

                any = true;
            }
            return any;
        }

        /// <summary>
        /// SE-5e — the INTEL yield: a resolved intelligence site RAISES the working faction's picture of its rivals,
        /// confirming the Military facet of every met faction in its <see cref="InformationLedgerDB"/> (Inferred →
        /// Confirmed, stamped <paramref name="when"/> for decay). Returns true if any rival's facet was confirmed.
        /// Pure over game state; fully defensive (no faction / no ledger / no met factions → no-op).
        /// </summary>
        public static bool DeliverIntel(Game game, int factionId, DateTime when, double magnitude)
        {
            if (game == null || magnitude <= 0) return false;
            if (!game.Factions.TryGetValue(factionId, out var faction)) return false;
            if (!faction.TryGetDataBlob<DiplomacyDB>(out var diplo)) return false;         // the met-faction list
            if (!faction.TryGetDataBlob<InformationLedgerDB>(out var ledger)) return false; // the intel store

            bool any = false;
            foreach (var rivalId in diplo.Relationships.Keys.ToList())
            {
                ledger.Confirm(rivalId, IntelFacet.Military, when);
                any = true;
            }
            return any;
        }
    }
}
