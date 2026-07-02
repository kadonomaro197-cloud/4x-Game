using System;
using Pulsar4X.Engine;
using Pulsar4X.Events;
using Pulsar4X.Extensions;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Faction-level diplomatic ACTS — the moves that change the war state, as opposed to the per-pair data in
    /// <see cref="RelationshipState"/> or the treaty engine in <see cref="Treaties"/>. Declaring war and making
    /// peace both latch the state on BOTH sides (a war is symmetric) and fire an event. Task #33.
    ///
    /// The domestic consequence of a war is DERIVED, not applied here: <see cref="Pulsar4X.Colonies.LegitimacyProcessor"/>
    /// reads the faction's war state each cycle and feeds it into legitimacy gated by the government's militarism
    /// (see <see cref="CasusBelliRules"/>). So declaring war doesn't poke a one-time number that gets recomputed
    /// away — it flips a standing flag the derivation reads, which is why the effect sticks and compounds while the
    /// war runs. That is the casus-belli → legitimacy loop, closed.
    /// </summary>
    public static class Diplomacy
    {
        /// <summary>
        /// Declare war from <paramref name="aggressorFaction"/> on <paramref name="targetFaction"/> with a stated
        /// <paramref name="casusBelli"/>. Latches AtWar on BOTH ledgers (creating the rows if needed), fires a
        /// diplomacy event, and returns true. No-op (false) for a null/same faction or a faction missing a
        /// <see cref="DiplomacyDB"/>. The domestic morale/legitimacy cost is read live by the legitimacy processor
        /// via the militarism gate — a naked war under a pacifist regime bleeds loyalty; a justified one under a
        /// militarist regime is a source of pride.
        /// </summary>
        public static bool DeclareWar(Entity aggressorFaction, Entity targetFaction, CasusBelli casusBelli, DateTime when)
        {
            if (aggressorFaction == null || targetFaction == null || aggressorFaction == targetFaction) return false;
            if (!aggressorFaction.TryGetDataBlob<DiplomacyDB>(out var aDip)) return false;
            if (!targetFaction.TryGetDataBlob<DiplomacyDB>(out var tDip)) return false;

            aDip.GetOrCreateRelationship(targetFaction.Id).DeclareWar();   // latches AtWar + floors the score
            tDip.GetOrCreateRelationship(aggressorFaction.Id).DeclareWar();

            EventManager.Instance.Publish(Event.Create(
                EventType.Diplomacy, when,
                $"{SafeName(aggressorFaction)} declares war on {SafeName(targetFaction)} (casus belli: {casusBelli}).",
                aggressorFaction.Id, null, targetFaction.Id));
            return true;
        }

        /// <summary>
        /// End a war between the two factions — un-latches AtWar on both stored rows (leaving the score at the
        /// floor: peace is fragile by default). No-op if either faction lacks a ledger or a row for the other.
        /// Fires a diplomacy event when at least one side actually transitions out of war.
        /// </summary>
        public static bool MakePeace(Entity factionA, Entity factionB, DateTime when)
        {
            if (factionA == null || factionB == null || factionA == factionB) return false;
            if (!factionA.TryGetDataBlob<DiplomacyDB>(out var aDip)) return false;
            if (!factionB.TryGetDataBlob<DiplomacyDB>(out var bDip)) return false;

            bool changed = false;
            if (aDip.HasMet(factionB.Id) && aDip.GetRelationship(factionB.Id).AtWar) { aDip.GetRelationship(factionB.Id).MakePeace(); changed = true; }
            if (bDip.HasMet(factionA.Id) && bDip.GetRelationship(factionA.Id).AtWar) { bDip.GetRelationship(factionA.Id).MakePeace(); changed = true; }

            if (changed)
                EventManager.Instance.Publish(Event.Create(
                    EventType.Diplomacy, when,
                    $"{SafeName(factionA)} and {SafeName(factionB)} have made peace.",
                    factionA.Id, null, factionB.Id));
            return changed;
        }

        private static string SafeName(Entity faction)
        {
            try { return faction.GetName(faction.Id); } catch { return "a faction"; }
        }
    }
}
