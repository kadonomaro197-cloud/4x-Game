using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Ships
{
    /// <summary>
    /// Holds all the generic information about a ship
    /// </summary>
    public class ShipInfoDB : BaseDataBlob
    {

        #region Properties

        [JsonProperty]
        public int CommanderID { get; internal set; } = -1;

        [JsonProperty]
        public ShipDesign Design { get; private set; }

        [JsonProperty]
        public bool Conscript { get; set; }

        // Should we have these: ??
        [JsonProperty]
        public bool Tanker { get; set; }
        [JsonProperty]
        public bool Collier { get; set; }
        [JsonProperty]
        public bool SupplyShip { get; set; }

        /// <summary>
        /// The Ships health minus its armour and sheilds, i.e. the total HTK of all its internal Components.
        /// </summary>
        [JsonProperty]
        public int InternalHTK { get; set; }

        [JsonProperty]
        public bool IsMilitary { get; set; }

        /// <summary>
        /// M3-2b crew provenance: the Id of the colony whose manpower pool crewed this ship at build, or -1 if
        /// none (a start-fleet / DevTools ship spawned directly, or a station-built hull with no pool). A ship
        /// roams between systems, so it must REMEMBER which pool it drew from — destroy/disband releases THAT
        /// colony's crew, not "the nearest one." Set in ShipDesign.OnConstructionComplete; read in
        /// ShipFactory.DestroyShip. -1 keeps the whole path inert for ships that never drew a pool.
        /// </summary>
        [JsonProperty]
        public int CrewSourceColonyId { get; internal set; } = -1;

        //public float Tonnage { get; set; }

        //public double TCS { get {return Tonnage * 0.02;} }

        ///  Ship orders.
        //public Queue<BaseOrder> Orders;

        #endregion

        #region Constructors

        [JsonConstructor]
        private ShipInfoDB()
        {
        }

        public ShipInfoDB(ShipDesign design)
        {
            Design = design;
            //design.ID
        }

        public ShipInfoDB(ShipInfoDB shipInfoDB)
        {
            CommanderID = shipInfoDB.CommanderID;
            Conscript = shipInfoDB.Conscript;
            Tanker = shipInfoDB.Tanker;
            Collier = shipInfoDB.Collier;
            SupplyShip = shipInfoDB.SupplyShip;
            InternalHTK = shipInfoDB.InternalHTK;
            //Tonnage = shipInfoDB.Tonnage;
            IsMilitary = shipInfoDB.IsMilitary;
            CrewSourceColonyId = shipInfoDB.CrewSourceColonyId;
            /*
            if (shipInfoDB.Orders == null)
                Orders = null;
            else
                Orders = new Queue<BaseOrder>(shipInfoDB.Orders);
                */
        }

        #endregion

        public override object Clone()
        {
            return new ShipInfoDB(this);
        }

    }
}
