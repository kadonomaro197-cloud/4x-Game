using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Factions;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// W-TRACK W1b — a UNIFIED SPACE WEAPON gives GROUND firepower once mounted (docs/combat/GROUND-CLOSING-FIGHT-W-TRACK.md
    /// §W1b). The developer's rule: "as long as a unit can provide power / ammo / hold the actual weapon, it gets to use
    /// it." The eligibility gates (carry + P2 power/ammo) already exist and are proven by GroundPowerGateTests /
    /// GroundAmmoGateTests; this proves the PAYOFF — a mounted, supported space weapon actually SHOOTS on the ground
    /// (the litmus test's #1 buildability gap), joining the W1 loadout so it bands (W2) and role-classifies (W3) like a
    /// native ground weapon.
    ///
    /// Calibration-INDEPENDENT where it matters: the assembled-firepower assertion checks Attack == firepower × the
    /// scale (not a hard-coded number), so it survives a re-tune of SpaceWeaponGround.AttackPerDps. Engine-only → CI.
    /// Uses the real faction laser/railgun/flak/reactor (the same parts a ship uses — the unification payoff).
    /// </summary>
    [TestFixture]
    public class GroundSpaceWeaponTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[w1b-spaceweapon] " + m);

        private static TestScenario _s;
        private static ComponentDesign Part(string id)
            => (ComponentDesign)_s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns[id];

        [Test]
        [Description("The pure map: a laser reads as ground ENERGY, a railgun + flak as BALLISTIC; a bigger space weapon "
                   + "(railgun, higher dps) yields more ground Attack than a laser; a native ground weapon (rifle) maps to null.")]
        public void SpaceWeaponMap_ByType_AndRelativeFirepower()
        {
            _s = TestScenario.CreateWithColony();
            var laser = Part("default-design-laser-weapon");
            var railgun = Part("default-design-railgun-weapon");
            var flak = Part("default-design-flak-weapon");
            var rifle = Part("default-design-ground-rifle");

            Assert.That(SpaceWeaponGround.IsSpaceWeapon(laser), Is.True);
            Assert.That(SpaceWeaponGround.IsSpaceWeapon(rifle), Is.False, "a native ground weapon is not a space weapon");
            Assert.That(SpaceWeaponGround.MountFor(rifle), Is.Null, "a native ground weapon maps to null (it uses its own GroundWeaponAtb path)");

            Assert.That(SpaceWeaponGround.ModeFor(laser), Is.EqualTo(GroundWeaponMode.Energy), "a laser fires as ground ENERGY");
            Assert.That(SpaceWeaponGround.ModeFor(railgun), Is.EqualTo(GroundWeaponMode.Ballistic), "a railgun is a kinetic slug → BALLISTIC");
            Assert.That(SpaceWeaponGround.ModeFor(flak), Is.EqualTo(GroundWeaponMode.Ballistic), "flak is kinetic pellets → BALLISTIC");

            var lm = SpaceWeaponGround.MountFor(laser);
            var rm = SpaceWeaponGround.MountFor(railgun);
            Assert.That(rm.Attack, Is.GreaterThan(lm.Attack), "the railgun (higher ship dps) hits harder on the ground than the laser");
            Assert.That(lm.Attack, Is.GreaterThan(0).And.LessThan(1000), "the laser lands in the ground-weapon band (not a one-shot, not nothing)");
            Assert.That(lm.RangeHexes, Is.GreaterThan(0));
            Log($"map: laser Attack {lm.Attack:0} (Energy, {lm.RangeHexes} hex) < railgun {rm.Attack:0} (Ballistic, {rm.RangeHexes} hex)");
        }

        [Test]
        [Description("Through the assembler: a laser mounted on a ground frame CONTRIBUTES ground firepower (Attack = its "
                   + "ship firepower × the scale), fires as ENERGY, and is a loadout entry (so W2/W3 apply). Powering it "
                   + "(reactors) clears the P2 gate WITHOUT changing the firepower — the payoff of 'if you can support it, you use it'.")]
        public void AssembledUnit_MountedLaser_ContributesGroundFirepower()
        {
            _s = TestScenario.CreateWithColony();
            var frame = Part("default-design-walker-frame");
            var laser = Part("default-design-laser-weapon");
            var reactor = Part("default-design-reactor-2t");

            // frame + laser, no reactor: the laser ALREADY gives ground firepower (W1b) — it's just not yet powered (P2b).
            var bare = GroundUnitAssembly.Compute(frame, new List<(ComponentDesign, int)> { (laser, 1) });
            double expected = SpaceWeaponGround.Firepower_Jps(laser) * SpaceWeaponGround.AttackPerDps;
            Assert.That(bare.Attack, Is.EqualTo(expected).Within(1e-6), "the laser's ground Attack = its ship firepower × the scale (calibration-independent)");
            Assert.That(bare.Attack, Is.GreaterThan(0), "a mounted laser gives the ground unit REAL firepower (the #1 buildability gap, closed)");
            Assert.That(bare.DamageType, Is.EqualTo(GroundWeaponMode.Energy), "it fires as ground ENERGY");
            Assert.That(bare.WeaponLoadout.Count, Is.EqualTo(1), "the laser is a loadout entry — so W2 range-banding + W3 roles apply to it");
            Assert.That(bare.WeaponLoadout[0].Mode, Is.EqualTo(GroundWeaponMode.Energy));
            Assert.That(bare.Problems.Any(p => p.Contains("under-powered")), Is.True, "with no reactor it's under-powered — the eligibility gate still bites");

            // power it (derive enough reactors from the gauges — calibration-independent): the gate clears, firepower unchanged.
            double perReactor = GroundUnitAssembly.Compute(frame, new List<(ComponentDesign, int)> { (reactor, 1) }).ReactorSupply_W;
            int n = (int)Math.Ceiling(bare.EnergyDemand_W / perReactor);
            var powered = GroundUnitAssembly.Compute(frame, new List<(ComponentDesign, int)> { (laser, 1), (reactor, n) });
            Assert.That(powered.Problems.Any(p => p.Contains("under-powered")), Is.False, "reactors clear the power gate");
            Assert.That(powered.Attack, Is.EqualTo(bare.Attack).Within(1e-6), "powering the laser doesn't change its firepower");
            Log($"laser on a walker frame: ground Attack {powered.Attack:0} (Energy, reach {powered.Range}), {n}× reactor clears the power gate");
        }

        [Test]
        [Description("Byte-identity: a native ground unit (frame + rifle) is UNCHANGED — the space-weapon branch never "
                   + "fires for a GroundWeaponAtb, so every existing ground design resolves exactly as before.")]
        public void NativeGroundWeapon_IsByteIdentical()
        {
            _s = TestScenario.CreateWithColony();
            var r = GroundUnitAssembly.Compute(Part("default-design-human-frame"),
                new List<(ComponentDesign, int)> { (Part("default-design-ground-rifle"), 1) });
            Assert.That(r.Attack, Is.EqualTo(40).Within(1e-6), "the rifle's ground Attack is unchanged");
            Assert.That(r.DamageType, Is.EqualTo(GroundWeaponMode.Ballistic), "still a ballistic rifle");
            Assert.That(r.WeaponLoadout.Count, Is.EqualTo(1));
            Assert.That(r.WeaponLoadout[0].Mode, Is.EqualTo(GroundWeaponMode.Ballistic));
            Log($"native rifle byte-identical: Attack {r.Attack:0}, {r.DamageType}");
        }
    }
}
