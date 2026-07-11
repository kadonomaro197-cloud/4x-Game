using Pulsar4X.Interfaces;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.8 P0-b (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md): the plan-time "will this SILENTLY stall?" predicate.
    /// The engine checks money/crew/tech/capacity only at EXECUTION (inside <c>IndustryTools.ConstructStuff</c>) and
    /// fails quietly (a job parks at <c>MissingResources</c>); a resolver consults this BEFORE queuing so it doesn't
    /// emit a build that stalls unseen. It must MIRROR execution order, never be a superset — a stricter check would
    /// make the AI refuse builds a player could make.
    ///
    /// v1 skeleton: the tech gate (design unlocked) + the capacity gate (a free line that runs this industry type).
    /// Money / crew / infra checks are added a slice at a time (P1-e), so it never regresses byte-identity.
    /// </summary>
    public static class FeasibilityOracle
    {
        /// <summary>
        /// True if queuing <paramref name="design"/> on <paramref name="colony"/> won't immediately, silently stall.
        /// v1: the design is unlocked (in <c>IndustryDesigns</c>) AND the colony has a free production line whose
        /// <c>IndustryTypeRates</c> covers the design's type. Defensive — a null colony/industry/design → false.
        /// </summary>
        public static bool CanQueue(ColonyState colony, IConstructableDesign design, FactionInfoDB factionInfo)
        {
            if (colony?.Industry == null || design == null || factionInfo == null) return false;
            if (!factionInfo.IndustryDesigns.ContainsKey(design.UniqueID)) return false;   // tech gate (unlocked?)

            foreach (var line in colony.Industry.ProductionLines.Values)
                if (line.IndustryTypeRates.ContainsKey(design.IndustryTypeID) && line.Jobs.Count == 0)
                    return true;   // a free line can run this type → won't stall on "no line"
            return false;
        }
    }
}
