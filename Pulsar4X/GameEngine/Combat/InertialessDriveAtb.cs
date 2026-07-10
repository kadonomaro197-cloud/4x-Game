using System;
using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// An INERTIALESS DRIVE — the Exotic-propulsion "dodge like a fighter regardless of your mass" component
    /// (Propulsion ⚙2, the one genuinely-new resolver field the whole category needs). Normal evasion is bound to
    /// mass: a ship dodges only as well as its acceleration (thrust ÷ mass) lets it change vector, so a heavy capital
    /// is a sitting target (see <see cref="ShipCombatValueDB.CalculateEvasion"/>). An inertialess drive breaks that
    /// coupling — it maneuvers without inertia, so it sets a FLOOR on the hull's evasion decoupled from its mass. A
    /// dreadnought with one dodges like a corvette.
    ///
    /// The lever is the physics-breaker slot's whole point (`docs/COMPONENT-DESIGNER-DIALS.md` §2.5 / ⚙2): a big
    /// power/tech cost buys evasion a normal drive can't. It's a FLOOR, not a set: a ship whose mass-bound evasion is
    /// already higher keeps its own (the max wins), and the hard <see cref="ShipCombatValueDB.EvasionCap"/> still
    /// applies — nothing is ever fully untouchable.
    ///
    /// A component (<see cref="IComponentDesignAttribute"/>, CONVENTIONS §6) so it's designed / researched / built /
    /// mounted / lost like any part — cradle to grave (a shot-off drive drops you back to mass-bound evasion, the grave
    /// rung). <see cref="ShipCombatValueDB.CalculateEvasion"/> reads the installed drives' <see cref="EvasionOverride"/>
    /// (health-scaled) as the floor. <b>0 = no inertialess drive</b> → evasion is the ordinary mass-bound value and
    /// combat is byte-identical (every current ship). Inert on install — the combat value reads the number;
    /// install/uninstall are no-ops. Never throws. Lives beside its sole reader (the combat evasion math), like the
    /// shield / radiator / point-defense atbs.
    /// </summary>
    public class InertialessDriveAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>The evasion FLOOR this drive guarantees (0..1, capped to <see cref="ShipCombatValueDB.EvasionCap"/>
        /// at read) — how hard the hull is to hit REGARDLESS of its mass. A ship's final evasion is the greater of its
        /// ordinary mass-bound evasion and this. 0 = no override.</summary>
        [JsonProperty] public double EvasionOverride { get; internal set; }

        public InertialessDriveAtb() { }

        // double arg for the JSON/NCalc binder (gotcha L7) — the base-mod drive template feeds AtbConstrArgs(PropertyValue(...)).
        public InertialessDriveAtb(double evasionOverride)
        {
            EvasionOverride = evasionOverride < 0 ? 0 : evasionOverride;
        }

        public override object Clone() => new InertialessDriveAtb(EvasionOverride);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Inertialess Drive";
        public string AtbDescription() => $"An inertialess drive — the hull maneuvers without inertia, dodging as if its evasion were at least {EvasionOverride:0.00} no matter how heavy it is (a capital that dodges like a fighter). A floor, not a cap; the hull keeps its own higher evasion if it has one.";
    }
}
