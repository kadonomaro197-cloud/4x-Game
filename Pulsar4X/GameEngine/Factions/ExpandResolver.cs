using System.Collections.Generic;
using Pulsar4X.Colonies;
using Pulsar4X.Engine;
using Pulsar4X.Extensions;
using Pulsar4X.GeoSurveys;
using Pulsar4X.People;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.8 P-2 (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md): the EXPAND resolver — the Ambition-tier "settle new
    /// worlds" brain. Before this, an NPC that settled the Expand objective had NO resolver and no-oped. This drives
    /// the FOUND leg of the survey→move→found chain, one step per monthly cycle (least-commitment).
    ///
    /// It reads colonizability the SAME way the player's own colonize UI does (SystemWindow.cs: a body is settleable
    /// when it carries a <see cref="ColonizeableDB"/> AND its geo-survey is complete) — cradle-to-grave correct: the
    /// AI uses the player's signal, not a parallel one. Among the colonizeable, surveyed, UNCOLONIZED bodies in a
    /// system where the faction already has a presence (v1 near-home expansion; <c>KnownSystems</c> widens the pool
    /// later), it picks the most habitable (lowest <see cref="SpeciesDBExtensions.ColonyCost"/>) and FOUNDS a colony
    /// there — an instant order (no colony ship, no landing): <see cref="CreateColonyOrder"/> → ColonyFactory.
    ///
    /// v1 scope: the FOUND leg. When the best world still needs a geo-survey, this surfaces that (the Visibility Gate)
    /// rather than founding blind — the survey→move sub-chain (find a survey-capable fleet, sail it, survey) is the
    /// follow-on slice. Pure decision (builds the <see cref="PlannerAction"/> closure; <c>EmitOrders</c> runs it).
    /// Byte-identical while order emission is off.
    /// </summary>
    public sealed class ExpandResolver : IObjectiveResolver
    {
        public StrategicObjective Handles => StrategicObjective.Expand;

        public PlannerAction Resolve(FactionState state, StrategicObjectiveDB objective)
        {
            if (state == null) return PlannerAction.None;
            if (state.Info.Species.Count == 0) return PlannerAction.None;
            var speciesEntity = state.Info.Species[0];
            if (!speciesEntity.TryGetDataBlob<SpeciesDB>(out var species)) return PlannerAction.None;

            // Among colonizeable, uncolonized bodies near our colonies: prefer the most habitable that is SURVEYED.
            // (Rank by ColonyCost — lower = kinder world; an unsurvivable-for-us cost sorts last but stays foundable,
            // mirroring the player's colonize gate, which trusts the ColonizeableDB author's intent.)
            Entity best = null;
            double bestRank = double.PositiveInfinity;
            int awaitingSurvey = 0;

            foreach (var body in CandidateBodies(state))
            {
                // The player's gate: settle only a surveyed world. No survey blob ⇒ nothing to survey ⇒ ready.
                if (body.TryGetDataBlob<GeoSurveyableDB>(out var gsd) && !gsd.IsSurveyComplete(state.FactionId))
                {
                    awaitingSurvey++;
                    continue;
                }

                double cost = species.ColonyCost(body);
                double rank = cost == SpeciesDBExtensions.UNSURVIVABLE_COST ? double.MaxValue : cost;
                if (rank < bestRank) { bestRank = rank; best = body; }
            }

            if (best != null)
            {
                var faction = state.Faction;
                var game = state.Game;
                var body2 = best;
                double costShown = bestRank == double.MaxValue ? -1.0 : bestRank;
                return new PlannerAction(
                    "Found",
                    $"found a colony on body {best.Id} (habitability cost {costShown:0.##})",
                    () => game.OrderHandler.HandleOrder(CreateColonyOrder.CreateCommand(faction, speciesEntity, body2)));
            }

            // No surveyed candidate. If colonizeable worlds are waiting on a survey, SAY so (Visibility Gate) — the
            // survey→move leg that clears it is the next slice; otherwise there is simply nothing to settle.
            if (awaitingSurvey > 0)
                return new PlannerAction("None", $"{awaitingSurvey} colonizeable world(s) await a geo-survey — survey leg pending", null);
            return PlannerAction.None;
        }

        /// <summary>
        /// The colonizeable, UNCOLONIZED bodies in each system where the faction already holds a colony (v1 near-home
        /// expansion; deduped across colonies that share a system). Uses <see cref="ColonizeableDB"/> — the same
        /// marker the player's colonize UI reads — so the AI and the player agree on what can be settled.
        /// </summary>
        private static IEnumerable<Entity> CandidateBodies(FactionState state)
        {
            var seenSystems = new HashSet<EntityManager>();
            foreach (var colony in state.Colonies)
            {
                var system = colony.Colony.Manager;
                if (system == null || !seenSystems.Add(system)) continue;
                foreach (var body in system.GetAllEntitiesWithDataBlob<ColonizeableDB>())
                {
                    if (body == null || !body.IsValid) continue;
                    if (body.IsOrHasColony().Item1) continue;   // already settled (ours or anyone's)
                    yield return body;
                }
            }
        }
    }
}
