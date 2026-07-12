using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// A HEAT RADIATOR — the sink that lets a ship SUSTAIN energy-weapon fire without cooking itself. Weapons pilot W5
    /// (heat → sustained rate): a beam/plasma barrage dumps waste heat into the ship; a radiator dissipates it. If the
    /// energy weapons out-produce the radiators, the ship's heat pool climbs past capacity and its energy fire THROTTLES
    /// (burst-vs-sustained) until it cools. The heat twin of the ammo magazine — the magazine limits how long the KINETIC
    /// guns fire, the radiator limits how hard the ENERGY guns can fire SUSTAINED.
    ///
    /// A component (<see cref="IComponentDesignAttribute"/>, CONVENTIONS §6) so it's designed / researched / built /
    /// mounted / lost like any part — cradle to grave. <see cref="ShipCombatValueDB.Calculate"/> sums the installed
    /// radiators' <see cref="Capacity_kJ"/> (health-scaled) into <c>ShipCombatValueDB.HeatCapacity_kJ</c>, which sets the
    /// fleet's heat ceiling AND its per-salvo cooling. A ship with NO radiator reads 0 capacity → the heat step is
    /// SKIPPED and its energy fire is untouched, so combat is byte-identical (every current ship). Inert on install — the
    /// combat value reads the number; install/uninstall are no-ops. Never throws.
    /// </summary>
    public class RadiatorAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>Heat the radiator can hold/shed, in kilojoules — the ship's heat ceiling (health-scaled sum) and the
        /// basis of its per-salvo cooling. Bigger radiator = more sustained energy fire before the guns throttle.</summary>
        [JsonProperty] public double Capacity_kJ { get; internal set; }

        public RadiatorAtb() { }

        // double arg for the JSON/NCalc binder (gotcha L7) — the base-mod radiator template feeds AtbConstrArgs(PropertyValue(...)).
        public RadiatorAtb(double capacity_kJ)
        {
            Capacity_kJ = capacity_kJ < 0 ? 0 : capacity_kJ;
        }

        public override object Clone() => new RadiatorAtb(Capacity_kJ);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Heat Radiator";
        public string AtbDescription() => $"A heat radiator shedding {Capacity_kJ:0} kJ — lets the ship sustain energy-weapon fire without overheating (the guns throttle if heat outruns the radiators).";
    }
}
