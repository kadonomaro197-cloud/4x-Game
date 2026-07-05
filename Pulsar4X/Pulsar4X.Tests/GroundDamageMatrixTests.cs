using NUnit.Framework;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// SYSTEM ① slice A — the damage × defence MATCHUP. Proves the rock-paper-scissors that makes a build's survival
    /// depend on the ATTACKER's flavour: dodge beats aimed fire but not area/melee; a shield soaks physical but energy
    /// bleeds through. This is what turns the Clone (shield/soak) and the Zergling (dodge) — identical on paper as
    /// "survivor" archetypes — into opposite units with opposite counters. Pure math → runs in CI.
    /// Design: docs/GROUND-UNIT-DESIGNER-DESIGN.md §6b system ①.
    /// </summary>
    [TestFixture]
    public class GroundDamageMatrixTests
    {
        private static GroundUnit Unit(double evasion, double shield)
            => new GroundUnit { Evasion = evasion, Shield = shield };

        [Test]
        [Description("Dodge beats AIMED fire only: an evasive unit takes reduced damage from Ballistic/Energy shots, but full damage from Artillery (area) and Melee (contact) — saturation and getting-in-its-face beat dodge.")]
        public void Evasion_ReducesAimedFire_ButNotAreaOrMelee()
        {
            var zergling = Unit(evasion: 0.4, shield: 0);
            Assert.That(GroundDamageMatrix.Matchup(GroundWeaponMode.Ballistic, zergling), Is.EqualTo(0.6).Within(1e-9), "dodges 40% of rifle fire");
            Assert.That(GroundDamageMatrix.Matchup(GroundWeaponMode.Energy, zergling), Is.EqualTo(0.6).Within(1e-9), "and blaster fire");
            Assert.That(GroundDamageMatrix.Matchup(GroundWeaponMode.Artillery, zergling), Is.EqualTo(1.0).Within(1e-9), "but can't dodge a shell (area)");
            Assert.That(GroundDamageMatrix.Matchup(GroundWeaponMode.Melee, zergling), Is.EqualTo(1.0).Within(1e-9), "or a blade in melee (contact)");
        }

        [Test]
        [Description("A shield is an innate % reduction, WEAKER vs energy: a shielded unit soaks physical (ballistic/melee) best, energy bleeds through, artillery is in between.")]
        public void Shield_SoaksPhysical_ButEnergyBleedsThrough()
        {
            var clone = Unit(evasion: 0, shield: 150);   // s = 150/(150+150) = 0.5
            double vsBallistic = GroundDamageMatrix.Matchup(GroundWeaponMode.Ballistic, clone);
            double vsEnergy = GroundDamageMatrix.Matchup(GroundWeaponMode.Energy, clone);
            double vsArtillery = GroundDamageMatrix.Matchup(GroundWeaponMode.Artillery, clone);
            Assert.That(vsBallistic, Is.EqualTo(0.5).Within(1e-9), "shield halves kinetic");
            Assert.That(vsEnergy, Is.EqualTo(0.75).Within(1e-9), "energy overloads it — only 25% soaked");
            Assert.That(vsArtillery, Is.EqualTo(0.625).Within(1e-9), "artillery partly bypasses");
            Assert.That(vsEnergy, Is.GreaterThan(vsBallistic), "so the counter to a shielded unit is an energy weapon");
        }

        [Test]
        [Description("Nothing is ever untouchable: a maxed dodger still takes the floor fraction (evasion is capped).")]
        public void Evasion_IsCapped_SoNothingIsInvulnerable()
        {
            var untouchable = Unit(evasion: 0.99, shield: 0);
            Assert.That(GroundDamageMatrix.Matchup(GroundWeaponMode.Ballistic, untouchable),
                Is.EqualTo(1.0 - GroundDamageMatrix.EvasionCap).Within(1e-9), "clamped to the evasion cap");
        }

        [Test]
        [Description("The Clone-vs-Zergling counter asymmetry, in one place: artillery/area is the answer to the Zergling's dodge; an energy weapon is the answer to the Clone's shield.")]
        public void CloneAndZergling_HaveOppositeCounters()
        {
            var zergling = Unit(evasion: 0.4, shield: 0);
            var clone = Unit(evasion: 0, shield: 150);
            // artillery ignores the Zergling's dodge (full damage) but the Clone has no shield-vs-that advantage worth more
            Assert.That(GroundDamageMatrix.Matchup(GroundWeaponMode.Artillery, zergling),
                Is.GreaterThan(GroundDamageMatrix.Matchup(GroundWeaponMode.Ballistic, zergling)), "area beats the swarm's dodge");
            // energy beats the Clone's shield (more damage through) vs a ballistic weapon
            Assert.That(GroundDamageMatrix.Matchup(GroundWeaponMode.Energy, clone),
                Is.GreaterThan(GroundDamageMatrix.Matchup(GroundWeaponMode.Ballistic, clone)), "energy beats the line's shield");
        }

        // ── SYSTEM ① — FLAT ARMOUR (the third, distinct defence flavour) ─────────────────────────────────────────────

        [Test]
        [Description("Flat ARMOUR is what makes armour armour: because the soak is flat PER SOURCE, the SAME total damage delivered as many small volleys is mostly bounced, while one big alpha strike punches through the same plating. This is the property % shield and dodge CANNOT have (they scale with the hit, so concentration doesn't matter to them) — it's the counter to chip-damage-by-numbers.")]
        public void Armour_IsFlatPerSource_SwarmBouncesButAlphaPunchesThrough()
        {
            const double defense = 20;
            // one big alpha of 100 damage — loses only ONE flat soak
            double big = GroundDamageMatrix.ArmourSoak(defense, 100.0);
            // the SAME 100 total delivered as ten little 10-damage volleys — each meets the flat soak
            double swarmTotal = 0;
            for (int i = 0; i < 10; i++) swarmTotal += GroundDamageMatrix.ArmourSoak(defense, 10.0);

            Assert.That(big, Is.EqualTo(100.0 - defense * GroundDamageMatrix.ArmourSoakPerPoint).Within(1e-9),
                "the big hit loses just one flat soak and mostly gets through");
            Assert.That(swarmTotal, Is.EqualTo(10 * (10.0 * GroundDamageMatrix.ArmourMinPassFraction)).Within(1e-9),
                "each little volley is soaked past the floor, so only the min-pass fraction of each lands");
            Assert.That(big, Is.GreaterThan(swarmTotal),
                "same total damage: the alpha punches through where the swarm bounces off");
        }

        [Test]
        [Description("Armour is never total immunity: no matter how much Defense a hit meets, it always lands at least the min-pass fraction (the counterpart to the evasion/shield caps).")]
        public void Armour_IsNeverTotalImmunity_FlooredAtMinPass()
        {
            double landed = GroundDamageMatrix.ArmourSoak(defense: 1000, sourceDamage: 50.0);
            Assert.That(landed, Is.EqualTo(50.0 * GroundDamageMatrix.ArmourMinPassFraction).Within(1e-9),
                "even absurd armour lets the min-pass fraction through");
            Assert.That(landed, Is.GreaterThan(0), "so nothing is ever fully immune to a hit");
        }

        [Test]
        [Description("Defense is opt-in: a unit with no armour (Defense 0) takes its full post-matchup damage — the soak only bites when there's plating.")]
        public void Armour_ZeroDefense_LandsFull()
        {
            Assert.That(GroundDamageMatrix.ArmourSoak(0, 42.0), Is.EqualTo(42.0).Within(1e-9));
            Assert.That(GroundDamageMatrix.ArmourSoak(10, 0.0), Is.EqualTo(0.0), "no incoming damage → nothing to soak");
        }
    }
}
