using Newtonsoft.Json;
using Pulsar4X.Components;
using Pulsar4X.Damage;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Movement;
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

        /// <summary>Evasion tuning (v1 stub): a ship of this volume (m³) is half as hard to hit on size alone.
        /// Bigger than this = an easy target; much smaller = a hard one.</summary>
        public const double SizeReference_m3 = 1_000.0;

        /// <summary>Evasion tuning (v1 stub): a ship pulling this acceleration (m/s²) gets half the agility bonus.
        /// It's the *rate it can change its vector* (thrust ÷ mass), not its top speed across the system.</summary>
        public const double AgilityReference_mps2 = 5.0;

        /// <summary>Hard cap on <see cref="Evasion"/> — nothing is ever fully untouchable (a beam is light-speed,
        /// and enough volume of fire saturates any dodge). v1 stub.</summary>
        public const double EvasionCap = 0.95;

        /// <summary>Damage-per-second the ship can deal (joules/sec). Higher = stronger.</summary>
        [JsonProperty] public double Firepower { get; internal set; }

        /// <summary>Punishment the ship can absorb before it dies (joules). Higher = harder to kill.</summary>
        [JsonProperty] public double Toughness { get; internal set; }

        /// <summary>1.0 for a combatant, <see cref="UtilityRoleWeight"/> for a utility hull. Utility ships
        /// are lower-priority targets (they absorb casualties last) and contribute less fleet strength.</summary>
        [JsonProperty] public double RoleWeight { get; internal set; } = 1.0;

        /// <summary>How hard this ship is to HIT, 0 (a sitting brick) to <see cref="EvasionCap"/> (a nimble
        /// fighter). Derived from its size (small = hard to hit) and the acceleration it can pull (thrust ÷ mass
        /// = how fast it changes vector). Separate from Toughness: toughness is soaking what lands, evasion is not
        /// being hit in the first place — and unlike toughness it depends on the WEAPON (you can't dodge a beam).
        /// v1 stub: sensors and crew experience are not yet factored (flagged for v2). Used by the dodge model.</summary>
        [JsonProperty] public double Evasion { get; internal set; }

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
            Evasion = db.Evasion;
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

            return new ShipCombatValueDB(firepower, toughness, roleWeight) { Evasion = CalculateEvasion(ship) };
        }

        /// <summary>
        /// How hard this ship is to hit (0..<see cref="EvasionCap"/>), from its size and the acceleration it can
        /// pull. Small + nimble = high; big + sluggish = ~0. A ship with no engine (no thrust) can't dodge at all.
        /// Defensive: missing mass/thrust data => 0. v1 stub: sensors + crew experience not yet factored.
        /// </summary>
        public static double CalculateEvasion(Entity ship)
        {
            if (!ship.TryGetDataBlob<MassVolumeDB>(out var mv) || mv.Volume_m3 <= 0 || mv.MassDry <= 0)
                return 0;

            // Small target = hard to hit: 1.0 when tiny, falls toward 0 as volume grows past the reference.
            double sizeFactor = SizeReference_m3 / (SizeReference_m3 + mv.Volume_m3);

            // Agile target = hard to track: acceleration is thrust ÷ mass (rate of vector change, not top speed).
            double accel = 0;
            if (ship.TryGetDataBlob<NewtonThrustAbilityDB>(out var thrust) && thrust.ThrustInNewtons > 0)
                accel = thrust.ThrustInNewtons / mv.MassDry;
            double agilityFactor = accel / (AgilityReference_mps2 + accel); // 0 when sluggish, → 1 when nimble

            return EvasionCap * sizeFactor * agilityFactor;
        }
    }
}
