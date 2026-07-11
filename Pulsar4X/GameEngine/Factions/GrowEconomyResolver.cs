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

            // Rung C — nothing stalled to heal yet (Rung B is P1+): start the next growth build on a free line.
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
