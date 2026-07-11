using System;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.8 P0-b (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md): the ONE step a resolver picks this cycle toward its
    /// goal. A pure RESULT — it CARRIES the step as a closure (<see cref="Execute"/>) but the processor runs it, so a
    /// resolver stays testable without mutating the sim. <see cref="Detail"/> is the human-readable line the (later)
    /// plan/queue visibility readout records — every planner failure is otherwise silent (the Visibility Gate).
    /// The <see cref="Execute"/> closure unifies the two ways rungs reach the sim: an <c>EntityCommand</c> submitted
    /// via the order handler (survey / move / found) OR a direct <c>IndustryTools.AddJob</c> (a build).
    /// </summary>
    public sealed class PlannerAction
    {
        /// <summary>A short tag for the gauge/readout — "QueueBuild" | "QueueMine" | "Survey" | "SetLogistics" | "None".</summary>
        public string Kind { get; }
        /// <summary>Human-readable line for the visibility readout (what the resolver decided and why).</summary>
        public string Detail { get; }
        /// <summary>Performs the one step. Null for <see cref="None"/>.</summary>
        public Action Execute { get; }

        /// <summary>The "nothing to do this cycle" sentinel — goal met, or no legal step exists.</summary>
        public static readonly PlannerAction None = new PlannerAction("None", "goal met or no legal step", null);

        public PlannerAction(string kind, string detail, Action execute)
        {
            Kind = kind;
            Detail = detail;
            Execute = execute;
        }
    }
}
