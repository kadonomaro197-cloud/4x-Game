using Pulsar4X.Components;
using Pulsar4X.Industry;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.8 P0-b (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md): the GrowEconomy resolver — the honest version of the
    /// blind 2.4c emitter. It walks the objective's prerequisites nearest-unmet-first and returns the ONE order that
    /// advances it, one step per monthly cycle (least-commitment, riding the 2.3 hysteresis loop).
    ///
    /// This slice implements **Rung C only** — start the next growth build on a free line, ROUTED THROUGH
    /// <see cref="IndustryTools.AutoAddSubJobs"/> so the engine auto-queues the build's refined sub-materials (the fix
    /// the live <c>TryQueueEconomyJob</c> lacks — it called <c>AddJob</c> directly and was blind to its own inputs).
    /// Rung B (heal a STALLED build via the mineral-floor bridge — survey / mine / logistics) lands in P1-a onward.
    /// Pure decision: it builds the <see cref="PlannerAction"/> closure; <c>EmitOrders</c> runs it.
    /// </summary>
    public sealed class GrowEconomyResolver : IObjectiveResolver
    {
        public StrategicObjective Handles => StrategicObjective.GrowEconomy;

        public PlannerAction Resolve(FactionState state, StrategicObjectiveDB objective)
        {
            if (state == null) return PlannerAction.None;

            // Rung B — heal a STALLED build by acquiring the raw mineral it's starved for. This slice handles the MINE
            // case (the mineral is present + accessible on the colony's own body but unmined); logistics (P1-c) and
            // survey (P1-d) are the other branches of the mineral-floor bridge.
            var mineStep = TryQueueMineForShortfall(state);
            if (mineStep != null) return mineStep;

            // Rung C — nothing to heal: start the next growth build on a free line.
            foreach (var colony in state.ColoniesWithFreeLine())
            {
                if (colony.Cargo == null) continue;   // AutoAddSubJobs throws without a CargoStorageDB

                foreach (var designKvp in state.Info.IndustryDesigns)
                {
                    IConstructableDesign design = designKvp.Value;
                    if (design == null) continue;
                    if (!FeasibilityOracle.CanQueue(colony, design, state.Info)) continue;

                    string lineId = FreeLineFor(colony.Industry, design.IndustryTypeID);
                    if (lineId == null) continue;   // (CanQueue already vetted this, but be explicit)

                    // Capture for the closure (the processor runs it; the resolver stays a pure decision).
                    var colonyEntity = colony.Colony;
                    var info = state.Info;
                    var designId = designKvp.Key;
                    var designName = design.Name;

                    return new PlannerAction(
                        "QueueBuild",
                        $"queue '{designName}' on colony {colonyEntity.Id} (+auto sub-jobs)",
                        () =>
                        {
                            var job = new IndustryJob(info, designId);
                            job.InitialiseJob(1, true);                    // repeat: keep the line producing
                            IndustryTools.AddJob(colonyEntity, lineId, job);
                            IndustryTools.AutoAddSubJobs(colonyEntity, job); // the fix: resolve the buildable sub-tree
                        });
                }
            }

            return PlannerAction.None;
        }

        /// <summary>
        /// P1-b (the mineral-floor bridge, MINE case): for the first mineral shortfall whose mineral is present +
        /// accessible on the colony's own body but has NO mining capacity, return a <c>QueueMine</c> step — build a
        /// Mine to start feeding the stalled build. Skips a non-mineral (the engine refines it), a mineral not present
        /// / inaccessible here (survey or logistics, P1-c/d), and one already being mined (transient — let the clock
        /// run). Returns null if no shortfall calls for a mine.
        /// </summary>
        private static PlannerAction TryQueueMineForShortfall(FactionState state)
        {
            var info = state.Info;
            foreach (var shortfall in state.MineralShortfalls())
            {
                // string id → int Mineral.ID (the load-bearing bridge; check unlocked then locked libraries).
                var lib = info.Data.CargoGoods.IsMineral(shortfall.MaterialId) ? info.Data.CargoGoods
                        : info.Data.LockedCargoGoods.IsMineral(shortfall.MaterialId) ? info.Data.LockedCargoGoods
                        : null;
                if (lib == null) continue;                       // not a mineral → the engine's refine chain handles it
                int mid = lib.GetMineral(shortfall.MaterialId).ID;

                var colony = shortfall.Colony;
                if (colony.PlanetMinerals == null) continue;
                if (!colony.PlanetMinerals.Minerals.TryGetValue(mid, out var deposit) || deposit.Accessibility <= 0.0)
                    continue;                                    // not on this body / inaccessible → survey or logistics (P1-c/d)
                if (colony.Mining != null && colony.Mining.ActualMiningRate.TryGetValue(mid, out var rate) && rate > 0)
                    continue;                                    // already mining it → transient shortfall, let the clock run

                var mine = FindMineDesign(info);
                if (mine.id == null || !FeasibilityOracle.CanQueue(colony, mine.design, info)) continue;
                string lineId = FreeLineFor(colony.Industry, mine.design.IndustryTypeID);
                if (lineId == null) continue;

                var colonyEntity = colony.Colony;
                var mineId = mine.id;
                var mineralId = shortfall.MaterialId;
                return new PlannerAction(
                    "QueueMine",
                    $"build a Mine on colony {colonyEntity.Id} to feed the stalled '{mineralId}'",
                    () =>
                    {
                        var job = new IndustryJob(info, mineId);
                        job.InitialiseJob(1, false);
                        IndustryTools.AddJob(colonyEntity, lineId, job);
                    });
            }
            return null;
        }

        /// <summary>The first buildable design that is a mine (a component carrying <see cref="MineResourcesAtbDB"/>),
        /// or (null, null) if the faction can't build one. v1: any mine (per-mineral targeting is a refinement).</summary>
        private static (string id, IConstructableDesign design) FindMineDesign(FactionInfoDB info)
        {
            foreach (var kvp in info.IndustryDesigns)
                if (kvp.Value is ComponentDesign cd && cd.HasAttribute<MineResourcesAtbDB>())
                    return (kvp.Key, kvp.Value);
            return (null, null);
        }

        /// <summary>The id of a free (empty-queue) production line on this colony that runs the given industry type,
        /// or null if none.</summary>
        private static string FreeLineFor(IndustryAbilityDB industry, string industryTypeId)
        {
            foreach (var lineKvp in industry.ProductionLines)
                if (lineKvp.Value.IndustryTypeRates.ContainsKey(industryTypeId) && lineKvp.Value.Jobs.Count == 0)
                    return lineKvp.Key;
            return null;
        }
    }
}
