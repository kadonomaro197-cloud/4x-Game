using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Pulsar4X.Components;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Auth;
using Pulsar4X.Interfaces;
using Pulsar4X.Events;
using Pulsar4X.Datablobs;
using Pulsar4X.Sensors;
using Pulsar4X.Ships;
using Pulsar4X.Weapons;

namespace Pulsar4X.Factions
{
    public class FactionInfoDB : BaseDataBlob
    {
        [JsonProperty]
        public string Abbreviation { get; internal set; } = "";

        /// <summary>
        /// The unique index (0-31) for this faction used in Masked&lt;T&gt; bit masks.
        /// Use FactionMask property to get the actual bit mask value.
        /// </summary>
        [JsonProperty]
        public int FactionMaskIndex { get; internal set; } = -1;

        /// <summary>
        /// The bit mask for this faction, computed from FactionMaskIndex.
        /// Use this value with Masked&lt;T&gt;.For() to retrieve faction-visible data.
        /// </summary>
        [JsonIgnore]
        public int FactionMask => FactionMaskIndex >= 0 ? 1 << FactionMaskIndex : 0;

        [JsonProperty]
        public Ledger Money { get; internal set; } = new ();

        [JsonProperty]
        public FactionDataStore Data { get; internal set; } = new FactionDataStore();

        [JsonProperty]
        public List<Entity> Species { get; internal set; } = new ();


        [JsonProperty]
        public List<string> KnownSystems { get; internal set; } = new ();

        [JsonProperty]
        public ReadOnlyDictionary<string, List<Entity>> KnownJumpPoints => new (InternalKnownJumpPoints);
        [JsonProperty]
        internal Dictionary<string, List<Entity>> InternalKnownJumpPoints = new ();


        [JsonProperty]
        public List<Entity> KnownFactions { get; internal set; } = new ();


        [PublicAPI]
        [JsonProperty]
        public List<Entity> Colonies { get; internal set; } = new ();

        /// <summary>
        /// Space station entities owned by this faction — the parallel registry to <see cref="Colonies"/>.
        /// A station is the cheap/fast/fragile off-world host (see StationFactory / docs/SPACE-STATIONS-DESIGN.md).
        /// </summary>
        [PublicAPI]
        [JsonProperty]
        public List<Entity> Stations { get; internal set; } = new ();

        [JsonProperty]
        public SafeList<Entity> Commanders { get; internal set; } = new ();

        [JsonProperty]
        public Dictionary<string, ShipDesign> ShipDesigns = new ();

        [JsonProperty]
        public Dictionary<string, OrdnanceDesign> MissileDesigns = new ();

        /// <summary>
        /// This includes non researched and not constructible designs.
        /// Does Not Include Refined Materials
        /// </summary>
        public ReadOnlyDictionary<string, ComponentDesign> ComponentDesigns => new (InternalComponentDesigns);
        [JsonProperty]
        internal Dictionary<string, ComponentDesign> InternalComponentDesigns = new ();


        /// <summary>
        /// this shoudl only be designs we can construct.
        /// Does Include Refined Materials.
        /// </summary>
        [JsonProperty]
        public Dictionary<string, IConstructableDesign> IndustryDesigns = new ();




        /// <summary>
        /// stores sensor contacts for the entire faction, when a contact is created it gets added here.
        /// </summary>
        [JsonProperty]
        internal Dictionary<int, SensorContact> SensorContacts = new ();
        [JsonProperty]
        public Dictionary<EventType, bool> HaltsOnEvent { get; } = new ();

        [JsonProperty]
        private Dictionary<Entity, uint> FactionAccessRoles { get; set; } = new ();
        internal ReadOnlyDictionary<Entity, AccessRole> AccessRoles => new (FactionAccessRoles.ToDictionary(kvp => kvp.Key, kvp => (AccessRole)kvp.Value));

        [JsonProperty]
        public IEventLog EventLog { get; internal set; }

        /// <summary>
        /// True for AI-controlled factions. The NPCDecisionProcessor only acts on factions where this is set.
        /// </summary>
        [JsonProperty]
        public bool IsNPC { get; set; } = false;

        /// <summary>
        /// Strategic priority weights for NPC decision-making. Ignored for player factions.
        /// </summary>
        [JsonProperty]
        public DoctrineVector Doctrine { get; set; } = new DoctrineVector();

        // Per-faction FLEET-COMPOSITION ladder (the min-to-deploy / ideal / perfect sizes the AI grows its fleets to) —
        // a militarist battle-line runs bigger fleets than an expansionist raider swarm. Defaults to the shared 3/8/18
        // strike-fleet ladder; a scenario authors its own via the "fleetComposition" JSON node (FactionFactory). Stored
        // as the three numbers here (the Fleets.FleetCompositionTemplate is a plain unserialised class); FleetAssembly
        // reads them back into a template. Kept as plain ints so FactionInfoDB doesn't depend on the Fleets namespace.
        [JsonProperty] public int FleetMinToDeploy { get; set; } = 3;
        [JsonProperty] public int FleetIdealSize   { get; set; } = 8;
        [JsonProperty] public int FleetPerfectSize { get; set; } = 18;
        [JsonProperty] public string FleetTemplateName { get; set; } = "Strike Fleet";

        // Per-faction GROUND GARRISON composition (unit-type name → count) the AI fields on its home worlds — the ground
        // echo of the fleet ladder: a militarist planet-empire garrisons a heavier combined-arms legion than a light
        // frontier watch. EMPTY → GroundStartGarrison uses its engine default (3 Inf / 2 Armor / 1 Arty), so every
        // existing scenario is byte-identical. Authored via the "garrison" JSON node (FactionFactory). Plain strings so
        // FactionInfoDB doesn't depend on the GroundCombat namespace (GroundStartGarrison parses them to GroundUnitType).
        [JsonProperty] public Dictionary<string, int> GarrisonComposition { get; set; } = new();

        public FactionInfoDB()
        {
            var componentDesigns = new Dictionary<string, ComponentDesign>();
            var shipClasses = new Dictionary<string, ShipDesign>();
            SetIndustryDesigns(componentDesigns, shipClasses);
            HaltsOnEvent.Add(EventType.OrdersHalt, true);
        }

        public FactionInfoDB(
            FactionDataStore factionDataStore,
            List<Entity> species,
            List<string> knownSystems,
            List<Entity> colonies,
            Dictionary<string, ComponentDesign> componentDesigns,
            Dictionary<string, ShipDesign> shipClasses)
        {
            Data = factionDataStore;
            Species = species;
            KnownSystems = knownSystems;
            Colonies = colonies;
            InternalComponentDesigns = componentDesigns;
            ShipDesigns = shipClasses;
            KnownFactions = new List<Entity>();
            SetIndustryDesigns(componentDesigns, shipClasses);
            HaltsOnEvent.Add(EventType.OrdersHalt, true);
        }


        public FactionInfoDB(FactionInfoDB factionDB)
        {
            Data = factionDB.Data;
            Species = new List<Entity>(factionDB.Species);
            KnownSystems = new List<string>(factionDB.KnownSystems);
            KnownFactions = new List<Entity>(factionDB.KnownFactions);
            Colonies = new List<Entity>(factionDB.Colonies);
            Stations = new List<Entity>(factionDB.Stations);
            InternalKnownJumpPoints = new Dictionary<string, List<Entity>>(factionDB.KnownJumpPoints);

            ShipDesigns = new Dictionary<string, ShipDesign>(factionDB.ShipDesigns);
            InternalComponentDesigns = new Dictionary<string, ComponentDesign>(factionDB.ComponentDesigns);
            IndustryDesigns = new Dictionary<string, IConstructableDesign>(factionDB.IndustryDesigns);
            HaltsOnEvent.Add(EventType.OrdersHalt, true);

        }

        public override object Clone()
        {
            return new FactionInfoDB(this);
        }

        void SetIndustryDesigns(
            Dictionary<string, ComponentDesign> componentDesigns,
            Dictionary<string, ShipDesign> shipClasses)
        {
            foreach (var mat in Data.CargoGoods.GetMaterialsList())
            {
                IndustryDesigns[mat.UniqueID] = mat;
            }
            foreach (var design in componentDesigns)
            {
                IndustryDesigns[design.Key] = design.Value;
            }
            foreach (var design in shipClasses)
            {
                IndustryDesigns[design.Key] = design.Value;
            }
        }
    }
}