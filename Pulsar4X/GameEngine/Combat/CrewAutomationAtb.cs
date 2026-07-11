using System;
using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// A SHIP-AUTOMATION module — the Enhancers ⚙6.3 "Systems ▸ Automation" component: an AI / automation suite that
    /// does part of the crew's job, so the hull runs with FEWER people. It is the one live-wireable dial in the
    /// otherwise net-new Systems door (the dossier's "cheapest live win"): the combat-computer half has no hook in the
    /// auto-resolver, but "run with less crew" rides straight onto the manpower system.
    ///
    /// What it does: at design time <see cref="Pulsar4X.Ships.ShipDesign.Recalculate"/> subtracts this module's
    /// <see cref="CrewReduction"/> from the hull's BULK crew requirement (the ratings/workers an automated system
    /// replaces) — never from the veteran-cadre <see cref="Pulsar4X.Ships.ShipDesign.TalentReq"/> (you can't automate
    /// away the officers). So an automated hull commits less scarce workforce from the building colony's
    /// <see cref="Pulsar4X.Colonies.ColonyManpowerDB"/> — a real economy×engineering trade: spend mass + high tech to
    /// spend fewer people.
    ///
    /// A component (<see cref="IComponentDesignAttribute"/>, CONVENTIONS §6) — designed / researched / built / mounted
    /// / lost like any part (cradle to grave). <b>No module → 0 reduction</b> → crew is unchanged and the whole
    /// manpower path is byte-identical (every current ship). Inert on install (the crew math reads the number at design
    /// recalc; install/uninstall are no-ops). Never throws. Lives beside the other Enhancers components.
    /// </summary>
    public class CrewAutomationAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>How many BULK crew positions this automation suite replaces (≥ 0). Subtracted from the hull's
        /// bulk-workforce requirement at design recalc (clamped so the total never falls below the veteran-cadre
        /// talent it can't automate away). 0 = an inert module → byte-identical.</summary>
        [JsonProperty] public double CrewReduction { get; internal set; } = 0;

        public CrewAutomationAtb() { }

        // double arg for the JSON/NCalc binder (gotcha L7) — the base-mod template feeds AtbConstrArgs(PropertyValue(...)).
        public CrewAutomationAtb(double crewReduction)
        {
            CrewReduction = crewReduction < 0 ? 0 : crewReduction;
        }

        public override object Clone() => new CrewAutomationAtb(CrewReduction);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Crew Automation";
        public string AtbDescription() => $"An automation / AI suite that runs part of the ship, cutting {CrewReduction:0} bulk crew positions (never the veteran cadre). Fewer people to draw from the colony, at the cost of mass and high tech.";
    }
}
