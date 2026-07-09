using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// RESOLVER MERGE, slice 3b-ii PREVIEW — the BEFORE/AFTER number readout the developer asked to review before the
    /// live ground-resolver swap. It computes, side by side, the per-attacker damage the CURRENT resolver deals
    /// (`GroundTerrain.TriangleMult` × `GroundDamageMatrix.Matchup`, then `ArmourSoak`) and what the KERNEL path will
    /// deal (the 3b-i bridge: `HitFraction` dodge + `SoakFractionOf` shield pool + `ArmourSoak`, NO triangle
    /// multiplier). Everything common to both — terrain affinity, stance, cover/fort, `SalvoScale` — is held equal
    /// (open terrain, no stance/fort) so the table isolates exactly WHAT THE MERGE CHANGES.
    ///
    /// It ASSERTS the exact numbers, so a green CI run certifies the comparison table is correct (this environment has
    /// no local SDK — CI is the calculator). NOTHING here changes live combat; the swap itself is the next slice, done
    /// only after the developer signs off on these deltas.
    /// </summary>
    [TestFixture]
    public class GroundResolverComparisonReadout
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[ground-compare] " + m);

        // Garrison stats (GroundStartGarrison): Infantry 100 atk / 10 def, Armor 140 / 15, Artillery 160 / 5.
        private static GroundUnit Tgt(GroundUnitType type, double def, double evasion = 0, double shield = 0) =>
            new GroundUnit { UnitType = type, Defense = def, Evasion = evasion, Shield = shield, Health = 500, MaxHealth = 500 };

        private const GroundTerrainClass Open = GroundTerrainClass.Open;

        /// <summary>The CURRENT resolver's per-attacker damage to one co-located target (open terrain, no stance/fort,
        /// SalvoScale factored out) = ArmourSoak(def, attack × terrainAffinity × TRIANGLE × Matchup).</summary>
        private static double OldCore(double attack, GroundUnitType atkType, GroundWeaponMode mode, GroundUnit t)
        {
            double pool = attack * GroundTerrain.TerrainAttackMult(atkType, Open)
                          * GroundTerrain.TriangleMult(atkType, t.UnitType)
                          * GroundDamageMatrix.Matchup(mode, t);
            return GroundDamageMatrix.ArmourSoak(t.Defense, pool);
        }

        /// <summary>The KERNEL path's per-attacker FIRST-hit damage (shield pool full) = ArmourSoak(def, attack ×
        /// terrainAffinity × HitFraction[dodge] − shieldAbsorbed). No triangle multiplier — it dissolves.</summary>
        private static double NewCoreFirstHit(double attack, GroundUnitType atkType, GroundWeaponMode mode, GroundUnit t)
        {
            var profile = GroundCombatant.ToWeaponProfile(new GroundUnit { Attack = attack, DamageType = mode, Range = mode == GroundWeaponMode.Artillery ? 3 : 1 });
            double afterTerrain = attack * GroundTerrain.TerrainAttackMult(atkType, Open);
            double afterDodge = afterTerrain * CombatKernel.HitFraction(profile, t.Evasion);
            double shieldSoak = CombatKernel.SoakFractionOf(new List<WeaponProfile> { profile });
            double absorbed = System.Math.Min(afterDodge * shieldSoak, t.Shield);   // first hit, pool full
            return GroundDamageMatrix.ArmourSoak(t.Defense, afterDodge - absorbed);
        }

        [Test]
        [Description("HEADLINE — the triangle DISSOLVES. Every Armor/Inf/Arty pairing loses its flat ×1.5 (strong) / ×0.67 (weak) triangle multiplier; the type edge now has to come from raw stats (attack, armour, HP) + weapon nature. Same-type fights are unchanged. Numbers are per-attacker, open terrain, no cover/fort.")]
        public void TriangleDissolve_BeforeAfter()
        {
            var inf = Tgt(GroundUnitType.Infantry, 10);
            var arm = Tgt(GroundUnitType.Armor, 15);
            var art = Tgt(GroundUnitType.Artillery, 5);

            // (attacker attack, type, mode) → target, with the hand-computed OLD/NEW this asserts.
            void Row(string name, double oldExp, double newExp, double oldAct, double newAct)
            {
                Log($"{name,-22} OLD {oldAct,7:0.00}   NEW {newAct,7:0.00}   ({(newAct >= oldAct ? "+" : "")}{(newAct - oldAct),6:0.0})");
                Assert.That(oldAct, Is.EqualTo(oldExp).Within(0.05), name + " OLD");
                Assert.That(newAct, Is.EqualTo(newExp).Within(0.05), name + " NEW");
            }

            Log("=== TRIANGLE DISSOLVE (per attacker, open terrain, no cover/fort) ===");
            Row("Armor->Infantry (STRONG)", 258.0, 167.0,
                OldCore(140, GroundUnitType.Armor, GroundWeaponMode.Ballistic, inf),
                NewCoreFirstHit(140, GroundUnitType.Armor, GroundWeaponMode.Ballistic, inf));
            Row("Infantry->Armor (weak)", 44.5, 77.5,
                OldCore(100, GroundUnitType.Infantry, GroundWeaponMode.Ballistic, arm),
                NewCoreFirstHit(100, GroundUnitType.Infantry, GroundWeaponMode.Ballistic, arm));
            Row("Infantry->Artillery(STR)", 142.5, 92.5,
                OldCore(100, GroundUnitType.Infantry, GroundWeaponMode.Ballistic, art),
                NewCoreFirstHit(100, GroundUnitType.Infantry, GroundWeaponMode.Ballistic, art));
            Row("Artillery->Armor (STRONG)", 217.5, 137.5,
                OldCore(160, GroundUnitType.Artillery, GroundWeaponMode.Artillery, arm),
                NewCoreFirstHit(160, GroundUnitType.Artillery, GroundWeaponMode.Artillery, arm));
            Row("Infantry->Infantry (none)", 85.0, 85.0,
                OldCore(100, GroundUnitType.Infantry, GroundWeaponMode.Ballistic, inf),
                NewCoreFirstHit(100, GroundUnitType.Infantry, GroundWeaponMode.Ballistic, inf));
        }

        [Test]
        [Description("PRESERVED — aimed-fire dodge. A Ballistic attacker vs a 0.6-evasion same-type target lands ≈(1−evasion) in BOTH models, so the dodge behaviour is unchanged by the swap (only the triangle differs). Confirms the swap doesn't disturb the dodge the ground matrix already had.")]
        public void Dodge_IsPreserved_AcrossTheSwap()
        {
            var dodger = Tgt(GroundUnitType.Infantry, 10, evasion: 0.6);
            double oldD = OldCore(100, GroundUnitType.Infantry, GroundWeaponMode.Ballistic, dodger);
            double newD = NewCoreFirstHit(100, GroundUnitType.Infantry, GroundWeaponMode.Ballistic, dodger);
            Log($"Ballistic -> dodger(0.6):  OLD {oldD:0.00}   NEW {newD:0.00}");
            Assert.That(newD, Is.EqualTo(oldD).Within(0.2), "aimed-fire dodge is preserved across the swap");
        }

        [Test]
        [Description("MODEL CHANGE — shield goes from an innate % reduction to a depleting POOL (the ship model). An Energy attacker vs a shielded target: the pool soaks MORE on the opening hits (a stronger shield early) but then COLLAPSES and later hits land in full — where the old % reduction was permanent. This is the deliberate change the developer weighs: burst-resistant-then-brittle vs steady.")]
        public void Shield_PercentVsPool_BeforeAfter()
        {
            var shielded = Tgt(GroundUnitType.Infantry, 10, shield: 250);
            double oldS = OldCore(100, GroundUnitType.Infantry, GroundWeaponMode.Energy, shielded);
            double newFirst = NewCoreFirstHit(100, GroundUnitType.Infantry, GroundWeaponMode.Energy, shielded);
            // depleted-pool hit: model the pool fully drained, so nothing is absorbed.
            double afterDodge = 100 * GroundTerrain.TerrainAttackMult(GroundUnitType.Infantry, Open) * 1.0;
            double newDepleted = GroundDamageMatrix.ArmourSoak(shielded.Defense, afterDodge);
            Log($"Energy -> shielded(250):  OLD {oldS:0.00} (permanent %)   NEW first-hit {newFirst:0.00} (pool full)   NEW shield-down {newDepleted:0.00}");
            Assert.That(newFirst, Is.LessThan(oldS), "the pool soaks MORE than the old % on the opening hit");
            Assert.That(newDepleted, Is.GreaterThan(oldS), "...but once the pool collapses, later hits land in full");
        }
    }
}
