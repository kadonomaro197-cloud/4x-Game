using Newtonsoft.Json;
using Pulsar4X.Interfaces;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// An AUGMENT part — the "make the base unit more than it is" slot that makes the loose-frame model work. This is
    /// the single most load-bearing part type: it's what turns a bare human into a Space Marine (power armour →
    /// <see cref="StrengthBonus"/> lets the frame lug a bolter it couldn't), a Jedi into a Jedi (the Force →
    /// <see cref="EvasionBonus"/> + <see cref="Shield"/> deflection, surviving with no armour), and a Zergling into a
    /// fast one (adrenal glands → evasion). One general attribute — power armour, servos, thrusters, adrenal glands,
    /// psychic wards are all THIS with different knobs (the developer's generalise-by-function call, 2026-07-05).
    ///
    ///   • <see cref="StrengthBonus"/> ADDS to the frame's carry budget — the whole reason augmentation unlocks heavier
    ///     gear (the power-armour story).
    ///   • <see cref="EvasionBonus"/> — dodge (rides the same evasion the ship dodge model uses).
    ///   • <see cref="ToughnessBonus"/> — a survivability multiplier hook (soak more per point of HP).
    ///   • <see cref="Shield"/> — flat incoming-damage soak / ranged deflection (an energy shield, a Force ward).
    ///
    /// Costs <see cref="Mass"/> like any part. A component attribute (CONVENTIONS §6); inert on install (the assembler
    /// reads it — G-D3). Design: docs/GROUND-COMBAT-MAP-DESIGN.md → unit designer.
    /// </summary>
    public class GroundAugmentAtb : BaseDataBlob, IComponentDesignAttribute
    {
        [JsonProperty] public double Mass { get; internal set; }
        /// <summary>ADDED to the frame's carry budget — augmentation is what lets a unit bear heavier gear.</summary>
        [JsonProperty] public double StrengthBonus { get; internal set; }
        /// <summary>Dodge bonus (same evasion currency as the ship dodge model).</summary>
        [JsonProperty] public double EvasionBonus { get; internal set; }
        /// <summary>Survivability multiplier hook — soak more per point of HP.</summary>
        [JsonProperty] public double ToughnessBonus { get; internal set; }
        /// <summary>Flat incoming-damage soak / ranged deflection (energy shield, Force ward).</summary>
        [JsonProperty] public double Shield { get; internal set; }
        /// <summary>How fast the shield POOL recharges — the FRACTION of full capacity restored per hour between
        /// salvos (⚙3 Defense: the ground twin of the ship shield's Recharge dial). Default 0.34 (the old global
        /// constant) → byte-identical. A high value is a small, fast-recharging WARD (shrugs sustained chip-fire but
        /// folds to one alpha); a low value is a big, slow shield (eats an alpha then stays down). The capacity-vs-
        /// recharge decision the ship shield already has.</summary>
        [JsonProperty] public double ShieldRegenFraction { get; internal set; } = 0.34;

        public GroundAugmentAtb() { }

        // double args for the JSON/NCalc binder (gotcha L7). Order = template PropertyFormula order. The 5-arg ctor
        // keeps every EXISTING augment template byte-identical — its ShieldRegenFraction stays the 0.34 default.
        public GroundAugmentAtb(double mass, double strengthBonus, double evasionBonus, double toughnessBonus, double shield)
        {
            Mass = mass < 0 ? 0 : mass;
            StrengthBonus = strengthBonus;
            EvasionBonus = evasionBonus;
            ToughnessBonus = toughnessBonus;
            Shield = shield < 0 ? 0 : shield;
        }

        // 6-arg ctor for an augment that DIALS its shield recharge (a fast ward vs a slow big shield). Binder matches
        // by exact arg count (L7), so only a 6-value template reaches this — the 5-arg augments are untouched.
        public GroundAugmentAtb(double mass, double strengthBonus, double evasionBonus, double toughnessBonus, double shield, double shieldRegenFraction)
        {
            Mass = mass < 0 ? 0 : mass;
            StrengthBonus = strengthBonus;
            EvasionBonus = evasionBonus;
            ToughnessBonus = toughnessBonus;
            Shield = shield < 0 ? 0 : shield;
            ShieldRegenFraction = shieldRegenFraction < 0 ? 0 : shieldRegenFraction;
        }

        public override object Clone()
            => new GroundAugmentAtb(Mass, StrengthBonus, EvasionBonus, ToughnessBonus, Shield, ShieldRegenFraction);

        public void OnComponentInstallation(Entity parentEntity, ComponentInstance componentInstance) { }
        public void OnComponentUninstallation(Entity parentEntity, ComponentInstance componentInstance) { }

        public string AtbName() => "Augment";
        public string AtbDescription()
            => $"Augment — +{StrengthBonus:0} strength (carry more), +{EvasionBonus:0.##} evasion, +{ToughnessBonus:0.##} toughness, {Shield:0} shield, mass {Mass:0}.";
    }
}
