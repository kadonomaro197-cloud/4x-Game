using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.DataStructures;

namespace Pulsar4X.GroundCombat
{
    /// <summary>How a frame gets around — informs terrain move-multipliers and, later, transport carry-class.</summary>
    public enum GroundLocomotion : byte
    {
        Foot,       // legs — infantry, mechs (walkers use Walker)
        Tracked,    // treads — tanks
        Walker,     // large multi-leg — Titans, AT-ATs
        Hover       // repulsor / grav — skimmers
    }

    /// <summary>
    /// The FRAME a ground unit is built on — the first part you pick in the assembler, the ground echo of a ship's hull.
    /// It is NOT a rigid class (no "Human type" / "Titan type"); it's a set of continuous numbers, so a Guardsman, a
    /// Space Marine, a mech and a walking cathedral are all just this attribute with different values (the developer's
    /// "anyone can create anything within reason" call, 2026-07-05). The frame provides the unit's structural budget:
    ///
    ///   • <see cref="BaseStrength"/> — the carry-capacity currency: how much mounted mass the frame can bear, AND
    ///     (via a per-item fraction) the heaviest single component it can shoulder. Augment components (power armour,
    ///     servos) ADD to this, which is exactly why an augmented human can lug a weapon a bare human can't.
    ///   • <see cref="BaseHP"/> — the frame's own toughness before armour.
    ///   • <see cref="Size"/> — the frame's bulk; feeds transport carry-size (a bigger frame eats more bay room).
    ///   • <see cref="Locomotion"/> — foot / tracked / walker / hover.
    ///   • <see cref="CarryClass"/> — which bay class hauls it (Personnel vs Vehicle), for the transport system.
    ///
    /// A component attribute (<see cref="IComponentDesignAttribute"/>) so a frame is designed / researched / built like
    /// any part (CONVENTIONS §6). Inert on install (the assembler reads these values when it computes the unit — G-D3);
    /// install/uninstall are no-ops. Design: docs/GROUND-COMBAT-MAP-DESIGN.md → unit designer.
    /// </summary>
    public class GroundChassisAtb : BaseDataBlob, IComponentDesignAttribute, IChassisAtb
    {
        /// <summary>Carry-capacity currency (mounted-mass budget) + basis for the heaviest single item the frame can bear.</summary>
        [JsonProperty] public double BaseStrength { get; internal set; }

        // --- IChassisAtb (additive, 2026-07-14): the uniform "chassis provides the budget" view. ---
        // COMPUTED getters over BaseStrength — NO backing field, NO [JsonProperty]. [JsonIgnore] is CRITICAL:
        // these public getters would otherwise be serialized as new JSON fields and change the save shape.
        // They map the existing carry-strength onto the shared shape; nothing is stored or gated through them.
        [JsonIgnore] public double StructuralBudget => BaseStrength;
        [JsonIgnore] public ChassisBudgetKind BudgetKind => ChassisBudgetKind.Carry;
        [JsonIgnore] public ComponentMountType PartMount => ComponentMountType.GroundUnit;
        /// <summary>The frame's own toughness, before armour parts add more.</summary>
        [JsonProperty] public double BaseHP { get; internal set; }
        /// <summary>The frame's bulk — feeds transport carry-size (bigger frame → more bay room).</summary>
        [JsonProperty] public double Size { get; internal set; }
        [JsonProperty] public GroundLocomotion Locomotion { get; internal set; } = GroundLocomotion.Foot;
        /// <summary>Which transport bay class hauls a unit on this frame (Personnel troops vs Vehicle).</summary>
        [JsonProperty] public GroundCarryClass CarryClass { get; internal set; } = GroundCarryClass.Personnel;

        public GroundChassisAtb() { }

        // double args for the JSON/NCalc binder (gotcha L7). Order = template PropertyFormula order.
        public GroundChassisAtb(double baseStrength, double baseHP, double size, double locomotion, double carryClass)
        {
            BaseStrength = baseStrength < 0 ? 0 : baseStrength;
            BaseHP = baseHP < 0 ? 0 : baseHP;
            Size = size < 0 ? 0 : size;
            Locomotion = (GroundLocomotion)(int)locomotion;
            CarryClass = (GroundCarryClass)(int)carryClass;
        }

        public override object Clone()
            => new GroundChassisAtb(BaseStrength, BaseHP, Size, (double)(int)Locomotion, (double)(int)CarryClass);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Chassis";
        public string AtbDescription()
            => $"A {Locomotion} frame — strength {BaseStrength:0} (carry budget), HP {BaseHP:0}, size {Size:0}, hauled as {CarryClass}.";
    }
}
