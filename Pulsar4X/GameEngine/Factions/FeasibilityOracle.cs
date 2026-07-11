using Pulsar4X.Colonies;
using Pulsar4X.Industry;
using Pulsar4X.Interfaces;
using Pulsar4X.Ships;

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

            // P1-e crew gate — SHIP hulls only (mirrored exactly from ConstructStuff: you can't build a ship you
            // can't crew). Inert for materials/installations and for a manpool-less host — so byte-identical for
            // GrowEconomy's refining/installation builds.
            if (design is ShipDesign ship && ship.CrewReq > 0)
            {
                var crew = ManpowerTools.ResolveBuild(colony.Colony, ship.CrewReq - ship.TalentReq);
                if (!crew.CanBuild || !ManpowerTools.HasTalentToBuild(colony.Colony, ship.TalentReq))
                    return false;
            }

            // P1-e capacity gate — a FREE line that runs this type AND turns over ≥ 1 pt/tick AFTER infra scaling.
            // ConstructStuff skips a job whose infra-scaled rate is < 1 (`(int)(rate*eff) < 1 → continue`), so such a
            // build would sit forever making no progress — the oracle refuses it up front.
            double eff = InfrastructureProcessor.GetEfficiency(colony.Colony);
            foreach (var line in colony.Industry.ProductionLines.Values)
                if (line.IndustryTypeRates.TryGetValue(design.IndustryTypeID, out int rate)
                    && line.Jobs.Count == 0
                    && (int)(rate * eff) >= 1)
                    return true;
            return false;
        }
    }
}
