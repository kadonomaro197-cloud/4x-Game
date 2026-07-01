using System;
using Pulsar4X.Engine;
using Pulsar4X.Events;
using Pulsar4X.Extensions;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// First contact — the moment one faction's sensors first detect an entity belonging to ANOTHER non-neutral
    /// faction. This is the "front door" to all of external politics (docs/DIPLOMACY-DESIGN.md): you cannot have
    /// relations with an empire you have never met, so nothing in the diplomacy layer can happen until a pair has
    /// made contact. It is what turns the raw sensor blip into a KNOWN faction on your diplomatic ledger.
    ///
    /// Called from the sensor scan (<see cref="Pulsar4X.Sensors.SensorScan"/>) on a real detection. The
    /// <see cref="DiplomacyDB.HasMet"/> guard makes the side effects fire exactly ONCE per faction pair — the first
    /// foreign entity a faction ever detects is a brand-new sensor contact, so that is where two factions first
    /// "meet." Contact is recorded as MUTUAL: both sides get a Neutral relationship row (they now know of each
    /// other) — a stranger you have spotted has, in the same instant, the chance to have spotted you.
    ///
    /// v1 scope: records the relationship rows (stamped with <see cref="RelationshipState.LastContact"/>) and fires
    /// a first-contact <see cref="EventType"/> (flavoured by the derived stance — Neutral by default). The stance
    /// stays Neutral (no auto-hostility), so this changes no combat behaviour on its own; it just makes the two
    /// factions KNOWN to each other, which is the prerequisite the treaty/casus-belli layers build on.
    /// </summary>
    public static class FirstContact
    {
        /// <summary>Liveness/telemetry counter: how many first-contact meetings have been registered this game
        /// (diagnostic only, no game effect). Lets a test or a remote review confirm the wire actually fires.</summary>
        public static long ContactCount;

        /// <summary>
        /// Register that <paramref name="detectorFaction"/> (a FACTION entity) has detected
        /// <paramref name="detectedEntity"/>. If the detected entity belongs to a different non-neutral faction the
        /// detector has not met, this records a mutual Neutral relationship row on BOTH factions, stamps
        /// <see cref="RelationshipState.LastContact"/>, fires a first-contact event, and returns true. No-op
        /// (returns false) for a neutral/own-faction entity or a pair already on record. Defensive: any missing
        /// game / faction-entity / <see cref="DiplomacyDB"/> is a silent no-op — first contact never throws inside
        /// the sensor hot loop.
        /// </summary>
        public static bool OnDetection(Entity detectorFaction, Entity detectedEntity, DateTime when)
        {
            if (detectorFaction == null || detectedEntity == null) return false;

            int detectorId = detectorFaction.Id;
            int detectedOwnerId = detectedEntity.FactionOwnerID;

            // Skip neutral targets (planets, asteroids, debris, wrecks) and our own entities.
            if (detectedOwnerId == Game.NeutralFactionId || detectedOwnerId == detectorId) return false;

            var game = detectorFaction.Manager?.Game;
            if (game == null) return false;

            if (!detectorFaction.TryGetDataBlob<DiplomacyDB>(out var detectorDip)) return false;
            if (detectorDip.HasMet(detectedOwnerId)) return false;   // already met — the once-only guard

            // Record the meeting on the detector's ledger (a fresh Neutral row, stamped with the contact time).
            var relToOther = detectorDip.GetOrCreateRelationship(detectedOwnerId);
            relToOther.LastContact = when;

            // Mirror it on the OTHER faction's ledger — contact is mutual, they now know of us too (if reachable).
            Entity otherFaction = null;
            if (game.Factions.TryGetValue(detectedOwnerId, out otherFaction)
                && otherFaction != null && otherFaction.IsValid
                && otherFaction.TryGetDataBlob<DiplomacyDB>(out var otherDip)
                && !otherDip.HasMet(detectorId))
            {
                otherDip.GetOrCreateRelationship(detectorId).LastContact = when;
            }

            System.Threading.Interlocked.Increment(ref ContactCount);

            // Notify — a first-contact event flavoured by the (default Neutral) stance.
            var eventType = relToOther.CurrentStance() switch
            {
                DiplomaticStance.War or DiplomaticStance.Hostile => EventType.NewHostileContact,
                DiplomaticStance.Friendly => EventType.NewFriendlyContact,
                DiplomaticStance.Allied => EventType.NewAlliedContact,
                _ => EventType.NewNeutralContact
            };

            string detectorName = SafeName(detectorFaction, detectorId);
            string otherName = otherFaction != null ? SafeName(otherFaction, detectedOwnerId) : "an unknown faction";

            EventManager.Instance.Publish(Event.Create(
                eventType,
                when,
                $"First contact: {detectorName} has detected {otherName}.",
                detectorId,
                detectedEntity.Manager?.ManagerID,
                detectedEntity.Id));

            return true;
        }

        private static string SafeName(Entity factionEntity, int factionId)
        {
            try { return factionEntity.GetName(factionId); }
            catch { return "an unknown faction"; }
        }
    }
}
