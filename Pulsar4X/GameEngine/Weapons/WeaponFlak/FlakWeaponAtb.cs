using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Components;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Weapons
{
    /// <summary>
    /// A FLAK / point-defense gun: a rapid-fire weapon that throws a CLOUD of small pellets each shot. Any single
    /// pellet barely scratches a capital's armour, but the sheer VOLUME of fire — rate-of-fire × pellets-per-shot
    /// — fills the sky, so an evasive target can't juke it. In the dodge model its high SATURATION floors the
    /// landed fraction: flak is the answer to fighters and missiles (the fast, evasive things a railgun misses),
    /// and the off-triangle "support" node every balanced fleet wants some of. The developer's point exactly: a
    /// flak cannon firing once a minute is useless; saturation is rate × spread, a weapon-design variable.
    ///
    /// v1 scope: like <see cref="RailgunWeaponAtb"/>, this Atb exists only to feed the AUTO-RESOLVE combat value
    /// (<see cref="Pulsar4X.Combat.ShipCombatValueDB"/> reads it into a <c>WeaponClass.Flak</c> <c>WeaponProfile</c>).
    /// It implements only <c>IComponentDesignAttribute</c> (no-op install, no <c>IFireWeaponInstr</c>), so the
    /// parked per-pixel firing sim never touches it. Built from JSON via
    /// <c>AtbConstrArgs(muzzleVelocity, damagePerPellet, roundsPerSecond, pelletsPerShot, tracking)</c> — the
    /// constructor arg order MUST match that formula in weapons.json.
    /// </summary>
    public class FlakWeaponAtb : IComponentDesignAttribute
    {
        /// <summary>Muzzle velocity of the pellets (m/s). Moderate — short-ranged, not a beam.</summary>
        [JsonProperty] public double MuzzleVelocity_mps { get; internal set; }

        /// <summary>Damage one pellet does on impact (joules). LOW — flak only tickles a capital; its strength is
        /// volume, not per-hit punch.</summary>
        [JsonProperty] public double DamagePerPellet_J { get; internal set; }

        /// <summary>Shots (bursts) per second.</summary>
        [JsonProperty] public double RoundsPerSecond { get; internal set; }

        /// <summary>Pellets thrown per shot — the spread. Saturation = RoundsPerSecond × PelletsPerShot, so a fast
        /// gun with a wide spread fills the sky (high saturation = floors the hit fraction on a dodger).</summary>
        [JsonProperty] public double PelletsPerShot { get; internal set; }

        /// <summary>How well the cloud follows an evasive target, 0..1. Medium — better than a single ballistic
        /// slug (the spread helps), well below a guided missile.</summary>
        [JsonProperty] public double Tracking { get; internal set; }

        /// <summary>RECOIL — the kick per burst (Weapons pilot W4; the same model as the railgun's). A rapid-fire flak
        /// mount on a light hull shakes it off aim, so <see cref="Pulsar4X.Combat.ShipCombatValueDB"/> reduces effective
        /// tracking by <c>chassisMass / (chassisMass + Recoil)</c> at build. 0 = no penalty (byte-identical). Trailing
        /// ctor arg so a template that omits it binds at 0.</summary>
        [JsonProperty] public double Recoil { get; internal set; }

        public FlakWeaponAtb() { }

        /// <summary>JSON constructor (original 5-arg form). The component binder (<c>Activator.CreateInstance</c>) matches
        /// ctors by EXACT ARITY — a default/optional param does NOT count — so this explicit 5-arg overload must stay for
        /// the existing <c>flak-weapon</c> template's 5-value <c>AtbConstrArgs(...)</c> to bind (recoil defaults 0).</summary>
        public FlakWeaponAtb(double muzzleVelocity, double damagePerPellet, double roundsPerSecond, double pelletsPerShot, double tracking)
            : this(muzzleVelocity, damagePerPellet, roundsPerSecond, pelletsPerShot, tracking, 0) { }

        /// <summary>JSON constructor WITH recoil (Weapons pilot W4). A template that dials recoil passes a 6th value.
        /// Arg order MUST match <c>AtbConstrArgs(...)</c> in weapons.json.</summary>
        public FlakWeaponAtb(double muzzleVelocity, double damagePerPellet, double roundsPerSecond, double pelletsPerShot, double tracking,
            double recoil)
        {
            MuzzleVelocity_mps = muzzleVelocity;
            DamagePerPellet_J = damagePerPellet;
            RoundsPerSecond = roundsPerSecond;
            PelletsPerShot = pelletsPerShot;
            Tracking = tracking;
            Recoil = recoil < 0 ? 0 : recoil;
        }

        public FlakWeaponAtb(FlakWeaponAtb db)
        {
            MuzzleVelocity_mps = db.MuzzleVelocity_mps;
            DamagePerPellet_J = db.DamagePerPellet_J;
            RoundsPerSecond = db.RoundsPerSecond;
            PelletsPerShot = db.PelletsPerShot;
            Tracking = db.Tracking;
            Recoil = db.Recoil;
        }

        // No-op install/uninstall: flak contributes to the combat VALUE (auto-resolve), not the parked per-pixel
        // firing sim, so it registers no fire-control / weapon state (cf. RailgunWeaponAtb / GenericWeaponAtb).
        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Flak / Point-Defense Gun";
        public string AtbDescription() => "High-saturation short-range gun: fills the sky with pellets. The fighter- and missile-killer; only tickles a capital.";
    }
}
