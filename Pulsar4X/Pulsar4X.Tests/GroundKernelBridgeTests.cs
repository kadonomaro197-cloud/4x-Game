using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// RESOLVER MERGE, slice 3b-i (docs/RESOLVER-MERGE-DESIGN.md §7) — the BRIDGE gauge.
    ///
    /// Proves that a <see cref="GroundUnit"/> presented as a <see cref="CombatKernel.Combatant"/> reproduces the ground
    /// combat semantics THROUGH THE SHARED KERNEL — i.e. the Armor▸Infantry▸Artillery triangle and the dodge/shield
    /// rules FALL OUT of a physically-sensible <see cref="WeaponProfile"/> instead of the bolted-on
    /// <c>GroundDamageMatrix</c> multipliers. This is the verification the developer asked for BEFORE the live resolver
    /// swap (3b-ii): if the emergent numbers here match the intended ground behaviour, the swap is a re-baseline, not a
    /// gamble. The mapper is ADDITIVE/unwired this slice, so live ground combat is byte-identical.
    /// </summary>
    [TestFixture]
    public class GroundKernelBridgeTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[ground-bridge] " + m);

        private static GroundUnit Unit(GroundWeaponMode mode, double attack = 100, int range = 1,
            double evasion = 0, double shield = 0, double defense = 0) =>
            new GroundUnit
            {
                FactionOwnerID = 1, Health = 500, MaxHealth = 500,
                Attack = attack, Range = range, DamageType = mode,
                Evasion = evasion, Shield = shield, Defense = defense,
            };

        [Test]
        [Description("DODGE emerges: an AIMED weapon (Ballistic) is dodged ≈(1−evasion) through the kernel, while AREA (Artillery) and CONTACT (Melee) are undodgeable (~1) — the ground 'IsAimed × (1−evasion), area/melee can't be dodged' rule, reproduced with sensible weapon specs, no triangle multiplier.")]
        public void Dodge_EmergesFromWeaponSpecs_ThroughTheKernel()
        {
            double ev = 0.6;
            var ballistic = GroundCombatant.ToWeaponProfile(Unit(GroundWeaponMode.Ballistic, evasion: ev));
            var artillery = GroundCombatant.ToWeaponProfile(Unit(GroundWeaponMode.Artillery, range: 3, evasion: ev));
            var melee     = GroundCombatant.ToWeaponProfile(Unit(GroundWeaponMode.Melee, range: 0, evasion: ev));

            double hBallistic = CombatKernel.HitFraction(ballistic, ev);
            double hArtillery = CombatKernel.HitFraction(artillery, ev);
            double hMelee     = CombatKernel.HitFraction(melee, ev);
            Log($"vs evasion {ev}: ballistic lands {hBallistic:0.000}, artillery {hArtillery:0.000}, melee {hMelee:0.000}");

            Assert.That(hBallistic, Is.EqualTo(1 - ev).Within(0.002), "aimed fire is dodged ≈(1−evasion)");
            Assert.That(hBallistic, Is.LessThan(1.0), "aimed fire IS dodgeable");
            Assert.That(hArtillery, Is.GreaterThan(0.99), "area fire is undodgeable (saturation floors it)");
            Assert.That(hMelee, Is.GreaterThan(0.99), "contact fire is undodgeable (perfect tracking)");
        }

        [Test]
        [Description("SHIELD nature matchup emerges: a Ballistic (Kinetic) weapon is fully shield-soakable (1.0), Energy bleeds (0.5), Artillery (Explosive) partly bypasses (0.75) — the ground 'energy overloads shields' rule = the kernel's SoakFractionOf, straight from the weapon's Nature.")]
        public void ShieldNatureMatchup_EmergesFromWeaponNature()
        {
            double ballisticSoak = CombatKernel.SoakFractionOf(new List<WeaponProfile> { GroundCombatant.ToWeaponProfile(Unit(GroundWeaponMode.Ballistic)) });
            double energySoak     = CombatKernel.SoakFractionOf(new List<WeaponProfile> { GroundCombatant.ToWeaponProfile(Unit(GroundWeaponMode.Energy)) });
            double artillerySoak  = CombatKernel.SoakFractionOf(new List<WeaponProfile> { GroundCombatant.ToWeaponProfile(Unit(GroundWeaponMode.Artillery, range: 3)) });
            Log($"shield soaks: ballistic {ballisticSoak}, energy {energySoak}, artillery {artillerySoak}");

            Assert.That(ballisticSoak, Is.EqualTo(CombatKernel.ShieldSoakVsKinetic), "kinetic is fully soakable");
            Assert.That(energySoak, Is.EqualTo(CombatKernel.ShieldSoakVsEnergy), "energy bleeds through a shield");
            Assert.That(artillerySoak, Is.EqualTo(CombatKernel.ShieldSoakVsExplosive), "an explosive partly bypasses");
            Assert.That(energySoak, Is.LessThan(ballisticSoak), "the Enterprise-vs-Galactica seam: energy leaks past a kinetic-tuned shield");
        }

        [Test]
        [Description("ARMOUR bounces a swarm through the kernel: many small ground hits are mostly soaked flat while one big hit punches through — the flat-per-source identity, reached from a unit's Defense.")]
        public void Armour_BouncesTheSwarm_ThroughTheKernel()
        {
            var t = Unit(GroundWeaponMode.Ballistic, defense: 20);
            double oneBigHit = CombatKernel.ArmourSoak(t.Defense, 100.0);
            double tenSmallHits = 0;
            for (int i = 0; i < 10; i++) tenSmallHits += CombatKernel.ArmourSoak(t.Defense, 10.0);
            Log($"vs Defense {t.Defense}: one 100-dmg hit lands {oneBigHit}, ten 10-dmg hits land {tenSmallHits} total");

            Assert.That(oneBigHit, Is.GreaterThan(tenSmallHits),
                "flat-per-source armour bounces the swarm but not the alpha strike");
        }

        [Test]
        [Description("PENETRATION (Weapons pilot W1b) cracks armour through the GROUND soak: a unit's Penetration flows into its synthesized WeaponProfile, and the resolver's own GroundDamageMatrix.ArmourSoak call (with that penetration) cancels the target's Defense point-for-point — so an AP unit lands far more on a heavily-plated defender than an identical non-AP unit that bounces off. Penetration 0 is byte-identical to the old flat soak, so an ordinary unit is unchanged.")]
        public void Penetration_FlowsToTheProfile_AndCracksArmourThroughTheGroundSoak()
        {
            // The dial flows unit → synthesized profile.
            var ap = Unit(GroundWeaponMode.Ballistic, attack: 100);
            ap.Penetration = 80;
            var apProfile = GroundCombatant.ToWeaponProfile(ap);
            Assert.That(apProfile.Penetration, Is.EqualTo(80), "the unit's Penetration flows into its WeaponProfile");

            var normal = Unit(GroundWeaponMode.Ballistic, attack: 100); // Penetration 0 (default)
            var normalProfile = GroundCombatant.ToWeaponProfile(normal);
            Assert.That(normalProfile.Penetration, Is.EqualTo(0), "an ordinary unit has no penetration");

            // Through the RESOLVER's OWN soak call — GroundDamageMatrix.ArmourSoak(defense, dmg, weapon penetration):
            // a heavily-plated defender (Defense 100) bounces the normal round (floored) but the AP round cracks it.
            const double defense = 100, oneHit = 100;
            double apLands = GroundDamageMatrix.ArmourSoak(defense, oneHit, apProfile.Penetration);
            double normalLands = GroundDamageMatrix.ArmourSoak(defense, oneHit, normalProfile.Penetration);
            Log($"vs Defense {defense}: AP(pen {apProfile.Penetration}) lands {apLands}, normal(pen 0) lands {normalLands}");

            Assert.That(apLands, Is.GreaterThan(normalLands), "an AP weapon cracks plate a normal round bounces off");
            // Byte-identity: penetration 0 is the old 2-arg flat soak, so a non-AP unit is unchanged by W1b.
            Assert.That(normalLands, Is.EqualTo(GroundDamageMatrix.ArmourSoak(defense, oneHit)).Within(1e-12),
                "an ordinary unit's soak is byte-identical to the pre-W1b flat soak");
        }

        [Test]
        [Description("ToCombatant maps a GroundUnit onto the neutral view the kernel reads: faction, health, evasion, flat armour, a seeded shield pool, and exactly one synthesized weapon profile.")]
        public void ToCombatant_MapsTheUnitOntoTheNeutralView()
        {
            var u = Unit(GroundWeaponMode.Energy, attack: 140, range: 2, evasion: 0.3, shield: 250, defense: 15);
            var c = GroundCombatant.ToCombatant(u);

            Assert.That(c.FactionId, Is.EqualTo(1));
            Assert.That(c.Health, Is.EqualTo(500));
            Assert.That(c.Evasion, Is.EqualTo(0.3));
            Assert.That(c.Armour, Is.EqualTo(15));
            Assert.That(c.ShieldPool, Is.EqualTo(250), "the flat Shield stat seeds a full pool");
            Assert.That(c.ShieldCapacity, Is.EqualTo(250));
            Assert.That(c.Weapons, Has.Count.EqualTo(1));
            Assert.That(c.Weapons[0].Nature, Is.EqualTo(WeaponNature.Energy), "an Energy unit fires an energy-nature weapon");
            Assert.That(c.Weapons[0].DamagePerSecond, Is.EqualTo(140));
            Assert.That(c.Weapons[0].Range_m, Is.EqualTo(2 * GroundCombatant.NominalHexPitch_m), "hex range → metres");
        }
    }
}
