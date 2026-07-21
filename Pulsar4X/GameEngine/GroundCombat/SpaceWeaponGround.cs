using Pulsar4X.Components;
using Pulsar4X.Weapons;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// W-TRACK W1b — lets a UNIFIED SPACE WEAPON (the same laser / railgun / flak / plasma / disruptor a ship mounts)
    /// contribute GROUND firepower when it's bolted onto a ground chassis. The developer's rule: <b>"as long as a unit
    /// can provide power / ammo / hold the actual weapon, it gets to use it."</b> The ELIGIBILITY half — can it be
    /// powered, fed, and carried? — is already enforced (<see cref="GroundUnitAssembly"/>'s carry gate + the P2 power/
    /// ammo gates via <see cref="WeaponSupply"/>). This is the PAYOFF half: a mounted, supported space weapon actually
    /// SHOOTS on the ground instead of drawing power for nothing (the litmus test's #1 buildability gap).
    ///
    /// Pure + deterministic — reads only the design's atbs (the SAME fields <c>Combat.ShipCombatValueDB.Calculate</c>
    /// reads to build a ship's firepower), so a weapon that's stronger in space is stronger on the ground. It maps a
    /// space weapon to a ground <see cref="GroundWeaponMount"/>, so it rides W1 (loadout) → W2 (per-weapon range
    /// banding) → W3 (role) exactly like a native ground weapon. No existing ground unit mounts a space weapon, so the
    /// assembler branch that reads this is byte-identical for every current design.
    /// </summary>
    public static class SpaceWeaponGround
    {
        /// <summary>FLAGGED balance dial — a space weapon's firepower (its ship damage-per-second, in J/s) TIMES this is
        /// its GROUND Attack. 1/2500 puts the base-mod weapons in the ground band alongside the native ground weapons:
        /// laser ≈ 39 (~ the rifle's 40), flak/plasma/disruptor ≈ 120 (between rifle 40 and cannon 220), railgun ≈ 400
        /// (above the cannon) — balanced BY CONSTRUCTION against the ship weapon-triangle, no one-shots. The developer
        /// tunes this ONE number to shift how hard space weapons hit on the ground. NUMBER TO REVIEW.</summary>
        public const double AttackPerDps = 1.0 / 2500.0;

        // FLAGGED per-type ground hex reach — a space weapon's REAL range is astronomical (km to Mm); on a planet
        // surface its RELATIVE reach maps to these hex bands, mirroring the space model (beam > railgun/plasma > flak).
        /// <summary>Laser / disruptor — the longest ground reach (an energy beam).</summary>
        public const int BeamHexRange = 4;
        /// <summary>Railgun / plasma — a finite-velocity gun, medium reach.</summary>
        public const int BoltHexRange = 3;
        /// <summary>Flak — rapid-fire suppression, short reach.</summary>
        public const int FlakHexRange = 2;

        /// <summary>Does this design carry one of the unified SPACE direct-fire weapon atbs (so it's a candidate for the
        /// ground firepower map)? A native ground weapon (<see cref="GroundWeaponAtb"/>) is NOT one of these.</summary>
        public static bool IsSpaceWeapon(ComponentDesign design)
        {
            if (design == null) return false;
            return design.HasAttribute<GenericBeamWeaponAtb>()
                || design.HasAttribute<RailgunWeaponAtb>()
                || design.HasAttribute<FlakWeaponAtb>()
                || design.HasAttribute<PlasmaBoltWeaponAtb>()
                || design.HasAttribute<DisruptorWeaponAtb>();
        }

        /// <summary>A space weapon's firepower (J/s) = the SAME damage-per-second <c>ShipCombatValueDB</c> computes
        /// (energy-per-shot × rate; flak = damage/pellet × pellets × rate). 0 for a non-space-weapon. Never throws.</summary>
        public static double Firepower_Jps(ComponentDesign design)
        {
            if (design == null) return 0;
            if (design.HasAttribute<GenericBeamWeaponAtb>())
            {
                var b = design.GetAttribute<GenericBeamWeaponAtb>();
                double charge = b.ChargePeriod > 0 ? b.ChargePeriod : 1.0;
                return b.Energy / charge;
            }
            if (design.HasAttribute<RailgunWeaponAtb>())
            {
                var r = design.GetAttribute<RailgunWeaponAtb>();
                return r.KineticEnergyPerShot_J * r.RoundsPerSecond;
            }
            if (design.HasAttribute<FlakWeaponAtb>())
            {
                var f = design.GetAttribute<FlakWeaponAtb>();
                return f.DamagePerPellet_J * f.PelletsPerShot * f.RoundsPerSecond;
            }
            if (design.HasAttribute<PlasmaBoltWeaponAtb>())
            {
                var p = design.GetAttribute<PlasmaBoltWeaponAtb>();
                return p.EnergyPerShot_J * p.RoundsPerSecond;
            }
            if (design.HasAttribute<DisruptorWeaponAtb>())
            {
                var d = design.GetAttribute<DisruptorWeaponAtb>();
                return d.EnergyPerShot_J * d.RoundsPerSecond;
            }
            return 0;
        }

        /// <summary>The ground weapon FLAVOUR a space weapon fires as: an energy beam/bolt (laser / plasma) and the
        /// shield-bypassing ion disruptor read as ground ENERGY; a slug-thrower (railgun) and rapid-fire flak read as
        /// BALLISTIC. (Ground has no Exotic/Area mode; Energy is the closest match for the disruptor's shield-bleed.)</summary>
        public static GroundWeaponMode ModeFor(ComponentDesign design)
        {
            if (design == null) return GroundWeaponMode.Ballistic;
            if (design.HasAttribute<GenericBeamWeaponAtb>()) return GroundWeaponMode.Energy;
            if (design.HasAttribute<PlasmaBoltWeaponAtb>())  return GroundWeaponMode.Energy;
            if (design.HasAttribute<DisruptorWeaponAtb>())   return GroundWeaponMode.Energy;
            return GroundWeaponMode.Ballistic;   // railgun / flak — kinetic
        }

        /// <summary>The ground hex reach a space weapon fires at (per-type bands above).</summary>
        public static int RangeHexesFor(ComponentDesign design)
        {
            if (design == null) return 1;
            if (design.HasAttribute<GenericBeamWeaponAtb>()) return BeamHexRange;
            if (design.HasAttribute<DisruptorWeaponAtb>())   return BeamHexRange;
            if (design.HasAttribute<FlakWeaponAtb>())         return FlakHexRange;
            return BoltHexRange;   // railgun / plasma
        }

        /// <summary>The ground weapon MOUNT a space weapon contributes (Attack scaled to the ground band, hex range +
        /// mode by type), or null if the design isn't a space weapon. This is the one call
        /// <c>GroundUnitAssembly.Compute</c> makes so a space weapon joins the W1 loadout (→ W2 banding → W3 role)
        /// exactly like a native ground weapon.</summary>
        public static GroundWeaponMount MountFor(ComponentDesign design)
        {
            if (!IsSpaceWeapon(design)) return null;
            return new GroundWeaponMount
            {
                Attack = Firepower_Jps(design) * AttackPerDps,
                RangeHexes = RangeHexesFor(design),
                Mode = ModeFor(design),
            };
        }
    }
}
