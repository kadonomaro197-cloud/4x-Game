using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// A ship AMMO MAGAZINE — the store an ammo-fed ship weapon (railgun slugs, flak pellets, missiles) draws from,
    /// measured in MASS (kg of ammo). The SPACE twin of the ground <see cref="GroundCombat.GroundMagazineAtb"/> (same
    /// model, same units): the reactor answers "can this ship POWER its energy weapons?", the magazine answers "can it
    /// FEED its ammo weapons?". This is Weapons pilot W3 — mid-battle ammo depletion for the ship resolver, the item the
    /// ground side already has (`GroundAmmo`) and the space stepped resolve lacked.
    ///
    /// A component (<see cref="IComponentDesignAttribute"/>, CONVENTIONS §6) so it's designed / researched / built /
    /// mounted / lost like any part — cradle to grave. <see cref="ShipCombatValueDB.Calculate"/> sums the installed
    /// magazines' <see cref="Capacity_kg"/> (health-scaled) into <c>ShipCombatValueDB.AmmoCapacity_kg</c>; the fleet's
    /// aggregate pool (`FleetCombatStateDB.AmmoPool_kg`) drains as its ammo weapons fire and, when dry, those weapons go
    /// silent while the fleet fights on with energy weapons — the ship echo of the ground ammo model. A ship with NO
    /// magazine reads 0 capacity, so its ammo pool is disabled and combat is byte-identical (the W3a/W3b invariant).
    /// Inert on install — like the shield/chassis, the combat value reads the number; install/uninstall are no-ops.
    /// Never throws.
    /// </summary>
    public class ShipMagazineAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>Ammunition the magazine holds, in kilograms. Summed (health-scaled) into the ship's combat value as
        /// <c>AmmoCapacity_kg</c>, which seeds the fleet's combat ammo pool.</summary>
        [JsonProperty] public double Capacity_kg { get; internal set; }

        public ShipMagazineAtb() { }

        // double arg for the JSON/NCalc binder (gotcha L7) — the base-mod magazine template feeds AtbConstrArgs(PropertyValue(...)).
        public ShipMagazineAtb(double capacity_kg)
        {
            Capacity_kg = capacity_kg < 0 ? 0 : capacity_kg;
        }

        public override object Clone() => new ShipMagazineAtb(Capacity_kg);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Ammo Magazine";
        public string AtbDescription() => $"A ship ammo magazine holding {Capacity_kg:0} kg of ammunition — feeds the ship's ammo weapons (railgun, flak, missiles).";
    }
}
