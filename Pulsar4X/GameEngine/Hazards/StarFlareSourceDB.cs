using System;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Hazards
{
    /// <summary>
    /// Marks a star as able to throw solar flares, and holds the schedule for the next one. Attached to a star
    /// entity so the star "has weather". <c>SolarFlareProcessor</c> is keyed to this DataBlob: each time it
    /// runs it checks whether the game clock has reached <see cref="NextFlareTime"/> and, if so, erupts a
    /// transient <see cref="SpaceHazardDB"/> flare near the star and schedules the next one.
    ///
    /// This is a SEPARATE marker (not <c>StarInfoDB</c>) on purpose: only one hotloop processor may key a given
    /// DataBlob type, and StarInfoDB's hotloop slot is already taken by the combat battle-trigger.
    /// </summary>
    public class StarFlareSourceDB : BaseDataBlob
    {
        /// <summary>Game-time at which the next flare erupts.</summary>
        [JsonProperty] public DateTime NextFlareTime { get; internal set; }

        /// <summary>Average game-days between flares. The actual gap is randomised around this.</summary>
        [JsonProperty] public double MeanDaysBetweenFlares { get; internal set; } = 30.0;

        /// <summary>How long a flare lasts (game-hours) from first eruption to fully faded.</summary>
        [JsonProperty] public double FlareDurationHours { get; internal set; } = 12.0;

        /// <summary>The radius (metres) a flare reaches at its peak.</summary>
        [JsonProperty] public double FlareMaxRadius_m { get; internal set; } = 1.5e10; // ~0.1 AU — a region hugging the star

        public StarFlareSourceDB() { }

        public StarFlareSourceDB(StarFlareSourceDB other)
        {
            NextFlareTime = other.NextFlareTime;
            MeanDaysBetweenFlares = other.MeanDaysBetweenFlares;
            FlareDurationHours = other.FlareDurationHours;
            FlareMaxRadius_m = other.FlareMaxRadius_m;
        }

        public override object Clone() => new StarFlareSourceDB(this);
    }
}
