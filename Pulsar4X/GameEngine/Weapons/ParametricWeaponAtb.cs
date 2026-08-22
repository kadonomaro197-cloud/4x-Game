using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Components;
using Pulsar4X.Combat;
using Pulsar4X.Components.Designers;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Weapons
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C / Path B — the GENERIC PARAMETRIC WEAPON (the real designer collapse).
    ///
    /// WHAT IT IS, in plain English: the weapons design tool (<c>docs/Actual HTMLs Of designers/weaponsderived.html</c>)
    /// replaces the eleven separate weapon templates (laser / railgun / flak / plasma / disruptor / …) with ONE weapon
    /// form — you pick how it DELIVERS its hit (Beam / Bolt / Slug / Cloud / Guided) and what NATURE of damage it does
    /// (Kinetic / Energy / Explosive / Exotic), slide a few numbers, and any gun in the game falls out. This component
    /// is the ENGINE HALF of that one form: a buildable part that stores those parametric inputs and, when installed,
    /// yields the SAME <see cref="WeaponProfile"/> the auto-resolve combat engine reads off the old per-type weapons.
    ///
    /// HOW IT REACHES THE FIGHT: <see cref="ShipCombatValueDB"/>.Calculate reads this atb and calls
    /// <see cref="WeaponsDesignModel.BuildProfile"/> (the slice-1 calculator, already fidelity-proven by
    /// <c>WeaponsDesignModelTests</c> to reproduce every base-mod gun) — so a weapon designed through the parametric
    /// form fights identically to the matching hand-authored template. Nothing else in the resolver changes: the profile
    /// it produces is an ordinary <see cref="WeaponProfile"/> that flows through the existing dodge / shield / armour math.
    ///
    /// SAVE-SAFE / ADDITIVE (Path B slice B1): a NEW atb class (not a ctor change to an existing one, so no L13 break),
    /// mirroring <see cref="RailgunWeaponAtb"/>'s shape exactly — a parameterless ctor, one NCalc double-arg ctor, a
    /// copy-ctor, <c>[JsonProperty]</c> dials, and a no-op install (it feeds the combat VALUE, not the parked per-pixel
    /// firing sim). The eleven per-type weapon templates and their atbs are UNTOUCHED and keep working; this adds a new
    /// design path beside them. Retiring the old templates in favour of parametric presets is a later, decision-gated
    /// slice (B5) — until then both paths coexist and every existing ship still builds.
    ///
    /// ENUM ARGS: the JSON binder (<c>AtbConstrArgs</c> → <c>Activator.CreateInstance</c>) passes every value as a
    /// double, so the Delivery and Nature enums arrive as doubles and are cast back — the exact pattern
    /// <see cref="Combat.AuraAtb"/> uses for its Effect/Target enum dials. Arg order MUST match the parametric-weapon
    /// template's <c>AtbConstrArgs(...)</c> in weapons.json (a mismatch throws at New Game / design build — gotcha #10).
    /// </summary>
    public class ParametricWeaponAtb : IComponentDesignAttribute
    {
        /// <summary>CHOICE 1 — how the weapon delivers its hit (what meets the dodge). Forces the velocity/range regime.</summary>
        [JsonProperty] public WeaponDelivery Delivery { get; internal set; }
        /// <summary>CHOICE 2 — what the weapon does to the defence (shields/armour matchup).</summary>
        [JsonProperty] public WeaponNature Nature { get; internal set; }
        /// <summary>SLIDER — total damage per second (joules/sec). Summed into a ship's Firepower.</summary>
        [JsonProperty] public double DamagePerSecond { get; internal set; }
        /// <summary>SLIDER — effective tracks per second (rate × projectiles): the floor on how much lands on a dodger.</summary>
        [JsonProperty] public double Saturation { get; internal set; }
        /// <summary>SLIDER — reach in metres. Only a non-exotic BEAM writes its own reach; every other delivery takes a
        /// fixed class range, so this is used for a Beam and ignored otherwise (matches <see cref="WeaponsDesignModel"/>).</summary>
        [JsonProperty] public double Range_m { get; internal set; }
        /// <summary>FREE input — shot velocity (m/s). ≤0 lets the delivery force its default (Beam = light-speed,
        /// Guided = the missile stub); a finite delivery (Slug/Bolt/Cloud) supplies its own (the high-velocity dial).</summary>
        [JsonProperty] public double Velocity { get; internal set; }
        /// <summary>FREE input — how well the weapon follows an evasive target, 0..1. Negative
        /// (<see cref="WeaponsDesignModel.UseDeliveryDefault"/> = -1) takes the delivery-forced default.</summary>
        [JsonProperty] public double Tracking { get; internal set; }
        /// <summary>SLIDER (armour) — flat armour this weapon ignores (0 = a normal round).</summary>
        [JsonProperty] public double Penetration { get; internal set; }
        /// <summary>SLIDER (armour) — joules in one shot (the alpha-vs-chip dial). 0 = treat the fire as one lump.</summary>
        [JsonProperty] public double PerShotEnergy { get; internal set; }
        /// <summary>Waste heat dumped into the ship while firing (kJ/s). 0 for a cool weapon; a hot beam sets it.</summary>
        [JsonProperty] public double HeatPerSecond { get; internal set; }

        public ParametricWeaponAtb() { }

        /// <summary>JSON constructor. All args are doubles because the NCalc binder passes numeric PropertyValues; the
        /// Delivery/Nature enum dials arrive as doubles and are cast back (the <see cref="Combat.AuraAtb"/> pattern).
        /// Arg order MUST match <c>AtbConstrArgs(...)</c> in the parametric-weapon template.</summary>
        public ParametricWeaponAtb(double delivery, double nature, double damagePerSecond, double saturation,
            double range_m, double velocity, double tracking, double penetration, double perShotEnergy, double heatPerSecond)
        {
            Delivery = (WeaponDelivery)(int)delivery;
            Nature = (WeaponNature)(int)nature;
            DamagePerSecond = damagePerSecond;
            Saturation = saturation;
            Range_m = range_m;
            Velocity = velocity;
            Tracking = tracking;
            Penetration = penetration;
            PerShotEnergy = perShotEnergy;
            HeatPerSecond = heatPerSecond;
        }

        public ParametricWeaponAtb(ParametricWeaponAtb db)
        {
            Delivery = db.Delivery;
            Nature = db.Nature;
            DamagePerSecond = db.DamagePerSecond;
            Saturation = db.Saturation;
            Range_m = db.Range_m;
            Velocity = db.Velocity;
            Tracking = db.Tracking;
            Penetration = db.Penetration;
            PerShotEnergy = db.PerShotEnergy;
            HeatPerSecond = db.HeatPerSecond;
        }

        /// <summary>Build the <see cref="WeaponProfile"/> the combat resolver reads — identical arithmetic to the
        /// hand-authored per-type weapons, via the fidelity-proven <see cref="WeaponsDesignModel"/>. This is the single
        /// call <see cref="ShipCombatValueDB"/> makes for a parametric weapon. <paramref name="healthScale"/> scales the
        /// damage-per-second by the component's <c>HealthPercent</c> exactly as every per-type weapon block does (a
        /// shot-off gun contributes less); default 1.0 = the un-scaled design profile (what the fidelity gauge asserts).</summary>
        public WeaponProfile BuildProfile(double healthScale = 1.0) =>
            new WeaponsDesignModel(Nature, Delivery, DamagePerSecond * healthScale, Saturation, Range_m, Velocity, Tracking,
                Penetration, PerShotEnergy, HeatPerSecond).BuildProfile();

        // No-op install/uninstall: a parametric weapon contributes to the combat VALUE (auto-resolve), not the parked
        // per-pixel firing sim, so it registers no fire-control / weapon state (cf. RailgunWeaponAtb / GenericWeaponAtb).
        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Parametric Weapon";
        public string AtbDescription() => "A weapon designed by delivery × nature + the numbers — reproduces any gun through one form.";
    }
}
