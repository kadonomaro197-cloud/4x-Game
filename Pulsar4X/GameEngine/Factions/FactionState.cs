using System.Collections.Generic;
using System.Linq;
using Pulsar4X.Colonies;
using Pulsar4X.Engine;
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
        public bool HasFreeLine { get; private set; }             // any ProductionLine with an empty job queue

        private ColonyState() { }

        internal static ColonyState Of(Entity colony)
        {
            colony.TryGetDataBlob<IndustryAbilityDB>(out var industry);
            colony.TryGetDataBlob<CargoStorageDB>(out var cargo);
            colony.TryGetDataBlob<MiningDB>(out var mining);

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
                HasFreeLine = hasFreeLine,
            };
        }
    }
}
