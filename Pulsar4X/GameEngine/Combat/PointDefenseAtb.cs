using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// A POINT-DEFENSE MOUNT — the dedicated missile-killer. Weapons pilot W6 (missiles as resolvable targets): a
    /// missile (a GUIDED projectile) is a thing you can SHOOT DOWN on its way in, unlike a beam or a slug. A ship's
    /// point-defense fires at the incoming missiles and knocks out a fraction of that guided fire before it reaches the
    /// hull. This is the term that makes an anti-missile screen a real decision — the space echo of "bring flak to
    /// answer the missile boats."
    ///
    /// The model is an AGGREGATE intercept (not a per-missile sim, which the auto-resolver doesn't do): a fleet's total
    /// point-defense <see cref="InterceptRating_Jps"/> intercepts a saturating fraction of the incoming GUIDED
    /// (<see cref="WeaponDelivery.Guided"/>) fire — lots of PD vs a light missile salvo stops most of it, but a big
    /// enough swarm SATURATES the defenses and leaks through (mirrors evasion/flak: nothing is ever fully immune). Only
    /// guided fire is interceptable — beams and slugs aren't projectiles you shoot down.
    ///
    /// A component (<see cref="IComponentDesignAttribute"/>, CONVENTIONS §6) so it's designed / researched / built /
    /// mounted / lost like any part — cradle to grave. <see cref="ShipCombatValueDB.Calculate"/> sums the installed
    /// mounts' <see cref="InterceptRating_Jps"/> (health-scaled) into <c>ShipCombatValueDB.PointDefense_Jps</c>, which the
    /// resolver reads to intercept incoming missile fire. A ship with NO PD reads 0 → the intercept step is SKIPPED and
    /// incoming fire is untouched, so combat is byte-identical (every current ship). Inert on install — the combat value
    /// reads the number; install/uninstall are no-ops. Never throws.
    /// </summary>
    public class PointDefenseAtb : BaseDataBlob, IComponentDesignAttribute
    {
        /// <summary>The rate of incoming GUIDED (missile) fire this mount can shoot down, in joules/sec — the same
        /// currency as a weapon's <see cref="WeaponProfile.DamagePerSecond"/>, so the intercept saturates against the
        /// incoming missile damage. Bigger rating = a bigger missile salvo it can stop before the PD is overwhelmed.</summary>
        [JsonProperty] public double InterceptRating_Jps { get; internal set; }

        public PointDefenseAtb() { }

        // double arg for the JSON/NCalc binder (gotcha L7) — the base-mod PD template feeds AtbConstrArgs(PropertyValue(...)).
        public PointDefenseAtb(double interceptRating_Jps)
        {
            InterceptRating_Jps = interceptRating_Jps < 0 ? 0 : interceptRating_Jps;
        }

        public override object Clone() => new PointDefenseAtb(InterceptRating_Jps);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Point Defense Mount";
        public string AtbDescription() => $"A point-defense mount intercepting up to {InterceptRating_Jps:0} J/s of incoming guided (missile) fire — knocks out a fraction of the missiles before they reach the hull (saturates against a big enough swarm).";
    }
}
