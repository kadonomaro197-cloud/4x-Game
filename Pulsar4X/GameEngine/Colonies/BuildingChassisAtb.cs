using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.DataStructures;

namespace Pulsar4X.Colonies
{
    /// <summary>
    /// A BUILDING FOUNDATION — the structural base a planet-side building is assembled on, the on-world echo of a
    /// ship's <see cref="Pulsar4X.Ships.ShipHullAtb"/>, a ground unit's <c>GroundChassisAtb</c>, and a station's
    /// <see cref="Pulsar4X.Stations.StationChassisAtb"/>. It is the first part you pick when you design a building in
    /// the Entity Assembler; the modules (factory machines, a research wing, a refinery line, defences) bolt onto it.
    /// It carries the building's FOOTPRINT BUDGET — how much module it can host — in the
    /// <see cref="ChassisBudgetKind.Footprint"/> currency (the "reserved for planetary buildings" budget kind that
    /// already existed on the shared chassis abstraction).
    ///
    /// A component (CONVENTIONS §6): researched → built → installed on the colony → lost. Inert on install (the
    /// assembler reads this value at design time; there is no per-entity install behaviour), exactly like the ship /
    /// station hull atbs. The foundation template also carries a <c>GroundFootprintAtb</c> so a completed building is a
    /// LOCATED presence on the planet's ground map (a capture/bombard target) — this atb is only the budget provider.
    /// Double-arg ctor for the JSON/NCalc binder (landmine L7); the ctor-arg order must match the template's
    /// <c>AtbConstrArgs(...)</c> order.
    /// </summary>
    public class BuildingChassisAtb : IComponentDesignAttribute, IChassisAtb
    {
        /// <summary>How much mounted module (footprint budget) this foundation can host.</summary>
        [JsonProperty] public double FootprintAllowance { get; internal set; }

        // --- IChassisAtb (additive): the uniform "chassis provides the budget" view. COMPUTED getters over
        // FootprintAllowance — NO backing field, NO [JsonProperty]. [JsonIgnore] is CRITICAL (this class serializes
        // OptOut, so every public getter is written unless ignored), exactly like the ship/station hull atbs. ---
        [JsonIgnore] public double StructuralBudget => FootprintAllowance;
        [JsonIgnore] public ChassisBudgetKind BudgetKind => ChassisBudgetKind.Footprint;
        [JsonIgnore] public ComponentMountType PartMount => ComponentMountType.PlanetInstallation;

        public BuildingChassisAtb() { }

        public BuildingChassisAtb(double footprintAllowance)
        {
            FootprintAllowance = footprintAllowance < 0 ? 0 : footprintAllowance;
        }

        // Read by the assembler, not an install hook — inert on install/uninstall.
        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Building Foundation";
        public string AtbDescription() => $"A building foundation — a {FootprintAllowance:0} footprint budget for mounted modules.";
    }
}
