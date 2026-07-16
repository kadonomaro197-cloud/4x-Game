using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Fleets
{
    /// <summary>
    /// Marks a fleet entity as an auto-formed ROLE sub-fleet (Q7 sub-fleets), carrying which fighting job it plays.
    /// A hand-made player fleet never carries this blob — so it's how a later slice tells a role sub-fleet apart from
    /// a player's own fleet, and finds them (via <c>HasDataBlob&lt;FleetRoleDB&gt;()</c>) to hand each its own doctrine.
    ///
    /// This is a passive tag: NO processor is keyed to it and nothing reads it yet, so attaching the type to the
    /// assembly changes no running game (it lives on zero entities until a sub-fleet is formed). B-2c reads
    /// <see cref="Role"/> to pick each sub-fleet's doctrine.
    /// </summary>
    public class FleetRoleDB : BaseDataBlob
    {
        /// <summary>Which fighting job this sub-fleet plays (Screen / Line / Artillery / Support).</summary>
        [JsonProperty] public FleetRole Role { get; internal set; }

        public FleetRoleDB() { }
        public FleetRoleDB(FleetRole role) { Role = role; }

        // A REAL clone that copies Role (deliberately NOT FleetDB.Clone()'s blank-return, which would drop the tag).
        public override object Clone() => new FleetRoleDB(Role);
    }
}
