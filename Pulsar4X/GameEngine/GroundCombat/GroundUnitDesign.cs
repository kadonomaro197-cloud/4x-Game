using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.DataStructures;
using Pulsar4X.Industry;
using Pulsar4X.Storage;
using Pulsar4X.Colonies;
using Pulsar4X.Galaxy;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// A buildable ground unit — the ground echo of a ship design. Implements <see cref="IConstructableDesign"/>, so
    /// it rides the EXISTING industry rails for free: you research it, queue it at a colony, it consumes materials,
    /// and when the build completes it is placed on that colony's planet (the cradle-to-grave chain, one axis over
    /// from a ship). The combat resolver (slice 5c) reads the stats a raised <see cref="GroundUnit"/> snapshots from
    /// here.
    ///
    /// v1 is a plain C# design (like the combat-test ship designs were, before their JSON registration) — a follow-up
    /// wires a base-mod JSON template so it's player-buildable in a New Game without the six-point registration
    /// crashing the start (gotcha #10). Design: docs/GROUND-COMBAT-MAP-DESIGN.md (slice 5a).
    /// </summary>
    public class GroundUnitDesign : IConstructableDesign
    {
        // A ground unit is neither installed on a ship nor launched — it's placed on the surface by our own hook.
        public ConstructableGuiHints GuiHints => ConstructableGuiHints.None;

        [JsonProperty] public string UniqueID { get; set; }
        [JsonProperty] public string Name { get; set; }
        public bool IsValid => !string.IsNullOrEmpty(UniqueID);

        [JsonProperty] public Dictionary<string, long> ResourceCosts { get; set; } = new Dictionary<string, long>();
        [JsonProperty] public long IndustryPointCosts { get; set; }
        [JsonProperty] public string IndustryTypeID { get; set; }
        public ushort OutputAmount => 1;

        // --- ground combat stats (snapshotted onto each raised unit) ---
        [JsonProperty] public GroundUnitType UnitType { get; set; } = GroundUnitType.Infantry;
        /// <summary>How this design's units cross ground (H2). Today's ground units are all <see cref="MovementDomain.Land"/>;
        /// the field lets naval/air designs opt into the water/air cost rules the pathfinder already understands.</summary>
        [JsonProperty] public MovementDomain Domain { get; set; } = MovementDomain.Land;
        /// <summary>How fast this design crosses ground, as a MULTIPLIER on the base march pace (1.0 = standard foot
        /// infantry; 2.0 = twice as fast). The developer's call: movement time depends on the UNIT's speed. Snapshotted
        /// onto the raised unit; the processor divides a step's terrain-weighted time by this. Moddable per-design.</summary>
        [JsonProperty] public double MovementSpeed { get; set; } = 1.0;
        [JsonProperty] public double Attack { get; set; }
        [JsonProperty] public double Defense { get; set; }
        [JsonProperty] public double HitPoints { get; set; }
        /// <summary>ENVIRONMENTAL GEAR (E4) — per-hazard protection this design's units carry, keyed by the shared
        /// <see cref="Pulsar4X.Hazards.HazardEffectType"/>. Value 0..1 = fraction of that hazard's attrition negated
        /// (a "heat-shielded" design has <c>{HeatDamage: 0.8}</c>). Snapshotted onto each raised <see cref="GroundUnit"/>.
        /// The cradle-to-grave counter: in the full model this is a researched/installed GEAR component (like a ship's
        /// <c>HazardResistanceAtb</c>); v1 folds it into the design (its cost already rides <see cref="ResourceCosts"/>),
        /// with gear-as-a-swappable-component the v2 promotion alongside units-as-entities.</summary>
        [JsonProperty] public Dictionary<Pulsar4X.Hazards.HazardEffectType, double> EnvironmentalResistance { get; set; }
            = new Dictionary<Pulsar4X.Hazards.HazardEffectType, double>();
        /// <summary>Which region a completed build musters into (v1: the capital region 0; a chosen muster point is a
        /// refinement). Clamped to the world's real region count at placement.</summary>
        [JsonProperty] public int DefaultRegionIndex { get; set; } = 0;

        /// <summary>
        /// The industry processor calls this when a build finishes. Mirrors <c>ComponentDesign.OnConstructionComplete</c>'s
        /// batch bookkeeping, but instead of installing a component it PLACES a ground unit on the colony's planet.
        /// Defensive: a colony with no planet / no region layer, or a missing production line, simply skips that part
        /// (never throws — it runs inside the daily industry hotloop).
        /// </summary>
        public void OnConstructionComplete(Entity industryEntity, CargoStorageDB storage, string productionLine, IndustryJob batchJob, IConstructableDesign designInfo)
        {
            batchJob.NumberCompleted++;
            batchJob.ResourcesRequiredRemaining = new Dictionary<string, long>(designInfo.ResourceCosts);
            batchJob.ProductionPointsLeft = designInfo.IndustryPointCosts;

            // Place the raised unit on the colony's planet surface (ground map).
            if (industryEntity.TryGetDataBlob<ColonyInfoDB>(out var colony)
                && colony.PlanetEntity != null && colony.PlanetEntity != Entity.InvalidEntity)
            {
                var body = colony.PlanetEntity;
                int region = DefaultRegionIndex;
                if (body.TryGetDataBlob<PlanetRegionsDB>(out var regions) && regions.Regions.Count > 0)
                    region = Math.Max(0, Math.Min(DefaultRegionIndex, regions.Regions.Count - 1));
                GroundForces.RaiseUnit(body, this, industryEntity.FactionOwnerID, region);
            }

            // Job lifecycle (same as ComponentDesign) — guarded so a test/host without a real production line is safe.
            if (batchJob.NumberCompleted >= batchJob.NumberOrdered
                && industryEntity.TryGetDataBlob<IndustryAbilityDB>(out var industryDB)
                && industryDB.ProductionLines.ContainsKey(productionLine))
            {
                industryDB.ProductionLines[productionLine].Jobs.Remove(batchJob);
                if (batchJob.Auto)
                {
                    batchJob.NumberCompleted = 0;
                    industryDB.ProductionLines[productionLine].Jobs.Add(batchJob);
                }
            }
        }
    }
}
