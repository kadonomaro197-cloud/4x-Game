using System.Collections.Generic;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.8 P0-b (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md): a per-objective backward-chaining RESOLVER. Given the
    /// settled objective and the <see cref="FactionState"/> snapshot, it names the single <see cref="PlannerAction"/>
    /// that advances the NEAREST unmet prerequisite — or <see cref="PlannerAction.None"/> when the goal is met or
    /// blocked. Pure decision (builds the step; the processor runs it), so it's CI-testable without ticking the sim —
    /// the same convention as <see cref="ObjectiveSelector"/> / <see cref="NeedsLadder"/>.
    /// </summary>
    public interface IObjectiveResolver
    {
        StrategicObjective Handles { get; }
        PlannerAction Resolve(FactionState state, StrategicObjectiveDB objective);
    }

    /// <summary>
    /// The registry the Tick consults — objective → its resolver. A handful of resolvers, so a static dictionary
    /// (mirrors the small catalog pattern of <see cref="ExchangeCatalog"/> / <see cref="CovertActionCatalog"/>).
    /// Objectives with no registered resolver simply emit nothing (a safe no-op); later phases register Expand /
    /// Conquer / Defend here.
    /// </summary>
    public static class ObjectiveResolvers
    {
        private static readonly Dictionary<StrategicObjective, IObjectiveResolver> _byObjective = new()
        {
            { StrategicObjective.GrowEconomy, new GrowEconomyResolver() },
            { StrategicObjective.Consolidate, new ConsolidateResolver() },
            { StrategicObjective.Defend, new DefendResolver() },
            { StrategicObjective.Expand, new ExpandResolver() },
            { StrategicObjective.AdvanceTech, new AdvanceTechResolver() },
            { StrategicObjective.Conquer, new ConquerResolver() },
        };

        public static bool TryGet(StrategicObjective objective, out IObjectiveResolver resolver)
            => _byObjective.TryGetValue(objective, out resolver);
    }
}
