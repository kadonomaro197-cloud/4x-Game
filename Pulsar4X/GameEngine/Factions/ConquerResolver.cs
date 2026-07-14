using Pulsar4X.Engine;
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
            // sailing AND that fleet can actually GET THERE (MilitaryReach), order that fleet to sail at the enemy
            // world. This is the "do" that turns the coalition into a war: 3.4b DECLARES war → MilitaryTarget SCORES
            // the world → MilitaryComposition confirms a mass fleet → MilitaryReach confirms the fleet can REACH it →
            // here we SAIL it. Reuses the player MoveToSystemBodyOrder, which guards the warp landmines (skips
            // 0-speed ships; the order handler try/catches). Byte-identical while order emission is off (this whole
            // resolver runs only inside the gated EmitOrders), AND for any faction not at war (no target → skip).
            var target = MilitaryTarget.BestEnemyTarget(state.Faction);
            if (target.IsValid && state.Game != null)
            {
                var strikeFleet = MilitaryComposition.ReadyStrikeFleet(state);
                // REACH GATE: only sail when the target is a DIRECT (same-system) warp the fleet has the fuel/range
                // for. MoveToSystemBodyOrder warps within one system — it can't cross a jump — so a one-jump or
                // unreachable target (or a drained/drive-less fleet) falls through to the build rung and keeps massing
                // until a route/range exists. The multi-jump auto-router that would sail a OneJump target is the
                // deferred reach polish (MilitaryReach documents the bound).
                var reach = strikeFleet != null && strikeFleet.IsValid
                    ? MilitaryReach.Assess(strikeFleet, target.ColonyBody)
                    : MilitaryReach.ReachResult.None;
                if (strikeFleet != null && strikeFleet.IsValid && !FleetIsMoving(strikeFleet)
                    && reach.Tier == MilitaryReach.ReachTier.SameSystem && reach.HasRange)
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

            // Rung 1.5 (B5-3 — LOAD the invasion): we hold a war target and own a transport that is sitting AT one of our
            // own bodies which has a standing ground unit, with free troop-bay room → order that unit loaded (the front
            // door to LoadTroopsOrder → GroundTransport.TryLoadUnit). Placed AFTER the strike-fleet sail (so the warfleet
            // launches to win the orbit first) and BEFORE BuildTransport (which only fires when we own NO transport, so
            // the two are mutually exclusive). Loading a garrison unit strips it off the home world to carry the invasion
            // — the aggressive "throw the army at them" v1 (a reserve-vs-expedition split is a later refinement). Sailing
            // the loaded transport to the target + landing it are the next B5-3 rungs. Byte-identical while emission is off
            // (runs only inside the gated EmitOrders), AND for any faction not at war (no target → skipped).
            if (target.IsValid && state.Game != null)
            {
                var transport = FindOwnedTransport(state);
                if (transport != null && transport.IsValid
                    && Pulsar4X.GroundCombat.GroundTransport.FreeCapacity(transport, Pulsar4X.GroundCombat.GroundCarryClass.Personnel) > 0)
                {
                    var shipBody = ShipBody(transport);
                    var unit = AvailableGroundUnitAt(shipBody, state.FactionId);
                    if (shipBody != null && unit != null)
                    {
                        var game = state.Game;
                        var t = transport;
                        var b = shipBody;
                        int uid = unit.UnitId;
                        string uname = unit.Name;
                        return new PlannerAction(
                            "LoadInvasion",
                            $"load '{uname}' aboard transport {t.Id} at body {b.Id} to carry the invasion",
                            () =>
                            {
                                var cmd = Pulsar4X.GroundCombat.LoadTroopsOrder.CreateCommand(t, b, uid);
                                game.OrderHandler.HandleOrder(cmd);   // the ONE step
                            });
                    }
                }
            }

            // Rung 2 (B5-2 — the invasion CARRIER): we hold a war target but own no ship that can LIFT troops → build a
            // troop transport. Placed AFTER the sail rung (a ready/charged/reachable fleet still sails FIRST, so
            // MilitaryCompositionTests stays byte-identical) and BEFORE massing more warships. It only runs when Rung 1
            // did NOT (no ready fleet, or the fleet is already en route). One transport (repeat: false — not a standing
            // mass); the load/land steps are B5-3. Byte-identical while EnableOrderEmission is off, and for any faction
            // not at war (no target → this whole block is skipped, so a default game still hits QueueWarship below).
            if (target.IsValid && !FactionOwnsTransport(state))
            {
                foreach (var colony in state.ColoniesWithFreeLine())
                {
                    if (colony.Cargo == null) continue;   // AutoAddSubJobs needs a CargoStorageDB

                    foreach (var designKvp in state.Info.IndustryDesigns)
                    {
                        if (!(designKvp.Value is ShipDesign transport) || !IsTroopTransport(transport)) continue;
                        if (!FeasibilityOracle.CanQueue(colony, transport, state.Info)) continue;

                        string tLineId = FreeLineFor(colony.Industry, transport.IndustryTypeID);
                        if (tLineId == null) continue;

                        var colonyEntity = colony.Colony;
                        var info = state.Info;
                        var designId = designKvp.Key;
                        var designName = transport.Name;
                        return new PlannerAction(
                            "BuildTransport",
                            $"build troop transport '{designName}' on colony {colonyEntity.Id} to carry the invasion",
                            () =>
                            {
                                var job = new IndustryJob(info, designId);
                                job.InitialiseJob(1, false);                     // ONE transport, not a standing mass
                                IndustryTools.AddJob(colonyEntity, tLineId, job);
                                IndustryTools.AutoAddSubJobs(colonyEntity, job); // material-aware (resolve the sub-tree)
                            });
                    }
                }
            }

            // Rung 3 (v1) — MASS the strike fleet: queue an armed hull on a free ship-construction line. (Reached
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

        /// <summary>A ship design that can LIFT ground troops — it mounts a troop bay (<see cref="Pulsar4X.GroundCombat.GroundBayAtb"/>).
        /// The transport twin of <see cref="IsWarship"/>. The build rung finds ANY buildable transport design this way,
        /// not a hardcoded id (the base-mod trooper is faction-specific). Internal for the gauge.</summary>
        internal static bool IsTroopTransport(ShipDesign ship)
            => ship.TryGetComponentsByAttribute<Pulsar4X.GroundCombat.GroundBayAtb>(out _);

        /// <summary>True if the faction already OWNS a ship that can carry troops (a Personnel troop bay). Internal for the gauge.</summary>
        internal static bool FactionOwnsTransport(FactionState state) => FindOwnedTransport(state) != null;

        /// <summary>The faction's owned transport SHIP (mounts a Personnel troop bay), or null. Scans ALL owned ships across
        /// every system — NOT just fleet members, because a freshly-built ship isn't auto-added to a fleet, so a fleet-only
        /// scan would miss it and re-build a transport every cycle. The entity twin of <see cref="FactionOwnsTransport"/>
        /// that the LOAD rung acts on. Internal for the gauge.</summary>
        internal static Entity FindOwnedTransport(FactionState state)
        {
            foreach (var system in state.Game.Systems)
            {
                if (system == null) continue;
                foreach (var ship in system.GetAllEntitiesWithDataBlob<ShipInfoDB>())
                    if (ship != null && ship.IsValid && ship.FactionOwnerID == state.FactionId
                        && Pulsar4X.GroundCombat.GroundTransport.BayCapacity(ship, Pulsar4X.GroundCombat.GroundCarryClass.Personnel) > 0)
                        return ship;
            }
            return null;
        }

        /// <summary>The body a ship is currently at (its <see cref="Pulsar4X.Movement.PositionDB"/> parent), or null — the
        /// staging body the LOAD rung loads from (and the same read <c>GroundTransport.ShipIsAtBody</c> uses).</summary>
        private static Entity ShipBody(Entity ship)
            => ship != null && ship.TryGetDataBlob<Pulsar4X.Movement.PositionDB>(out var pos) ? pos.Parent : null;

        /// <summary>A standing ground unit of <paramref name="factionId"/> on <paramref name="body"/> that can be loaded
        /// (any of our units on the roster; <c>TryLoadUnit</c> clears its march state), or null. Internal for the gauge.</summary>
        internal static Pulsar4X.GroundCombat.GroundUnit AvailableGroundUnitAt(Entity body, int factionId)
        {
            if (body == null || !body.IsValid || !body.TryGetDataBlob<Pulsar4X.GroundCombat.GroundForcesDB>(out var forces))
                return null;
            foreach (var u in forces.Units)
                if (u != null && u.FactionOwnerID == factionId)
                    return u;
            return null;
        }

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
