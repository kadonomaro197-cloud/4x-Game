using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Components;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Weapons
{
    /// <summary>
    /// A RAILGUN / slug-thrower: a kinetic weapon that flings a solid mass at a finite (not light-speed) muzzle
    /// velocity. The opposite of a beam in the dodge model — its shots travel slow enough to be dodged by a
    /// nimble ship, but they hit brutally hard against something too sluggish to get out of the way (a capital).
    ///
    /// v1 scope: this Atb exists purely to give the AUTO-RESOLVE combat engine its flavor stats (it is read by
    /// <see cref="Pulsar4X.Combat.ShipCombatValueDB"/>.Calculate to build a <c>WeaponProfile</c> of
    /// <c>WeaponClass.Railgun</c>). It deliberately does NOT implement <c>IFireWeaponInstr</c> and registers
    /// nothing on install (like <see cref="GenericWeaponAtb"/>), so the per-pixel firing sim never touches it —
    /// the firing sim is a parked v2 visual skin (docs/WEAPONS-AND-DODGE-DESIGN.md). Built from JSON via
    /// <c>AtbConstrArgs(muzzleVelocity, kineticEnergyPerShot, roundsPerSecond, tracking)</c> — the constructor
    /// arg order MUST match that formula in weapons.json (gotcha: a mismatch throws at New Game / design build).
    /// </summary>
    public class RailgunWeaponAtb : IComponentDesignAttribute
    {
        /// <summary>Muzzle velocity of the slug (m/s). Fast but FINITE — far below light-speed, so the dodge model
        /// lets an evasive target juke it (compare a beam at ~3e8 m/s).</summary>
        [JsonProperty] public double MuzzleVelocity_mps { get; internal set; }

        /// <summary>Kinetic energy delivered by one slug on impact (joules) — its damage per shot.</summary>
        [JsonProperty] public double KineticEnergyPerShot_J { get; internal set; }

        /// <summary>Shots per second. Drives both damage/sec (× energy/shot) AND saturation (the floor on how much
        /// fire still lands on an evasive target) — a 1000-round/sec spinal slug saturates the sky.</summary>
        [JsonProperty] public double RoundsPerSecond { get; internal set; }

        /// <summary>How well the weapon follows an evasive target, 0..1. A railgun is BALLISTIC (no guidance), so
        /// this is near zero — it can't correct after the slug leaves the barrel.</summary>
        [JsonProperty] public double Tracking { get; internal set; }

        /// <summary>RECOIL — the kick this gun delivers to its own ship each shot (Weapons pilot W4). A heavy gun on a
        /// light hull throws the whole ship off its aim, so <see cref="Pulsar4X.Combat.ShipCombatValueDB"/> reduces the
        /// weapon's EFFECTIVE tracking by <c>chassisMass / (chassisMass + Recoil)</c> at build — the same gun tracks
        /// worse on a frigate than on a battleship. 0 = a recoilless mount / no penalty (byte-identical). Units are
        /// mass-comparable (kg-scale) so recoil ≈ a fraction of the hull's mass is a meaningful penalty. Trailing ctor
        /// arg so a template that omits it binds at 0.</summary>
        [JsonProperty] public double Recoil { get; internal set; }

        public RailgunWeaponAtb() { }

        /// <summary>JSON constructor (original 4-arg form). The component binder (<c>Activator.CreateInstance</c>) matches
        /// ctors by EXACT ARITY — a default/optional param does NOT count — so this explicit 4-arg overload must stay for
        /// the existing <c>railgun-weapon</c> template's 4-value <c>AtbConstrArgs(...)</c> to bind (recoil defaults 0).</summary>
        public RailgunWeaponAtb(double muzzleVelocity, double kineticEnergyPerShot, double roundsPerSecond, double tracking)
            : this(muzzleVelocity, kineticEnergyPerShot, roundsPerSecond, tracking, 0) { }

        /// <summary>JSON constructor WITH recoil (Weapons pilot W4). A template that dials recoil passes a 5th value.
        /// Arg order MUST match <c>AtbConstrArgs(...)</c> in weapons.json.</summary>
        public RailgunWeaponAtb(double muzzleVelocity, double kineticEnergyPerShot, double roundsPerSecond, double tracking,
            double recoil)
        {
            MuzzleVelocity_mps = muzzleVelocity;
            KineticEnergyPerShot_J = kineticEnergyPerShot;
            RoundsPerSecond = roundsPerSecond;
            Tracking = tracking;
            Recoil = recoil < 0 ? 0 : recoil;
        }

        public RailgunWeaponAtb(RailgunWeaponAtb db)
        {
            MuzzleVelocity_mps = db.MuzzleVelocity_mps;
            KineticEnergyPerShot_J = db.KineticEnergyPerShot_J;
            RoundsPerSecond = db.RoundsPerSecond;
            Tracking = db.Tracking;
            Recoil = db.Recoil;
        }

        // No-op install/uninstall: a railgun contributes to the combat VALUE (auto-resolve), not the parked
        // per-pixel firing sim, so it registers no fire-control / weapon state (cf. GenericWeaponAtb).
        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Railgun Weapon";
        public string AtbDescription() => "Kinetic slug-thrower: finite-velocity, ballistic, brutal vs slow targets, dodged by nimble ones.";
    }
}
