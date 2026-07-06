using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Components;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Weapons
{
    /// <summary>
    /// An ION DISRUPTOR — the ANTI-SHIELD exotic weapon (docs/WEAPON-TAXONOMY-DESIGN.md §5/§6, Phase D). It fires a
    /// coherent ion lance at ~light-speed (so, like a beam, it can't be dodged), but its damage NATURE is
    /// <c>WeaponNature.Exotic</c>: it is DESIGNED to pass straight through a shield pool (the shield's exotic-soak
    /// fraction is 0), striking the hull as if the deflector weren't there. It is the weapon a shielded ship
    /// (the Enterprise/energy archetype) has no answer to — the rock to the shield's scissors. Against an
    /// UNSHIELDED hull it's just a modest light-speed gun (raw exotic damage, no bonus), so it isn't a
    /// strictly-better beam; its whole point is the shield matchup.
    ///
    /// v1 scope: like the railgun/flak Atbs, this exists purely to feed the AUTO-RESOLVE combat engine its flavor
    /// stats — <see cref="Pulsar4X.Combat.ShipCombatValueDB"/>.Calculate reads it into a light-speed, Exotic-nature
    /// <c>WeaponProfile</c>. It does NOT implement <c>IFireWeaponInstr</c> and registers nothing on install, so the
    /// parked per-pixel firing sim never touches it. Built from JSON via
    /// <c>AtbConstrArgs(energyPerShot, roundsPerSecond)</c> — the constructor arg order MUST match that formula in
    /// weapons.json (a mismatch throws at New Game / design build, gotcha #10).
    /// </summary>
    public class DisruptorWeaponAtb : IComponentDesignAttribute
    {
        /// <summary>Energy delivered by one ion lance on impact (joules) — its damage per shot.</summary>
        [JsonProperty] public double EnergyPerShot_J { get; internal set; }

        /// <summary>Shots per second. Drives damage/sec (× energy/shot). Saturation is moot for an undodgeable
        /// light-speed lance, but it's carried for readout parity with the other weapon types.</summary>
        [JsonProperty] public double RoundsPerSecond { get; internal set; }

        public DisruptorWeaponAtb() { }

        /// <summary>JSON constructor. Arg order MUST match <c>AtbConstrArgs(...)</c> in weapons.json.</summary>
        public DisruptorWeaponAtb(double energyPerShot, double roundsPerSecond)
        {
            EnergyPerShot_J = energyPerShot;
            RoundsPerSecond = roundsPerSecond;
        }

        public DisruptorWeaponAtb(DisruptorWeaponAtb db)
        {
            EnergyPerShot_J = db.EnergyPerShot_J;
            RoundsPerSecond = db.RoundsPerSecond;
        }

        // No-op install/uninstall: feeds the combat VALUE (auto-resolve), not the parked per-pixel firing sim.
        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Ion Disruptor";
        public string AtbDescription() => "Anti-shield ion lance: light-speed (undodgeable), and its exotic nature bypasses shields to strike the hull.";
    }
}
