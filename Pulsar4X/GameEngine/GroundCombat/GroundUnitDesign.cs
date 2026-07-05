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
        [JsonProperty] public double Attack { get; set; }
        [JsonProperty] public double Defense { get; set; }
        [JsonProperty] public double HitPoints { get; set; }
        /// <summary>Strike RANGE in HEXES (H3) — the max hex-distance at which this unit can hit an enemy (0 = same hex
        /// only, 1 = also the adjacent ring, 3 = out to three rings). The unit's operational REACH: a longer-ranged
        /// unit damages a shorter-ranged one as it closes, without being hit back until the enemy is in ITS range — the
        /// ground echo of the space first-strike ("clone trooper vs a zerg swarm has the advantage until they reach
        /// them"). Range is in HEXES, not real metres, because at operational scale a hex is continental and its real
        /// size differs body-to-body (Earth vs Io) — <see cref="GroundRangeTools.RealReachKm"/> converts it for the
        /// readout. 0/unset → a per-type default (<see cref="GroundRangeTools.DefaultRangeFor"/>: Infantry 1, Armor 1,
        /// Artillery 3). Moddable per design.</summary>
        [JsonProperty] public int Range { get; set; }
        /// <summary>SYSTEM ① survivability-by-dodge (0..1) — Σ augment evasion; snapshotted onto each raised unit.</summary>
        [JsonProperty] public double Evasion { get; set; }
        /// <summary>SYSTEM ① survivability-by-shield — flat incoming-damage soak pool; Σ augment shield.</summary>
        [JsonProperty] public double Shield { get; set; }
        /// <summary>SYSTEM ① primary damage flavour (from the heaviest weapon), for the future damage×defence matrix.</summary>
        [JsonProperty] public GroundWeaponMode DamageType { get; set; } = GroundWeaponMode.Ballistic;
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
