using System;
using System.Collections.Generic;

namespace Pulsar4X.Combat
{
    /// <summary>
    /// THE ONE SALVO KERNEL — the pure damage math both a SHIP battle and a PLANETARY (ground) battle run through.
    ///
    /// Plain English: today the ship resolver (<see cref="CombatEngagement"/>) and the ground resolver
    /// (<c>GroundForcesProcessor.ResolveRegionCombat</c>) compute the SAME ideas — does the shot land, does a shield
    /// soak it, does armour bounce it, how much health is left — but with two separate copies of the arithmetic. This
    /// class is the shared home for that arithmetic, written to a **neutral view** (<see cref="Combatant"/>) that a
    /// ship OR a ground unit can present, so neither the hex board nor the ship <c>Entity</c> leaks into the math. It
    /// is the seam the resolver-merge (docs/RESOLVER-MERGE-DESIGN.md) is built on.
    ///
    /// **Purity is the load-bearing property.** Every function here is pure arithmetic — no entity mutation, no RNG,
    /// no clock. That is what keeps combat DETERMINISTIC (the locked rule: fast-forward must equal watch). The caller
    /// applies the results (destroys a ship / drains a unit's Health); the kernel only computes them.
    ///
    /// **Wiring status: SHIP side routes through here as of slice 2 (2026-07-08).** The dodge/shield arithmetic
    /// (<c>HitFraction</c> / <c>LandedFraction</c> / <c>SoakFractionOf</c> / <c>ResolveShield</c> /
    /// <c>ShieldSoakFraction</c>) now lives ONLY here — <see cref="CombatEngagement"/>'s same-named helpers are thin
    /// delegators to this class, and the tuning constants there forward to the ones below, so the kernel is the single
    /// source of truth for the ship path (no drift possible). The ship combat fixtures (CombatPerformance / Dodge /
    /// Shield / Triangle / Stress / BattleSims) are the byte-identity tripwire that proved the delegation moved no
    /// number. <c>ArmourSoak</c> is ALSO wired as of slice 3a — <c>GroundCombat.GroundDamageMatrix.ArmourSoak</c> and
    /// its two armour constants now delegate/forward here, so the kernel is the single source of truth for the flat
    /// armour math on BOTH domains. The rest of the planetary resolver (weapon profiles, the dodge/shield reconcile,
    /// the closing model on the hex board) adopts this kernel in slice 3b+. <see cref="CombatKernelTests"/> pins these
    /// outputs. See docs/RESOLVER-MERGE-DESIGN.md §5.
    /// </summary>
    public static class CombatKernel
    {
        // ── Dodge-model tuning (copied from CombatEngagement — keep in lockstep until slice 2 removes the ship copy) ─

        /// <summary>Shot velocity (m/s) at which a weapon half-defeats evasion (beam ≫ this ≈ always hits; slug ≪
        /// this is dodgeable). Mirror of <see cref="CombatEngagement.VelocityReference_mps"/>.</summary>
        public const double VelocityReference_mps = 1_000_000.0;

        /// <summary>Saturation (tracks/sec) at which a weapon half-guarantees a hit regardless of dodge (flak ≫ this).
        /// Mirror of <see cref="CombatEngagement.SaturationReference"/>.</summary>
        public const double SaturationReference = 50.0;

        /// <summary>Floor on the fraction of fire that lands, so enough volume kills even a perfect dodger.
        /// Mirror of <see cref="CombatEngagement.MinLandedFraction"/>.</summary>
        public const double MinLandedFraction = 0.02;

        /// <summary>Flight time (s) at which a ballistic shot's RANGE penalty reaches half its max — the
        /// "accuracy falls off with distance" knob. Inert at separation 0. Mirror of
        /// <see cref="CombatEngagement.FlightTimeReference_s"/>.</summary>
        public const double FlightTimeReference_s = 10.0;

        /// <summary>Evasion-independent base miss added to the range term, so even a 0-evasion hull is hard to hit
        /// with a dumb slug at long range (forces fleets to close). The kernel OWNS this dial as of slice 2 —
        /// <see cref="CombatEngagement.RangeBaseMiss"/> is a forwarding property onto it, so the ship path and the
        /// shared kernel can never read different values. Live-tuned; 0 = old evasion-only behaviour.</summary>
        public static double RangeBaseMiss = 0.9;

        // ── Shield nature matchup (copied from CombatEngagement — the same numbers the ground GroundDamageMatrix uses)

        /// <summary>Fraction of a KINETIC salvo a shield can absorb (mass-at-velocity is stopped best).</summary>
        public const double ShieldSoakVsKinetic = 1.0;
        /// <summary>Fraction of an ENERGY salvo a shield can absorb (coherent energy bleeds through).</summary>
        public const double ShieldSoakVsEnergy = 0.5;
        /// <summary>Fraction of an EXPLOSIVE salvo a shield can absorb (an area detonation partly bypasses).</summary>
        public const double ShieldSoakVsExplosive = 0.75;
        /// <summary>Fraction of an EXOTIC salvo a shield can absorb (anti-shield archetype — full bypass to hull).</summary>
        public const double ShieldSoakVsExotic = 0.0;

        // ── Armour soak (copied from GroundCombat.GroundDamageMatrix — the flat-per-source plating the space side gains)

        /// <summary>Damage soaked off EACH attacker-source per point of the target's armour (Defense). FLAT — so a
        /// swarm of small volleys is mostly bounced while one big alpha punches through. Mirror of
        /// <c>GroundDamageMatrix.ArmourSoakPerPoint</c>.</summary>
        public const double ArmourSoakPerPoint = 1.5;
        /// <summary>A source always lands at least this fraction no matter how much armour it meets (armour is never
        /// total immunity). Mirror of <c>GroundDamageMatrix.ArmourMinPassFraction</c>.</summary>
        public const double ArmourMinPassFraction = 0.1;

        /// <summary>
        /// THE NEUTRAL VIEW — what a ship entity OR a ground unit presents to the kernel. No hex, no <c>Entity</c>:
        /// the kernel sees only these value fields plus <see cref="Weapons"/> (the SAME <see cref="WeaponProfile"/>
        /// type both domains carry) and a 1-D <see cref="Position_m"/> (fleet separation in space; hex-distance ×
        /// metres-per-hex on a planet). The caller keeps its own back-reference (ship id / GroundUnit ref) to apply
        /// the results the kernel returns. See docs/RESOLVER-MERGE-DESIGN.md §2.
        /// </summary>
        public sealed class Combatant
        {
            /// <summary>Which side this combatant is on (a faction id). The kernel never decides hostility — the
            /// caller groups combatants into attackers/defenders before calling in.</summary>
            public int FactionId;

            /// <summary>Current hit-points, joules-scale (a ship's Toughness | a unit's Health × its scale).</summary>
            public double Health;

            /// <summary>Full hit-points at build — the ceiling Health was seeded to.</summary>
            public double MaxHealth;

            /// <summary>How hard it is to HIT (0 = a sitting brick; capped near 1 for a nimble target). Read by
            /// <see cref="HitFraction"/> — note you cannot dodge a beam, so evasion only bites finite-velocity fire.</summary>
            public double Evasion;

            /// <summary>Flat armour (a ground unit's Defense). Soaked flat off each incoming source via
            /// <see cref="ArmourSoak"/>. 0 = unarmoured. (Ship toughness folds armour into Health today; the shared
            /// per-source model is what slice 2+ reconciles — see the design doc §3 pt 4.)</summary>
            public double Armour;

            /// <summary>Current shield charge (joules). 0 = no shield / down. Drained by <see cref="ResolveShield"/>
            /// before Health takes damage.</summary>
            public double ShieldPool;

            /// <summary>Full shield capacity (joules) the pool regenerates toward. 0 = unshielded.</summary>
            public double ShieldCapacity;

            /// <summary>Shield recharge rate (joules/sec) toward <see cref="ShieldCapacity"/> between volleys.</summary>
            public double ShieldRegen;

            /// <summary>What this combatant FIRES — the same <see cref="WeaponProfile"/> list a ship carries. A
            /// planetary unit's designer produces these too (the weapon-unification work).</summary>
            public List<WeaponProfile> Weapons;

            /// <summary>Its position on the 1-D range axis (metres): fleet separation in space, or
            /// hex-distance × metres-per-hex on a planet surface. The kernel reads only the metric gap; the hex board
            /// stays in the planetary caller.</summary>
            public double Position_m;
        }

        // ── Pure salvo math ───────────────────────────────────────────────────────────────────────────────────────

        /// <summary>How much of a nature's damage a shield is ABLE to stop (the rest bleeds through no matter the
        /// charge). Mirror of the private helper in <see cref="CombatEngagement"/> and
        /// <c>GroundDamageMatrix.ShieldEff</c>.</summary>
        public static double ShieldSoakFraction(WeaponNature nature) => nature switch
        {
            WeaponNature.Kinetic => ShieldSoakVsKinetic,
            WeaponNature.Energy => ShieldSoakVsEnergy,
            WeaponNature.Explosive => ShieldSoakVsExplosive,
            WeaponNature.Exotic => ShieldSoakVsExotic,
            _ => ShieldSoakVsKinetic,
        };

        /// <summary>Fraction of one weapon's shots that LAND on a target with the given evasion, at the given
        /// engagement separation. Byte-for-byte the ship <see cref="CombatEngagement.HitFraction"/>: fast/guided
        /// weapons defeat evasion (a beam ignores it), high saturation floors the result (flak fills the sky), and
        /// range degrades accuracy for ballistic weapons (guided resists via Tracking). <paramref name="separation_m"/>
        /// 0 = point blank / closing off → the range term is inert and this equals the pre-closing curve.</summary>
        public static double HitFraction(WeaponProfile w, double evasion, double separation_m = 0)
        {
            double velocityTerm = w.Velocity / (w.Velocity + VelocityReference_mps);                  // beam → ~1, slug → low
            double trackingEffectiveness = velocityTerm > w.Tracking ? velocityTerm : w.Tracking;     // guided tracks even when slow
            double dodgeChance = evasion * (1.0 - trackingEffectiveness);

            if (separation_m > 0 && w.Velocity > 0)
            {
                double flightTime = separation_m / w.Velocity;
                double timeFactor = flightTime / (flightTime + FlightTimeReference_s);
                double tracking = w.Tracking < 0 ? 0 : w.Tracking > 1 ? 1 : w.Tracking;
                dodgeChance += (evasion + RangeBaseMiss) * timeFactor * (1.0 - tracking);
            }
            if (dodgeChance > 1.0) dodgeChance = 1.0;

            double saturationFloor = double.IsInfinity(w.Saturation) ? 1.0 : w.Saturation / (w.Saturation + SaturationReference);
            if (saturationFloor < MinLandedFraction) saturationFloor = MinLandedFraction;

            double hit = 1.0 - dodgeChance;
            if (hit < saturationFloor) hit = saturationFloor;
            if (hit > 1.0) hit = 1.0;
            return hit;
        }

        /// <summary>The damage-weighted fraction of an incoming fire mix that LANDS on a target with the given
        /// evasion — byte-for-byte the ship <see cref="CombatEngagement"/> LandedFraction. Returns 1.0 for an empty
        /// / zero-damage mix.</summary>
        public static double LandedFraction(List<WeaponProfile> fire, double evasion, double separation_m = 0)
        {
            double total = 0, landed = 0;
            foreach (var w in fire)
            {
                total += w.DamagePerSecond;
                landed += w.DamagePerSecond * HitFraction(w, evasion, separation_m);
            }
            return total > 0 ? landed / total : 1.0;
        }

        /// <summary>The damage-weighted fraction of an incoming fire mix a shield CAN stop — the nature matchup rolled
        /// up over the salvo (all-kinetic → 1.0, all-energy → 0.5, all-exotic → 0.0, mixes interpolate). Byte-for-byte
        /// the ship <see cref="CombatEngagement"/> SoakFractionOf.</summary>
        public static double SoakFractionOf(List<WeaponProfile> incoming)
        {
            double total = 0, soak = 0;
            foreach (var w in incoming)
            {
                total += w.DamagePerSecond;
                soak += w.DamagePerSecond * ShieldSoakFraction(w.Nature);
            }
            return total > 0 ? soak / total : 0;
        }

        /// <summary>Drain a shield pool against one salvo, then regenerate toward capacity — byte-for-byte the ship
        /// <see cref="CombatEngagement"/> ResolveShield. Given the pool's current charge, its capacity, regen, the
        /// salvo's total hull-damage, the salvo's soakable FRACTION (<see cref="SoakFractionOf"/>) and dt: absorb the
        /// soakable part up to the charge, then recharge. Returns how much was ABSORBED and the pool AFTER. A
        /// 0-capacity (unshielded) pool absorbs nothing — byte-identical no-op.</summary>
        public static (double absorbed, double newPool) ResolveShield(
            double pool, double capacity, double regen, double salvoDamage, double soakFraction, double dt)
        {
            double absorbed = 0;
            if (capacity > 0 && pool > 0 && soakFraction > 0 && salvoDamage > 0)
            {
                double soakable = salvoDamage * soakFraction;
                absorbed = soakable < pool ? soakable : pool;
                pool -= absorbed;
            }
            if (regen > 0 && pool < capacity)
                pool = Math.Min(capacity, pool + regen * dt);
            return (absorbed, pool);
        }

        /// <summary>Flat ARMOUR soak for ONE attacker-source's contribution — byte-for-byte the ground
        /// <c>GroundDamageMatrix.ArmourSoak</c>. Subtract a flat amount (scaled by the target's armour), floored so
        /// the source always lands <see cref="ArmourMinPassFraction"/> of its damage. Because it's flat-per-source,
        /// N small volleys lose N×(flat) total while one big volley loses only (flat): armour bounces the swarm, the
        /// alpha punches through. Never throws.</summary>
        public static double ArmourSoak(double armour, double sourceDamage) => ArmourSoak(armour, sourceDamage, 0.0);

        /// <summary>Flat ARMOUR soak WITH weapon PENETRATION — the armour half of the matchup
        /// (docs/COMPONENT-DESIGNER-DIALS.md ⚙1 backlog #1). Penetration cancels armour point-for-point BEFORE the flat
        /// soak: an AP/sabot/lance round with <paramref name="penetration"/> ≥ the target's armour meets no effective
        /// plating and lands in full (like an unarmoured target), while a normal round (penetration 0) is byte-for-byte
        /// the flat soak above — so this reduces to the old <see cref="ArmourSoak(double,double)"/> when penetration is
        /// 0. Penetration is clamped at 0 (it can never make armour STRONGER). Never throws. Same flat-per-source rule,
        /// so N small volleys still lose N×(flat) while one big alpha loses only (flat) — penetration just shrinks the
        /// plating each source meets.</summary>
        public static double ArmourSoak(double armour, double sourceDamage, double penetration)
        {
            if (sourceDamage <= 0) return 0.0;
            if (penetration < 0) penetration = 0.0;
            double effectiveArmour = armour - penetration;
            if (effectiveArmour <= 0) return sourceDamage;   // penetration meets/beats the plating → full pass (as if unarmoured)
            double floor = sourceDamage * ArmourMinPassFraction;
            double after = sourceDamage - effectiveArmour * ArmourSoakPerPoint;
            return after < floor ? floor : after;
        }

        /// <summary>Upper bound on how many shots a single source's fire is split into for the burst soak — keeps a
        /// tiny-per-shot weapon (a minigun) from splitting into a pathological chunk count. Flagged balance value.</summary>
        public const int BurstSoakMaxShots = 1000;

        /// <summary>How many shots a weapon's per-second fire is treated as for the alpha-vs-chip armour soak: its
        /// damage-per-second divided by its <see cref="WeaponProfile.PerShotEnergy"/>, clamped to [1, <see cref="BurstSoakMaxShots"/>].
        /// A cannon (huge PerShotEnergy) → 1 (one alpha); a repeater (small PerShotEnergy) → many. 0/unspecified
        /// PerShotEnergy → 1 (single lump), so an un-dialled weapon is byte-identical. This is the shared rule both
        /// domains use so the granularity is defined ONCE.</summary>
        public static int BurstShotCount(WeaponProfile w)
        {
            if (w == null || w.PerShotEnergy <= 0 || w.DamagePerSecond <= 0) return 1;
            double n = Math.Round(w.DamagePerSecond / w.PerShotEnergy);
            if (n < 1) return 1;
            if (n > BurstSoakMaxShots) return BurstSoakMaxShots;
            return (int)n;
        }

        /// <summary>Flat ARMOUR soak of a source whose fire is a BURST of <paramref name="shotCount"/> equal shots — the
        /// alpha-vs-chip identity (⚙1 backlog #2). Splits the source's damage into that many equal chunks and soaks each
        /// flat (with <paramref name="penetration"/>), then sums. Because armour is flat-per-shot, many small chunks each
        /// lose (flat) so most is bounced, while one big chunk loses only (flat) and punches through — so a repeater and
        /// a cannon of EQUAL total damage land very differently against plate. <paramref name="shotCount"/> ≤ 1 is
        /// byte-for-byte <see cref="ArmourSoak(double,double,double)"/> (one lump), so an un-dialled weapon is unchanged.
        /// Never throws.</summary>
        public static double ArmourSoakBurst(double armour, double sourceDamage, int shotCount, double penetration = 0)
        {
            if (sourceDamage <= 0) return 0.0;
            if (shotCount <= 1) return ArmourSoak(armour, sourceDamage, penetration);
            double perChunk = sourceDamage / shotCount;
            return ArmourSoak(armour, perChunk, penetration) * shotCount;
        }
    }
}
