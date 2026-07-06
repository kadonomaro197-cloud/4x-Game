using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// An AMMO MAGAZINE — the store an ammo-fed weapon (flak pellets, railgun slugs, …) draws from. Measured in MASS
    /// (kg of ammo), the developer's call (2026-07-06). It is the AMMO-axis twin of the reactor on the supply side: the
    /// reactor answers "can this unit POWER its energy weapons?", the magazine answers "can it FEED its ammo weapons?".
    ///
    /// A component (<see cref="IComponentDesignAttribute"/>, CONVENTIONS §6) so it's designed / researched / built /
    /// mounted / lost like any part — cradle to grave. The ground assembler (<see cref="GroundUnitAssembly"/>) reads
    /// <see cref="Capacity_kg"/> for the DESIGN-TIME ammo gate (an Ammo/Both weapon with no magazine is an illegal design,
    /// P2c-a) and, later, to size how long the unit fights before it runs dry and must resupply (P2c combat depletion).
    /// Inert on install — like the chassis, the assembler reads the value; install/uninstall are no-ops. Never throws.
    /// Design: docs/WEAPON-UNIFICATION-DESIGN.md P2.
    /// </summary>
    public class GroundMagazineAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>Ammunition the magazine holds, in kilograms. Feeds the design-time ammo gate and (P2c depletion) the
        /// unit's fight-duration-before-resupply.</summary>
        [JsonProperty] public double Capacity_kg { get; internal set; }

        public GroundMagazineAtb() { }

        // double arg for the JSON/NCalc binder (gotcha L7) — the base-mod magazine template feeds AtbConstrArgs(PropertyValue(...)).
        public GroundMagazineAtb(double capacity_kg)
        {
            Capacity_kg = capacity_kg < 0 ? 0 : capacity_kg;
        }

        public override object Clone() => new GroundMagazineAtb(Capacity_kg);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Ammo Magazine";
        public string AtbDescription() => $"An ammo magazine holding {Capacity_kg:0} kg of ammunition — feeds a unit's ammo weapons (flak, railgun).";
    }
}
