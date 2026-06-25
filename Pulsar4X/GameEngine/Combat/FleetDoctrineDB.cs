using System;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// A fleet's ACTIVE combat posture — the doctrine the player (or NPC) has chosen for it. Set by copying a
    /// <c>CombatDoctrineBlueprint</c> from the moddable catalog via <see cref="FleetDoctrine.TrySetDoctrine"/>.
    /// The auto-resolver reads the multipliers as a read-time modifier on this fleet's strength/toughness (the
    /// BonusesDB pattern) — nothing is baked into ship stats, so switching is reversible.
    ///
    /// NOT the same as <c>FactionInfoDB.Doctrine</c> (the strategic AI vector) — same word, different thing.
    /// </summary>
    public class FleetDoctrineDB : BaseDataBlob
    {
        [JsonProperty] public string DoctrineId { get; internal set; } = "";
        [JsonProperty] public string Family { get; internal set; } = "";
        [JsonProperty] public double FirepowerMult { get; internal set; } = 1.0;
        [JsonProperty] public double ToughnessMult { get; internal set; } = 1.0;
        [JsonProperty] public double SpeedMult { get; internal set; } = 1.0;
        [JsonProperty] public bool IsRetreat { get; internal set; } = false;

        /// <summary>Game time at/after which this fleet may switch posture again (the switch cooldown clock).</summary>
        [JsonProperty] public DateTime SwitchableAfter { get; internal set; } = DateTime.MinValue;

        public FleetDoctrineDB() { }

        public FleetDoctrineDB(FleetDoctrineDB db)
        {
            DoctrineId = db.DoctrineId;
            Family = db.Family;
            FirepowerMult = db.FirepowerMult;
            ToughnessMult = db.ToughnessMult;
            SpeedMult = db.SpeedMult;
            IsRetreat = db.IsRetreat;
            SwitchableAfter = db.SwitchableAfter;
        }

        public override object Clone() => new FleetDoctrineDB(this);
    }
}
