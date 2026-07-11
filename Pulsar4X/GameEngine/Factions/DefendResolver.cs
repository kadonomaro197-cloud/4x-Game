using Pulsar4X.Combat;
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
    /// deferral, not a gap. Two rungs, nearest-unmet first:
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
    }
}
