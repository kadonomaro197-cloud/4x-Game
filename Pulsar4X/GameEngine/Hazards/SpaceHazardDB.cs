using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Hazards
{
    /// <summary>
    /// The kind of space hazard — drives only flavour and display (map colour) and the transient lifecycle.
    /// What the hazard actually DOES is the typed <see cref="SpaceHazardDB.Effects"/> list, not this.
    /// </summary>
    public enum SpaceHazardType : byte
    {
        GasCloud,
        SolarFlare,
        StarCorona,
        Generic,        // anything authored from JSON that isn't one of the named flavours
    }

    /// <summary>
    /// A circular region in space that affects ships inside it — the shared component behind every hazard
    /// (gas cloud, solar flare, star corona, and anything authored from JSON). The region is centred on this
    /// entity's <c>PositionDB</c> and reaches <see cref="Radius_m"/>.
    ///
    /// What it does is a LIST of typed <see cref="HazardEffect"/>s (damage at a wavelength, sensor jam, drag,
    /// warp inhibit, …). This is the "spine": a hazard is just a bag of typed effects (data), and a resistance
    /// component (<see cref="HazardResistanceAtb"/>) counters effects by kind (data) — so a vast variety of
    /// hazards and counters is content, not engine code.
    /// </summary>
    public class SpaceHazardDB : BaseDataBlob
    {
        [JsonProperty] public SpaceHazardType HazardType { get; internal set; }
        [JsonProperty] public double Radius_m { get; internal set; }
        [JsonProperty] public List<HazardEffect> Effects { get; internal set; } = new List<HazardEffect>();

        // Transient (solar-flare) lifecycle. A permanent hazard (gas cloud / corona) leaves these at default
        // and lives forever; a flare grows from a point to MaxRadius_m at its peak, fades, and is removed at ExpiresAt.
        [JsonProperty] public bool IsTransient { get; internal set; } = false;
        [JsonProperty] public DateTime StartedAt { get; internal set; }
        [JsonProperty] public DateTime ExpiresAt { get; internal set; }
        [JsonProperty] public double MaxRadius_m { get; internal set; }

        public SpaceHazardDB() { }

        public SpaceHazardDB(SpaceHazardDB other)
        {
            HazardType = other.HazardType;
            Radius_m = other.Radius_m;
            Effects = other.Effects?.Select(e => new HazardEffect(e)).ToList() ?? new List<HazardEffect>();
            IsTransient = other.IsTransient;
            StartedAt = other.StartedAt;
            ExpiresAt = other.ExpiresAt;
            MaxRadius_m = other.MaxRadius_m;
        }

        public override object Clone() => new SpaceHazardDB(this);

        // ── Convenience reads over the effect list (one source of truth) ─────────────────────────────────────

        /// <summary>The combined multiplier for a stat-multiplier effect kind (SensorJam/MovementDrag/WarpInhibit):
        /// the product of every matching effect's magnitude. 1.0 if the hazard has no such effect.</summary>
        public double MultiplierFor(HazardEffectType kind)
        {
            double mult = 1.0;
            if (Effects == null) return mult;
            foreach (var e in Effects)
                if (e.Type == kind)
                    mult *= e.Magnitude;
            return mult;
        }

        /// <summary>True if this hazard fully blinds sensors (a SensorJam effect with magnitude ≤ 0).</summary>
        public bool BlindsSensors => Effects != null && Effects.Any(e => e.Type == HazardEffectType.SensorJam && e.Magnitude <= 0.0);
    }
}
