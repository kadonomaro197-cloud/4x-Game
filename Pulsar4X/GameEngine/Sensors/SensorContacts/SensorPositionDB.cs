using Newtonsoft.Json;
using Pulsar4X.Orbital;
using Pulsar4X.Interfaces;
using Pulsar4X.Datablobs;
using Pulsar4X.Movement;

namespace Pulsar4X.Sensors
{
    public class SensorPositionDB : BaseDataBlob, IPosition
    {
        [JsonProperty]
        internal DataFrom GetDataFrom = DataFrom.Parent;

        [JsonProperty]
        public PositionDB ActualEntityPositionDB; //the detected actual entity

        [JsonProperty]
        public PositionDB? ParentPositionDB; //detected actual entity positional parent for relative positions.

        [JsonProperty]
        public Vector3 MemoryrelativePosition_m;

        [JsonProperty]
        internal Vector3 AcuracyOffset = new Vector3();

        public Vector3 AbsolutePosition
        {
            get
            {
                if (GetDataFrom == DataFrom.Parent)
                    return ActualEntityPositionDB.AbsolutePosition;
                // Sensors = a FRESH scan snapshot (lagged one scan interval); Memory = a STALE last-known (track lost) —
                // BOTH read the frozen snapshot stored in MemoryrelativePosition_m (SetSnapshot nulls ParentPositionDB to
                // store it as an ABSOLUTE freeze), so the blip shows where the target was last SCANNED, not its live
                // position. The client tells the two apart via PositionIsMemory (Memory ⇒ fade the blip to "last known").
                // Guarded against a null parent (previously NRE'd on a deep-space contact): null ⇒ add to zero.
                return (ParentPositionDB != null ? ParentPositionDB.AbsolutePosition : new Vector3()) + MemoryrelativePosition_m;
            }
        }

        /// <summary>Freeze this contact's DRAWN position at a scanned SNAPSHOT — fog of war. Called on every successful
        /// detection: the blip then shows where the target was AT THE LAST SCAN (advancing scan-by-scan, staying put
        /// between scans) instead of the target's live real-time position — which glued the blip to the real ship and let
        /// it glide smoothly across the map even out of reach. Uses <see cref="DataFrom.Sensors"/> = a FRESH snapshot (the
        /// client renders it normally); the scan's track-loss path flips it to <see cref="DataFrom.Memory"/> = STALE (the
        /// client fades it to "last known"). Stored as an ABSOLUTE (ParentPositionDB nulled) so it can't drift or NRE.</summary>
        public void SetSnapshot(Vector3 absolutePosition)
        {
            MemoryrelativePosition_m = absolutePosition;
            ParentPositionDB = null;
            GetDataFrom = DataFrom.Sensors;
        }

        public Vector3 RelativePosition_AU
        {
            get { return Distance.MToAU(RelativePosition); }
        }

        public Vector3 RelativePosition
        {
            get
            {
                if (GetDataFrom == DataFrom.Parent)
                    return ActualEntityPositionDB.RelativePosition;
                // Sensors (fresh snapshot) + Memory (stale) both read the frozen snapshot; only Parent is live.
                return MemoryrelativePosition_m;
            }
        }


        [JsonConstructor]
        private SensorPositionDB()
        { }

        public SensorPositionDB(PositionDB actualEntityPosition, DataFrom dataFrom = DataFrom.Parent)
        {
            ActualEntityPositionDB = actualEntityPosition;
            //if(actualEntityPosition.ParentDB != null)
            ParentPositionDB = (PositionDB?)actualEntityPosition.ParentDB;
            GetDataFrom = DataFrom.Parent;
        }

        public SensorPositionDB(SensorPositionDB toClone)
        {
            GetDataFrom = toClone.GetDataFrom;
            MemoryrelativePosition_m = toClone.MemoryrelativePosition_m;
        }

        public override object Clone()
        {
            return new SensorPositionDB(this);
        }
    }
}
