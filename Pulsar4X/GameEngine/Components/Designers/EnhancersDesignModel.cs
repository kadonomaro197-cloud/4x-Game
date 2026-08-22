using System;
using Pulsar4X.Combat;
using Pulsar4X.GroundCombat;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Components.Designers
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the ENHANCERS door parametric designer, slice 1 (ENGINE MODEL).
    ///
    /// WHAT IT IS, in plain English: the Enhancers door is the "make a unit MORE than its bare chassis" menu — the
    /// veteran-crew stamp, the automation suite, the power armour, the personal shield, the training cadre, the sealed
    /// life-support suit. Today those are eight hand-authored components in the base mod. This class is the ENGINE HALF
    /// of a ONE-form designer that reproduces all eight: you pick two things (a KIND that narrows the list, then the
    /// ENHANCER itself) and slide a few dials, and the form emits the exact <c>*Atb</c> constructor arguments and the
    /// exact component MASS the hand-authored component would — so a design made through this form is byte-for-byte the
    /// same part the base mod ships. Nothing in the live game calls it yet (slice 1 is model + gauge only); the ImGui
    /// screen that will drive it is a later slice.
    ///
    /// WHY IT'S SHAPED THIS WAY (the derivation, per <c>docs/economy/DESIGNER-NORTH-STAR.md</c>): a "door" is DERIVED
    /// from the values the simulation actually reads. This door does NOT collapse to a fixed 2-choice/4-slider form the
    /// way Weapons does — the eight enhancers write to FIVE different <c>*Atb</c> types and hence five different sim
    /// variables, so its honest natural shape is TWO CHOICES plus a per-enhancer slider set:
    ///   • CHOICE 1 — <see cref="EnhancerKind"/> (Augmentation / Advanced Training / Systems): a grouping/FILTER only.
    ///     It writes NO sim variable — it fails the North Star's "which sim variable does this write?" test — so it is
    ///     deliberately kept as the locked "first door" that narrows the menu, not as a real dial. <see cref="Compute"/>
    ///     ignores it; it is carried only so the UI (and the gauge) can group the list. Use <see cref="KindOf"/> to read
    ///     the canonical Kind of any enhancer.
    ///   • CHOICE 2 — <see cref="EnhancerType"/> (the eight live enhancers): the FORCED/intrinsic choice. It selects the
    ///     <c>*Atb</c> TYPE emitted and thus which sim variable(s) the part writes — caliber → firepower/toughness on a
    ///     ship's <see cref="ShipCombatValueDB"/>; automation → the ship's bulk crew; the four augments → a ground unit's
    ///     carry/evasion/toughness/shield via the assembler; the cadre → a ground unit's training multiplier; the seal →
    ///     a ground unit's vacuum/toxic resistance.
    ///   • SLIDERS — vary by enhancer (see the ctor params). The maximal case is the GroundAugment attribute, whose four
    ///     presets (power armour / shield generator / ward projector / reflex booster) are ALL the one attribute with a
    ///     different axis dialed up: CarryMass + StrengthBonus + EvasionBonus + ToughnessBonus + Shield (+ the ward's
    ///     ShieldRegenFraction). The ship/cadre/seal enhancers are a magnitude or two plus, for caliber and automation, a
    ///     declared mass.
    ///
    /// THE HONEST CAVEAT (flagged for the developer, the standard door-model caveat): a literal fixed-slider form can't
    /// reproduce a per-instance variant unless the variant's dial is EXPOSED as a free input. Here every dial defaults to
    /// the ENHANCER's own forced value (pass <see cref="UseEnhancerDefault"/> — the default for every ctor param — to take
    /// it), and the eight base-mod default-designs carry NO Property overrides, so at defaults the form reproduces each
    /// shipped component exactly. The one place arity is load-bearing: <see cref="GroundAugmentAtb"/> has a 5-arg ctor
    /// (ShieldRegenFraction stays the 0.34 default) AND a 6-arg ctor (recharge dialed). The base-mod Ward Projector is the
    /// ONLY component that uses the 6-arg ctor; the other three augments MUST emit 5 args or they'd silently overwrite the
    /// 0.34 default (the exact-arity binder trap, gotcha 6 / L13). This model emits 6 args ONLY for the Ward Projector (or
    /// if a caller explicitly dials <see cref="ShieldRegenFraction"/>), and 5 for the rest.
    ///
    /// THE MASS RULE IT MIRRORS (the anti-drift heart of the gauge): the component MASS is the currency the Chassis door
    /// budgets on, and each template already prices its dials with the baseline-anchored idiom
    /// <c>+ Max(0, dial − baseline) × factor</c> — so a dial AT its shipped default costs nothing extra (byte-identical),
    /// and only an UPGRADED dial adds mass. The baseline for every augment axis IS that preset's shipped default value, so
    /// the whole augment family shares one formula with per-preset defaults. <see cref="Compute"/> duplicates each
    /// template's Mass formula EXACTLY; the gauge cross-checks the numbers so a future JSON re-tune trips it.
    ///
    /// BYTE-IDENTICAL / SAFE (slice-1 rules): this is a NEW file, a pure value type with no serialized state, no DataBlob,
    /// no <c>*Atb</c> ctor change — nothing in the live game calls it yet, so the whole engine is unchanged. It calls the
    /// EXISTING <c>*Atb</c> constructors (never adds an overload — that is the L13 save-break the campaign guards against),
    /// so the atb it builds carries the same clamped fields the JSON path produces.
    ///
    /// OUT OF SCOPE (nothing to reproduce — flagged, not faked): the interface/self-repair/morale "enhancers" (iface,
    /// fieldrep, fury) write NO sim variable and have NO <c>*Atb</c>, so the reproducible set is exactly these eight. The
    /// seal is homed here because its template + attribute physically sit in the base mod as an enhancers-door emit; if a
    /// later ruling moves it (and environmental hardening) to the Defense door, drop those two cases.
    /// </summary>
    public readonly struct EnhancersDesignModel
    {
        /// <summary>Sentinel for "let the chosen enhancer force its own default" on any slider. A real dial is never NaN.</summary>
        public const double UseEnhancerDefault = double.NaN;

        /// <summary>CHOICE 1 — the grouping/filter door. Writes no sim variable; ignored by <see cref="Compute"/>.</summary>
        public EnhancerKind Kind { get; }
        /// <summary>CHOICE 2 — the enhancer itself: selects the <c>*Atb</c> type emitted and the sim variable it writes.</summary>
        public EnhancerType Enhancer { get; }

        /// <summary>SLIDER — caliber PRIMARY: the firepower multiplier a Ship Cadre grants (1.0 = green crew).</summary>
        public double FirepowerCaliber { get; }
        /// <summary>SLIDER — caliber SECONDARY: the toughness multiplier a Ship Cadre grants.</summary>
        public double ToughnessCaliber { get; }
        /// <summary>SLIDER — Crew Automation: bulk crew positions the suite replaces.</summary>
        public double CrewReduction { get; }
        /// <summary>SLIDER — Ground Cadre: the training/veterancy multiplier (clamped ≥ 1.0 by the atb).</summary>
        public double TrainingMultiplier { get; }
        /// <summary>SLIDER — Sealed Systems: the fraction of vacuum + toxic attrition negated (clamped 0..1 by the atb).</summary>
        public double Sealing { get; }

        /// <summary>SLIDER (augment / declared-mass) — the mass arg: CarryMass for an augment, Cadre Mass for caliber,
        /// Automation Mass for crew. Ignored by the training/seal enhancers (their mass is computed from magnitude).</summary>
        public double DeclaredMass { get; }
        /// <summary>SLIDER (augment) — StrengthBonus (added to the frame's carry budget).</summary>
        public double StrengthBonus { get; }
        /// <summary>SLIDER (augment) — EvasionBonus (dodge).</summary>
        public double EvasionBonus { get; }
        /// <summary>SLIDER (augment) — ToughnessBonus (survivability multiplier hook).</summary>
        public double ToughnessBonus { get; }
        /// <summary>SLIDER (augment) — Shield (flat incoming-damage soak pool).</summary>
        public double Shield { get; }
        /// <summary>SLIDER (augment) — ShieldRegenFraction (recharge dial). Supplying it (or choosing Ward Projector)
        /// promotes the emit to the 6-arg <see cref="GroundAugmentAtb"/> ctor; the other augments stay at the 5-arg
        /// default (0.34).</summary>
        public double ShieldRegenFraction { get; }

        public EnhancersDesignModel(
            EnhancerKind kind, EnhancerType enhancer,
            double firepowerCaliber = UseEnhancerDefault, double toughnessCaliber = UseEnhancerDefault,
            double crewReduction = UseEnhancerDefault, double trainingMultiplier = UseEnhancerDefault,
            double sealing = UseEnhancerDefault, double declaredMass = UseEnhancerDefault,
            double strengthBonus = UseEnhancerDefault, double evasionBonus = UseEnhancerDefault,
            double toughnessBonus = UseEnhancerDefault, double shield = UseEnhancerDefault,
            double shieldRegenFraction = UseEnhancerDefault)
        {
            Kind = kind;
            Enhancer = enhancer;
            FirepowerCaliber = firepowerCaliber;
            ToughnessCaliber = toughnessCaliber;
            CrewReduction = crewReduction;
            TrainingMultiplier = trainingMultiplier;
            Sealing = sealing;
            DeclaredMass = declaredMass;
            StrengthBonus = strengthBonus;
            EvasionBonus = evasionBonus;
            ToughnessBonus = toughnessBonus;
            Shield = shield;
            ShieldRegenFraction = shieldRegenFraction;
        }

        /// <summary>The canonical filter-door group of each enhancer (CHOICE 1 is a grouping only — this is the mapping
        /// the UI would use to narrow the list). Writes nothing; a readout.</summary>
        public static EnhancerKind KindOf(EnhancerType enhancer)
        {
            switch (enhancer)
            {
                case EnhancerType.ShipCadre:
                case EnhancerType.GroundCadre:
                    return EnhancerKind.AdvancedTraining;
                case EnhancerType.CrewAutomation:
                    return EnhancerKind.Systems;
                default: // PowerArmour / ShieldGenerator / WardProjector / ReflexBooster / SealedSystems
                    return EnhancerKind.Augmentation;
            }
        }

        private static double Resolve(double over, double def) => double.IsNaN(over) ? def : over;

        /// <summary>
        /// Build the <see cref="EnhancerProfile"/> — the exact <c>*Atb</c> constructor arguments (with the exact arity),
        /// the constructed attribute (so its own clamping is authentic), and the component MASS — that the chosen
        /// enhancer + dials would produce, mirroring the base-mod template's Mass formula and <c>AtbConstrArgs</c> order
        /// EXACTLY. A design made through this profile is byte-for-byte the hand-authored base-mod component.
        /// </summary>
        public EnhancerProfile Compute()
        {
            switch (Enhancer)
            {
                case EnhancerType.ShipCadre:
                {
                    // unit-caliber — UnitCaliberAtb(firepowerMult, toughnessMult); Mass baseline-anchored at 1.3 / 1.2.
                    double fp = Resolve(FirepowerCaliber, 1.3);
                    double tuf = Resolve(ToughnessCaliber, 1.2);
                    double cadreMass = Resolve(DeclaredMass, 3000);
                    double mass = cadreMass + Math.Max(0, fp - 1.3) * 2000 + Math.Max(0, tuf - 1.2) * 2000;
                    return new EnhancerProfile(typeof(UnitCaliberAtb), new[] { fp, tuf },
                        new UnitCaliberAtb(fp, tuf), mass);
                }

                case EnhancerType.CrewAutomation:
                {
                    // crew-automation — CrewAutomationAtb(crewReduction); Mass baseline-anchored at 30.
                    double cr = Resolve(CrewReduction, 30);
                    double autoMass = Resolve(DeclaredMass, 5000);
                    double mass = autoMass + Math.Max(0, cr - 30) * 50;
                    return new EnhancerProfile(typeof(CrewAutomationAtb), new[] { cr },
                        new CrewAutomationAtb(cr), mass);
                }

                case EnhancerType.PowerArmour:
                    // GroundAugmentAtb 5-arg; defaults (=baselines) carry 30, str 300, ev 0, tough 0.2, shield 0.
                    return BuildAugment(defCarry: 30, defStr: 300, defEv: 0, defTough: 0.2, defShield: 0,
                        forceRegen: false, defRegen: 0.34);

                case EnhancerType.ShieldGenerator:
                    // GroundAugmentAtb 5-arg; defaults carry 20, shield 150, rest 0.
                    return BuildAugment(defCarry: 20, defStr: 0, defEv: 0, defTough: 0, defShield: 150,
                        forceRegen: false, defRegen: 0.34);

                case EnhancerType.WardProjector:
                    // GroundAugmentAtb 6-arg — the ONLY 6-arg component; defaults carry 20, shield 60, regen 1.0.
                    return BuildAugment(defCarry: 20, defStr: 0, defEv: 0, defTough: 0, defShield: 60,
                        forceRegen: true, defRegen: 1.0);

                case EnhancerType.ReflexBooster:
                    // GroundAugmentAtb 5-arg; defaults carry 15, evasion 0.4, rest 0.
                    return BuildAugment(defCarry: 15, defStr: 0, defEv: 0.4, defTough: 0, defShield: 0,
                        forceRegen: false, defRegen: 0.34);

                case EnhancerType.GroundCadre:
                {
                    // ground-training-cadre — GroundTrainingAtb(trainingMultiplier); Mass = 50 * (1 + (mult-1)*4).
                    double tm = Resolve(TrainingMultiplier, 1.2);
                    double mass = 50 * (1 + (tm - 1) * 4);
                    return new EnhancerProfile(typeof(GroundTrainingAtb), new[] { tm },
                        new GroundTrainingAtb(tm), mass);
                }

                case EnhancerType.SealedSystems:
                {
                    // sealed-systems — GroundSealAtb(sealing); Mass = 40 * (1 + sealing*1.5).
                    double s = Resolve(Sealing, 0.9);
                    double mass = 40 * (1 + s * 1.5);
                    return new EnhancerProfile(typeof(GroundSealAtb), new[] { s },
                        new GroundSealAtb(s), mass);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(Enhancer), Enhancer, "Unknown enhancer.");
            }
        }

        /// <summary>
        /// The shared GroundAugment builder — the four presets are ONE attribute with per-preset default dial values that
        /// double as the mass baselines. Mass mirrors the template formula exactly:
        /// <c>CarryMass + Max(0,Str-b)*0.1 + Max(0,Ev-b)*200 + Max(0,Tough-b)*100 + Max(0,Shield-b)*0.5 [+ Max(0,Regen-b)*20]</c>.
        /// The regen term + the 6-arg ctor are used ONLY when this preset forces a recharge dial (Ward Projector) or the
        /// caller explicitly dials <see cref="ShieldRegenFraction"/>; otherwise the 5-arg ctor keeps the 0.34 default.
        /// </summary>
        private EnhancerProfile BuildAugment(
            double defCarry, double defStr, double defEv, double defTough, double defShield,
            bool forceRegen, double defRegen)
        {
            double carry = Resolve(DeclaredMass, defCarry);
            double str = Resolve(StrengthBonus, defStr);
            double ev = Resolve(EvasionBonus, defEv);
            double tough = Resolve(ToughnessBonus, defTough);
            double shield = Resolve(Shield, defShield);

            double mass = carry
                + Math.Max(0, str - defStr) * 0.1
                + Math.Max(0, ev - defEv) * 200
                + Math.Max(0, tough - defTough) * 100
                + Math.Max(0, shield - defShield) * 0.5;

            // 6-arg iff this preset forces a recharge dial (Ward) OR the caller explicitly supplied one.
            bool useRegenCtor = forceRegen || !double.IsNaN(ShieldRegenFraction);
            if (useRegenCtor)
            {
                double regen = Resolve(ShieldRegenFraction, defRegen);
                mass += Math.Max(0, regen - defRegen) * 20;
                return new EnhancerProfile(typeof(GroundAugmentAtb),
                    new[] { carry, str, ev, tough, shield, regen },
                    new GroundAugmentAtb(carry, str, ev, tough, shield, regen), mass);
            }

            return new EnhancerProfile(typeof(GroundAugmentAtb),
                new[] { carry, str, ev, tough, shield },
                new GroundAugmentAtb(carry, str, ev, tough, shield), mass);
        }
    }

    /// <summary>CHOICE 1 — the grouping/filter door (writes no sim variable; narrows the enhancer list only).</summary>
    public enum EnhancerKind
    {
        Augmentation,
        AdvancedTraining,
        Systems,
    }

    /// <summary>CHOICE 2 — the enhancer itself. Selects the <c>*Atb</c> type emitted and the sim variable it writes.</summary>
    public enum EnhancerType
    {
        /// <summary>unit-caliber — <see cref="UnitCaliberAtb"/> (ship firepower/toughness).</summary>
        ShipCadre,
        /// <summary>crew-automation — <see cref="CrewAutomationAtb"/> (ship bulk crew).</summary>
        CrewAutomation,
        /// <summary>power-armor — <see cref="GroundAugmentAtb"/> 5-arg (carry/toughness).</summary>
        PowerArmour,
        /// <summary>shield-generator — <see cref="GroundAugmentAtb"/> 5-arg (shield pool).</summary>
        ShieldGenerator,
        /// <summary>ward-projector — <see cref="GroundAugmentAtb"/> 6-arg (shield + fast recharge). The only 6-arg emit.</summary>
        WardProjector,
        /// <summary>reflex-booster — <see cref="GroundAugmentAtb"/> 5-arg (evasion).</summary>
        ReflexBooster,
        /// <summary>ground-training-cadre — <see cref="GroundTrainingAtb"/> (ground veterancy).</summary>
        GroundCadre,
        /// <summary>sealed-systems — <see cref="GroundSealAtb"/> (vacuum/toxic resistance).</summary>
        SealedSystems,
    }

    /// <summary>
    /// The output of <see cref="EnhancersDesignModel.Compute"/>: the exact <c>*Atb</c> ctor args (with the load-bearing
    /// arity), the constructed attribute (authentic clamping), and the component MASS (= the JSON MassPerUnit) — enough
    /// to prove a form-built enhancer is byte-for-byte the hand-authored base-mod component.
    /// </summary>
    public readonly struct EnhancerProfile
    {
        /// <summary>The concrete <c>*Atb</c> type this enhancer emits.</summary>
        public Type AttributeType { get; }
        /// <summary>The exact constructor arguments, in template <c>AtbConstrArgs</c> order — its LENGTH is the arity the
        /// exact-arity JSON binder would match (5 vs 6 for the augment is load-bearing).</summary>
        public double[] CtorArgs { get; }
        /// <summary>The constructed attribute (built through the real ctor, so its own clamps are applied).</summary>
        public IComponentDesignAttribute Attribute { get; }
        /// <summary>The component mass (kg) — the JSON MassPerUnit; the currency the Chassis door budgets on.</summary>
        public double Mass { get; }
        /// <summary>The number of ctor args emitted (= the binder arity).</summary>
        public int ArgCount => CtorArgs.Length;

        public EnhancerProfile(Type attributeType, double[] ctorArgs, IComponentDesignAttribute attribute, double mass)
        {
            AttributeType = attributeType;
            CtorArgs = ctorArgs;
            Attribute = attribute;
            Mass = mass;
        }
    }
}
