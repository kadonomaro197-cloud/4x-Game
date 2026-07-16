using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.DataStructures;
using Pulsar4X.Industry;
using Pulsar4X.Storage;
using Pulsar4X.Factions;
using Pulsar4X.Components;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// A buildable planet-side BUILDING — the on-world echo of a station design. Implements
    /// <see cref="IConstructableDesign"/>, so it rides the EXISTING industry rails for free: you design it (foundation +
    /// modules) in the Entity Assembler, queue it at a colony, it consumes materials, and when the build completes its
    /// foundation + modules are INSTALLED on that colony — a multi-part building assembled from pieces, not a single
    /// installation. A "factory building" = a foundation plus machine modules; a "research campus" = a foundation plus
    /// research wings.
    ///
    /// The MODEL is build-at-home-install-on-the-colony: unlike a station (which is DEPLOYED as its own host entity) or
    /// a station carried to a star, a building's home IS the colony — so <see cref="OnConstructionComplete"/> installs
    /// each designed component directly on the building colony (the same install path a single colony installation uses,
    /// just several at once). Brand-new class — never renamed/moved, so it is save-safe forever (gotcha L3/#7 does not
    /// apply to a class that only ever gains fields).
    ///
    /// v1 registers a design at runtime via <see cref="RegisterBuildingDesign"/> (the assembler entry point, mirror of
    /// <c>StationDesign.RegisterStationDesign</c>). The base-mod <c>building-foundation</c> is six-point-registered, so a
    /// building is designable from turn one without crashing New Game.
    /// </summary>
    public class BuildingDesign : IConstructableDesign
    {
        // A building is installed on the colony by our own hook, not launched — no ship/station GUI hint.
        public ConstructableGuiHints GuiHints => ConstructableGuiHints.None;

        [JsonProperty] public string UniqueID { get; set; }
        [JsonProperty] public string Name { get; set; }
        public bool IsValid => !string.IsNullOrEmpty(UniqueID);

        [JsonProperty] public Dictionary<string, long> ResourceCosts { get; set; } = new Dictionary<string, long>();
        [JsonProperty] public long IndustryPointCosts { get; set; }
        [JsonProperty] public string IndustryTypeID { get; set; }
        public ushort OutputAmount => 1;

        /// <summary>The COMPONENTS this building is assembled from — mounted component-design ids → count (foundation +
        /// modules). Kept (like <c>StationDesign.ComponentDesignIds</c>) so the install hook can resolve them off
        /// <see cref="FactionInfoDB.IndustryDesigns"/> and install each on the building colony.</summary>
        [JsonProperty] public Dictionary<string, int> ComponentDesignIds { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// The industry processor calls this when a building build finishes. Mirrors <c>ComponentDesign.OnConstructionComplete</c>'s
        /// batch bookkeeping, but instead of installing ONE component it installs the building's foundation + every module
        /// on the building colony (the <paramref name="industryEntity"/> is the colony that ran the job). FULLY GUARDED —
        /// a missing faction / design just skips (never throws; it runs inside the daily industry hotloop, gotcha L4/#1).
        /// </summary>
        public void OnConstructionComplete(Entity industryEntity, CargoStorageDB storage, string productionLine, IndustryJob batchJob, IConstructableDesign designInfo)
        {
            batchJob.NumberCompleted++;
            batchJob.ResourcesRequiredRemaining = new Dictionary<string, long>(designInfo.ResourceCosts);
            batchJob.ProductionPointsLeft = designInfo.IndustryPointCosts;

            // Install the foundation + modules on the building colony itself. Guarded so a host without a faction / a
            // dropped design just skips — never throws.
            try
            {
                if (industryEntity != null
                    && industryEntity.Manager?.Game != null
                    && industryEntity.Manager.Game.Factions.TryGetValue(industryEntity.FactionOwnerID, out var factionEntity)
                    && factionEntity.TryGetDataBlob<FactionInfoDB>(out var factionInfo)
                    && ComponentDesignIds != null)
                {
                    foreach (var kv in ComponentDesignIds)
                    {
                        if (kv.Value < 1) continue;
                        if (!factionInfo.IndustryDesigns.TryGetValue(kv.Key, out var d) || d is not ComponentDesign moduleDesign)
                            continue;   // a module the faction no longer holds — skip it, install the rest
                        industryEntity.AddComponent(moduleDesign, kv.Value);
                    }
                }
            }
            catch { /* a failed install never crashes the industry hotloop — the job still completes cleanly below */ }

            // Job lifecycle (same as ComponentDesign / StationDesign) — guarded so a test/host without a real production
            // line is safe.
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

        /// <summary>THE ASSEMBLER ENTRY POINT (mirror of <c>StationDesign.RegisterStationDesign</c>): assemble a
        /// <paramref name="foundation"/> + <paramref name="modules"/> into a <see cref="BuildingDesign"/> whose costs are
        /// the sum of the parts, AND register it on the faction as a buildable, so it rides the normal industry rails
        /// (queue → consume materials → <see cref="OnConstructionComplete"/> installs it on the colony). Returns the
        /// registered design. Never throws. The industry line is the foundation's own
        /// <see cref="ComponentDesign.IndustryTypeID"/> (fallback <c>installation-construction</c> — the line a colony
        /// provides).</summary>
        public static BuildingDesign RegisterBuildingDesign(FactionInfoDB faction, string uniqueId, string name,
            ComponentDesign foundation, IEnumerable<(ComponentDesign design, int count)> modules)
        {
            var design = new BuildingDesign
            {
                UniqueID = uniqueId,
                Name = name,
                IndustryTypeID = string.IsNullOrEmpty(foundation?.IndustryTypeID) ? "installation-construction" : foundation.IndustryTypeID,
            };

            if (foundation != null)
            {
                AddCosts(design.ResourceCosts, foundation.ResourceCosts);
                design.IndustryPointCosts += foundation.IndustryPointCosts;
                if (!string.IsNullOrEmpty(foundation.UniqueID))
                    design.ComponentDesignIds[foundation.UniqueID] =
                        design.ComponentDesignIds.TryGetValue(foundation.UniqueID, out var fc) ? fc + 1 : 1;
            }
            if (modules != null)
            {
                foreach (var (d, c) in modules)
                {
                    if (d == null || c <= 0) continue;
                    for (int i = 0; i < c; i++) { AddCosts(design.ResourceCosts, d.ResourceCosts); design.IndustryPointCosts += d.IndustryPointCosts; }
                    if (!string.IsNullOrEmpty(d.UniqueID))
                        design.ComponentDesignIds[d.UniqueID] =
                            design.ComponentDesignIds.TryGetValue(d.UniqueID, out var mc) ? mc + c : c;
                }
            }

            if (faction != null)
                faction.IndustryDesigns[design.UniqueID] = (IConstructableDesign)design;
            return design;
        }

        private static void AddCosts(Dictionary<string, long> into, Dictionary<string, long> from)
        {
            if (from == null) return;
            foreach (var kv in from)
                into[kv.Key] = (into.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value;
        }
    }
}
