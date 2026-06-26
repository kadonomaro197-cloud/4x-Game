using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Messaging;
using Pulsar4X.Movement;
using Pulsar4X.Names;

namespace Pulsar4X.Sensors
{
    public enum DataFrom
    {
        Parent,
        Sensors,
        Memory
    }

    public class SensorContact
    {
        public int ActualEntityId;
        public Entity ActualEntity;

        public SensorInfoDB SensorInfo;
        public SensorPositionDB Position;
        //public SensorOrbitDB Orbit;

        public string Name = "UnNamed";

        // ── UI read-only accessors ─────────────────────────────────────────────────────────────────────────
        // The client is a SEPARATE assembly and can't reach the internal detection fields (SensorInfoDB's quality
        // struct, SensorPositionDB's source flag). SensorContact lives in the engine, so it CAN — expose just what
        // the map blip needs to draw "the information you have, which varies." Computed; never serialized.
        /// <summary>TRUE once the detected entity is gone and we're coasting on its last-known ("memory") position —
        /// the grave rung: you see where it WAS, not where it is. The map draws these faded as "(last known)".</summary>
        [System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public bool PositionIsMemory => Position != null && Position.GetDataFrom == DataFrom.Memory;

        /// <summary>Latest detected signal strength (kW): the loudness of the return — falls off with distance, rises
        /// with the target's signature/EMCON. The map scales the blip by it (louder = bolder). 0 if not yet scanned.</summary>
        [System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public double SignalStrength_kW => SensorInfo != null ? SensorInfo.LatestDetectionQuality.SignalStrength_kW : 0.0;

        [JsonConstructor]
        public SensorContact() { }

        public SensorContact(Entity factionEntity, Entity actualEntity, DateTime atDateTime)
        {
            ActualEntity = actualEntity;
            ActualEntityId = actualEntity.Id;
            SensorInfo = new SensorInfoDB(factionEntity, actualEntity, atDateTime);
            Position = new SensorPositionDB(actualEntity.GetDataBlob<PositionDB>());
            var factionInfoDB = factionEntity.GetDataBlob<FactionInfoDB>();
            if (!factionInfoDB.SensorContacts.ContainsKey(actualEntity.Id))
                factionInfoDB.SensorContacts.Add(actualEntity.Id, this);
            Name = actualEntity.GetDataBlob<NameDB>().GetName(factionEntity);

            MessagePublisher.Instance.Subscribe(MessageTypes.EntityRemoved, EntityRemoved, msg => msg.EntityId != null && msg.EntityId.Value == actualEntity.Id);
        }

        async Task EntityRemoved(Message message)
        {
            await Task.Run(() => Position.GetDataFrom = DataFrom.Memory);
        }

    }
}