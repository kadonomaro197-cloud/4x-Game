using Pulsar4X.Components;
using Pulsar4X.Industry;
using Pulsar4X.Interfaces;
using Pulsar4X.Technology;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.8 (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md): the ADVANCE-TECH resolver — the Thrive-tier "push research"
    /// brain (chosen when the faction's doctrine is tech-led). It had NO resolver and no-oped; now it BUILDS research
    /// capacity — a design carrying <see cref="ResearchPointsAtbDB"/> (a research lab: more labs → more research points
    /// per cycle, the same lever a player pulls). Queued through the shared build machinery (oracle-gated, routed
    /// through <see cref="IndustryTools.AutoAddSubJobs"/> so it's material-aware).
    ///
    /// v1 lever: queue a lab. Assigning idle scientists / raising research funding / steering the tech queue are the
    /// finer levers a later slice can add. One step per monthly cycle. Pure decision (builds the
    /// <see cref="PlannerAction"/> closure; <c>EmitOrders</c> runs it). Byte-identical while order emission is off.
    /// </summary>
    public sealed class AdvanceTechResolver : IObjectiveResolver
    {
        public StrategicObjective Handles => StrategicObjective.AdvanceTech;

        public PlannerAction Resolve(FactionState state, StrategicObjectiveDB objective)
        {
            if (state == null) return PlannerAction.None;

            foreach (var colony in state.ColoniesWithFreeLine())
            {
                if (colony.Cargo == null) continue;   // AutoAddSubJobs needs a CargoStorageDB

                var lab = FindResearchLabDesign(state.Info);
                if (lab.id == null || !FeasibilityOracle.CanQueue(colony, lab.design, state.Info)) continue;

                string lineId = FreeLineFor(colony.Industry, lab.design.IndustryTypeID);
                if (lineId == null) continue;

                var colonyEntity = colony.Colony;
                var info = state.Info;
                var designId = lab.id;
                var designName = lab.design.Name;
                return new PlannerAction(
                    "QueueResearchLab",
                    $"build '{designName}' on colony {colonyEntity.Id} to push research",
                    () =>
                    {
                        var job = new IndustryJob(info, designId);
                        job.InitialiseJob(1, true);                    // repeat: keep raising research capacity
                        IndustryTools.AddJob(colonyEntity, lineId, job);
                        IndustryTools.AutoAddSubJobs(colonyEntity, job); // material-aware (resolve the sub-tree)
                    });
            }

            return PlannerAction.None;
        }

        /// <summary>The first buildable design that is a research lab (a component carrying
        /// <see cref="ResearchPointsAtbDB"/>), or (null, null) if the faction can't build one. Mirrors
        /// <see cref="GrowEconomyResolver"/>'s FindMineDesign — a capability is a component, found by its attribute.</summary>
        private static (string id, IConstructableDesign design) FindResearchLabDesign(FactionInfoDB info)
        {
            foreach (var kvp in info.IndustryDesigns)
                if (kvp.Value is ComponentDesign cd && cd.HasAttribute<ResearchPointsAtbDB>())
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
