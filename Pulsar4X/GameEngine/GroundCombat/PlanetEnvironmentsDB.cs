using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Hazards;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// One dynamic environmental hazard sitting on a region — the ground twin of a space `HazardEffect`. It REUSES
    /// the shared <see cref="HazardEffectType"/> vocabulary (the locked "share the vocabulary, don't refactor the
    /// hazard engine" decision), but is hosted on a REGION (by index) instead of a position in space. A fire-tornado
    /// field is `HeatDamage`; a corrosive superstorm is `CorrosiveDamage`; a dust/lightning storm is `SensorJam`; a
    /// bog/high-grav is `MovementDrag`. Plain serializable value object (parameterless + copy ctors), so it deep-clones.
    ///
    /// Magnitude semantics mirror the space hazard: for a DAMAGE effect it's per-HOUR attrition (applied scaled by
    /// the tick's game-seconds, like `SpaceHazardProcessor`); for `SensorJam`/`MovementDrag` it's a 0..1 fraction.
    /// </summary>
    public class RegionEnvironment
    {
        [JsonProperty] public int RegionIndex { get; internal set; }
        /// <summary>Player-facing name — "Fire Tornadoes", "Corrosive Superstorm" (the sci-fi flavour).</summary>
        [JsonProperty] public string Name { get; internal set; }
        /// <summary>The shared effect kind (the vocabulary shared with `GameEngine/Hazards`).</summary>
        [JsonProperty] public HazardEffectType Effect { get; internal set; }
        /// <summary>Per-hour damage (damage effects) or a 0..1 stat fraction (SensorJam / MovementDrag).</summary>
        [JsonProperty] public double Magnitude { get; internal set; }

        public RegionEnvironment() { }
        public RegionEnvironment(int regionIndex, string name, HazardEffectType effect, double magnitude)
        {
            RegionIndex = regionIndex; Name = name; Effect = effect; Magnitude = magnitude;
        }
        public RegionEnvironment(RegionEnvironment o)
        {
            RegionIndex = o.RegionIndex; Name = o.Name; Effect = o.Effect; Magnitude = o.Magnitude;
        }
    }

    /// <summary>
    /// A planet's DYNAMIC environmental hazards — the ground twin of the space-hazard layer, and the sci-fi
    /// "weather/menace" over the static geography (`PlanetRegionsDB.Features`). Attached to the planet BODY, generated
    /// from the world's PHYSICS by <see cref="PlanetEnvironmentFactory"/> (gas-giant-gated — no surface, no surface
    /// hazards). Persistent (<see cref="Clone"/> + [JsonProperty]) from day one.
    ///
    /// The INFRASTRUCTURE the developer asked for: the engine is a small generic core (this + the factory + the
    /// per-tick applier), so "more environments" is a data/rule change, never new engine code. Design:
    /// docs/ENVIRONMENTS-DESIGN.md.
    /// </summary>
    public class PlanetEnvironmentsDB : BaseDataBlob
    {
        [JsonProperty] public List<RegionEnvironment> Environments { get; internal set; } = new List<RegionEnvironment>();

        public PlanetEnvironmentsDB() { }
        public PlanetEnvironmentsDB(PlanetEnvironmentsDB o)
        {
            Environments = new List<RegionEnvironment>();
            foreach (var e in o.Environments) Environments.Add(new RegionEnvironment(e));
        }

        public override object Clone() => new PlanetEnvironmentsDB(this);

        /// <summary>The environmental hazards active in one region.</summary>
        public IEnumerable<RegionEnvironment> ForRegion(int regionIndex)
        {
            foreach (var e in Environments)
                if (e.RegionIndex == regionIndex) yield return e;
        }
    }
}
