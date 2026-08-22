using Pulsar4X.Combat;
using Pulsar4X.GroundCombat;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Components.Designers
{
    /// <summary>CHOICE 1 — the two combat-DEFENCE layers this door builds. Everything else about a defensive
    /// component (which sim value it writes, which sliders are live) follows from this pick.</summary>
    public enum DefenseLayer
    {
        /// <summary>A depleting + regenerating POOL that soaks fire before the hull/HP is touched (a force field).</summary>
        Shield,
        /// <summary>A per-hit BOUNCE — plating that soaks a share of each incoming shot (a hardened hull).</summary>
        Armour,
    }

    /// <summary>CHOICE 2 — which battlefield the piece bolts onto. It is INTRINSIC (it IS the component's mount type),
    /// and it picks the concrete engine part + the "pool currency" (ship shields count joules, ground shields count
    /// HP-scale points).</summary>
    public enum DefenseDomain
    {
        /// <summary>Bolts onto a ship (space combat).</summary>
        Ship,
        /// <summary>Bolts onto a ground unit / planetary battalion.</summary>
        Ground,
    }

    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL, Phase C — the DEFENSE door parametric designer, slice 1 (ENGINE MODEL).
    ///
    /// WHAT IT IS, in plain English: the same "one form instead of a menu of parts" idea the WEAPONS door proved
    /// (<see cref="WeaponsDesignModel"/>), applied to the four DEFENSIVE parts the game already ships. Instead of
    /// hand-authoring a separate "deflector array," "ablative plating," "shield generator," and so on, you make TWO
    /// picks and slide a few dials, and the right defensive component falls out of the one form. This class is the
    /// ENGINE HALF of that form: a pure calculator that turns those picks + dials into the exact component-attribute
    /// object (<c>*Atb</c>) the game already builds from JSON — so a design made through this model is byte-for-byte the
    /// same part the hand-authored base-mod template produces. The ImGui screen that drives it is a later slice
    /// (client, verified on the developer's machine — CI can compile the client but can't run it).
    ///
    /// WHY IT'S SHAPED THIS WAY (the derivation, per <c>docs/economy/DESIGNER-NORTH-STAR.md</c>): a "door" is DERIVED
    /// from the numbers the simulation actually reads off a defensive part. Group them by the question each answers, and
    /// the ones the design is FORCED into become the two CHOICES while the ones the player is FREE to set become the
    /// sliders:
    ///   • CHOICE 1 — <see cref="DefenseLayer"/> (Shield / Armour): a POOL that depletes vs. a per-hit BOUNCE. This is
    ///     the headline forced pick — it selects the <c>*Atb</c> FAMILY and which sliders are live.
    ///   • CHOICE 2 — <see cref="DefenseDomain"/> (Ship / Ground): which battlefield. It IS the mount type, so it's
    ///     intrinsic; it picks the CONCRETE part inside the family and the pool's units. The 2×2 maps like this:
    ///       (Shield, Ship)   → <see cref="ShieldAtb"/>          — a joules pool + a joules/sec recharge.
    ///       (Shield, Ground) → <see cref="GroundAugmentAtb"/>   — the shield-only slice of the augment part (a Force
    ///                                                             ward / personal shield); Strength/Evasion/Toughness
    ///                                                             are OTHER doors' outputs, so this door zeroes them.
    ///       (Armour, Ship)   → <see cref="ArmourHardeningAtb"/> — four SOAK FRACTIONS (0..0.9) tuned per damage nature.
    ///       (Armour, Ground) → <see cref="GroundArmorAtb"/>     — a mass/HP/defence plate, optionally tuned per nature.
    ///   • SLIDERS (≤4 live per branch, each writing exactly ONE atb value):
    ///       (1) MAGNITUDE — how much it stops (ship shield Capacity_J / ground shield Shield points / ground plate
    ///           Defense). Ship armour has no separate magnitude: its strength IS the sum of the four soak fractions.
    ///       (2) REGEN — how fast a shield refills (ship RegenRate_Jps / ground ShieldRegenFraction). Inert for armour.
    ///       (3) NATURE TUNING — armour only, four sub-dials for the K/E/X/O damage natures. On SHIP armour these are
    ///           SOAK FRACTIONS (0 = plain, up to 0.9); on GROUND armour they are RESIST FACTORS (1.0 = plain, &gt;1 =
    ///           tuned to that nature). Same slider, different engine meaning — the model emits the domain-correct one.
    ///       (4) BULK / HP — ground only (the frame must bear it): ground plate carries CarryMass + HP alongside its
    ///           Defense; a ground shield carries CarryMass. Ship mass is DERIVED from the other dials by the template
    ///           formula, so this slider is inert on the ship side.
    ///
    /// THE KEYSTONE ASYMMETRY (flagged, per DESIGNER-NORTH-STAR): a SHIELD's soak-by-nature is the ATTACKER's business
    /// (the resolver's fixed <c>ShieldSoakVsKinetic/Energy/…</c> in <see cref="CombatKernel"/> decide how much a given
    /// weapon bleeds through), so the shield-builder has NO nature dial — only capacity + recharge. ARMOUR's nature
    /// matchup is the DEFENDER's business (the plating you chose), so armour DOES get the four nature dials. That is why
    /// the Nature-Tuning slider is armour-only and inert for shields.
    ///
    /// THE HONEST CAVEAT (the load-bearing finding, flagged for the developer): a LITERAL two-choice/one-Magnitude form
    /// can't reproduce the per-instance variety the base mod ships — ground armour actually carries THREE independent
    /// size dials (CarryMass 25 + HP 150 + Defense 5), and the ground shield can dial its recharge fraction. This model
    /// therefore also accepts <see cref="Bulk"/>/<see cref="HP"/>/<see cref="Regen"/> and the four nature dials as free
    /// inputs, defaulting each to the CHOICE-forced value when the caller leaves it unset (<see cref="UseLayerDefault"/>):
    /// a ground plate left un-tuned reproduces the plain 3-arg plate (natures fall to 1.0); a ground shield left at
    /// default recharge builds the 5-arg augment (fraction stays 0.34); a dialed recharge builds the 6-arg ward. The
    /// slice-2 UI decides whether to expose these as advanced dials or accept a collapse — parked as an ADJUDICATION
    /// item, it does NOT block this model.
    ///
    /// WHAT IS DELIBERATELY NOT DONE HERE (the single load-bearing correctness decision, flagged): this is a pure
    /// IDENTITY pass-through — it constructs each <c>*Atb</c> through the SAME public ctor + arg order the JSON binder
    /// uses, so the ctor's own clamping (shield floors negatives at 0; ship soak clamps to [0, 0.9]) is the only
    /// transform. The HTML mock-up's proposed "zero-sum resist renormalization" (rescale the four armour resists to a
    /// fixed sum) and the resist floor/ceiling band are a DELIBERATE later balance pass — baking them in now would make
    /// this model fail to reproduce the shipped ablative (sum 4.6) and reactive (sum 5.2) platings byte-for-byte. Slice
    /// 1 is faithful reproduction; balance is a separate, flag-gated slice with its own gauge.
    ///
    /// CROSS-DOOR (scoped OUT): the base-mod <c>power-armor</c> (StrengthBonus) and <c>reflex-booster</c> (EvasionBonus)
    /// are also <see cref="GroundAugmentAtb"/> parts, but their non-shield dials belong to the CHASSIS / PROPULSION
    /// doors — the Defense door owns only their (zero) Shield arg. This model emits Str = Eva = Tough = 0 for every
    /// ground shield, which is the correct Defense-door contribution to those units; the Entity Assembler composes the
    /// rest. They are NOT reproducible by this door alone (by design), and the gauge documents that.
    ///
    /// BYTE-IDENTICAL / SAFE (slice-1 rules): this is a NEW file, a pure value type with no serialized state, no
    /// DataBlob, no <c>*Atb</c> ctor change — nothing in the live game calls it yet, so the whole engine is unchanged.
    /// It reuses the four existing save-safe ctors, so there is no L13 save-load risk and no gotcha-6 arity break. The
    /// gauge <c>Pulsar4X.Tests.DefenseDesignModelTests</c> proves it reproduces every pure-defense base-mod
    /// component's atb fields.
    /// </summary>
    public readonly struct DefenseDesignModel
    {
        /// <summary>Sentinel for "let the LAYER/DOMAIN force the default" on a slider the caller leaves unset. A real
        /// magnitude/regen/soak/resist is never negative, so a negative value means "unset" — ship soak falls to 0,
        /// ground resist falls to 1.0, ground recharge stays the 0.34 template default (a 5-arg augment).</summary>
        public const double UseLayerDefault = -1.0;

        /// <summary>CHOICE 1 — POOL (Shield) vs per-hit BOUNCE (Armour). Selects the atb family + which sliders are live.</summary>
        public DefenseLayer Layer { get; }
        /// <summary>CHOICE 2 — which battlefield (== the mount type). Selects the concrete atb + the pool currency.</summary>
        public DefenseDomain Domain { get; }

        /// <summary>SLIDER — how much it stops. Ship shield: Capacity_J (joules). Ground shield: Shield (HP-scale
        /// points). Ground armour: Defense (points). Inert for ship armour (whose strength is the soak-fraction sum).</summary>
        public double Magnitude { get; }
        /// <summary>SLIDER (Shield only) — how fast the pool refills. Ship: RegenRate_Jps (J/s). Ground:
        /// ShieldRegenFraction (fraction/hr) — left at <see cref="UseLayerDefault"/> it builds the 5-arg augment
        /// (fraction stays the 0.34 template default); dialed, it builds the 6-arg ward. Inert for armour.</summary>
        public double Regen { get; }

        /// <summary>SLIDER (Armour only) — vs KINETIC. Ship: a SOAK FRACTION (0 plain..0.9). Ground: a RESIST FACTOR
        /// (1.0 plain). <see cref="UseLayerDefault"/> → ship 0 / ground 1.0.</summary>
        public double NatureVsKinetic { get; }
        /// <summary>SLIDER (Armour only) — vs ENERGY. Ship soak fraction / ground resist factor (see VsKinetic).</summary>
        public double NatureVsEnergy { get; }
        /// <summary>SLIDER (Armour only) — vs EXPLOSIVE. Ship soak fraction / ground resist factor (see VsKinetic).</summary>
        public double NatureVsExplosive { get; }
        /// <summary>SLIDER (Armour only) — vs EXOTIC. Ship soak fraction / ground resist factor (see VsKinetic).</summary>
        public double NatureVsExotic { get; }

        /// <summary>SLIDER (Ground only) — the frame's carry-mass (CarryMass) the piece costs. Inert on the ship side
        /// (ship mass is derived from the other dials by the template formula).</summary>
        public double Bulk { get; }
        /// <summary>SLIDER (Ground armour only) — the health the plate adds (HP). Inert elsewhere.</summary>
        public double HP { get; }

        public DefenseDesignModel(DefenseLayer layer, DefenseDomain domain,
            double magnitude = 0,
            double regen = UseLayerDefault,
            double natureVsKinetic = UseLayerDefault,
            double natureVsEnergy = UseLayerDefault,
            double natureVsExplosive = UseLayerDefault,
            double natureVsExotic = UseLayerDefault,
            double bulk = 0, double hp = 0)
        {
            Layer = layer;
            Domain = domain;
            Magnitude = magnitude;
            Regen = regen;
            NatureVsKinetic = natureVsKinetic;
            NatureVsEnergy = natureVsEnergy;
            NatureVsExplosive = natureVsExplosive;
            NatureVsExotic = natureVsExotic;
            Bulk = bulk;
            HP = hp;
        }

        /// <summary>
        /// Build the concrete component attribute (<c>*Atb</c>) the game reads — the SAME object the matching base-mod
        /// JSON template produces, constructed through the SAME public ctor + arg order the JSON binder uses, so a
        /// design made through this model behaves identically to the hand-authored part. The (Layer, Domain) choice
        /// picks the type; the sliders fill its values, falling to the choice-forced default where left unset.
        /// </summary>
        public IComponentDesignAttribute Build()
        {
            switch (Layer)
            {
                case DefenseLayer.Shield:
                    if (Domain == DefenseDomain.Ship)
                    {
                        // ShieldAtb(capacity_J, regenRate_Jps) — the ctor clamps negatives to 0.
                        double regen = Regen >= 0 ? Regen : 0.0;
                        return new ShieldAtb(Magnitude, regen);
                    }
                    else
                    {
                        // GroundAugmentAtb, shield-only: Strength/Evasion/Toughness are OTHER doors' outputs, so the
                        // Defense door zeroes them and owns only the Shield arg (Magnitude) + the recharge (Regen).
                        if (Regen >= 0)
                            // 6-arg: a dialed recharge (the fast Ward Projector).
                            return new GroundAugmentAtb(Bulk, 0, 0, 0, Magnitude, Regen);
                        // 5-arg: recharge left at the template default 0.34 (the big slow Shield Generator).
                        return new GroundAugmentAtb(Bulk, 0, 0, 0, Magnitude);
                    }

                case DefenseLayer.Armour:
                    if (Domain == DefenseDomain.Ship)
                    {
                        // ArmourHardeningAtb(K, E, X, O) SOAK FRACTIONS — ctor clamps each to [0, MaxSoakFraction 0.9].
                        // Unset natures fall to 0 (a plain plate soaks nothing of that nature).
                        double k = NatureVsKinetic >= 0 ? NatureVsKinetic : 0.0;
                        double e = NatureVsEnergy >= 0 ? NatureVsEnergy : 0.0;
                        double x = NatureVsExplosive >= 0 ? NatureVsExplosive : 0.0;
                        double o = NatureVsExotic >= 0 ? NatureVsExotic : 0.0;
                        return new ArmourHardeningAtb(k, e, x, o);
                    }
                    else
                    {
                        // GroundArmorAtb RESIST FACTORS default 1.0 (plain plate). If the caller tuned NONE of the four,
                        // build the plain 3-arg plate (exactly what the ground-plating template's 3 AtbConstrArgs bind);
                        // any tuned nature builds the 7-arg plate (ablative/reactive).
                        bool tuned = NatureVsKinetic >= 0 || NatureVsEnergy >= 0
                                  || NatureVsExplosive >= 0 || NatureVsExotic >= 0;
                        if (!tuned)
                            return new GroundArmorAtb(Bulk, HP, Magnitude);

                        double k = NatureVsKinetic >= 0 ? NatureVsKinetic : 1.0;
                        double e = NatureVsEnergy >= 0 ? NatureVsEnergy : 1.0;
                        double x = NatureVsExplosive >= 0 ? NatureVsExplosive : 1.0;
                        double o = NatureVsExotic >= 0 ? NatureVsExotic : 1.0;
                        return new GroundArmorAtb(Bulk, HP, Magnitude, k, e, x, o);
                    }

                default:
                    return null;
            }
        }
    }
}
