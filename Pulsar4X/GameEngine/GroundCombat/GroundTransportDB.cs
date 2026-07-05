using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// The ground units currently EMBARKED on a ship — the in-transit half of transport. Sits on the SHIP entity (the
    /// parallel to <see cref="GroundForcesDB"/> on a planet body: a body holds units standing on the ground, a ship
    /// holds units riding in its bays). A unit lives in exactly one place at a time — on a body's roster OR in a ship's
    /// bays — so loading MOVES the <see cref="GroundUnit"/> object out of the body's roster into <see cref="LoadedUnits"/>,
    /// and landing moves it back onto a (possibly enemy) body. The unit keeps its identity and health across the trip.
    ///
    /// Capacity isn't stored here — it's summed on demand from the ship's installed <see cref="GroundBayAtb"/> bays
    /// (like fortification), so losing a bay in combat immediately shrinks the room. Save-safe (deep-cloned units).
    /// Managed by <see cref="GroundTransport"/>. Design: docs/GROUND-COMBAT-MAP-DESIGN.md → transport.
    /// </summary>
    public class GroundTransportDB : BaseDataBlob
    {
        /// <summary>Units riding this ship's bays right now. Each is the SAME object that was on a body's roster
        /// (identity + health preserved); it re-joins a body's roster on landing.</summary>
        [JsonProperty] public List<GroundUnit> LoadedUnits { get; internal set; } = new List<GroundUnit>();

        public GroundTransportDB() { }

        public GroundTransportDB(GroundTransportDB other)
        {
            LoadedUnits = new List<GroundUnit>();
            if (other.LoadedUnits != null)
                foreach (var u in other.LoadedUnits)
                    LoadedUnits.Add(new GroundUnit(u));   // deep copy (the unit's copy ctor)
        }

        public override object Clone() => new GroundTransportDB(this);
    }
}
