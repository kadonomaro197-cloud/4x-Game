using System.Collections.Generic;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Industry;
using Pulsar4X.Ships;
using Pulsar4X.Weapons;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.8 (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md): the DEFEND resolver — the crisis brain for the
    /// <see cref="NeedTier.Survive"/> tier (at war / under threat). The other half of parking-lot gap G1: the NPC
    /// settled the Defend objective but had NO resolver, so it FROZE while being attacked. This closes G1.
    ///
    /// v1 acts AT HOME. The "position force WHERE the threat is" half — a reachability read (no <c>CanReach</c>
    /// exists), a multi-jump auto-router, fuel/charge-readiness (production ships spawn empty), fleet composition,
    /// and target selection — is the deferred P-3 military sub-subsystem (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md
    /// §Conquer/Defend). So v1 Defend BUILDS and POSTURES; it does not yet sail to the border — a deliberate
    /// deferral, not a gap. Three rungs, nearest-unmet first:
    ///   Rung 0 — RECALL an in-flight OFFENSIVE fleet home (P3.4 — never orphan an invasion). When the faction flips
    ///            to Defend (a genuine crisis), a fleet still sailing into foreign/enemy space is called back to the
    ///            home colony body, so a committed invasion that Defend interrupted doesn't coast on unmanaged.
    ///   Rung A — BUILD a warship at a colony with a free ship-construction line (an armed <see cref="ShipDesign"/>,
    ///            the same queue lever GrowEconomy pulls, filtered to hulls that mount a weapon; the oracle's
    ///            ShipDesign crew/talent gate already applies).
    ///   Rung B — POSTURE an owned fleet defensively (the 'defensive-line' catalog doctrine) when no warship can be
    ///            queued — the always-available fallback, so Defend is never a pure no-op.
    ///
    /// One step per monthly cycle. Pure decision (builds the <see cref="PlannerAction"/> closure; <c>EmitOrders</c>
    /// runs it). Byte-identical while order emission is off.
    /// </summary>
    public sealed class DefendResolver : IObjectiveResolver
    {
        public StrategicObjective Handles => StrategicObjective.Defend;

        private const string DefensiveDoctrineId = "defensive-line";

        public PlannerAction Resolve(FactionState state, StrategicObjectiveDB objective)
        {
            if (state == null) return PlannerAction.None;

            // Rung 0 (P3.4, Operation Earthfall findings/A3 seam 5 — RECALL: never orphan a sortie on a genuine Defend
            // switch). When the faction flips to Defend (a real crisis, not the transient wobble ObjectiveTransition
            // already protects), any in-flight OFFENSIVE fleet still warping into foreign/enemy space is called HOME to
            // defend. Issues the player's own MoveToSystemBodyOrder to the home colony body (the SAME fleet-move lever
            // the Conquer strike uses outbound), so the recalled fleet warps back — instead of coasting toward an enemy
            // world with no one driving the invasion. Placed FIRST (getting the guns home outranks building or
            // re-posturing); one recall per cycle. Skips a fleet already heading home / between our own worlds
            // (IsOutbound), so it isn't re-issued endlessly. v1 is same-system (MoveToSystemBodyOrder warps within a
            // system — a cross-system fleet's leg-by-leg recall rides the same multi-jump routing the Conquer strike
            // defers). No home colony (a station-only / colony-less faction) → nothing to recall to → skip. Byte-identical
            // while emission is off (this whole resolver runs inside the gated EmitOrders) AND when no fleet is outbound.
            var homeBody = HomeBody(state);
            if (homeBody != null)
            {
                var ownBodyIds = OwnColonyBodyIds(state);
                foreach (var fleet in state.OwnedFleets())
                {
                    if (fleet == null || !fleet.IsValid) continue;
                    if (!IsOutbound(fleet, ownBodyIds)) continue;

                    var game = state.Game;
                    int factionId = state.FactionId;
                    var f = fleet;
                    var home = homeBody;
                    return new PlannerAction(
                        "RecallFleet",
                        $"recall in-flight fleet {f.Id} home to body {home.Id} (Defend — don't orphan the sortie)",
                        () =>
                        {
                            var cmd = Pulsar4X.Movement.MoveToSystemBodyOrder.CreateCommand(factionId, f, home);
                            game.OrderHandler.HandleOrder(cmd);   // the ONE step (warp the fleet back home)
                        });
                }
            }

            // Rung A — build defensive strength: queue an armed hull on a free ship-construction line.
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
                        $"build '{designName}' on colony {colonyEntity.Id} for defense",
                        () =>
                        {
                            var job = new IndustryJob(info, designId);
                            job.InitialiseJob(1, true);                    // repeat: keep the yard producing
                            IndustryTools.AddJob(colonyEntity, lineId, job);
                            IndustryTools.AutoAddSubJobs(colonyEntity, job); // material-aware (resolve the sub-tree)
                        });
                }
            }

            // Rung B — no warship to build: posture an owned fleet defensively (the always-available fallback).
            if (state.Game.StartingGameData.CombatDoctrines.TryGetValue(DefensiveDoctrineId, out var defDoctrine))
            {
                var now = state.Game.TimePulse.GameGlobalDateTime;
                foreach (var fleet in state.OwnedFleets())
                {
                    // Skip a fleet already holding a defensive posture (don't churn a no-op every cycle).
                    if (fleet.TryGetDataBlob<FleetDoctrineDB>(out var d) && d.DoctrineId == DefensiveDoctrineId)
                        continue;

                    var f = fleet;
                    return new PlannerAction(
                        "SetDefensivePosture",
                        $"set fleet {f.Id} to {DefensiveDoctrineId}",
                        () => FleetDoctrine.TrySetDoctrine(f, defDoctrine, now));   // honours the switch cooldown
                }
            }

            return PlannerAction.None;
        }

        /// <summary>
        /// A ship design is a WARSHIP if any component design it mounts carries a direct-fire or ordnance weapon
        /// attribute. <c>ShipCombatValueDB.Firepower</c> is only computed at BUILD, so at PLAN time we read the
        /// design's own components (the same signal the combat-value calculator sums).
        /// </summary>
        private static bool IsWarship(ShipDesign ship)
            => ship.TryGetComponentsByAttribute<GenericBeamWeaponAtb>(out _)
            || ship.TryGetComponentsByAttribute<RailgunWeaponAtb>(out _)
            || ship.TryGetComponentsByAttribute<FlakWeaponAtb>(out _)
            || ship.TryGetComponentsByAttribute<PlasmaBoltWeaponAtb>(out _)
            || ship.TryGetComponentsByAttribute<DisruptorWeaponAtb>(out _)
            || ship.TryGetComponentsByAttribute<MissileLauncherAtb>(out _);

        /// <summary>The id of a free (empty-queue) production line on this colony that runs the given industry type,
        /// or null if none. (Mirrors <see cref="GrowEconomyResolver"/>'s helper — kept local for slice independence.)</summary>
        private static string FreeLineFor(IndustryAbilityDB industry, string industryTypeId)
        {
            foreach (var lineKvp in industry.ProductionLines)
                if (lineKvp.Value.IndustryTypeRates.ContainsKey(industryTypeId) && lineKvp.Value.Jobs.Count == 0)
                    return lineKvp.Key;
            return null;
        }

        /// <summary>P3.4 — the faction's HOME colony body: the planet of its first valid colony (the recall destination).
        /// Null for a colony-less / station-only faction (there's nowhere to recall to → the recall rung is skipped).</summary>
        private static Entity HomeBody(FactionState state)
        {
            foreach (var colony in state.Info.Colonies)
            {
                if (colony == null || !colony.IsValid) continue;
                if (colony.TryGetDataBlob<Pulsar4X.Colonies.ColonyInfoDB>(out var ci)
                    && ci.PlanetEntity != null && ci.PlanetEntity.IsValid)
                    return ci.PlanetEntity;
            }
            return null;
        }

        /// <summary>P3.4 — the entity ids of every body this faction holds a colony on. A fleet warping toward one of
        /// these is heading into friendly space (not an outbound sortie), so the recall skips it — the guard that keeps
        /// a fleet moving between our own worlds from being needlessly recalled.</summary>
        private static HashSet<int> OwnColonyBodyIds(FactionState state)
        {
            var ids = new HashSet<int>();
            foreach (var colony in state.Info.Colonies)
            {
                if (colony == null || !colony.IsValid) continue;
                if (colony.TryGetDataBlob<Pulsar4X.Colonies.ColonyInfoDB>(out var ci)
                    && ci.PlanetEntity != null && ci.PlanetEntity.IsValid)
                    ids.Add(ci.PlanetEntity.Id);
            }
            return ids;
        }

        /// <summary>P3.4 — is <paramref name="fleet"/> an OUTBOUND sortie worth recalling: does any ship in it hold a
        /// <see cref="Pulsar4X.Movement.WarpMovingDB"/> whose warp target is a body NOT in <paramref name="ownBodyIds"/>
        /// (i.e. warping into foreign/enemy space)? A fleet that isn't moving, is warping home / between our own worlds,
        /// or is on a target-less direct-position warp is NOT outbound (returns false) — so the recall isn't re-issued
        /// once the fleet's warp target has flipped to a home body. Read-only; uses the recursive fleet-ship walk.</summary>
        private static bool IsOutbound(Entity fleet, HashSet<int> ownBodyIds)
        {
            foreach (var ship in FleetCombat.Ships(fleet))
            {
                if (ship == null || !ship.IsValid) continue;
                if (!ship.TryGetDataBlob<Pulsar4X.Movement.WarpMovingDB>(out var warp)) continue;
                var target = warp.TargetEntity;
                if (target != null && target.IsValid && !ownBodyIds.Contains(target.Id))
                    return true;   // a ship warping toward foreign/enemy space → recall the fleet
            }
            return false;
        }
    }
}
