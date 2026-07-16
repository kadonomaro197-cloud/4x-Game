using System;
using Pulsar4X.Movement;       // PositionDB (active class lives in Pulsar4X.Movement, not Datablobs)
using Pulsar4X.DataStructures; // BodyType
using Pulsar4X.Engine;
using Pulsar4X.Events;         // Event, EventType, EventManager
using Pulsar4X.Extensions;     // GetDefaultName
using Pulsar4X.Factions;       // DiplomacyDB
using Pulsar4X.Galaxy;         // SystemBodyInfoDB
using Pulsar4X.Ships;          // ShipInfoDB

namespace Pulsar4X.Sensors
{
    /// <summary>
    /// Turns a sensor DETECTION into a game EVENT (the Event Logger mechanic, 2026-07-16). When a faction newly
    /// picks up an ENEMY foreign ship, this publishes a <see cref="EventType.NewHostileContact"/> event —
    /// "Enemy Fleet detected at [nearest large body]." The player's <see cref="FactionEventLog"/> is set to HALT on
    /// that type, so it pauses the clock AND drops the fast-forward step back to 1-hour steps (see FactionEventLog),
    /// letting the player react instead of blasting past the danger. Published for every faction's detections, but
    /// each faction's log only stores/halts on its OWN — so only the player (who set the halt) gets paused.
    /// </summary>
    public static class SensorEvents
    {
        /// <summary>Fire an "enemy fleet detected" event if <paramref name="detectedEntity"/> is a newly-detected
        /// enemy foreign SHIP. Called from the new-contact branch of the sensor scan. No-op for our own ships,
        /// neutrals, non-ships, and known-but-not-hostile foreigners.</summary>
        public static void OnNewShipContact(Entity detectingFaction, Entity detectedEntity, EntityManager manager, DateTime when)
        {
            if (detectingFaction == null || detectedEntity == null || manager == null) return;
            if (!detectedEntity.HasDataBlob<ShipInfoDB>()) return;                 // a "fleet" = ships

            int myId = detectingFaction.Id;
            int tgtId = detectedEntity.FactionOwnerID;
            if (tgtId == myId || tgtId == Game.NeutralFactionId) return;           // not our own, not a neutral body
            if (!IsHostile(detectingFaction, tgtId)) return;                       // enemy (or unknown foreign) only

            string body = NearestLargeBodyName(manager, detectedEntity);
            string sysId = (manager as StarSystem)?.ID;
            EventManager.Instance.Publish(Event.Create(
                EventType.NewHostileContact, when,
                $"Enemy Fleet detected at {body}.",
                factionId: myId, systemId: sysId, entityId: detectedEntity.Id));
        }

        /// <summary>v1 hostility rule: an UNKNOWN foreign faction (never met) is treated as hostile (could be anyone —
        /// worth an alert), and a KNOWN one only when AT WAR. Tighten/loosen later (e.g. add the Hostile-stance band).
        /// A faction with no <see cref="DiplomacyDB"/> at all alerts on any foreign contact.</summary>
        private static bool IsHostile(Entity detectingFaction, int otherFactionId)
        {
            if (!detectingFaction.TryGetDataBlob<DiplomacyDB>(out var dip)) return true;
            if (!dip.HasMet(otherFactionId)) return true;                          // unknown foreign military → alert
            return dip.GetRelationship(otherFactionId).AtWar;                      // known → only when at war
        }

        /// <summary>The name of the nearest LARGE body (planet or moon — not an asteroid/comet) to
        /// <paramref name="entity"/>, for the alert message. "an unknown location" if the entity has no position or
        /// the system has no named body.</summary>
        public static string NearestLargeBodyName(EntityManager manager, Entity entity)
        {
            if (manager == null || entity == null || !entity.TryGetDataBlob<PositionDB>(out var pos))
                return "an unknown location";

            var here = pos.AbsolutePosition;
            Entity best = null;
            double bestDist = double.MaxValue;
            foreach (var body in manager.GetAllEntitiesWithDataBlob<SystemBodyInfoDB>())
            {
                if (!IsLargeBody(body) || !body.TryGetDataBlob<PositionDB>(out var bpos)) continue;
                double d = (bpos.AbsolutePosition - here).Length();
                if (d < bestDist) { bestDist = d; best = body; }
            }
            return best != null ? best.GetDefaultName() : "an unknown location";
        }

        /// <summary>A "large" (notable, named) body — a planet or moon, not an asteroid/comet/unknown.</summary>
        private static bool IsLargeBody(Entity body)
        {
            if (!body.TryGetDataBlob<SystemBodyInfoDB>(out var info)) return false;
            return info.BodyType != BodyType.Unknown
                && info.BodyType != BodyType.Asteroid
                && info.BodyType != BodyType.Comet;
        }
    }
}
