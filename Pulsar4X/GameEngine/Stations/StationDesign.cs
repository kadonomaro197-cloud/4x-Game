using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.DataStructures;
using Pulsar4X.Industry;
using Pulsar4X.Storage;
using Pulsar4X.Colonies;
using Pulsar4X.Factions;
using Pulsar4X.Components;

namespace Pulsar4X.Stations
{
    /// <summary>
    /// A buildable SPACE STATION — the off-world echo of a ship design and the ground <c>GroundUnitDesign</c>.
    /// Implements <see cref="IConstructableDesign"/>, so it rides the EXISTING industry rails for free: you design
    /// it (chassis + modules) in the Entity Assembler, queue it at a colony, it consumes materials, and when the
    /// build completes it is DEPLOYED as a real station at the building colony's body with its designed modules
    /// installed on it.
    ///
    /// The MODEL is build-then-deploy (locked): industry constructs the station AS A JOB at a colony (exactly like
    /// a bunker or a ground unit), and <see cref="OnConstructionComplete"/> is the deploy step — it calls
    /// <see cref="StationFactory.CreateStation"/> to place the station at the colony's planet, then installs each
    /// designed module component on the new station (the same install path a colony/ship uses). This is a
    /// brand-new class — never renamed or moved, so it is save-safe forever (gotcha L3/#7 does not apply to a
    /// class that only ever gains fields).
    ///
    /// v1 registers a design at runtime via <see cref="RegisterStationDesign"/> (the assembler entry point, mirror
    /// of <c>GroundUnitAssembly.RegisterAssembledDesign</c>); a follow-up wires the designer UI. The chassis is a
    /// six-point-registered base-mod component (<see cref="StationChassisAtb"/>), so a station is designable from
    /// turn one without crashing New Game.
    /// </summary>
    public class StationDesign : IConstructableDesign
    {
        // A station is neither installed on a host nor launched — it is DEPLOYED by our own construction hook.
        public ConstructableGuiHints GuiHints => ConstructableGuiHints.None;

        [JsonProperty] public string UniqueID { get; set; }
        [JsonProperty] public string Name { get; set; }
        public bool IsValid => !string.IsNullOrEmpty(UniqueID);

        [JsonProperty] public Dictionary<string, long> ResourceCosts { get; set; } = new Dictionary<string, long>();
        [JsonProperty] public long IndustryPointCosts { get; set; }
        [JsonProperty] public string IndustryTypeID { get; set; }
        public ushort OutputAmount => 1;

        /// <summary>The COMPONENTS this station is assembled from — mounted component-design ids → count (chassis +
        /// modules). Kept (like <c>GroundUnitDesign.ComponentDesignIds</c>) so the deploy hook can resolve them off
        /// <see cref="FactionInfoDB.IndustryDesigns"/> and install each module on the freshly deployed station.</summary>
        [JsonProperty] public Dictionary<string, int> ComponentDesignIds { get; set; } = new Dictionary<string, int>();

        /// <summary>Population the deployed station is manned with (0 = an automated platform, the default). A manned
        /// station needs habitat modules in its component list or it becomes a tomb (see StationPopulationProcessor).</summary>
        [JsonProperty] public long InitialPopulation { get; set; }

        /// <summary>
        /// The industry processor calls this when a station build finishes. Mirrors <c>ComponentDesign.OnConstructionComplete</c>'s
        /// batch bookkeeping, but instead of installing a component it DEPLOYS a station at the colony's body and
        /// installs the designed modules on it. FULLY GUARDED — a missing colony / body / faction / factory simply
        /// skips the deploy (never throws; it runs inside the daily industry hotloop, gotcha L4/#1).
        /// </summary>
        public void OnConstructionComplete(Entity industryEntity, CargoStorageDB storage, string productionLine, IndustryJob batchJob, IConstructableDesign designInfo)
        {
            batchJob.NumberCompleted++;
            batchJob.ResourcesRequiredRemaining = new Dictionary<string, long>(designInfo.ResourceCosts);
            batchJob.ProductionPointsLeft = designInfo.IndustryPointCosts;

            // Deploy the station at the building colony's body, then install the designed modules on it. Guarded so a
            // host without a colony/planet/faction (a test rig, a station-hosted builder) just skips — never throws.
            try
            {
                if (industryEntity.TryGetDataBlob<ColonyInfoDB>(out var colony)
                    && colony.PlanetEntity != null && colony.PlanetEntity != Entity.InvalidEntity
                    && industryEntity.Manager?.Game != null
                    && industryEntity.Manager.Game.Factions.TryGetValue(industryEntity.FactionOwnerID, out var factionEntity)
                    && factionEntity.TryGetDataBlob<FactionInfoDB>(out var factionInfo))
                {
                    var station = StationFactory.CreateStation(factionEntity, colony.PlanetEntity, InitialPopulation);

                    // Install each designed module on the new station (chassis parts are inert on install; functional
                    // modules — reactor/research/factory — light up their host-agnostic economy processors here).
                    if (station != null && ComponentDesignIds != null)
                    {
                        foreach (var kv in ComponentDesignIds)
                        {
                            if (kv.Value < 1) continue;
                            if (!factionInfo.IndustryDesigns.TryGetValue(kv.Key, out var d) || d is not ComponentDesign moduleDesign)
                                continue;   // a module the faction no longer holds — skip it, install the rest
                            station.AddComponent(moduleDesign, kv.Value);
                        }
                    }
                }
            }
            catch { /* a failed deploy never crashes the industry hotloop — the job still completes cleanly below */ }

            // Job lifecycle (same as ComponentDesign / GroundUnitDesign) — guarded so a test/host without a real
            // production line is safe.
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

        /// <summary>THE ASSEMBLER ENTRY POINT (mirror of <c>GroundUnitAssembly.RegisterAssembledDesign</c>): assemble a
        /// <paramref name="chassis"/> + <paramref name="modules"/> into a <see cref="StationDesign"/> whose costs are the
        /// sum of the parts, AND register it on the faction as a buildable, so it rides the normal industry rails
        /// (queue → consume materials → <see cref="OnConstructionComplete"/> deploys the station). Returns the registered
        /// design. Never throws on the assembly. The industry line is the chassis's own <see cref="ComponentDesign.IndustryTypeID"/>
        /// (fallback <c>installation-construction</c> — the line a colony provides, the same one the bunker/ground units use).</summary>
        public static StationDesign RegisterStationDesign(FactionInfoDB faction, string uniqueId, string name,
            ComponentDesign chassis, IEnumerable<(ComponentDesign design, int count)> modules, long initialPopulation = 0)
        {
            var design = new StationDesign
            {
                UniqueID = uniqueId,
                Name = name,
                InitialPopulation = initialPopulation,
                IndustryTypeID = string.IsNullOrEmpty(chassis?.IndustryTypeID) ? "installation-construction" : chassis.IndustryTypeID,
            };

            // costs = chassis + every module (× count) — the same sum the ship / ground designers do
            if (chassis != null)
            {
                AddCosts(design.ResourceCosts, chassis.ResourceCosts);
                design.IndustryPointCosts += chassis.IndustryPointCosts;
                if (!string.IsNullOrEmpty(chassis.UniqueID))
                    design.ComponentDesignIds[chassis.UniqueID] =
                        design.ComponentDesignIds.TryGetValue(chassis.UniqueID, out var cc) ? cc + 1 : 1;
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
