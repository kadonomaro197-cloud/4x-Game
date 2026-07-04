using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// FOOTPRINT gear on a buildable installation — the component that gives a building a PRESENCE on the operational
    /// war map (a fort / spaceport / HQ), so it becomes something the ground war can capture and bomb. It's the
    /// "occupies a tile" flag from the two-zoom model (docs/GROUND-CITY-AND-WARMAP-DESIGN.md): a building carries this
    /// attribute → it lands on a specific <see cref="Galaxy.GroundHex.InstallationIds">operational hex</see> (the "ship
    /// icon"), not just the region's economy list → capturing that hex captures it, bombing that hex damages it. A
    /// Bunker / Spaceport / HQ carries it; a solar panel doesn't (it's economy, not a strategic target).
    ///
    /// A component attribute (implements <see cref="IComponentDesignAttribute"/>) so it rides the normal
    /// research/design/build/install/save rails — the reason <c>CONVENTIONS.md</c> §6 says abilities are components,
    /// and what makes the footprint cradle-to-grave (researched → built → occupies a hex → captured/bombed = lost).
    /// The footprint axis is read ON DEMAND (like <see cref="GroundDefenseAtb"/> fortification), so the
    /// install/uninstall hooks are no-ops.
    ///
    /// <see cref="TileFootprint"/> is how many FINE city-tiles this building will occupy once the City sub-grid
    /// (C-track) is built; the War-map layer (W-track) only needs the attribute's PRESENCE (a building either shows on
    /// the war map or it doesn't), so v1 stores the size for later and gates on presence.
    /// </summary>
    public class GroundFootprintAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>How many FINE city-tiles this building occupies in the City sub-grid (C-track). ≥ 1. The War-map
        /// layer only reads the attribute's PRESENCE; this is stored for the city builder to come.</summary>
        [JsonProperty] public int TileFootprint { get; internal set; } = 1;

        public GroundFootprintAtb() { }
        // double arg mirrors GroundDefenseAtb — the JSON binder feeds AtbConstrArgs(PropertyValue(...)) values as
        // doubles (NCalc), so the ctor must accept a double for the base-mod component to bind (gotcha L7).
        public GroundFootprintAtb(double tileFootprint) { TileFootprint = tileFootprint < 1 ? 1 : (int)tileFootprint; }

        public override object Clone() => new GroundFootprintAtb(TileFootprint);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Ground Footprint";
        public string AtbDescription()
            => $"A strategic building with a presence on the operational map (occupies {TileFootprint} city-tile(s)) — a capture/bombard target.";
    }
}
