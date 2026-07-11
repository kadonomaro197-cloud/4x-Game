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

            // Rung (v1) — MASS the strike fleet: queue an armed hull on a free ship-construction line. (The target /
            // reach / fuel / strike rungs are the deferred P-3 military sub-subsystem.)
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
