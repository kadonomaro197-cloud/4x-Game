using System;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Hazards
{
    /// <summary>
    /// The kind of space hazard. Both kinds are "a region of space that affects ships inside it" — the
    /// difference is flavour and lifecycle: a gas cloud is permanent terrain, a solar flare is a transient
    /// eruption that grows, peaks and fades.
    /// </summary>
    public enum SpaceHazardType : byte
    {
        GasCloud,
        SolarFlare,
    }

    /// <summary>
    /// A circular region in space that changes how ships inside it behave — the shared component behind both
    /// the gas cloud (other systems) and the solar flare (near the star). The region is centred on this
    /// entity's <c>PositionDB</c> and reaches out to <see cref="Radius_m"/>. Anything within that radius is
    /// "inside" the hazard.
    ///
    /// The effect knobs are deliberately generic so one component covers every hazard:
    ///   • <see cref="SensorRangeMultiplier"/> — scales how far a ship inside can SEE (1 = no effect, &lt;1 cuts).
    ///   • <see cref="MoveSpeedMultiplier"/>   — drag on sub-light (Newtonian) movement (1 = none, &lt;1 slows).
    ///   • <see cref="WarpSpeedMultiplier"/>   — scales warp speed for a ship departing inside (1 = none).
    ///   • <see cref="DamagePerSecond"/>       — hull-damage energy applied each second to ships inside.
    ///   • <see cref="BlindsSensors"/>         — hard sensor wash-out (a flare); overrides the multiplier to "blind".
    ///
    /// Query-time effects (sensors, warp) are read by their own processors via <see cref="SpaceHazardTools"/>;
    /// per-tick effects (damage, drag) are applied by <c>SpaceHazardProcessor</c>.
    /// </summary>
    public class SpaceHazardDB : BaseDataBlob
    {
        [JsonProperty] public SpaceHazardType HazardType { get; internal set; }
        [JsonProperty] public double Radius_m { get; internal set; }

        [JsonProperty] public double SensorRangeMultiplier { get; internal set; } = 1.0;
        [JsonProperty] public double MoveSpeedMultiplier { get; internal set; } = 1.0;
        [JsonProperty] public double WarpSpeedMultiplier { get; internal set; } = 1.0;
        [JsonProperty] public double DamagePerSecond { get; internal set; } = 0.0;
        [JsonProperty] public bool BlindsSensors { get; internal set; } = false;

        // Transient (solar-flare) lifecycle. A gas cloud leaves these at default (IsTransient = false) and
        // lives forever; a flare grows from a point to MaxRadius_m at its peak then fades back to nothing,
        // and is removed from the game at ExpiresAt.
        [JsonProperty] public bool IsTransient { get; internal set; } = false;
        [JsonProperty] public DateTime StartedAt { get; internal set; }
        [JsonProperty] public DateTime ExpiresAt { get; internal set; }
        [JsonProperty] public double MaxRadius_m { get; internal set; }

        public SpaceHazardDB() { }

        public SpaceHazardDB(SpaceHazardDB other)
        {
            HazardType = other.HazardType;
            Radius_m = other.Radius_m;
            SensorRangeMultiplier = other.SensorRangeMultiplier;
            MoveSpeedMultiplier = other.MoveSpeedMultiplier;
            WarpSpeedMultiplier = other.WarpSpeedMultiplier;
            DamagePerSecond = other.DamagePerSecond;
            BlindsSensors = other.BlindsSensors;
            IsTransient = other.IsTransient;
            StartedAt = other.StartedAt;
            ExpiresAt = other.ExpiresAt;
            MaxRadius_m = other.MaxRadius_m;
        }

        public override object Clone() => new SpaceHazardDB(this);
    }
}
