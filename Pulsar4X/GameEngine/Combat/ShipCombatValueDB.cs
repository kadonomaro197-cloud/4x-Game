using Newtonsoft.Json;
using Pulsar4X.Components;
using Pulsar4X.Damage;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Weapons;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// The "spec sheet" for a ship in the auto-resolve combat engine.
    ///
    /// Two numbers, computed once when the ship is built (<see cref="Pulsar4X.Ships.ShipFactory"/>.CreateShip)
    /// from the ship's REAL installed parts:
    ///   - <see cref="Firepower"/> : hurt dealt per second   — joules/sec from its beam weapons
    ///   - <see cref="Toughness"/> : punishment it can absorb — joules, from components + armour
    /// plus <see cref="RoleWeight"/> (combatant vs utility) used to decide which ships die first.
    ///
    /// Firepower (J/s) and Toughness (J) are on the SAME scale on purpose: Firepower x time is joules, so
    /// the salvo loop's time-to-kill (Toughness / Firepower) comes out in seconds. This is the v1 spine input
    /// the auto-resolver multiplies by doctrine/commander/range modifiers.
    ///
    /// It deliberately does NOT use the per-pixel damage sim (which deposits ~0 damage today and is parked
    /// for v2) — it reads static design data, so it is fast and can't be broken by that sim.
    /// See docs/COMBAT-DESIGN.md -> "What we're building (v1)".
    ///
    /// v1 stubs (flagged): missile launchers add a flat <see cref="MissileLauncherFirepowerStub"/> each (real
    /// value = warhead energy x salvo rate, wired in v2); toughness weights every component equally (a simple
    /// hull-integrity proxy); recalc-on-damage is a v2 refinement — in v1 a ship is alive at full value or
    /// removed whole, so a value computed once at build is enough.
    /// </summary>
    public class ShipCombatValueDB : BaseDataBlob
    {
        /// <summary>Flat firepower (J/s) a single missile launcher contributes until ordnance energy is wired (v2).</summary>
        public const double MissileLauncherFirepowerStub = 100_000.0;

        /// <summary>Role weight for a hull that carries no weapons (utility/transport). v1 stub.</summary>
        public const double UtilityRoleWeight = 0.25;

        /// <summary>Joules a single component absorbs before it is destroyed. Straight from the damage tuning:
        /// 1000 damage points = a dead component at 100 J per point => 1e5 J (a 100 kJ hit kills a component).
        /// Keeps Toughness (joules) on the same scale as Firepower x time (joules).</summary>
        public const double ComponentHitPoints_J = 100_000.0;

        /// <summary>Joules of protection one unit of armour thickness adds to Toughness.</summary>
        public const double ArmorHitPointsPerThickness_J = 100_000.0;

        /// <summary>Damage-per-second the ship can deal (joules/sec). Higher = stronger.</summary>
        [JsonProperty] public double Firepower { get; internal set; }

        /// <summary>Punishment the ship can absorb before it dies (joules). Higher = harder to kill.</summary>
        [JsonProperty] public double Toughness { get; internal set; }

        /// <summary>1.0 for a combatant, <see cref="UtilityRoleWeight"/> for a utility hull. Utility ships
        /// are lower-priority targets (they absorb casualties last) and contribute less fleet strength.</summary>
        [JsonProperty] public double RoleWeight { get; internal set; } = 1.0;

        public ShipCombatValueDB() { }

        public ShipCombatValueDB(double firepower, double toughness, double roleWeight)
        {
            Firepower = firepower;
            Toughness = toughness;
            RoleWeight = roleWeight;
        }

        public ShipCombatValueDB(ShipCombatValueDB db)
        {
            Firepower = db.Firepower;
            Toughness = db.Toughness;
            RoleWeight = db.RoleWeight;
        }

        public override object Clone()
        {
            return new ShipCombatValueDB(this);
        }

        /// <summary>
        /// Reads a built ship's installed components + armour and returns its combat value.
        /// Defensive: never throws — a ship with no parts simply rates 0 firepower / 0 toughness.
        /// </summary>
        public static ShipCombatValueDB Calculate(Entity ship)
        {
            double firepower = 0;
            double toughness = 0;

            if (ship.TryGetDataBlob<ComponentInstancesDB>(out var instances))
            {
                // Toughness: every working component is a hit-point sink (joules), scaled by current health.
                foreach (var comp in instances.AllComponents.Values)
                    toughness += comp.HealthPercent * ComponentHitPoints_J;

                // Firepower from beam weapons: joules-per-second = Energy / ChargePeriod, scaled by health.
                if (instances.TryGetComponentsByAttribute<GenericBeamWeaponAtb>(out var beams))
                {
                    foreach (var comp in beams)
                    {
                        if (comp.Design.TryGetAttribute<GenericBeamWeaponAtb>(out var beam))
                        {
                            double period = beam.ChargePeriod > 0 ? beam.ChargePeriod : 1.0;
                            firepower += (beam.Energy / period) * comp.HealthPercent;
                        }
                    }
                }

                // Firepower from missile launchers: flat stub each until warhead energy is wired (v2).
                if (instances.TryGetComponentsByAttribute<MissileLauncherAtb>(out var launchers))
                {
                    foreach (var comp in launchers)
                        firepower += MissileLauncherFirepowerStub * comp.HealthPercent;
                }
            }

            // Armour thickness adds straight to toughness (joules).
            if (ship.TryGetDataBlob<EntityDamageProfileDB>(out var dmgProfile))
                toughness += dmgProfile.Armor.thickness * ArmorHitPointsPerThickness_J;

            // Role: anything that can shoot is a combatant; everything else is a low-priority utility hull.
            double roleWeight = firepower > 0 ? 1.0 : UtilityRoleWeight;

            return new ShipCombatValueDB(firepower, toughness, roleWeight);
        }
    }
}
