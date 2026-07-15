using Pulsar4X.DataStructures;

namespace Pulsar4X.Interfaces
{
    /// <summary>
    /// The CURRENCY a chassis measures its structural budget in — the units the "how much can it carry"
    /// number is counted in. A ship hull budgets in kilograms of MASS; a ground chassis budgets in
    /// carry-STRENGTH; a station or a building would budget in structure/footprint. Naming the currency
    /// keeps the shared <see cref="IChassisAtb.StructuralBudget"/> number honest — you always know what it
    /// means without knowing which kind of frame produced it.
    /// </summary>
    public enum ChassisBudgetKind
    {
        /// <summary>Kilograms of mass — a ship hull's mass ceiling (<see cref="Pulsar4X.Ships.ShipHullAtb"/>).</summary>
        Mass,
        /// <summary>Carry-strength — a ground chassis's mounted-mass budget (<see cref="Pulsar4X.GroundCombat.GroundChassisAtb"/>).</summary>
        Carry,
        /// <summary>Structural allowance — reserved for station/off-world hosts.</summary>
        Structure,
        /// <summary>Ground footprint — reserved for planetary buildings.</summary>
        Footprint
    }

    /// <summary>
    /// A CHASSIS — the structural frame something is built on: a ship's hull, a ground unit's chassis, and
    /// (later) a station or a building. This is the one common shape they all share: every frame answers
    /// "how much can my mounted parts weigh/carry?" (<see cref="StructuralBudget"/>), "in what currency?"
    /// (<see cref="BudgetKind"/>), and "which mount do my parts use?" (<see cref="PartMount"/>). Think of it
    /// as the rating plate on any load-bearing frame — a crane, a shelf, a truck bed — the number that says
    /// how much it can hold, stamped in whatever units make sense for that frame.
    ///
    /// PURELY ADDITIVE (2026-07-14): the two existing frames (<see cref="Pulsar4X.Ships.ShipHullAtb"/> and
    /// <see cref="Pulsar4X.GroundCombat.GroundChassisAtb"/>) implement this as [JsonIgnore] COMPUTED getters
    /// over the property they ALREADY store — no backing field, no new JSON field, no rename. The live budget
    /// gates (ShipDesign.Recalculate / GroundUnitAssembly.Compute) are untouched; this interface only lets
    /// future code read a frame's budget uniformly without caring which kind it is.
    /// </summary>
    public interface IChassisAtb
    {
        /// <summary>How much this chassis can carry — its structural budget, in the units named by <see cref="BudgetKind"/>.</summary>
        double StructuralBudget { get; }

        /// <summary>The currency the <see cref="StructuralBudget"/> is measured in (kg mass vs carry-strength vs ...).</summary>
        ChassisBudgetKind BudgetKind { get; }

        /// <summary>Which <see cref="ComponentMountType"/> the parts mounted on this chassis use.</summary>
        ComponentMountType PartMount { get; }
    }
}
