using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Components;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Weapons
{
    /// <summary>
    /// A PLASMA REPEATER — a bolt-thrower: it flings discrete blobs of charged plasma at a FINITE velocity. It is the
    /// weapon the TWO-AXIS model (docs/WEAPON-TAXONOMY-DESIGN.md) was created to express and the old single-axis
    /// <see cref="Pulsar4X.Combat.WeaponClass"/> could NOT — a blaster/plasma bolt is <b>Energy in NATURE</b> (so a
    /// shield only half-soaks it — it bleeds through, like a beam) yet <b>DODGEABLE in DELIVERY</b> (a discrete, finite
    /// velocity shot — unlike a beam, a nimble ship can juke it, like a railgun slug). Energy × dodgeable is a corner the
    /// fused Beam/Railgun/Missile/Flak enum had no cell for; the split Nature (Energy) × Delivery (Bolt) axes do.
    ///
    /// v1 scope: like the railgun/flak/disruptor Atbs, this exists purely to feed the AUTO-RESOLVE combat engine its
    /// flavor stats — <see cref="Pulsar4X.Combat.ShipCombatValueDB"/>.Calculate reads it into a finite-velocity,
    /// Energy-nature, Bolt-delivery <c>WeaponProfile</c>. It does NOT implement <c>IFireWeaponInstr</c> and registers
    /// nothing on install, so the parked per-pixel firing sim never touches it. Built from JSON via
    /// <c>AtbConstrArgs(energyPerShot, roundsPerSecond, muzzleVelocity, tracking)</c> — the constructor arg order MUST
    /// match that formula in weapons.json (a mismatch throws at New Game / design build, gotcha #10).
    /// </summary>
    public class PlasmaBoltWeaponAtb : IComponentDesignAttribute
    {
        /// <summary>Energy delivered by one plasma bolt on impact (joules) — its damage per shot.</summary>
        [JsonProperty] public double EnergyPerShot_J { get; internal set; }

        /// <summary>Shots per second. Drives damage/sec (× energy/shot) and the saturation floor on a dodger.</summary>
        [JsonProperty] public double RoundsPerSecond { get; internal set; }

        /// <summary>Bolt velocity (m/s). FINITE — well below light-speed — so, unlike a beam, an evasive ship can
        /// dodge it. Faster than a railgun slug, but still dodgeable by the nimble.</summary>
        [JsonProperty] public double MuzzleVelocity_mps { get; internal set; }

        /// <summary>How well it follows an evasive target, 0..1. A dumb bolt is low (it can't steer after firing).</summary>
        [JsonProperty] public double Tracking { get; internal set; }

        public PlasmaBoltWeaponAtb() { }

        /// <summary>JSON constructor. Arg order MUST match <c>AtbConstrArgs(...)</c> in weapons.json.</summary>
        public PlasmaBoltWeaponAtb(double energyPerShot, double roundsPerSecond, double muzzleVelocity, double tracking)
        {
            EnergyPerShot_J = energyPerShot;
            RoundsPerSecond = roundsPerSecond;
            MuzzleVelocity_mps = muzzleVelocity;
            Tracking = tracking;
        }

        public PlasmaBoltWeaponAtb(PlasmaBoltWeaponAtb db)
        {
            EnergyPerShot_J = db.EnergyPerShot_J;
            RoundsPerSecond = db.RoundsPerSecond;
            MuzzleVelocity_mps = db.MuzzleVelocity_mps;
            Tracking = db.Tracking;
        }

        // No-op install/uninstall: feeds the combat VALUE (auto-resolve), not the parked per-pixel firing sim.
        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Plasma Repeater";
        public string AtbDescription() => "Dodgeable energy bolt: finite-velocity (a nimble ship can juke it) but energy in nature (a shield only half-soaks it — it bleeds through).";
    }
}
