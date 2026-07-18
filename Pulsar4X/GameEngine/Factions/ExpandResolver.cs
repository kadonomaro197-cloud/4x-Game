using System.Collections.Generic;
using Pulsar4X.Colonies;
using Pulsar4X.Datablobs;   // OrderableDB (the idle-surveyor busy check)
using Pulsar4X.Engine;
using Pulsar4X.Extensions;
using Pulsar4X.GeoSurveys;
using Pulsar4X.Industry;    // IndustryJob / IndustryTools / IndustryAbilityDB (the fallback build rung)
using Pulsar4X.People;
using Pulsar4X.Ships;       // ShipInfoDB / ShipDesign (the surveyor scan + the buildable surveyor design)

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
    /// SURVEY LEG (Operation Earthfall D1.1, 2026-07-18 — task #35, the Kithrin were structurally DEAD at three rungs).
    /// When the best world still needs a geo-survey, this no longer just SAYS "survey leg pending" (the old Execute=null
    /// message). It now advances the survey chain:
    ///   (b) if the faction owns an IDLE survey-capable ship, emit a real <see cref="GeoSurveyOrder"/> at the
    ///       best unsurveyed colonizeable world (the same order the player's FleetWindow right-click issues), so the
    ///       survey actually starts and, once complete, the FOUND rung above founds a colony there; and
    ///   (c) if the faction owns NO surveyor and none is already building, queue ONE surveyor on a free ship line
    ///       (an already-in-production guard stops it re-queueing a surveyor every cycle — the least-commitment
    ///       "build the tool once" step, the sibling of ConquerResolver's build-transport guard).
    /// v1 scope note: the emitted GeoSurveyOrder does NOT also sail the surveyor to the target — GeoSurveyProcessor does
    /// not yet gate survey points on being AT the body (its own TODO), so the survey progresses from where the ship sits;
    /// sailing the surveyor to the world is the follow-on refinement (and harmless to add later).
    ///
    /// One step per monthly cycle. Pure decision (builds the <see cref="PlannerAction"/> closure; <c>EmitOrders</c>
    /// runs it under the default-off <c>EnableOrderEmission</c> gate) → byte-identical while order emission is off.
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
            // mirroring the player's colonize gate, which trusts the ColonizeableDB author's intent.) Track the FIRST
            // UNSURVEYED world (deterministic iteration order) as the survey target — surveying ANY colonizeable world
            // advances the chain, and the FOUND rung above picks the BEST once several are surveyed, so the survey
            // target need not be ranked. Deliberately does NOT call ColonyCost on unsurveyed bodies — that keeps the
            // ColonyCost call-count identical to the pre-survey-leg code (only surveyed candidates), so a faction with
            // ONLY unsurveyed candidates (the Kithrin) runs exactly the reads it did before + the cheap survey scan.
            Entity best = null;
            double bestRank = double.PositiveInfinity;
            Entity firstUnsurveyed = null;
            int awaitingSurvey = 0;

            foreach (var body in CandidateBodies(state))
            {
                // The player's gate: settle only a surveyed world. No survey blob ⇒ nothing to survey ⇒ ready.
                if (body.TryGetDataBlob<GeoSurveyableDB>(out var gsd) && !gsd.IsSurveyComplete(state.FactionId))
                {
                    awaitingSurvey++;
                    if (firstUnsurveyed == null) firstUnsurveyed = body;
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

            // No surveyed candidate, but colonizeable worlds are waiting on a survey → advance the SURVEY leg (D1.1).
            if (awaitingSurvey > 0 && firstUnsurveyed != null)
            {
                // (b) SURVEY: an idle survey-capable ship we own scans an unsurveyed world. Emit the SAME
                // GeoSurveyOrder the player's FleetWindow right-click issues (front door), commanding the surveyor
                // ship directly (GeoSurveyProcessor reads its GeoSurveyAbilityDB). Once complete, the FOUND rung above
                // takes over next cycle.
                var surveyor = FindIdleSurveyor(state);
                if (surveyor != null)
                {
                    var game = state.Game;
                    int factionId = state.FactionId;
                    var s = surveyor;
                    var body = firstUnsurveyed;
                    return new PlannerAction(
                        "Survey",
                        $"survey colonizeable world {body.Id} with surveyor {s.Id}",
                        () => game.OrderHandler.HandleOrder(GeoSurveyOrder.CreateCommand(factionId, s, body)));
                }

                // (c) FALLBACK BUILD: we own NO surveyor (FindIdleSurveyor returned null because there is none, not
                // because ours is busy) AND none is already in production → build ONE surveyor on a free ship line.
                // The two guards ARE the already-queued guard: FactionOwnsSurveyor stops us building a second while one
                // exists (even if that one is busy surveying), and SurveyorInProduction stops us re-queueing a surveyor
                // every monthly cycle while the first is still on the slipway (the ConquerResolver build-transport guard
                // pattern, implemented here independently). One surveyor (repeat:false — a tool, not a standing mass).
                if (!FactionOwnsSurveyor(state) && !SurveyorInProduction(state))
                {
                    foreach (var colony in state.ColoniesWithFreeLine())
                    {
                        if (colony.Cargo == null) continue;   // AutoAddSubJobs needs a CargoStorageDB

                        foreach (var designKvp in state.Info.IndustryDesigns)
                        {
                            if (!(designKvp.Value is ShipDesign surveyorDesign) || !IsSurveyor(surveyorDesign)) continue;
                            if (!FeasibilityOracle.CanQueue(colony, surveyorDesign, state.Info)) continue;

                            string lineId = FreeLineFor(colony.Industry, surveyorDesign.IndustryTypeID);
                            if (lineId == null) continue;

                            var colonyEntity = colony.Colony;
                            var info = state.Info;
                            var designId = designKvp.Key;
                            var designName = surveyorDesign.Name;
                            return new PlannerAction(
                                "BuildSurveyor",
                                $"build survey ship '{designName}' on colony {colonyEntity.Id} to open the frontier",
                                () =>
                                {
                                    var job = new IndustryJob(info, designId);
                                    job.InitialiseJob(1, false);                     // ONE surveyor, not a standing mass
                                    IndustryTools.AddJob(colonyEntity, lineId, job);
                                    IndustryTools.AutoAddSubJobs(colonyEntity, job); // material-aware (resolve the sub-tree)
                                });
                        }
                    }
                }

                // Survey pending: our surveyor is busy, one is on the slipway, or we can't build one this cycle.
                return new PlannerAction("None",
                    $"{awaitingSurvey} colonizeable world(s) await a geo-survey — surveyor working / building / unavailable",
                    null);
            }
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

        /// <summary>The faction's own survey-capable ship that is IDLE (not already running a geo-survey), or null. Scans
        /// ALL owned ships across every system (like <see cref="ConquerResolver.FindOwnedTransport"/>), not just fleet
        /// members, because a freshly-built surveyor isn't auto-added to a fleet. What the SURVEY rung commands. Internal
        /// for the CI gauge.</summary>
        internal static Entity FindIdleSurveyor(FactionState state)
        {
            foreach (var system in state.Game.Systems)
            {
                if (system == null) continue;
                foreach (var ship in system.GetAllEntitiesWithDataBlob<ShipInfoDB>())
                    if (ship.FactionOwnerID == state.FactionId && IsSurveyCapableShip(ship) && !IsBusySurveying(ship))
                        return ship;
            }
            return null;
        }

        /// <summary>True if the faction OWNS any survey-capable ship (idle OR busy) — the "don't build a second surveyor
        /// while we already have one" half of the fallback build guard. Internal for the gauge.</summary>
        internal static bool FactionOwnsSurveyor(FactionState state)
        {
            foreach (var system in state.Game.Systems)
            {
                if (system == null) continue;
                foreach (var ship in system.GetAllEntitiesWithDataBlob<ShipInfoDB>())
                    if (ship.FactionOwnerID == state.FactionId && IsSurveyCapableShip(ship))
                        return true;
            }
            return false;
        }

        /// <summary>A ship is survey-capable if the geo-surveyor component installed a working
        /// <see cref="GeoSurveyAbilityDB"/> on it (Speed &gt; 0). Faction filtering is the caller's job (this is the
        /// capability check only), validity-guarded so a destroyed-but-not-removed ship is skipped.</summary>
        private static bool IsSurveyCapableShip(Entity ship)
            => ship != null && ship.IsValid
            && ship.TryGetDataBlob<GeoSurveyAbilityDB>(out var ab) && ab.Speed > 0;

        /// <summary>True if the ship is already running a <see cref="GeoSurveyOrder"/> (it's in its order queue) — so the
        /// SURVEY rung doesn't stack a redundant survey order on it every cycle. A finished survey order is removed from
        /// the queue by the OrderableProcessor, so its presence == still working.</summary>
        private static bool IsBusySurveying(Entity ship)
        {
            if (!ship.TryGetDataBlob<OrderableDB>(out var od)) return false;
            foreach (var cmd in od.ActionList)
                if (cmd is GeoSurveyOrder) return true;
            return false;
        }

        /// <summary>A ship design is a SURVEYOR if it mounts a geo-surveyor component (a <see cref="GeoSurveyAtb"/>) — the
        /// same attribute that installs the <see cref="GeoSurveyAbilityDB"/> at build. The build rung finds ANY buildable
        /// survey design this way, not a hardcoded id. Internal for the gauge (mirrors ConquerResolver.IsWarship).</summary>
        internal static bool IsSurveyor(ShipDesign ship)
            => ship.TryGetComponentsByAttribute<GeoSurveyAtb>(out _);

        /// <summary>True if a surveyor ship is ALREADY queued in any colony's production lines — the "don't re-queue a
        /// surveyor every cycle while one is on the slipway" half of the fallback build guard. Internal for the gauge.</summary>
        internal static bool SurveyorInProduction(FactionState state)
        {
            foreach (var colony in state.Colonies)
            {
                if (colony.Industry == null) continue;
                foreach (var line in colony.Industry.ProductionLines.Values)
                    foreach (var job in line.Jobs)
                        if (state.Info.IndustryDesigns.TryGetValue(job.ItemGuid, out var design)
                            && design is ShipDesign shipDesign && IsSurveyor(shipDesign))
                            return true;
            }
            return false;
        }

        /// <summary>The id of a free (empty-queue) production line on this colony that runs the given industry type.
        /// (Mirrors ConquerResolver's private helper — a later shared-build extraction can fold the two.)</summary>
        private static string FreeLineFor(IndustryAbilityDB industry, string industryTypeId)
        {
            foreach (var lineKvp in industry.ProductionLines)
                if (lineKvp.Value.IndustryTypeRates.ContainsKey(industryTypeId) && lineKvp.Value.Jobs.Count == 0)
                    return lineKvp.Key;
            return null;
        }
    }
}
