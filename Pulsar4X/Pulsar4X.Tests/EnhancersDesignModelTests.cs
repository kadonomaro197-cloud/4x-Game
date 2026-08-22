using System;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.GroundCombat;
using Pulsar4X.Components.Designers;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the ENHANCERS door parametric designer, slice-1 gauge.
    ///
    /// Proves the pure <see cref="EnhancersDesignModel"/> REPRODUCES every base-mod enhancer's <c>*Atb</c> constructor
    /// arguments (with the load-bearing ARITY), the constructed attribute's clamped fields, and the component MASS from
    /// its two choices (Kind × Enhancer) + dials — i.e. all eight hand-authored enhancers fall out of the one parametric
    /// form (the DESIGNER-NORTH-STAR reproduction claim, made executable). PURE (no colony harness → fast, not the slow
    /// CI shard); byte-identical to the live game (nothing calls the model yet).
    ///
    /// The reproduction VALUES are the shipped template defaults (verified against the JSON — every base-mod
    /// default-design carries NO Property override, so the values ARE the template PropertyFormula defaults):
    ///   • unit-caliber        Cadre Mass 3000, Firepower 1.3, Toughness 1.2 → UnitCaliberAtb(1.3,1.2), Mass 3000
    ///   • crew-automation     Automation Mass 5000, Crew Reduction 30       → CrewAutomationAtb(30),   Mass 5000
    ///   • power-armor         Carry 30, Str 300, Ev 0, Tough 0.2, Shield 0  → GroundAugmentAtb 5-arg,  Mass 30
    ///   • shield-generator    Carry 20, Shield 150, rest 0                  → GroundAugmentAtb 5-arg,  Mass 20
    ///   • ward-projector      Carry 20, Shield 60, Regen 1.0                → GroundAugmentAtb 6-arg,  Mass 20
    ///   • reflex-booster      Carry 15, Evasion 0.4, rest 0                 → GroundAugmentAtb 5-arg,  Mass 15
    ///   • ground-training     TrainingMultiplier 1.2                        → GroundTrainingAtb(1.2),  Mass 90
    ///   • sealed-systems      Sealing 0.9                                   → GroundSealAtb(0.9),      Mass 94
    ///
    /// The Mass values (3000/5000/30/20/20/15) are the same MassPerUnit the existing DesignerFreeDialCostTests pins from
    /// the real ComponentDesigner, so a drift in the model's duplicated JSON mass formula fails here. The ARITY assertion
    /// (ward = 6, the other three augments = 5) is the load-bearing tripwire: a model that always emitted 6 args would
    /// silently overwrite the 0.34 ShieldRegenFraction default and diverge from the base-mod part.
    /// </summary>
    [TestFixture]
    public class EnhancersDesignModelTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[enhancers-model] " + m);

        // Shared type/arity/mass check — every case runs through this before the per-atb field asserts.
        private static EnhancerProfile AssertShape(string name, EnhancersDesignModel m, Type atbType, int argCount, double mass)
        {
            var p = m.Compute();
            Log($"{name}: {p.AttributeType.Name} args={p.ArgCount} mass={p.Mass}");
            Assert.That(p.AttributeType, Is.EqualTo(atbType), $"{name} AttributeType");
            Assert.That(p.ArgCount, Is.EqualTo(argCount), $"{name} ctor arity");
            Assert.That(p.Mass, Is.EqualTo(mass).Within(1e-6), $"{name} Mass (= MassPerUnit)");
            Assert.That(p.Attribute, Is.Not.Null, $"{name} constructed attribute");
            return p;
        }

        [Test]
        [Description("The SHIP enhancers (Advanced Training ▸ Ship Cadre, Systems ▸ Automation) fall out of the form: caliber emits UnitCaliberAtb(1.3,1.2) at Mass 3000; automation emits CrewAutomationAtb(30) at Mass 5000.")]
        public void ShipEnhancers_ReproduceFromTheForm()
        {
            // unit-caliber — only the two choices needed; every dial defaults to the enhancer's forced value.
            var caliber = AssertShape("unit-caliber",
                new EnhancersDesignModel(EnhancerKind.AdvancedTraining, EnhancerType.ShipCadre),
                typeof(UnitCaliberAtb), 2, 3000);
            Assert.That(caliber.CtorArgs[0], Is.EqualTo(1.3).Within(1e-6), "caliber firepower arg");
            Assert.That(caliber.CtorArgs[1], Is.EqualTo(1.2).Within(1e-6), "caliber toughness arg");
            var uc = (UnitCaliberAtb)caliber.Attribute;
            Assert.That(uc.FirepowerMult, Is.EqualTo(1.3).Within(1e-6), "UnitCaliberAtb.FirepowerMult");
            Assert.That(uc.ToughnessMult, Is.EqualTo(1.2).Within(1e-6), "UnitCaliberAtb.ToughnessMult");

            // crew-automation
            var crew = AssertShape("crew-automation",
                new EnhancersDesignModel(EnhancerKind.Systems, EnhancerType.CrewAutomation),
                typeof(CrewAutomationAtb), 1, 5000);
            Assert.That(crew.CtorArgs[0], Is.EqualTo(30).Within(1e-6), "crew reduction arg");
            Assert.That(((CrewAutomationAtb)crew.Attribute).CrewReduction, Is.EqualTo(30).Within(1e-6), "CrewAutomationAtb.CrewReduction");
        }

        [Test]
        [Description("The FOUR GroundAugment presets are ONE attribute with a different axis dialed: power armour / shield generator / reflex booster emit the 5-arg ctor (ShieldRegenFraction stays the 0.34 default), and ONLY ward-projector emits the 6-arg ctor (regen 1.0) — the load-bearing arity split.")]
        public void GroundAugments_ReproduceFromTheForm_WithTheAritySplit()
        {
            // power-armor — 5-arg (30, 300, 0, 0.2, 0), regen defaults 0.34.
            var pa = AssertShape("power-armor",
                new EnhancersDesignModel(EnhancerKind.Augmentation, EnhancerType.PowerArmour),
                typeof(GroundAugmentAtb), 5, 30);
            AssertAugment("power-armor", pa, mass: 30, str: 300, ev: 0, tough: 0.2, shield: 0, regen: 0.34);

            // shield-generator — 5-arg (20, 0, 0, 0, 150).
            var sg = AssertShape("shield-generator",
                new EnhancersDesignModel(EnhancerKind.Augmentation, EnhancerType.ShieldGenerator),
                typeof(GroundAugmentAtb), 5, 20);
            AssertAugment("shield-generator", sg, mass: 20, str: 0, ev: 0, tough: 0, shield: 150, regen: 0.34);

            // ward-projector — 6-arg (20, 0, 0, 0, 60, 1.0) — the ONLY 6-arg component.
            var wp = AssertShape("ward-projector",
                new EnhancersDesignModel(EnhancerKind.Augmentation, EnhancerType.WardProjector),
                typeof(GroundAugmentAtb), 6, 20);
            AssertAugment("ward-projector", wp, mass: 20, str: 0, ev: 0, tough: 0, shield: 60, regen: 1.0);
            Assert.That(wp.CtorArgs[5], Is.EqualTo(1.0).Within(1e-6), "ward 6th arg (ShieldRegenFraction)");

            // reflex-booster — 5-arg (15, 0, 0.4, 0, 0).
            var rb = AssertShape("reflex-booster",
                new EnhancersDesignModel(EnhancerKind.Augmentation, EnhancerType.ReflexBooster),
                typeof(GroundAugmentAtb), 5, 15);
            AssertAugment("reflex-booster", rb, mass: 15, str: 0, ev: 0.4, tough: 0, shield: 0, regen: 0.34);
        }

        [Test]
        [Description("The GROUND cadre + seal (their mass is COMPUTED from magnitude, no declared-mass slider): a 1.2 training cadre emits GroundTrainingAtb(1.2) at Mass 90; a 0.9 seal emits GroundSealAtb(0.9) at Mass 94.")]
        public void GroundCadreAndSeal_ReproduceFromTheForm()
        {
            // ground-training-cadre — Mass = 50*(1+(1.2-1)*4) = 90.
            var cadre = AssertShape("ground-training-cadre",
                new EnhancersDesignModel(EnhancerKind.AdvancedTraining, EnhancerType.GroundCadre),
                typeof(GroundTrainingAtb), 1, 90);
            Assert.That(cadre.CtorArgs[0], Is.EqualTo(1.2).Within(1e-6), "training arg");
            Assert.That(((GroundTrainingAtb)cadre.Attribute).TrainingMultiplier, Is.EqualTo(1.2).Within(1e-6), "GroundTrainingAtb.TrainingMultiplier");

            // sealed-systems — Mass = 40*(1+0.9*1.5) = 94.
            var seal = AssertShape("sealed-systems",
                new EnhancersDesignModel(EnhancerKind.Augmentation, EnhancerType.SealedSystems),
                typeof(GroundSealAtb), 1, 94);
            Assert.That(seal.CtorArgs[0], Is.EqualTo(0.9).Within(1e-6), "sealing arg");
            Assert.That(((GroundSealAtb)seal.Attribute).Sealing, Is.EqualTo(0.9).Within(1e-6), "GroundSealAtb.Sealing");
        }

        [Test]
        [Description("The Kind door is a grouping/filter only (writes no sim variable): KindOf maps each enhancer to its canonical group, and the model ignores the Kind passed in — a caliber built under the 'wrong' Kind computes identically.")]
        public void KindDoor_IsAGroupingFilter_NotADial()
        {
            Assert.That(EnhancersDesignModel.KindOf(EnhancerType.ShipCadre), Is.EqualTo(EnhancerKind.AdvancedTraining));
            Assert.That(EnhancersDesignModel.KindOf(EnhancerType.GroundCadre), Is.EqualTo(EnhancerKind.AdvancedTraining));
            Assert.That(EnhancersDesignModel.KindOf(EnhancerType.CrewAutomation), Is.EqualTo(EnhancerKind.Systems));
            Assert.That(EnhancersDesignModel.KindOf(EnhancerType.PowerArmour), Is.EqualTo(EnhancerKind.Augmentation));
            Assert.That(EnhancersDesignModel.KindOf(EnhancerType.WardProjector), Is.EqualTo(EnhancerKind.Augmentation));
            Assert.That(EnhancersDesignModel.KindOf(EnhancerType.SealedSystems), Is.EqualTo(EnhancerKind.Augmentation));

            // Kind writes nothing: the atb + mass are identical regardless of the Kind supplied.
            var right = new EnhancersDesignModel(EnhancerKind.AdvancedTraining, EnhancerType.ShipCadre).Compute();
            var wrong = new EnhancersDesignModel(EnhancerKind.Systems, EnhancerType.ShipCadre).Compute();
            Assert.That(wrong.Mass, Is.EqualTo(right.Mass).Within(1e-6), "Kind must not change Mass");
            Assert.That(wrong.CtorArgs[0], Is.EqualTo(right.CtorArgs[0]).Within(1e-6), "Kind must not change ctor args");
            Assert.That(wrong.CtorArgs[1], Is.EqualTo(right.CtorArgs[1]).Within(1e-6), "Kind must not change ctor args");
        }

        [Test]
        [Description("The honesty caveat, made executable: dialing a slider ABOVE its baseline costs mass exactly as the baseline-anchored template formula prices it, and dialing ShieldRegenFraction on a normally-5-arg augment promotes it to the 6-arg ctor (the intended-divergence path, distinct from the byte-identical default).")]
        public void UpgradedDials_CostMass_AndRegenPromotesToSixArg()
        {
            // Upgrade the caliber above its 1.3/1.2 baselines: +Max(0,1.5-1.3)*2000 + Max(0,1.4-1.2)*2000 = +800.
            var hotCadre = new EnhancersDesignModel(EnhancerKind.AdvancedTraining, EnhancerType.ShipCadre,
                firepowerCaliber: 1.5, toughnessCaliber: 1.4).Compute();
            Assert.That(hotCadre.Mass, Is.EqualTo(3000 + 800).Within(1e-6), "upgraded caliber mass");
            Assert.That(hotCadre.ArgCount, Is.EqualTo(2), "caliber stays 2-arg");

            // Upgrade power-armour strength above its 300 baseline: +Max(0,500-300)*0.1 = +20; still 5-arg.
            var heavyPA = new EnhancersDesignModel(EnhancerKind.Augmentation, EnhancerType.PowerArmour,
                strengthBonus: 500).Compute();
            Assert.That(heavyPA.Mass, Is.EqualTo(30 + 20).Within(1e-6), "upgraded power-armour mass");
            Assert.That(heavyPA.ArgCount, Is.EqualTo(5), "power-armour stays 5-arg at default regen");

            // Explicitly dialing regen on the shield generator promotes it to the 6-arg ctor (intended divergence).
            var wardedSG = new EnhancersDesignModel(EnhancerKind.Augmentation, EnhancerType.ShieldGenerator,
                shieldRegenFraction: 0.5).Compute();
            Assert.That(wardedSG.ArgCount, Is.EqualTo(6), "dialing regen promotes to 6-arg");
            Assert.That(((GroundAugmentAtb)wardedSG.Attribute).ShieldRegenFraction, Is.EqualTo(0.5).Within(1e-6), "dialed regen sticks");
        }

        // Reads the constructed GroundAugmentAtb's clamped fields (the authentic 5/6-arg ctor result).
        private static void AssertAugment(string name, EnhancerProfile p,
            double mass, double str, double ev, double tough, double shield, double regen)
        {
            var g = (GroundAugmentAtb)p.Attribute;
            Assert.That(g.Mass, Is.EqualTo(mass).Within(1e-6), $"{name} GroundAugmentAtb.Mass (CarryMass)");
            Assert.That(g.StrengthBonus, Is.EqualTo(str).Within(1e-6), $"{name} StrengthBonus");
            Assert.That(g.EvasionBonus, Is.EqualTo(ev).Within(1e-6), $"{name} EvasionBonus");
            Assert.That(g.ToughnessBonus, Is.EqualTo(tough).Within(1e-6), $"{name} ToughnessBonus");
            Assert.That(g.Shield, Is.EqualTo(shield).Within(1e-6), $"{name} Shield");
            Assert.That(g.ShieldRegenFraction, Is.EqualTo(regen).Within(1e-6), $"{name} ShieldRegenFraction (0.34 default for 5-arg)");
            // The augment ctor's first arg is CarryMass; at defaults CarryMass == component Mass.
            Assert.That(p.CtorArgs[0], Is.EqualTo(mass).Within(1e-6), $"{name} CarryMass arg == component Mass at defaults");
        }
    }
}
