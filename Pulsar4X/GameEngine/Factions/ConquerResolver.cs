using Pulsar4X.Industry;
using Pulsar4X.Ships;
using Pulsar4X.Weapons;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.8 P-3 (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md): the CONQUER resolver — the Ambition-tier "go to war for
    /// gain" brain (chosen when the faction is dominant, secure, and aggressive). It had NO resolver and no-oped; this
    /// is the SIXTH and last objective to get one, so every strategic objective now resolves.
    ///
    /// v1 does the FIRST rung only: BUILD UP a war fleet (queue an armed hull on a free ship-construction line — the
    /// same lever <see cref="DefendResolver"/>'s Rung A pulls). You cannot conquer without a fleet, so massing one is
    /// step one. The rest of the chain — picking WHICH rival to hit, checking you can physically REACH it, fuelling and
    /// charging the built ships (they spawn empty), composing the strike group, and the STRIKE itself — is the deferred
    /// P-3 military sub-subsystem (needs rival factions to exercise and three new helpers: MilitaryTarget /
    /// MilitaryComposition / MilitaryReach, plus a multi-jump auto-router). A deliberate, labelled deferral, not a gap.
    ///
    /// One step per monthly cycle. Pure decision (builds the <see cref="PlannerAction"/> closure; <c>EmitOrders</c>
    /// runs it). Byte-identical while order emission is off.
    /// </summary>
    public sealed class ConquerResolver : IObjectiveResolver
    {
        public StrategicObjective Handles => StrategicObjective.Conquer;

        public PlannerAction Resolve(FactionState state, StrategicObjectiveDB objective)
        {
            if (state == null) return PlannerAction.None;

            // Rung 1 (P-3 REACH — the STRIKE, 2026-07-12): if we hold a scored enemy target (a colony of a faction
            // we're at war with, MilitaryTarget) AND a MASSED strike fleet (MilitaryComposition) that isn't already
            // sailing, order that fleet to sail at the enemy world. This is the "do" that turns the coalition into a
            // war: 3.4b DECLARES war → MilitaryTarget SCORES the world → MilitaryComposition confirms a mass fleet →
            // here we SAIL it. Reuses the player MoveToSystemBodyOrder, which guards the warp landmines (skips
            // 0-speed ships; the order handler try/catches). Byte-identical while order emission is off (this whole
            // resolver runs only inside the gated EmitOrders), AND for any faction not at war (no target → skip).
            var target = MilitaryTarget.BestEnemyTarget(state.Faction);
            if (target.IsValid && state.Game != null)
            {
                var strikeFleet = MilitaryComposition.ReadyStrikeFleet(state);
                if (strikeFleet != null && strikeFleet.IsValid && !FleetIsMoving(strikeFleet))
                {
                    var game = state.Game;
                    int factionId = state.FactionId;
                    var fleet = strikeFleet;
                    var body = target.ColonyBody;
                    return new PlannerAction(
                        "StrikeFleet",
                        $"sail strike fleet {fleet.Id} at enemy world {body.Id} (target score {target.Score:F0})",
                        () =>
                        {
                            var cmd = Pulsar4X.Movement.MoveToSystemBodyOrder.CreateCommand(factionId, fleet, body);
                            game.OrderHandler.HandleOrder(cmd);   // the ONE step (the only side effect)
                        });
                }
            }

            // Rung 2 (v1) — MASS the strike fleet: queue an armed hull on a free ship-construction line. (Reached
            // while we have no target yet, or the strike group isn't massed / is already en route.)
            foreach (var colony in state.ColoniesWithFreeLine())
            {
                if (colony.Cargo == null) continue;   // AutoAddSubJobs needs a CargoStorageDB

                foreach (var designKvp in state.Info.IndustryDesigns)
                {
                    if (!(designKvp.Value is ShipDesign ship) || !IsWarship(ship)) continue;
                    if (!FeasibilityOracle.CanQueue(colony, ship, state.Info)) continue;

                    string lineId = FreeLineFor(colony.Industry, ship.IndustryTypeID);
                    if (lineId == null) continue;

                    var colonyEntity = colony.Colony;
                    var info = state.Info;
                    var designId = designKvp.Key;
                    var designName = ship.Name;
                    return new PlannerAction(
                        "QueueWarship",
                        $"build '{designName}' on colony {colonyEntity.Id} to mass a strike fleet",
                        () =>
                        {
                            var job = new IndustryJob(info, designId);
                            job.InitialiseJob(1, true);                    // repeat: keep massing the fleet
                            IndustryTools.AddJob(colonyEntity, lineId, job);
                            IndustryTools.AutoAddSubJobs(colonyEntity, job); // material-aware (resolve the sub-tree)
                        });
                }
            }

            return PlannerAction.None;
        }

        /// <summary>A ship design is a WARSHIP if any component design it mounts carries a weapon attribute. (Mirrors
        /// <see cref="DefendResolver"/>'s helper — a later MilitaryComposition slice can extract the shared build.)</summary>
        private static bool IsWarship(ShipDesign ship)
            => ship.TryGetComponentsByAttribute<GenericBeamWeaponAtb>(out _)
            || ship.TryGetComponentsByAttribute<RailgunWeaponAtb>(out _)
            || ship.TryGetComponentsByAttribute<FlakWeaponAtb>(out _)
            || ship.TryGetComponentsByAttribute<PlasmaBoltWeaponAtb>(out _)
            || ship.TryGetComponentsByAttribute<DisruptorWeaponAtb>(out _)
            || ship.TryGetComponentsByAttribute<MissileLauncherAtb>(out _);

        /// <summary>True if any ship in the fleet is already in warp transit — so the strike rung doesn't re-issue the
        /// sail order every monthly cycle (which would thrash the fleet's warp). A cheap en-route guard; a fuller
        /// "already ordered to this target" check rides the later reach polish.</summary>
        private static bool FleetIsMoving(Entity fleet)
        {
            foreach (var ship in Pulsar4X.Combat.FleetCombat.Ships(fleet))
                if (ship != null && ship.IsValid && ship.HasDataBlob<Pulsar4X.Movement.WarpMovingDB>())
                    return true;
            return false;
        }

        /// <summary>The id of a free (empty-queue) production line on this colony that runs the given industry type.</summary>
        private static string FreeLineFor(IndustryAbilityDB industry, string industryTypeId)
        {
            foreach (var lineKvp in industry.ProductionLines)
                if (lineKvp.Value.IndustryTypeRates.ContainsKey(industryTypeId) && lineKvp.Value.Jobs.Count == 0)
                    return lineKvp.Key;
            return null;
        }
    }
}
