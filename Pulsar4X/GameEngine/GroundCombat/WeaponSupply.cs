using Pulsar4X.Components;
using Pulsar4X.Weapons;
using Pulsar4X.Energy;

namespace Pulsar4X.GroundCombat
{
    /// <summary>How a weapon feeds itself — the developer's supply-mode call (2026-07-06): a weapon takes ENERGY (from a
    /// reactor), AMMO (from a magazine), or BOTH. It's a designer SETTING with smart defaults derived from the weapon's
    /// own physics (the dials decide, the type emerges).</summary>
    public enum WeaponSupplyMode { Energy, Ammo, Both }

    /// <summary>
    /// The SUPPLY side of the "a Titan can, infantry can't" gate (weapon-unification P2, docs/WEAPON-UNIFICATION-DESIGN.md).
    /// Pure + deterministic (no engine state) — the ground echo of <see cref="WeaponClassifier"/>: it reads what a weapon
    /// DRAWS and what a reactor SUPPLIES so the assembler (<see cref="GroundUnitAssembly"/>) can refuse a design whose guns
    /// out-draw their power plant.
    ///
    /// Two halves, matching the developer's model:
    ///   • ENERGY (P2b, wired) — a beam/plasma/disruptor/railgun draws WATTS = its own declared energy flux (energy per
    ///     shot × rate, or beam energy ÷ charge period). NO efficiency coefficient is invented — draw = the weapon's real
    ///     output flux; a deliberate inefficiency knob is a later, flagged choice. A reactor supplies watts
    ///     (<see cref="EnergyGenerationAtb.PowerOutputMax"/>, authored in kW).
    ///   • AMMO (P2c, not yet built) — a flak/missile draws from a magazine; that gate + the magazine component land next.
    ///
    /// The per-weapon default mode below is the "be smart about what gets 1 or both" mapping; a designer OVERRIDE (e.g.
    /// plasma = the player's choice of Energy vs Both — the developer's call) rides in with the ammo half, since Energy-vs-
    /// Both only changes behaviour once ammo is a real cost. Never throws.
    /// </summary>
    public static class WeaponSupply
    {
        /// <summary>The reactor's <see cref="EnergyGenerationAtb.PowerOutputMax"/> is authored in kW; a weapon's draw is in
        /// W. Unit reconciliation only — arithmetic, NOT a balance dial (flagged for the developer per the standing rule).</summary>
        public const double ReactorKwToW = 1000.0;

        /// <summary>Smart default supply mode from the weapon's own atbs (energy beams draw power; a railgun launches a slug
        /// electromagnetically = both; flak throws propellant-driven pellets = ammo). A non-weapon reads Energy harmlessly
        /// (its <see cref="PowerDraw_W"/> is 0 either way).</summary>
        public static WeaponSupplyMode DefaultModeFor(ComponentDesign design)
        {
            if (design == null) return WeaponSupplyMode.Energy;
            if (design.HasAttribute<GenericBeamWeaponAtb>()) return WeaponSupplyMode.Energy;  // laser — pure light
            if (design.HasAttribute<DisruptorWeaponAtb>())   return WeaponSupplyMode.Energy;  // ion beam
            if (design.HasAttribute<PlasmaBoltWeaponAtb>())  return WeaponSupplyMode.Energy;  // power-formed bolt (player may set Both, P2c)
            if (design.HasAttribute<RailgunWeaponAtb>())     return WeaponSupplyMode.Both;    // EM launch of a slug: power + ammo
            if (design.HasAttribute<FlakWeaponAtb>())        return WeaponSupplyMode.Ammo;    // pellets + propellant
            return WeaponSupplyMode.Energy;
        }

        /// <summary>Does this weapon draw reactor power (mode Energy or Both)?</summary>
        public static bool DrawsEnergy(ComponentDesign design)
        {
            var m = DefaultModeFor(design);
            return m == WeaponSupplyMode.Energy || m == WeaponSupplyMode.Both;
        }

        /// <summary>The weapon's power appetite in WATTS = its own energy flux (energy per shot × rate, or beam energy ÷
        /// charge period). 0 for an ammo-only weapon (its pellet energy is chemical, from the magazine, not the reactor)
        /// or a non-weapon. Never throws (a zero/absent charge period is treated as 1 s).</summary>
        public static double PowerDraw_W(ComponentDesign design)
        {
            if (design == null) return 0;
            if (design.HasAttribute<GenericBeamWeaponAtb>())
            {
                var b = design.GetAttribute<GenericBeamWeaponAtb>();
                double charge = b.ChargePeriod > 0 ? b.ChargePeriod : 1.0;
                return b.Energy / charge;
            }
            if (design.HasAttribute<DisruptorWeaponAtb>())
            {
                var d = design.GetAttribute<DisruptorWeaponAtb>();
                return d.EnergyPerShot_J * d.RoundsPerSecond;
            }
            if (design.HasAttribute<PlasmaBoltWeaponAtb>())
            {
                var p = design.GetAttribute<PlasmaBoltWeaponAtb>();
                return p.EnergyPerShot_J * p.RoundsPerSecond;
            }
            if (design.HasAttribute<RailgunWeaponAtb>())
            {
                var r = design.GetAttribute<RailgunWeaponAtb>();
                return r.KineticEnergyPerShot_J * r.RoundsPerSecond;   // the launch energy the reactor must feed
            }
            return 0;   // flak (Ammo) + non-weapons draw no reactor power
        }

        /// <summary>The reactor's sustained output in WATTS (0 if the part is not a reactor).</summary>
        public static double ReactorOutput_W(ComponentDesign design)
        {
            if (design == null || !design.HasAttribute<EnergyGenerationAtb>()) return 0;
            return design.GetAttribute<EnergyGenerationAtb>().PowerOutputMax * ReactorKwToW;
        }
    }
}
