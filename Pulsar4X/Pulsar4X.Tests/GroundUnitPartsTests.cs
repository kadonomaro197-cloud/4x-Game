using NUnit.Framework;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// GROUND-UNIT DESIGNER track, slice G-D1 — the parts bin. A ground unit is assembled from component parts the same
    /// way a ship is (frame + weapons + armour + augments), and these are the four GENERAL part attributes every unit
    /// across every franchise is built from (the developer's generalise-by-function call: no per-flavour types, just
    /// knobs). This gauge pins the double-argument constructors (the NCalc/JSON binding path, gotcha L7) and Clone —
    /// so a base-mod part template (G-D2) that mis-orders or mis-counts ctor args fails HERE, not in a player's New
    /// Game. The assembly + stat-emergence + capacity gate ride on top in G-D3.
    /// Design: docs/GROUND-COMBAT-MAP-DESIGN.md → unit designer.
    /// </summary>
    [TestFixture]
    public class GroundUnitPartsTests
    {
        [Test]
        [Description("G-D1: the Chassis (frame) part binds strength/HP/size/locomotion/carry-class from its NCalc-style double ctor and clones exactly.")]
        public void Chassis_BindsFromDoubleCtor_AndClones()
        {
            // (baseStrength, baseHP, size, locomotion, carryClass) — Walker=2, Vehicle=1
            var c = new GroundChassisAtb(500, 2000, 40, (double)(int)GroundLocomotion.Walker, (double)(int)GroundCarryClass.Vehicle);
            Assert.That(c.BaseStrength, Is.EqualTo(500));
            Assert.That(c.BaseHP, Is.EqualTo(2000));
            Assert.That(c.Size, Is.EqualTo(40));
            Assert.That(c.Locomotion, Is.EqualTo(GroundLocomotion.Walker), "locomotion enum bound from the double");
            Assert.That(c.CarryClass, Is.EqualTo(GroundCarryClass.Vehicle), "carry-class enum bound from the double");

            var clone = (GroundChassisAtb)c.Clone();
            Assert.That(clone.BaseStrength, Is.EqualTo(c.BaseStrength));
            Assert.That(clone.Locomotion, Is.EqualTo(c.Locomotion));
            Assert.That(clone.CarryClass, Is.EqualTo(c.CarryClass));
        }

        [Test]
        [Description("G-D1: the Weapon part binds mass/attack/range/mode and clones; range truncates to a hex int.")]
        public void Weapon_BindsFromDoubleCtor_AndClones()
        {
            // (mass, attack, range, mode) — Energy=2
            var w = new GroundWeaponAtb(30, 120, 3, (double)(int)GroundWeaponMode.Energy);
            Assert.That(w.Mass, Is.EqualTo(30));
            Assert.That(w.Attack, Is.EqualTo(120));
            Assert.That(w.Range, Is.EqualTo(3));
            Assert.That(w.Mode, Is.EqualTo(GroundWeaponMode.Energy), "weapon mode enum bound from the double");

            var clone = (GroundWeaponAtb)w.Clone();
            Assert.That(clone.Attack, Is.EqualTo(w.Attack));
            Assert.That(clone.Range, Is.EqualTo(w.Range));
            Assert.That(clone.Mode, Is.EqualTo(w.Mode));
        }

        [Test]
        [Description("G-D1: the Armour part binds mass/HP/defence and clones (the survive-by-soaking option).")]
        public void Armour_BindsFromDoubleCtor_AndClones()
        {
            var a = new GroundArmorAtb(20, 300, 8);
            Assert.That(a.Mass, Is.EqualTo(20));
            Assert.That(a.HP, Is.EqualTo(300));
            Assert.That(a.Defense, Is.EqualTo(8));

            var clone = (GroundArmorAtb)a.Clone();
            Assert.That(clone.HP, Is.EqualTo(a.HP));
            Assert.That(clone.Defense, Is.EqualTo(a.Defense));
        }

        [Test]
        [Description("G-D1: the Augment part binds mass + strength/evasion/toughness/shield and clones — the part that turns a bare human into a Marine (strength) or lets a Jedi survive with no armour (evasion + shield).")]
        public void Augment_BindsFromDoubleCtor_AndClones()
        {
            // (mass, strengthBonus, evasionBonus, toughnessBonus, shield)
            var g = new GroundAugmentAtb(15, 400, 0.5, 0.25, 100);
            Assert.That(g.Mass, Is.EqualTo(15));
            Assert.That(g.StrengthBonus, Is.EqualTo(400), "power-armour strength — what lets a frame carry heavier gear");
            Assert.That(g.EvasionBonus, Is.EqualTo(0.5));
            Assert.That(g.ToughnessBonus, Is.EqualTo(0.25));
            Assert.That(g.Shield, Is.EqualTo(100));

            var clone = (GroundAugmentAtb)g.Clone();
            Assert.That(clone.StrengthBonus, Is.EqualTo(g.StrengthBonus));
            Assert.That(clone.EvasionBonus, Is.EqualTo(g.EvasionBonus));
            Assert.That(clone.Shield, Is.EqualTo(g.Shield));
        }
    }
}
