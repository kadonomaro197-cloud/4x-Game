using Pulsar4X.Colonies;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.8 (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md): the CONSOLIDATE resolver — the crisis brain for the
    /// <see cref="NeedTier.Stabilize"/> tier (unrest, low legitimacy). Before this existed the NPC settled the
    /// Consolidate objective but had NO resolver, so it FROZE in a crisis (parking-lot gap G1: "acts in good
    /// times, does nothing when the house is on fire"). This is the honest first fix — an INTERNAL-lever resolver
    /// (no military reach, so buildable before the P-3 fleet work).
    ///
    /// The one lever this slice pulls: EASE TAX on the most-restless colony. Low morale IS the unrest signal
    /// (<see cref="ColonyMoraleDB.Morale"/> below the neutral 50), and tax is a real two-way lever — the tax rate
    /// feeds straight back into morale (the tax penalty in <see cref="ColonyMoraleDB.ComputeMorale"/>) AND into the
    /// treasury (<see cref="ColonyEconomyProcessor"/>). How FAR it eases is the faction's disposition: the pure
    /// <see cref="TaxPolicy.TaxRateUnderUnrest"/> helper (already built, M2-1d) lets an authoritarian regime hold
    /// the line (suppress) while a permissive one cuts hard (appease). Raising morale directly / quelling an active
    /// rebellion (RebellionDB) are the later Consolidate slices.
    ///
    /// One step per monthly cycle (least-commitment, riding the 2.3 hysteresis loop). Pure decision — it builds the
    /// <see cref="PlannerAction"/> closure; <c>EmitOrders</c> runs it. Byte-identical while order emission is off.
    ///
    /// STATION-LEGAL FALL-THROUGH (Operation Earthfall D2.1): the tax-ease lever needs a <see cref="ColonyEconomyDB"/>,
    /// which a STATION doesn't carry (it has a <see cref="Pulsar4X.Stations.StationEconomyDB"/> instead). So a
    /// station-only faction (the Kithrin outpost) found NO legal Consolidate step here and FROZE in a crisis anyway
    /// (A6 finding). When no host carries a colony tax lever at all, this now falls through to the host-agnostic
    /// GrowEconomy build rungs — growing infrastructure is a legitimate stabilization move when tax relief is off the
    /// table, and it keeps Consolidate from being a guaranteed no-op for a station faction. Gated on "no colony tax
    /// lever anywhere", so a normal colony faction's Consolidate is byte-identical (a content colony faction still
    /// returns None).
    /// </summary>
    public sealed class ConsolidateResolver : IObjectiveResolver
    {
        public StrategicObjective Handles => StrategicObjective.Consolidate;

        /// <summary>The tax must sit at least this far above the appeasement target before we bother easing it — so a
        /// colony already relieved doesn't churn a no-op "ease" order every cycle.</summary>
        private const double EaseMargin = 0.005;   // half a percentage point

        public PlannerAction Resolve(FactionState state, StrategicObjectiveDB objective)
        {
            if (state == null) return PlannerAction.None;

            // Read the faction's disposition + tax ceiling once (null personality → neutral in TaxPolicy; a
            // government-less faction → the shared neutral Mid default, so this never throws).
            state.Faction.TryGetDataBlob<PersonalityDB>(out var personality);
            double taxCeiling = GovernmentTools.Of(state.Faction).TaxCeiling();

            // Find the most-restless colony whose tax could still be eased: lowest morale below the neutral line,
            // where the current rate sits meaningfully above what this personality would set under that unrest.
            ColonyState worst = null;
            double worstMorale = ColonyMoraleDB.Neutral;   // only colonies BELOW neutral count as restless
            double worstTarget = 0.0;

            foreach (var colony in state.Colonies)
            {
                if (colony.Morale == null || colony.Economy == null) continue;   // can't read unrest / pull the lever
                double morale = colony.Morale.Morale;
                if (morale >= ColonyMoraleDB.Neutral) continue;                  // content — nothing to consolidate

                double unrest = (ColonyMoraleDB.Neutral - morale) / ColonyMoraleDB.Neutral;   // 0 at neutral, 1 at zero morale
                double target = TaxPolicy.TaxRateUnderUnrest(personality, taxCeiling, unrest);
                if (colony.Economy.TaxRate - target <= EaseMargin) continue;    // already eased enough here

                if (morale < worstMorale)
                {
                    worst = colony;
                    worstMorale = morale;
                    worstTarget = target;
                }
            }

            if (worst != null)
            {
                // Capture for the closure (the processor runs it; the resolver stays a pure decision).
                var econ = worst.Economy;
                double newRate = worstTarget;
                double oldRate = econ.TaxRate;
                int colonyId = worst.Colony.Id;
                double moraleShown = worstMorale;

                return new PlannerAction(
                    "EaseTax",
                    $"ease tax on colony {colonyId} from {oldRate:P0} to {newRate:P0} to quell unrest (morale {moraleShown:N0})",
                    () => econ.TaxRate = newRate);
            }

            // No colony tax lever to ease. For a STATION-ONLY faction (no host carries a ColonyEconomyDB — the lever
            // above needs one), Consolidate would otherwise be a guaranteed no-op (A6). Fall through to the
            // host-agnostic GrowEconomy build rungs so a station faction is never frozen in a crisis. Gated on "no
            // colony tax lever anywhere", so a colony faction whose colonies are all content still returns None here
            // (byte-identical — the ContentColony gauge).
            if (!HasColonyTaxLever(state))
            {
                var buildStep = new GrowEconomyResolver().Resolve(state, objective);
                if (buildStep != null && buildStep.Kind != PlannerAction.None.Kind)
                    return buildStep;
            }

            return PlannerAction.None;   // nobody restless enough / all relieved, and no station build available
        }

        /// <summary>True if ANY host in the snapshot carries a colony tax lever (<see cref="ColonyEconomyDB"/>) — i.e.
        /// the faction has a colony the tax-ease step above could act on. False for a purely station-based faction
        /// (a station carries <see cref="Pulsar4X.Stations.StationEconomyDB"/>, not this), which is exactly when the
        /// station-legal fall-through fires.</summary>
        private static bool HasColonyTaxLever(FactionState state)
        {
            foreach (var host in state.Colonies)
                if (host.Economy != null) return true;
            return false;
        }
    }
}
