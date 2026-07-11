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

        /// <summary>Ion disruptor (anti-shield exotic) effective range (m) — MID. A coherent ion lance that reaches
        /// like a light gun but pays for its shield-piercing exotic nature with a modest raw yield. v1 class-default
        /// (a per-design range field is the follow-up, like beam's MaxRange). FLAGGED default for the balance pass.</summary>
        public const double DisruptorRange_m = 400_000.0; // ~400 km (mid: reaches, but not a standoff missile)

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

        /// <summary>Speed of light (m/s) — the muzzle velocity a light-speed weapon (a beam / ion lance) reports, so
        /// the dodge model treats it as undodgeable (velocity ≫ the dodge reference).</summary>
        public const double LightSpeed_mps = 299_792_458.0;

        /// <summary>FIRE CONTROL → tracking (Sensors ⚙3, Fire-Control CONNECT). Default OFF so every existing combat
        /// fixture is byte-identical (the fire-control component already lives on the Aegis/Picket/Bastion with a
        /// non-neutral <c>TrackingSpeed</c>, so wiring it changes those ships — this is a behaviour change, gated like
        /// the closing-range / detection flags). The client turns it ON at startup. When on, a ship's BEAM weapons have
        /// their <see cref="WeaponProfile.Tracking"/> raised toward 1.0 by its best installed
        /// <see cref="Weapons.BeamFireControlAtbDB"/> director — a better director lands more fire on an evasive target.
        /// The dead knob (`BeamFireControlAtbDB.TrackingSpeed`, verified 0 reads) comes ALIVE.</summary>
        public static bool EnableFireControlTracking = false;

        /// <summary>Tracking-speed at which a fire-control director half-closes the gap between a weapon's own tracking
        /// and perfect (1.0). The base-mod `beam-fire-control` reports 5000, so at the reference it's a moderate ×0.5
        /// director. Flagged BALANCE dial.</summary>
        public const double FireControlTrackingReference = 5000.0;

        /// <summary>The 0..1 factor a fire-control director's <paramref name="trackingSpeed"/> contributes:
        /// <c>ts / (ts + FireControlTrackingReference)</c> — 0 with no director, rising toward 1 for a fast one. The
        /// weapon's effective tracking becomes <c>base + (1 − base) × factor</c> (the director closes the gap to a
        /// perfect track). Returns 0 for a non-positive speed. Pure.</summary>
        public static double FireControlTrackingFactor(double trackingSpeed)
            => trackingSpeed <= 0 ? 0 : trackingSpeed / (trackingSpeed + FireControlTrackingReference);

        /// <summary>FIRE CONTROL → PD-only mode (Sensors ⚙3, Fire-Control CONNECT). Default OFF → byte-identical: no
        /// base ship carries a <c>FinalFireOnly</c> director, so even turned ON nothing changes until one is installed.
        /// The client turns it on. When on, a ship with a live FinalFireOnly (CIWS) director routes its BEAM weapons'
        /// damage/sec into the <see cref="PointDefense_Jps"/> pool (missile interception) instead of anti-ship
        /// firepower — the dead <c>BeamFireControlAtbDB.FinalFireOnly</c> knob (verified 0 reads) comes ALIVE and pairs
        /// with the W6 point-defense system. v1: ALL the ship's beams switch (a dedicated CIWS hull); per-weapon
        /// director→weapon assignment is the flagged follow-up.</summary>
        public static bool EnableFinalFireOnlyPD = false;

        /// <summary>True if the ship has at least one INSTALLED, undestroyed beam director set to <c>FinalFireOnly</c>
        /// (a CIWS director). Health-gated: a shot-off director (health 0) doesn't count, so knocking out the CIWS
        /// director reverts its beams to ordinary fire — the grave rung. Defensive: no components / no director → false.</summary>
        public static bool HasLiveFinalFireOnlyDirector(ComponentInstancesDB instances)
        {
            if (instances != null && instances.TryGetComponentsByAttribute<Weapons.BeamFireControlAtbDB>(out var directors))
                foreach (var comp in directors)
                    if (comp.HealthPercent > 0 && comp.Design.TryGetAttribute<Weapons.BeamFireControlAtbDB>(out var fc) && fc.FinalFireOnly)
                        return true;
            return false;
        }

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

        /// <summary>The SHIELD pool in joules (sum of installed shield generators, health-scaled) — a depleting/regen
        /// buffer the resolve drains BEFORE toughness (docs/WEAPON-TAXONOMY-DESIGN.md §6). 0 = no shield generator, so
        /// combat is byte-identical for an unshielded ship until the resolve wiring lands.</summary>
        [JsonProperty] public double ShieldCapacity_J { get; internal set; }

        /// <summary>How fast the shield pool refills (joules/sec) — the "shields recharging" rate between salvos.</summary>
        [JsonProperty] public double ShieldRegen_Jps { get; internal set; }

        /// <summary>The AMMO magazine capacity in kg (sum of installed <see cref="ShipMagazineAtb"/> magazines,
        /// health-scaled) — Weapons pilot W3, the space echo of the ground magazine. Seeds the fleet's combat ammo pool
        /// (`FleetCombatStateDB.AmmoPool_kg`), which the resolve drains as the fleet's ammo weapons fire; when dry those
        /// weapons go silent. <b>0 = no magazine</b> → the ammo pool is disabled and combat is byte-identical (every
        /// current ship, until the W3c base-mod magazine). v1: one aggregate pool per fleet, like the shield.</summary>
        [JsonProperty] public double AmmoCapacity_kg { get; internal set; }

        /// <summary>The HEAT radiator capacity in kJ (sum of installed <see cref="RadiatorAtb"/>, health-scaled) —
        /// Weapons pilot W5. Sets the fleet's heat ceiling and per-salvo cooling; when the fleet's energy-weapon fire
        /// drives its heat pool (`FleetCombatStateDB.HeatPool_kJ`) past this, its energy fire throttles (sustained-fire
        /// limit). <b>0 = no radiator</b> → the heat step is skipped and combat is byte-identical (every current ship,
        /// until the W5c base-mod radiator). v1: one aggregate per fleet, like the shield/ammo.</summary>
        [JsonProperty] public double HeatCapacity_kJ { get; internal set; }

        /// <summary>The POINT-DEFENSE intercept rating in joules/sec (sum of installed <see cref="PointDefenseAtb"/>,
        /// health-scaled) — Weapons pilot W6. The resolver reads the fleet's total PD to intercept a saturating fraction
        /// of incoming GUIDED (missile) fire before it reaches the hull, so an anti-missile screen is a real decision.
        /// <b>0 = no point-defense</b> → the intercept step is skipped and incoming fire is byte-identical (every current
        /// ship, until the W6c base-mod PD mount). v1: one aggregate per fleet, like the shield/ammo/heat.</summary>
        [JsonProperty] public double PointDefense_Jps { get; internal set; }

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
            AmmoCapacity_kg = db.AmmoCapacity_kg;
            HeatCapacity_kJ = db.HeatCapacity_kJ;
            PointDefense_Jps = db.PointDefense_Jps;
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
            double shieldCapacity = 0, shieldRegen = 0;   // the space shield pool (0 if no generator → combat unchanged)
            double ammoCapacity = 0;                      // the ammo magazine pool (0 if no magazine → combat unchanged, W3)
            double heatCapacity = 0;                      // the radiator heat ceiling (0 if no radiator → combat unchanged, W5)
            double pointDefense = 0;                      // the PD intercept rating (0 if no PD → combat unchanged, W6)
            // Chassis mass (kg) for the recoil→tracking penalty (W4): a heavy kinetic gun shakes a light hull off aim.
            // 0 if unknown → RecoilTrackingFactor returns 1.0 (no penalty). Every weapon defaults Recoil 0 → byte-identical.
            double chassisMass = ship.TryGetDataBlob<MassVolumeDB>(out var chassisMv) && chassisMv.MassDry > 0 ? chassisMv.MassDry : 0;

            if (ship.TryGetDataBlob<ComponentInstancesDB>(out var instances))
            {
                // Toughness: every working component is a hit-point sink (joules), scaled by current health.
                foreach (var comp in instances.AllComponents.Values)
                    toughness += comp.HealthPercent * ComponentHitPoints_J;

                // FIRE CONTROL → beam tracking (⚙3): the ship's best installed director (health-scaled TrackingSpeed)
                // raises how well its beams track an evasive target. 0 unless the flag is on (default off → byte-
                // identical; the client turns it on). A shot-off director tracks worse (health-scaled = grave rung).
                double fcTracking = 0;
                if (EnableFireControlTracking && instances.TryGetComponentsByAttribute<BeamFireControlAtbDB>(out var directors))
                {
                    double bestTrackingSpeed = 0;
                    foreach (var comp in directors)
                        if (comp.Design.TryGetAttribute<BeamFireControlAtbDB>(out var fc))
                        {
                            double ts = fc.TrackingSpeed * comp.HealthPercent;
                            if (ts > bestTrackingSpeed) bestTrackingSpeed = ts;
                        }
                    fcTracking = FireControlTrackingFactor(bestTrackingSpeed);
                }

                // PD-ONLY MODE (⚙3, Fire-Control CONNECT): a FinalFireOnly director (CIWS) dedicates this ship's beams
                // to intercepting incoming ordnance — their damage/sec feeds the point-defense pool, not anti-ship
                // firepower. Off/false unless the flag is on AND a live FinalFireOnly director is installed (no base
                // ship has one → byte-identical). A shot-off director reverts the beams to normal fire (grave rung).
                bool beamsArePointDefense = EnableFinalFireOnlyPD && HasLiveFinalFireOnlyDirector(instances);

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
                            // PD-only director (⚙3): dedicate this beam's output to interception (Jps) — it counts as
                            // point-defense, not anti-ship firepower. Skip adding it to the weapons list.
                            if (beamsArePointDefense) { pointDefense += dps; continue; }
                            // Fire control (⚙3): the director raises the beam's tracking toward 1.0 (closes the gap).
                            // fcTracking is 0 unless the flag is on → beamTracking = BaseHitChance → byte-identical.
                            double beamTracking = beam.BaseHitChance + (1.0 - beam.BaseHitChance) * fcTracking;
                            // Range (Root A): beams carry their design MaxRange (0 = unbounded, the legacy convention).
                            // Combat heat (W5): a hot beam's CombatHeat_kJps flows into HeatPerSecond → the heat pool.
                            weapons.Add(new WeaponProfile(dps, beam.BeamSpeed, beamTracking, 1.0 / period, beam.MaxRange, WeaponNature.Energy, WeaponDelivery.Beam, 0, 0, beam.CombatHeat_kJps));
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
                            // Recoil (W4): a heavy slug-thrower on a light hull tracks worse (its kick shakes the ship).
                            double rgTracking = rg.Tracking * RecoilTrackingFactor(rg.Recoil, chassisMass);
                            // Range: a finite MID range (RailgunRange_m) so the closing model holds railgun fire until
                            // the gap is within it — the fix for railguns firing across the whole engagement bubble
                            // (and so "outside detection range"). v1 class-default; a per-design field (an Atb + JSON,
                            // like beam's MaxRange) is the next step. Only bites when EnableClosingRange is on (live);
                            // with it off (the headless fixtures) SeparationOf is 0 so the range gate is a no-op.
                            weapons.Add(new WeaponProfile(dps, rg.MuzzleVelocity_mps, rgTracking, rg.RoundsPerSecond, RailgunRange_m, WeaponNature.Kinetic, WeaponDelivery.Slug));
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
                            // Recoil (W4): a heavy rapid-fire mount on a light hull shakes it off aim.
                            double flakTracking = flak.Tracking * RecoilTrackingFactor(flak.Recoil, chassisMass);
                            // Range (the authentic-closing pass): flak is SHORT-ranged point defense (hard cutoff).
                            weapons.Add(new WeaponProfile(dps, flak.MuzzleVelocity_mps, flakTracking, saturation, FlakRange_m, WeaponNature.Kinetic, WeaponDelivery.Cloud));
                        }
                    }
                }

                // Ion disruptors: the ANTI-SHIELD exotic (docs/WEAPON-TAXONOMY-DESIGN.md §5, Phase D). Light-speed
                // (undodgeable, tracks perfectly like a beam) but EXOTIC nature — the shield's exotic-soak is 0, so it
                // bypasses the pool and strikes the hull. damage/sec = energy/shot × rounds/sec.
                if (instances.TryGetComponentsByAttribute<DisruptorWeaponAtb>(out var disruptors))
                {
                    foreach (var comp in disruptors)
                    {
                        if (comp.Design.TryGetAttribute<DisruptorWeaponAtb>(out var dis))
                        {
                            double dps = dis.EnergyPerShot_J * dis.RoundsPerSecond * comp.HealthPercent;
                            // Light-speed delivery (undodgeable, tracks 1.0) so it lands like a beam; Exotic nature so
                            // it bypasses shields. Delivery.Beam, Nature.Exotic — the two-axis split in action.
                            weapons.Add(new WeaponProfile(dps, LightSpeed_mps, 1.0, dis.RoundsPerSecond, DisruptorRange_m, WeaponNature.Exotic, WeaponDelivery.Beam));
                        }
                    }
                }

                // Plasma repeaters: the two-axis corner the old enum couldn't name — ENERGY nature (a shield only
                // half-soaks it, it bleeds through like a beam) but a finite-velocity BOLT delivery (dodgeable like a
                // slug, unlike a beam). Reads as Railgun-CLASS in the dodge model (finite velocity → juke-able), but its
                // Energy nature is what meets the shield. damage/sec = energy/shot × rounds/sec.
                if (instances.TryGetComponentsByAttribute<PlasmaBoltWeaponAtb>(out var plasmas))
                {
                    foreach (var comp in plasmas)
                    {
                        if (comp.Design.TryGetAttribute<PlasmaBoltWeaponAtb>(out var pl))
                        {
                            double dps = pl.EnergyPerShot_J * pl.RoundsPerSecond * comp.HealthPercent;
                            // Class Railgun (dodge behaviour = finite-velocity ballistic), Nature Energy, Delivery Bolt —
                            // the split axes in action. Reuses the mid RailgunRange_m (a bolt reaches like a light gun).
                            weapons.Add(new WeaponProfile(dps, pl.MuzzleVelocity_mps, pl.Tracking, pl.RoundsPerSecond, RailgunRange_m, WeaponNature.Energy, WeaponDelivery.Bolt));
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
                        weapons.Add(new WeaponProfile(dps, MissileVelocityStub_mps, MissileTrackingStub, MissileSaturationStub, MissileRange_m, WeaponNature.Explosive, WeaponDelivery.Guided));
                    }
                }

                // SHIELD generators — the space shield pool (docs/WEAPON-TAXONOMY-DESIGN.md §6). A depleting/regen
                // energy pool the resolve will drain before the hull's toughness (a later slice). Scaled by the
                // generator's own health (a damaged/shot-off generator projects a weaker/no shield — the grave rung).
                if (instances.TryGetComponentsByAttribute<ShieldAtb>(out var shieldGens))
                {
                    foreach (var comp in shieldGens)
                    {
                        if (comp.Design.TryGetAttribute<ShieldAtb>(out var sh))
                        {
                            shieldCapacity += sh.Capacity_J * comp.HealthPercent;
                            shieldRegen += sh.RegenRate_Jps * comp.HealthPercent;
                        }
                    }
                }

                // Ammo magazines (W3): sum the installed magazines' kg (health-scaled — a shot-off magazine feeds
                // less). 0 if none → the fleet's ammo pool stays disabled and combat is byte-identical.
                if (instances.TryGetComponentsByAttribute<ShipMagazineAtb>(out var mags))
                {
                    foreach (var comp in mags)
                    {
                        if (comp.Design.TryGetAttribute<ShipMagazineAtb>(out var mag))
                            ammoCapacity += mag.Capacity_kg * comp.HealthPercent;
                    }
                }

                // Heat radiators (W5): sum the installed radiators' kJ (health-scaled — a shot-off radiator sheds less).
                // 0 if none → the fleet's heat step stays disabled and energy fire is byte-identical.
                if (instances.TryGetComponentsByAttribute<RadiatorAtb>(out var radiators))
                {
                    foreach (var comp in radiators)
                    {
                        if (comp.Design.TryGetAttribute<RadiatorAtb>(out var rad))
                            heatCapacity += rad.Capacity_kJ * comp.HealthPercent;
                    }
                }

                // Point-defense (W6): sum the installed PD mounts' intercept rating (health-scaled — a shot-off mount
                // stops fewer missiles). 0 if none → the resolver's intercept step stays disabled and incoming fire is
                // byte-identical.
                if (instances.TryGetComponentsByAttribute<PointDefenseAtb>(out var pdMounts))
                {
                    foreach (var comp in pdMounts)
                    {
                        if (comp.Design.TryGetAttribute<PointDefenseAtb>(out var pd))
                            pointDefense += pd.InterceptRating_Jps * comp.HealthPercent;
                    }
                }
            }

            // Armour thickness adds straight to toughness (joules).
            if (ship.TryGetDataBlob<EntityDamageProfileDB>(out var dmgProfile))
                toughness += dmgProfile.Armor.thickness * ArmorHitPointsPerThickness_J;

            // Firepower is the sum of every weapon's damage/sec (same value as before — backward compatible).
            double firepower = 0;
            foreach (var w in weapons) firepower += w.DamagePerSecond;

            // Enhancers ⚙6.2 UNIT CALIBER — a per-hull elite stamp: the best installed caliber module multiplies this
            // hull's Firepower AND Toughness (so two identical chassis under the same admiral can fight differently —
            // the axis doctrine/commander can't express). 1.0 when no module → byte-identical.
            firepower *= UnitCaliberFirepowerMult(ship);
            toughness *= UnitCaliberToughnessMult(ship);

            // Role: anything that can shoot is a combatant; everything else is a low-priority utility hull.
            double roleWeight = firepower > 0 ? 1.0 : UtilityRoleWeight;

            return new ShipCombatValueDB(firepower, toughness, roleWeight)
            {
                Evasion = CalculateEvasion(ship),
                Weapons = weapons,
                ShieldCapacity_J = shieldCapacity,
                ShieldRegen_Jps = shieldRegen,
                AmmoCapacity_kg = ammoCapacity,
                HeatCapacity_kJ = heatCapacity,
                PointDefense_Jps = pointDefense,
            };
        }

        /// <summary>
        /// How hard this ship is to hit (0..<see cref="EvasionCap"/>), from its size and the acceleration it can
        /// pull. Small + nimble = high; big + sluggish = ~0. A ship with no engine (no thrust) can't dodge at all.
        /// Defensive: missing mass/thrust data => 0. v1 stub: sensors + crew experience not yet factored.
        /// </summary>
        /// <summary>Reference for the recoil→tracking penalty (Weapons pilot W4). Recoil is in mass-comparable units,
        /// so the penalty factor is <c>mass / (mass + Recoil × this)</c>; 1.0 keeps recoil directly comparable to hull
        /// mass. Flagged BALANCE dial.</summary>
        public const double RecoilTrackingReference = 1.0;

        /// <summary>The factor a kinetic weapon's TRACKING is multiplied by because its recoil shakes the firing ship
        /// (W4): <c>chassisMass / (chassisMass + recoil × RecoilTrackingReference)</c> — a heavy gun (high recoil) on a
        /// light hull (low mass) tracks much worse; the same gun on a battleship barely notices. Returns 1.0 (no
        /// penalty) when recoil ≤ 0 or the mass is unknown, so a recoilless/undialled weapon is byte-identical.</summary>
        public static double RecoilTrackingFactor(double recoil, double chassisMass)
        {
            if (recoil <= 0 || chassisMass <= 0) return 1.0;
            return chassisMass / (chassisMass + recoil * RecoilTrackingReference);
        }

        public static double CalculateEvasion(Entity ship)
        {
            double evasion = 0;
            if (ship.TryGetDataBlob<MassVolumeDB>(out var mv) && mv.Volume_m3 > 0 && mv.MassDry > 0)
            {
                // Small target = hard to hit: 1.0 when tiny, falls toward 0 as volume grows past the reference.
                double sizeFactor = SizeReference_m3 / (SizeReference_m3 + mv.Volume_m3);

                // Agile target = hard to track: acceleration is thrust ÷ mass (rate of vector change, not top speed).
                double accel = 0;
                if (ship.TryGetDataBlob<NewtonThrustAbilityDB>(out var thrust) && thrust.ThrustInNewtons > 0)
                    accel = thrust.ThrustInNewtons / mv.MassDry;
                double agilityFactor = accel / (AgilityReference_mps2 + accel); // 0 when sluggish, → 1 when nimble

                evasion = EvasionCap * sizeFactor * agilityFactor;
            }

            // Inertialess drive (Exotic propulsion, ⚙2): breaks the mass↔evasion coupling — the hull maneuvers
            // without inertia, so it dodges at least this well REGARDLESS of its mass (a capital that dodges like a
            // fighter). It's a FLOOR, not a set: a ship whose ordinary evasion is already higher keeps it. 0 (no such
            // drive, health-scaled) → evasion is the ordinary mass-bound value → byte-identical for every current ship.
            double inertialessFloor = InertialessEvasionFloor(ship);
            if (inertialessFloor > evasion) evasion = inertialessFloor;
            if (evasion > EvasionCap) evasion = EvasionCap;   // nothing is ever fully untouchable
            return evasion;
        }

        /// <summary>The evasion floor an inertialess drive guarantees a hull (0 if none) — the greatest installed
        /// <see cref="InertialessDriveAtb.EvasionOverride"/>, each scaled by the drive's current health (a shot-off
        /// drive drops toward no override — the grave rung). Defensive: no components → 0.</summary>
        internal static double InertialessEvasionFloor(Entity ship)
        {
            if (!ship.TryGetDataBlob<ComponentInstancesDB>(out var instances)) return 0;
            double best = 0;
            if (instances.TryGetComponentsByAttribute<InertialessDriveAtb>(out var drives))
            {
                foreach (var comp in drives)
                {
                    if (comp.Design.TryGetAttribute<InertialessDriveAtb>(out var drive))
                    {
                        double floor = drive.EvasionOverride * comp.HealthPercent;
                        if (floor > best) best = floor;
                    }
                }
            }
            return best;
        }

        /// <summary>The FIREPOWER multiplier the best installed unit-caliber module grants this hull (1.0 if none) —
        /// each module's <see cref="UnitCaliberAtb.FirepowerMult"/> health-scaled toward 1.0 (<c>1 + (mult-1)×HealthPercent</c>),
        /// so a shot-off module reverts the hull to a green-crew baseline (the grave rung). Defensive: no components → 1.0.</summary>
        internal static double UnitCaliberFirepowerMult(Entity ship)
        {
            if (!ship.TryGetDataBlob<ComponentInstancesDB>(out var instances)) return 1.0;
            double best = 1.0;
            if (instances.TryGetComponentsByAttribute<UnitCaliberAtb>(out var mods))
            {
                foreach (var comp in mods)
                {
                    if (comp.Design.TryGetAttribute<UnitCaliberAtb>(out var cal))
                    {
                        double m = 1.0 + (cal.FirepowerMult - 1.0) * comp.HealthPercent;
                        if (m > best) best = m;
                    }
                }
            }
            return best;
        }

        /// <summary>The TOUGHNESS multiplier the best installed unit-caliber module grants this hull (1.0 if none) —
        /// <see cref="UnitCaliberAtb.ToughnessMult"/> health-scaled toward 1.0, the toughness twin of
        /// <see cref="UnitCaliberFirepowerMult"/>. Defensive: no components → 1.0.</summary>
        internal static double UnitCaliberToughnessMult(Entity ship)
        {
            if (!ship.TryGetDataBlob<ComponentInstancesDB>(out var instances)) return 1.0;
            double best = 1.0;
            if (instances.TryGetComponentsByAttribute<UnitCaliberAtb>(out var mods))
            {
                foreach (var comp in mods)
                {
                    if (comp.Design.TryGetAttribute<UnitCaliberAtb>(out var cal))
                    {
                        double m = 1.0 + (cal.ToughnessMult - 1.0) * comp.HealthPercent;
                        if (m > best) best = m;
                    }
                }
            }
            return best;
        }
    }
}
