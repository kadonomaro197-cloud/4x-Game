using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// A LOCOMOTION component — the drive you DESIGN and mount on a unit (units-as-entities, Option A). Fully
    /// PARAMETRIC (the developer's call): instead of picking one of a fixed menu of types, you TWEAK dials so you can
    /// build ANY locomotion for ANY environment — a cheap fast wheel for open plains, an amphibious skimmer for a water
    /// world, an all-terrain track for the mountains. The named "wheels / tracks / hover" are emergent starting points,
    /// not hardcoded types.
    ///
    /// Benefits AND costs are apparent in the dials (the designer-transparency rule — every option shows its trade in
    /// stats): faster + amphibious + all-terrain is expensive/heavy; cheap + fast is fragile in rough. Its stats FALL
    /// OUT of the unit's component store (TryGetComponentsByAttribute), exactly like the radar and chassis. A component
    /// like any other: designed / researched / built / mounted / lost; inert on install (read by movement).
    ///
    /// ⚠ The defaults here are FLAGGED tunable numbers; real values are authored per-design in the component template
    /// (sliders, like the mine's Area). Design: docs/GROUND-UNITS-AS-ENTITIES-DESIGN.md.
    /// </summary>
    public class GroundLocomotionAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>Speed multiplier on march time (1.0 = Foot baseline; higher = faster = pricier). The BENEFIT dial.</summary>
        [JsonProperty] public double SpeedFactor { get; internal set; } = 1.0;
        /// <summary>How well it crosses rough/broken ground, 0..1 (0 = wheels — penalised in rough; 1 = tracks/walker —
        /// even across rough). Drives the terrain move penalty (wired slice 5b). The all-terrain dial.</summary>
        [JsonProperty] public double RoughHandling { get; internal set; } = 0.5;
        /// <summary>Can it cross water (ocean)? A cost-adding option for water worlds (pathfinding wire is a follow-up).</summary>
        [JsonProperty] public bool Amphibious { get; internal set; } = false;

        public GroundLocomotionAtb() { }
        // double args for the JSON/NCalc binder (gotcha L7). amphibious ≥ 0.5 → true.
        public GroundLocomotionAtb(double speedFactor, double roughHandling, double amphibious)
        {
            SpeedFactor = speedFactor < 0.1 ? 0.1 : speedFactor;
            RoughHandling = roughHandling < 0 ? 0 : (roughHandling > 1 ? 1 : roughHandling);
            Amphibious = amphibious >= 0.5;
        }
        public GroundLocomotionAtb(GroundLocomotionAtb db)
        { SpeedFactor = db.SpeedFactor; RoughHandling = db.RoughHandling; Amphibious = db.Amphibious; }

        public override object Clone() => new GroundLocomotionAtb(this);
        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Locomotion";
        public string AtbDescription()
            => $"Locomotion — speed ×{SpeedFactor:0.0}, rough-terrain handling {RoughHandling * 100:0}%{(Amphibious ? ", amphibious" : "")}. Faster / all-terrain / amphibious costs more.";
    }
}
