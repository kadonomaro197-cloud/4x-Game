using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// A RADAR / ground-sensor — a component the player designs and mounts on a ground unit to REVEAL THE MAP. Its
    /// only stat is a detection <see cref="Range_km"/> (real kilometres). Because a raised unit now carries its
    /// components as a real store (units-as-entities, Option A), the reveal ability FALLS OUT: <see cref="GroundSensors"/>
    /// finds this component on the unit's backing entity via <c>TryGetComponentsByAttribute</c> — exactly the way a ship
    /// finds its sensors — and each tick reveals the ground within reach. The real range is TRANSLATED to a hex reach on
    /// the planet map (<c>Range_km / GroundRangeTools.HexPitchKm(region)</c>), since a hex's real size differs body to
    /// body (Earth vs Io) — the developer's "translate the actual radar range into hexes" call.
    ///
    /// A component like any other (CONVENTIONS §6): designed / researched / built / mounted / lost. Inert on install
    /// (the reveal is read off the mounted component by the ground processor, not an install side-effect).
    /// </summary>
    public class GroundSensorAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>Detection range in real kilometres — how far this radar sees on the ground.</summary>
        [JsonProperty] public double Range_km { get; internal set; }

        public GroundSensorAtb() { }
        public GroundSensorAtb(double rangeKm) { Range_km = rangeKm < 0 ? 0 : rangeKm; }
        public GroundSensorAtb(GroundSensorAtb db) { Range_km = db.Range_km; }

        public override object Clone() => new GroundSensorAtb(this);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Ground Radar";
        public string AtbDescription()
            => $"Radar — a unit carrying this reveals the ground within {Range_km:0} km each tick (translated to hexes on the planet map).";
    }
}
