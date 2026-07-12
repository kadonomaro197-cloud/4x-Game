using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;

namespace Pulsar4X.Ships
{
    /// <summary>
    /// A SHIP HULL — the structural frame a ship is built on, the space echo of the ground
    /// <c>GroundChassisAtb</c>. It carries the ship's MASS BUDGET: the mass ceiling the mounted
    /// components must fit within (§0b, the one hard wall — "a box can't hold more than it can hold").
    /// <see cref="ShipDesign.Recalculate"/> reads a mounted hull's <see cref="MassBudget"/> as the
    /// design's budget; a hull-less design falls back to a generous self-derived figure, so this is
    /// additive — nothing is over budget until a real (tighter) hull is fitted.
    ///
    /// A component (CONVENTIONS §6): researched → built → mounted → lost. Inert on install — the
    /// design reads the value at recalc time; there is no per-entity install behaviour (like the weapon
    /// and shield atbs). Double-arg ctor for the JSON/NCalc binder (landmine L7); order = the template's
    /// <c>AtbConstrArgs(...)</c> order.
    /// </summary>
    public class ShipHullAtb : IComponentDesignAttribute
    {
        /// <summary>The mass ceiling (kg) a ship's mounted components must fit within.</summary>
        [JsonProperty] public double MassBudget { get; internal set; }

        public ShipHullAtb() { }

        public ShipHullAtb(double massBudget)
        {
            MassBudget = massBudget < 0 ? 0 : massBudget;
        }

        // Read by ShipDesign.Recalculate, not an install hook — inert on install/uninstall.
        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Ship Hull";
        public string AtbDescription() => $"A ship hull — a {MassBudget:0} kg mass budget for mounted components.";
    }
}
