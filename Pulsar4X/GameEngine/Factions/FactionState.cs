using System.Collections.Generic;
using System.Linq;
using Pulsar4X.Colonies;
using Pulsar4X.DataStructures;
using Pulsar4X.Engine;
using Pulsar4X.Fleets;
using Pulsar4X.Industry;
using Pulsar4X.Storage;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.8 P0-a (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md — the means-ends planner): the "WHAT I HAVE" SNAPSHOT.
    /// Gathered ONCE per monthly decision cycle so a resolver reads memory, not the entity graph, as it walks a
    /// goal's prerequisites backward. NOT a DataBlob — per-Tick scratch, never serialized. Pure/read-only →
    /// byte-identical (nothing consumes it yet; the P0-b resolver + the `EmitOrders` rewire do). Defensive
    /// throughout (a missing blob is skipped; a snapshot of a blob-less / manager-less faction is null), so it
    /// never throws from the hotloop.
    /// </summary>
    public sealed class FactionState
    {
        public Entity Faction { get; private set; }
        public FactionInfoDB Info { get; private set; }
        public Game Game { get; private set; }
        public int FactionId => Faction.Id;

        // --- faction-wide gauges (eager, off the built FactionRollup readers) ---
        public decimal Balance { get; private set; }
        public double MilitaryStrength { get; private set; }
        public double MeanMorale { get; private set; }
        public double MeanLegitimacy { get; private set; }

        // --- per-colony slices ---
        public IReadOnlyList<ColonyState> Colonies { get; private set; }

        private FactionState() { }

        /// <summary>
        /// Gather the snapshot off a faction entity, or null if it has no <see cref="FactionInfoDB"/> / manager /
        /// game (the caller no-ops on null — same guard style as <c>UpdateStrategicObjective</c>).
        /// </summary>
        public static FactionState Snapshot(Entity faction)
        {
            if (faction == null || !faction.IsValid) return null;
            if (!faction.TryGetDataBlob<FactionInfoDB>(out var info)) return null;
            var game = faction.Manager?.Game;
            if (game == null) return null;

            var colonies = new List<ColonyState>();
            foreach (var colony in info.Colonies)
            {
                if (colony == null || !colony.IsValid) continue;
                colonies.Add(ColonyState.Of(colony));
            }

            return new FactionState
            {
                Faction = faction,
                Info = info,
                Game = game,
                Balance = FactionRollup.Balance(faction),
                MilitaryStrength = FactionRollup.MilitaryStrength(faction),
                MeanMorale = FactionRollup.MeanMorale(faction),
                MeanLegitimacy = FactionRollup.MeanLegitimacy(faction),
                Colonies = colonies,
            };
        }

        /// <summary>Colonies that have an industry line with an empty job queue — somewhere to start new work.</summary>
        public IEnumerable<ColonyState> ColoniesWithFreeLine() => Colonies.Where(c => c.HasFreeLine);

        /// <summary>
        /// Phase-2.8 (Defend): every FLEET this faction owns, across all its systems — the postureable/taskable
        /// fleets the crisis resolvers act on. Lazy + defensive (a null/blob-less system is skipped); read-only.
        /// </summary>
        public IEnumerable<Entity> OwnedFleets()
        {
            foreach (var system in Game.Systems)
            {
                if (system == null) continue;
                foreach (var fleet in system.GetAllEntitiesWithDataBlob<FleetDB>())
                    if (fleet.FactionOwnerID == FactionId)
                        yield return fleet;
            }
        }

        /// <summary>
        /// Phase-2.8 P1-a: every STALLED build across all colonies — a job the engine parked at
        /// <see cref="IndustryJobStatus.MissingResources"/> because it couldn't get its inputs. Colony-tagged. This is
        /// the "I'm stuck, resolve the supply chain" signal the GrowEconomy resolver's Rung B acts on.
        /// </summary>
        public IEnumerable<(ColonyState colony, IndustryJob job)> StalledJobs()
        {
            foreach (var colony in Colonies)
            {
                if (colony.Industry == null) continue;
                foreach (var line in colony.Industry.ProductionLines.Values)
                    foreach (var job in line.Jobs)
                        if (job.Status == IndustryJobStatus.MissingResources)
                            yield return (colony, job);
            }
        }

        /// <summary>
        /// Phase-2.8 P1-a: the below-the-mineral-floor shortfalls implied by the stalled jobs — a material a stalled
        /// job still needs (<see cref="JobBase.ResourcesRequiredRemaining"/> &gt; 0) that is NOT itself buildable (absent
        /// from <see cref="FactionInfoDB.IndustryDesigns"/>), i.e. a raw MINERAL the engine's <c>AutoAddSubJobs</c>
        /// silently dropped. This is exactly the seam the planner owns (the mineral-line reconciliation); the bridge
        /// (P1-b onward) decides mine / survey / logistics per shortfall. Buildable shortfalls are excluded — the
        /// engine resolves those for free.
        /// </summary>
        public IEnumerable<MineralShortfall> MineralShortfalls()
        {
            foreach (var (colony, job) in StalledJobs())
            {
                foreach (var kvp in job.ResourcesRequiredRemaining)
                {
                    if (kvp.Value <= 0) continue;                         // nothing still owed
                    if (Info.IndustryDesigns.ContainsKey(kvp.Key)) continue; // buildable → above the floor (engine handles it)
                    yield return new MineralShortfall { Colony = colony, MaterialId = kvp.Key, Missing = kvp.Value };
                }
            }
        }
    }

    /// <summary>
    /// A raw-material demand the engine's <c>AutoAddSubJobs</c> can't resolve (below the mineral floor) — surfaced by
    /// <see cref="FactionState.MineralShortfalls"/>. The mineral-floor bridge (P1-b onward) turns each into one
    /// acquisition order (survey / mine / logistics), or an escalation to Expand.
    /// </summary>
    public readonly struct MineralShortfall
    {
        public ColonyState Colony { get; init; }
        public string MaterialId { get; init; }   // the cargo/mineral id the stalled job still needs
        public long Missing { get; init; }         // how much the job still owes (its ResourcesRequiredRemaining)
    }

    /// <summary>
    /// Per-colony slice of the planner snapshot. Any blob the colony lacks is null (the resolvers skip a null slot,
    /// matching <see cref="FactionRollup"/>'s "missing blob is skipped" rule).
    /// </summary>
    public sealed class ColonyState
    {
        public Entity Colony { get; private set; }
        public IndustryAbilityDB Industry { get; private set; }   // null if none
        public CargoStorageDB Cargo { get; private set; }         // null if none
        public MiningDB Mining { get; private set; }              // null if none
        public MineralsDB PlanetMinerals { get; private set; }    // the colony's body's deposits; null if none
        public ColonyMoraleDB Morale { get; private set; }        // the unrest gauge (Consolidate reads it); null if none
        public ColonyEconomyDB Economy { get; private set; }      // the tax lever (Consolidate eases it); null if none
        public bool HasFreeLine { get; private set; }             // any ProductionLine with an empty job queue

        private ColonyState() { }

        internal static ColonyState Of(Entity colony)
        {
            colony.TryGetDataBlob<IndustryAbilityDB>(out var industry);
            colony.TryGetDataBlob<CargoStorageDB>(out var cargo);
            colony.TryGetDataBlob<MiningDB>(out var mining);
            colony.TryGetDataBlob<ColonyMoraleDB>(out var morale);
            colony.TryGetDataBlob<ColonyEconomyDB>(out var economy);

            MineralsDB planetMinerals = null;
            if (colony.TryGetDataBlob<ColonyInfoDB>(out var ci) && ci.PlanetEntity != null && ci.PlanetEntity.IsValid)
                ci.PlanetEntity.TryGetDataBlob<MineralsDB>(out planetMinerals);

            bool hasFreeLine = industry != null
                && industry.ProductionLines.Values.Any(l => l.Jobs.Count == 0);

            return new ColonyState
            {
                Colony = colony,
                Industry = industry,
                Cargo = cargo,
                Mining = mining,
                PlanetMinerals = planetMinerals,
                Morale = morale,
                Economy = economy,
                HasFreeLine = hasFreeLine,
            };
        }
    }
}
