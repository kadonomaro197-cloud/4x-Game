using System.Collections.Generic;
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

        /// <summary>v1 stub: a missile's shot velocity (m/s) until ordnance speed is read from the design (v2). Slow vs a beam.</summary>
        public const double MissileVelocityStub_mps = 5_000.0;

        /// <summary>v1 stub: missiles are guided, so they track an evasive target well (0..1).</summary>
        public const double MissileTrackingStub = 0.9;

        /// <summary>v1 stub: a launcher's effective tracks/sec until salvo size + reload are read (v2).</summary>
        public const double MissileSaturationStub = 1.0;

        /// <summary>Flak effective range (m) — SHORT. Point-defense: pellets disperse and bleed energy fast, so it
        /// only reaches the close-in screen (catches fighters/missiles at knife-to-near range). The hard cutoff the
        /// closing model gates on. v1 class-default; a per-design field (paid-for in the designer) is the follow-up.</summary>
        public const double FlakRange_m = 50_000.0;       // ~50 km

        /// <summary>Railgun effective range (m) — MID. A kinetic slug is unguided and bleeds accuracy with distance,
        /// so it's a medium-range gun: longer than close-in flak (~50 km), shorter than a guided missile (~1000 km).
        /// This is the number that turns "railguns are rangeless, so they fire across the whole engagement bubble"
        /// (the live "ships firing outside their detection range" report, 2026-06-28) into a real closing fight — the
        /// closing model holds railgun fire until the gap is within this range, which is FAR inside any sensor reach,
        /// so a railgun shot only lands on a target the ship has actually closed with. Was the flagged Root-A
        /// follow-up (railguns had no design range field → 0 = unbounded). v1 class-default; a per-design field
        /// (paid-for in the designer, like beam's MaxRange) is the next step.</summary>
        public const double RailgunRange_m = 500_000.0;   // ~500 km (mid: flak 50 km < railgun < missile 1000 km)

        /// <summary>Missile range (m) — LONG. The standoff opener: guided, fuel/Δv-limited, out-reaches every gun so
        /// it fires first as fleets close. v1 class-default stub (the launcher/ordnance Δv would derive the real
        /// number); a per-design field (paid-for) is the follow-up. Gives the range LAYERING the closing fight wants:
        /// missile (long) → flak/railgun (mid, railgun rangeless-but-inaccurate) → beam (knife).</summary>
        public const double MissileRange_m = 1_000_000.0; // ~1000 km

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

        /// <summary>The ship's weapons as flavor profiles (damage/velocity/tracking/saturation per weapon) — the
        /// per-weapon-type breakdown the dodge model + weapon triangle read. <see cref="Firepower"/> is the sum of
        /// these profiles' damage. Empty for an unarmed hull. See docs/WEAPONS-AND-DODGE-DESIGN.md.</summary>
        [JsonProperty] public List<WeaponProfile> Weapons { get; internal set; } = new();

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
            Weapons = new List<WeaponProfile>();
            if (db.Weapons != null)
                foreach (var w in db.Weapons) Weapons.Add(new WeaponProfile(w));
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
            double toughness = 0;
            var weapons = new List<WeaponProfile>();

            if (ship.TryGetDataBlob<ComponentInstancesDB>(out var instances))
            {
                // Toughness: every working component is a hit-point sink (joules), scaled by current health.
                foreach (var comp in instances.AllComponents.Values)
                    toughness += comp.HealthPercent * ComponentHitPoints_J;

                // Beam weapons: damage/sec = Energy / ChargePeriod (scaled by health); ~light-speed; tracks well
                // (BaseHitChance); saturation = one pulse per charge period (its rate of fire).
                if (instances.TryGetComponentsByAttribute<GenericBeamWeaponAtb>(out var beams))
                {
                    foreach (var comp in beams)
                    {
                        if (comp.Design.TryGetAttribute<GenericBeamWeaponAtb>(out var beam))
                        {
                            double period = beam.ChargePeriod > 0 ? beam.ChargePeriod : 1.0;
                            double dps = (beam.Energy / period) * comp.HealthPercent;
                            // Range (Root A): beams carry their design MaxRange (0 = unbounded, the legacy convention).
                            weapons.Add(new WeaponProfile(WeaponClass.Beam, dps, beam.BeamSpeed, beam.BaseHitChance, 1.0 / period, beam.MaxRange, WeaponNature.Energy, WeaponDelivery.Beam));
                        }
                    }
                }

                // Railguns / slug-throwers: kinetic, FINITE muzzle velocity, ballistic (low tracking). damage/sec
                // = energy-per-shot × rounds/sec; saturation = rounds/sec (one slug per shot). Dodged by the
                // nimble, brutal vs the sluggish — the corner of the triangle opposite the beam.
                if (instances.TryGetComponentsByAttribute<RailgunWeaponAtb>(out var railguns))
                {
                    foreach (var comp in railguns)
                    {
                        if (comp.Design.TryGetAttribute<RailgunWeaponAtb>(out var rg))
                        {
                            double dps = rg.KineticEnergyPerShot_J * rg.RoundsPerSecond * comp.HealthPercent;
                            // Range: a finite MID range (RailgunRange_m) so the closing model holds railgun fire until
                            // the gap is within it — the fix for railguns firing across the whole engagement bubble
                            // (and so "outside detection range"). v1 class-default; a per-design field (an Atb + JSON,
                            // like beam's MaxRange) is the next step. Only bites when EnableClosingRange is on (live);
                            // with it off (the headless fixtures) SeparationOf is 0 so the range gate is a no-op.
                            weapons.Add(new WeaponProfile(WeaponClass.Railgun, dps, rg.MuzzleVelocity_mps, rg.Tracking, rg.RoundsPerSecond, RailgunRange_m, WeaponNature.Kinetic, WeaponDelivery.Slug));
                        }
                    }
                }

                // Flak / point-defense: rapid-fire pellet clouds. Low per-pellet damage, but HIGH saturation
                // (rounds/sec × pellets/shot) floors the hit fraction — it catches the fast, evasive things a
                // railgun misses (fighters, missiles). damage/sec = damage/pellet × saturation.
                if (instances.TryGetComponentsByAttribute<FlakWeaponAtb>(out var flaks))
                {
                    foreach (var comp in flaks)
                    {
                        if (comp.Design.TryGetAttribute<FlakWeaponAtb>(out var flak))
                        {
                            double saturation = flak.RoundsPerSecond * flak.PelletsPerShot;
                            double dps = flak.DamagePerPellet_J * saturation * comp.HealthPercent;
                            // Range (the authentic-closing pass): flak is SHORT-ranged point defense (hard cutoff).
                            weapons.Add(new WeaponProfile(WeaponClass.Flak, dps, flak.MuzzleVelocity_mps, flak.Tracking, saturation, FlakRange_m, WeaponNature.Kinetic, WeaponDelivery.Cloud));
                        }
                    }
                }

                // Missile launchers: flat damage stub each (warhead energy is v2); slow + guided (tracks) — the
                // weapon flak answers. Velocity/tracking/saturation are v1 stubs.
                if (instances.TryGetComponentsByAttribute<MissileLauncherAtb>(out var launchers))
                {
                    foreach (var comp in launchers)
                    {
                        double dps = MissileLauncherFirepowerStub * comp.HealthPercent;
                        // Range (the authentic-closing pass): missiles are the LONG-range standoff opener (hard cutoff).
                        weapons.Add(new WeaponProfile(WeaponClass.Missile, dps, MissileVelocityStub_mps, MissileTrackingStub, MissileSaturationStub, MissileRange_m, WeaponNature.Explosive, WeaponDelivery.Guided));
                    }
                }
            }

            // Armour thickness adds straight to toughness (joules).
            if (ship.TryGetDataBlob<EntityDamageProfileDB>(out var dmgProfile))
                toughness += dmgProfile.Armor.thickness * ArmorHitPointsPerThickness_J;

            // Firepower is the sum of every weapon's damage/sec (same value as before — backward compatible).
            double firepower = 0;
            foreach (var w in weapons) firepower += w.DamagePerSecond;

            // Role: anything that can shoot is a combatant; everything else is a low-priority utility hull.
            double roleWeight = firepower > 0 ? 1.0 : UtilityRoleWeight;

            return new ShipCombatValueDB(firepower, toughness, roleWeight)
            {
                Evasion = CalculateEvasion(ship),
                Weapons = weapons,
            };
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
