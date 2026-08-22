using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Components.Designers;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the DEFENSE door parametric designer, slice-1 gauge.
    ///
    /// Proves the pure <see cref="DefenseDesignModel"/> (the engine half of the two-choice/four-slider defence form)
    /// REPRODUCES every PURE-defense base-mod component's <c>*Atb</c> from its two choices (Layer × Domain) + dials —
    /// i.e. every hand-authored shield/plate falls out of the one parametric form (the DESIGNER-NORTH-STAR reproduction
    /// claim, made executable). PURE (no colony harness → fast, not the slow CI shard); the reproduction VALUES are the
    /// exact ones the base-mod JSON templates carry, VERIFIED against the template files
    /// (<c>GameData/basemod/TemplateFiles/weapons.json</c> + <c>installations.json</c>). Byte-identical to the live game
    /// (nothing calls the model yet).
    ///
    /// The model constructs each atb through the SAME public ctor + arg order the JSON binder uses, so the assertions
    /// are field-by-field EXACT (identity pass-through — no float slop, though we keep a 1e-6 tolerance per the harness
    /// idiom). This is the reference the other door models mirror. The two KITHRIN overrides (built on the same
    /// templates with different dial values) are included to prove the form scales past the default design.
    ///
    /// SCOPED OUT (documented, not faked): the base-mod <c>power-armor</c> (StrengthBonus) and <c>reflex-booster</c>
    /// (EvasionBonus) are <see cref="GroundAugmentAtb"/> parts whose non-shield dials belong to the CHASSIS / PROPULSION
    /// doors — the Defense door owns only their (zero) Shield arg. The cross-door guard test pins that the door always
    /// emits Str = Eva = Tough = 0 for a ground shield, which is its correct contribution to those units.
    /// </summary>
    [TestFixture]
    public class DefenseDesignModelTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[defense-model] " + m);

        private static void AssertShield(string name, DefenseDesignModel m, double capacity_J, double regen_Jps)
        {
            var a = m.Build() as ShieldAtb;
            Assert.That(a, Is.Not.Null, $"{name}: expected a ShieldAtb");
            Log($"{name}: ShieldAtb cap={a.Capacity_J} regen={a.RegenRate_Jps}");
            Assert.That(a.Capacity_J, Is.EqualTo(capacity_J).Within(1e-6), $"{name} Capacity_J");
            Assert.That(a.RegenRate_Jps, Is.EqualTo(regen_Jps).Within(1e-6), $"{name} RegenRate_Jps");
        }

        private static void AssertArmourHardening(string name, DefenseDesignModel m,
            double k, double e, double x, double o)
        {
            var a = m.Build() as ArmourHardeningAtb;
            Assert.That(a, Is.Not.Null, $"{name}: expected an ArmourHardeningAtb");
            Log($"{name}: ArmourHardeningAtb K={a.SoakVsKinetic} E={a.SoakVsEnergy} X={a.SoakVsExplosive} O={a.SoakVsExotic}");
            Assert.That(a.SoakVsKinetic, Is.EqualTo(k).Within(1e-6), $"{name} SoakVsKinetic");
            Assert.That(a.SoakVsEnergy, Is.EqualTo(e).Within(1e-6), $"{name} SoakVsEnergy");
            Assert.That(a.SoakVsExplosive, Is.EqualTo(x).Within(1e-6), $"{name} SoakVsExplosive");
            Assert.That(a.SoakVsExotic, Is.EqualTo(o).Within(1e-6), $"{name} SoakVsExotic");
        }

        private static void AssertGroundArmor(string name, DefenseDesignModel m,
            double mass, double hp, double defense, double k, double e, double x, double o)
        {
            var a = m.Build() as GroundArmorAtb;
            Assert.That(a, Is.Not.Null, $"{name}: expected a GroundArmorAtb");
            Log($"{name}: GroundArmorAtb mass={a.Mass} hp={a.HP} def={a.Defense} vsK={a.VsKinetic} vsE={a.VsEnergy} vsX={a.VsExplosive} vsO={a.VsExotic}");
            Assert.That(a.Mass, Is.EqualTo(mass).Within(1e-6), $"{name} Mass");
            Assert.That(a.HP, Is.EqualTo(hp).Within(1e-6), $"{name} HP");
            Assert.That(a.Defense, Is.EqualTo(defense).Within(1e-6), $"{name} Defense");
            Assert.That(a.VsKinetic, Is.EqualTo(k).Within(1e-6), $"{name} VsKinetic");
            Assert.That(a.VsEnergy, Is.EqualTo(e).Within(1e-6), $"{name} VsEnergy");
            Assert.That(a.VsExplosive, Is.EqualTo(x).Within(1e-6), $"{name} VsExplosive");
            Assert.That(a.VsExotic, Is.EqualTo(o).Within(1e-6), $"{name} VsExotic");
        }

        private static void AssertGroundAugment(string name, DefenseDesignModel m,
            double mass, double str, double eva, double tough, double shield, double regenFraction)
        {
            var a = m.Build() as GroundAugmentAtb;
            Assert.That(a, Is.Not.Null, $"{name}: expected a GroundAugmentAtb");
            Log($"{name}: GroundAugmentAtb mass={a.Mass} str={a.StrengthBonus} eva={a.EvasionBonus} tough={a.ToughnessBonus} shield={a.Shield} regenFrac={a.ShieldRegenFraction}");
            Assert.That(a.Mass, Is.EqualTo(mass).Within(1e-6), $"{name} Mass");
            Assert.That(a.StrengthBonus, Is.EqualTo(str).Within(1e-6), $"{name} StrengthBonus");
            Assert.That(a.EvasionBonus, Is.EqualTo(eva).Within(1e-6), $"{name} EvasionBonus");
            Assert.That(a.ToughnessBonus, Is.EqualTo(tough).Within(1e-6), $"{name} ToughnessBonus");
            Assert.That(a.Shield, Is.EqualTo(shield).Within(1e-6), $"{name} Shield");
            Assert.That(a.ShieldRegenFraction, Is.EqualTo(regenFraction).Within(1e-6), $"{name} ShieldRegenFraction");
        }

        [Test]
        [Description("Every base-mod SHIP SHIELD (deflector-array + the kithrin resonance override) falls out of the form: (Shield, Ship) → ShieldAtb(Capacity, Regen). Only the two dials move.")]
        public void ShipShields_ReproduceFromTheForm()
        {
            // deflector-array / default-design-deflector-array (weapons.json:607/617 template defaults).
            AssertShield("deflector-array",
                new DefenseDesignModel(DefenseLayer.Shield, DefenseDomain.Ship, magnitude: 5_000_000, regen: 100_000),
                5_000_000, 100_000);

            // kithrin-resonance-shield (componentDesigns.json override on the same template): a bigger, faster pool.
            AssertShield("kithrin-resonance-shield",
                new DefenseDesignModel(DefenseLayer.Shield, DefenseDomain.Ship, magnitude: 15_000_000, regen: 300_000),
                15_000_000, 300_000);
        }

        [Test]
        [Description("Every base-mod SHIP ARMOUR-NATURE plate (armour-hardening + the kithrin ward-lattice override) falls out of the form: (Armour, Ship) → ArmourHardeningAtb with the four SOAK FRACTIONS. All inside [0,0.9] so the ctor clamp is identity.")]
        public void ShipArmourHardening_ReproduceFromTheForm()
        {
            // armour-hardening / default-design-armour-hardening (weapons.json:734/744/754/764 = 0.1/0.4/0.2/0.0).
            AssertArmourHardening("armour-hardening",
                new DefenseDesignModel(DefenseLayer.Armour, DefenseDomain.Ship,
                    natureVsKinetic: 0.1, natureVsEnergy: 0.4, natureVsExplosive: 0.2, natureVsExotic: 0.0),
                0.1, 0.4, 0.2, 0.0);

            // kithrin-ward-lattice (override on the armour-hardening template): 0.1/0.8/0.0/0.0.
            AssertArmourHardening("kithrin-ward-lattice",
                new DefenseDesignModel(DefenseLayer.Armour, DefenseDomain.Ship,
                    natureVsKinetic: 0.1, natureVsEnergy: 0.8, natureVsExplosive: 0.0, natureVsExotic: 0.0),
                0.1, 0.8, 0.0, 0.0);
        }

        [Test]
        [Description("Every base-mod GROUND PLATE falls out of the form: a plain plate (ground-plating) → the 3-arg GroundArmorAtb (natures fall to the 1.0 ctor defaults); a nature-tuned plate (ablative/reactive) → the 7-arg ctor. NO renormalization — the shipped sums 4.6/5.2 are preserved (slice-1 identity pass-through).")]
        public void GroundArmour_ReproduceFromTheForm()
        {
            // ground-plating / default-design-ground-plating (installations.json:2347/2356/2365 = 25/150/5, plain).
            // No nature dials passed → the model builds the 3-arg ctor → the four resists default to 1.0.
            AssertGroundArmor("ground-plating",
                new DefenseDesignModel(DefenseLayer.Armour, DefenseDomain.Ground, magnitude: 5, bulk: 25, hp: 150),
                25, 150, 5, 1.0, 1.0, 1.0, 1.0);

            // ablative-plating (25/150/5, VsK 0.6 / VsE 2.0 / VsX 1.0 / VsO 1.0 → sum 4.6 preserved).
            AssertGroundArmor("ablative-plating",
                new DefenseDesignModel(DefenseLayer.Armour, DefenseDomain.Ground, magnitude: 5, bulk: 25, hp: 150,
                    natureVsKinetic: 0.6, natureVsEnergy: 2.0, natureVsExplosive: 1.0, natureVsExotic: 1.0),
                25, 150, 5, 0.6, 2.0, 1.0, 1.0);

            // reactive-plating (25/150/5, VsK 1.6 / VsE 0.6 / VsX 2.0 / VsO 1.0 → sum 5.2 preserved).
            AssertGroundArmor("reactive-plating",
                new DefenseDesignModel(DefenseLayer.Armour, DefenseDomain.Ground, magnitude: 5, bulk: 25, hp: 150,
                    natureVsKinetic: 1.6, natureVsEnergy: 0.6, natureVsExplosive: 2.0, natureVsExotic: 1.0),
                25, 150, 5, 1.6, 0.6, 2.0, 1.0);
        }

        [Test]
        [Description("Every base-mod GROUND SHIELD falls out of the form: (Shield, Ground) → GroundAugmentAtb, shield-only (Str=Eva=Tough=0). Recharge at default → the 5-arg ctor (fraction 0.34, the big slow Shield Generator); dialed → the 6-arg ctor (the fast Ward Projector). This is the ARITY assertion.")]
        public void GroundShields_ReproduceFromTheForm()
        {
            // shield-generator (installations.json:3137/3173 = CarryMass 20, Shield 150; recharge left at default →
            // 5-arg ctor → ShieldRegenFraction stays the 0.34 template default).
            AssertGroundAugment("shield-generator",
                new DefenseDesignModel(DefenseLayer.Shield, DefenseDomain.Ground, magnitude: 150, bulk: 20),
                20, 0, 0, 0, 150, 0.34);

            // ward-projector (CarryMass 20, Shield 60, ShieldRegenFraction 1.0 → the 6-arg ctor).
            AssertGroundAugment("ward-projector",
                new DefenseDesignModel(DefenseLayer.Shield, DefenseDomain.Ground, magnitude: 60, bulk: 20, regen: 1.0),
                20, 0, 0, 0, 60, 1.0);
        }

        [Test]
        [Description("CROSS-DOOR guard: a (Shield, Ground) build ALWAYS emits Strength=Evasion=Toughness=0 — the Defense door owns only the shield arg, so power-armor (StrengthBonus) and reflex-booster (EvasionBonus) are NOT reproducible by this door (their non-shield dials are Chassis/Propulsion outputs the Assembler composes). Documents the scope, doesn't fake it.")]
        public void GroundShield_OwnsOnlyTheShieldArg_CrossDoorZeroed()
        {
            var a = new DefenseDesignModel(DefenseLayer.Shield, DefenseDomain.Ground, magnitude: 999, bulk: 30, regen: 0.5)
                .Build() as GroundAugmentAtb;
            Assert.That(a, Is.Not.Null);
            Assert.That(a.StrengthBonus, Is.EqualTo(0).Within(1e-9), "Defense door must not write StrengthBonus (a Chassis output)");
            Assert.That(a.EvasionBonus, Is.EqualTo(0).Within(1e-9), "Defense door must not write EvasionBonus (a Propulsion/Chassis output)");
            Assert.That(a.ToughnessBonus, Is.EqualTo(0).Within(1e-9), "Defense door must not write ToughnessBonus (an Enhancer output)");
            // …but it DOES own the shield pool + recharge it was given.
            Assert.That(a.Shield, Is.EqualTo(999).Within(1e-6));
            Assert.That(a.ShieldRegenFraction, Is.EqualTo(0.5).Within(1e-6));
        }
    }
}
