using Newtonsoft.Json;
using Pulsar4X.Datablobs;
using Pulsar4X.Orbital;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// Records that a fleet BROKE OFF from a battle (rather than being wiped). The auto-resolver attaches this
    /// when a fleet retreats — because it lost too large a fraction of its ships (the casualty threshold) or
    /// because it was flying a withdraw posture (a doctrine with <c>IsRetreat</c>, e.g. fighting-withdrawal).
    ///
    /// v1 is a MATH OUTCOME only: it flags the retreat and records the direction the fleet WOULD flee (away from
    /// the enemy). It does NOT issue a movement order — wiring the withdraw vector into the movement system so
    /// ships actually run is a v2 layer (docs/COMBAT-DESIGN.md System 5, "Tier 1/2"). This blob is the hook that
    /// layer will read. It persists after the engagement ends (unlike <see cref="FleetCombatStateDB"/>, which is
    /// removed on break-off), so the outcome is still visible once the fight is over.
    /// </summary>
    public class FleetRetreatDB : BaseDataBlob
    {
        /// <summary>Always true while this blob is present — its presence IS the "this fleet retreated" flag.</summary>
        [JsonProperty] public bool HasRetreated { get; internal set; } = true;

        /// <summary>Unit direction the fleet would withdraw (away from the enemy it broke off from). Zero if the
        /// fleet positions weren't available/distinct when the retreat was recorded — v1 records best-effort.</summary>
        [JsonProperty] public Vector3 RetreatVector { get; internal set; } = Vector3.Zero;

        /// <summary>Entity id of the fleet this fleet broke off from.</summary>
        [JsonProperty] public int FledFromFleetId { get; internal set; } = -1;

        public FleetRetreatDB() { }

        public FleetRetreatDB(Vector3 retreatVector, int fledFromFleetId)
        {
            RetreatVector = retreatVector;
            FledFromFleetId = fledFromFleetId;
        }

        public FleetRetreatDB(FleetRetreatDB db)
        {
            HasRetreated = db.HasRetreated;
            RetreatVector = db.RetreatVector;
            FledFromFleetId = db.FledFromFleetId;
        }

        public override object Clone() => new FleetRetreatDB(this);
    }
}
