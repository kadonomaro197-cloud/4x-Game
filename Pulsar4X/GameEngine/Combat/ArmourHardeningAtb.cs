using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// A NATURE-HARDENED PLATING module — the ship-side of the ⚙3 Defense "armour against WHAT" decision (the space
    /// twin of the ground <c>GroundArmorAtb</c> nature dials). Plain ship armour folds into one flat toughness pool,
    /// blind to what's hitting it; this component makes a ship's plating TUNED to a damage NATURE, soaking a fraction of
    /// the matching incoming fire before it reaches the hull (an ablative-clad cruiser shrugs off beams; a composite one
    /// walls kinetic). It reads like the shield's nature matchup but on the armour layer — and it STACKS after the
    /// shield (shield soaks its share first, then hardened plating bounces some of what leaks through).
    ///
    /// A component (<see cref="IComponentDesignAttribute"/>, CONVENTIONS §6) — researched → built → installed → LOST
    /// (a shot-off plating module drops the hardening; the grave rung). The four fields are SOAK FRACTIONS (0..1) of the
    /// matching-nature incoming damage. <b>All default 0 → no soak → combat byte-identical</b> until a hardened plate is
    /// actually fitted (every current ship). Read by <see cref="ShipCombatValueDB"/> into the ship's per-nature armour
    /// soak; the fleet resolve applies the toughness-weighted fleet average after the shield step. Inert on install.
    /// </summary>
    public class ArmourHardeningAtb : IComponentDesignAttribute
    {
        /// <summary>Fraction (0..1) of incoming KINETIC damage this plating soaks (0 = plain plate).</summary>
        [JsonProperty] public double SoakVsKinetic { get; internal set; }
        /// <summary>Fraction (0..1) of incoming ENERGY damage this plating soaks (0 = plain plate).</summary>
        [JsonProperty] public double SoakVsEnergy { get; internal set; }
        /// <summary>Fraction (0..1) of incoming EXPLOSIVE damage this plating soaks (0 = plain plate).</summary>
        [JsonProperty] public double SoakVsExplosive { get; internal set; }
        /// <summary>Fraction (0..1) of incoming EXOTIC damage this plating soaks (0 = plain plate).</summary>
        [JsonProperty] public double SoakVsExotic { get; internal set; }

        /// <summary>A soak fraction can never make armour total immunity — capped below 1 (a big enough hit always leaks
        /// something). Flagged balance value.</summary>
        public const double MaxSoakFraction = 0.9;

        public ArmourHardeningAtb() { }

        // double args for the JSON/NCalc binder (landmine L7). Order = template PropertyFormula order. Clamped [0, cap].
        public ArmourHardeningAtb(double soakVsKinetic, double soakVsEnergy, double soakVsExplosive, double soakVsExotic)
        {
            SoakVsKinetic = Clamp(soakVsKinetic);
            SoakVsEnergy = Clamp(soakVsEnergy);
            SoakVsExplosive = Clamp(soakVsExplosive);
            SoakVsExotic = Clamp(soakVsExotic);
        }

        private static double Clamp(double v) => v < 0 ? 0 : (v > MaxSoakFraction ? MaxSoakFraction : v);

        /// <summary>This plating's soak fraction vs a given incoming damage nature (0 for a plain plate).</summary>
        public double SoakFor(WeaponNature nature) => nature switch
        {
            WeaponNature.Kinetic => SoakVsKinetic,
            WeaponNature.Energy => SoakVsEnergy,
            WeaponNature.Explosive => SoakVsExplosive,
            WeaponNature.Exotic => SoakVsExotic,
            _ => 0,
        };

        // Read by ShipCombatValueDB, not an install hook — inert on install/uninstall (like the weapon/shield atbs).
        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Nature-Hardened Plating";
        public string AtbDescription() => $"Nature-hardened plating — soaks kinetic {SoakVsKinetic:P0} / energy {SoakVsEnergy:P0} / explosive {SoakVsExplosive:P0} / exotic {SoakVsExotic:P0} of the matching incoming fire (after shields).";
    }
}
