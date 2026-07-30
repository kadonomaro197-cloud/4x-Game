using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Docking
{
    /// <summary>
    /// What is currently docked inside a carrier. Lives on the CARRIER, not on the docked ship, because the carrier is
    /// the thing with the capacity to spend and the thing that gets shot.
    ///
    /// <para><b>Ids, not references</b> — the convention for anything that must survive save/load
    /// (<c>[JsonIgnore]</c> on runtime refs, persist ids). Resolving an id back to an entity is
    /// <see cref="DockTools"/>'s job, and it tolerates an id that no longer resolves: a docked ship destroyed by a hit
    /// simply stops being found, rather than leaving a dangling reference that throws inside a hotloop.</para>
    ///
    /// <para>Absent by default. A hull with no <see cref="DockBayAtb"/> never gets one, so every existing design is
    /// untouched.</para>
    /// </summary>
    public class DockedShipsDB : BaseDataBlob
    {
        /// <summary>Entity ids of the vessels currently docked here, in the order they docked.</summary>
        [JsonProperty] public List<int> DockedShipIds { get; internal set; } = new List<int>();

        public DockedShipsDB() { }

        public DockedShipsDB(DockedShipsDB other)
        {
            DockedShipIds = new List<int>(other.DockedShipIds);
        }

        // L12: no Clone() ⇒ this blob silently becomes a bare System.Object when the carrier is moved between managers,
        // which is EXACTLY what happens when a carrier jumps systems — the one case a dock most needs to survive.
        public override object Clone() => new DockedShipsDB(this);
    }
}
