using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.DataStructures;

namespace Pulsar4X.Stations
{
    /// <summary>
    /// A STATION CHASSIS — the structural frame a space station is assembled on, the off-world echo of a ship's
    /// <see cref="Pulsar4X.Ships.ShipHullAtb"/> and a ground unit's <c>GroundChassisAtb</c>. It is the first part
    /// you pick when you design a station in the Entity Assembler; the modules (reactor, cargo, research lab,
    /// factory, refinery) bolt onto it. It carries the station's STRUCTURAL BUDGET — how much module it can bear —
    /// in the <see cref="ChassisBudgetKind.Structure"/> currency (the "reserved for station/off-world hosts" budget
    /// kind that already existed on the shared chassis abstraction).
    ///
    /// A component (CONVENTIONS §6): researched → built → mounted → lost. Inert on install — the assembler reads
    /// this value at design time; there is no per-entity install behaviour (exactly like <see cref="Pulsar4X.Ships.ShipHullAtb"/>
    /// and the weapon/shield atbs). Double-arg ctor for the JSON/NCalc binder (landmine L7); the ctor-arg order
    /// must match the template's <c>AtbConstrArgs(...)</c> order.
    /// </summary>
    public class StationChassisAtb : IComponentDesignAttribute, IChassisAtb
    {
        /// <summary>How much mounted module (structure budget) this chassis can bear.</summary>
        [JsonProperty] public double StructuralAllowance { get; internal set; }

        // --- IChassisAtb (additive): the uniform "chassis provides the budget" view. ---
        // COMPUTED getters over StructuralAllowance — NO backing field, NO [JsonProperty]. [JsonIgnore] is
        // CRITICAL: this class serializes OptOut (every public getter is written unless ignored), so without it
        // Newtonsoft would emit these as new JSON fields and change the save shape. They map the stored
        // StructuralAllowance onto the shared shape; nothing is stored or gated through them.
        [JsonIgnore] public double StructuralBudget => StructuralAllowance;
        [JsonIgnore] public ChassisBudgetKind BudgetKind => ChassisBudgetKind.Structure;
        [JsonIgnore] public ComponentMountType PartMount => ComponentMountType.Station;

        public StationChassisAtb() { }

        public StationChassisAtb(double structuralAllowance)
        {
            StructuralAllowance = structuralAllowance < 0 ? 0 : structuralAllowance;
        }

        // Read by the assembler, not an install hook — inert on install/uninstall.
        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Station Chassis";
        public string AtbDescription() => $"A station chassis — a {StructuralAllowance:0} structure budget for mounted modules.";
    }
}
