using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Components;    // ComponentDesign
using Pulsar4X.Factions;      // FactionInfoDB
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// W-TRACK W1 — the multi-weapon ground LOADOUT (docs/combat/GROUND-CLOSING-FIGHT-W-TRACK.md §W1). Proves a ground
    /// unit no longer COLLAPSES its weapons into one Attack + one Range: it now also carries a per-weapon
    /// <see cref="GroundWeaponMount"/> loadout (one per mounted weapon component, each with its OWN range/mode), so W2
    /// can fire each weapon in its own range band as the unit closes (a lascannon reaches before a chainsword).
    ///
    /// ADDITIVE + BYTE-IDENTICAL this slice: the resolver still reads the collapsed Attack/Range, so live combat is
    /// unchanged; the loadout is populated but unread. The byte-identity INVARIANT is asserted here — Σ mount.Attack ==
    /// unit.Attack and Max(mount.RangeHexes) == unit.Range — so the collapse can always be reproduced from the loadout.
    /// Engine-only → CI (`rest` shard). Assembler idioms mirror the green PlayerGroundChainRailsTests / GroundUnitFieldingTests.
    /// </summary>
    [TestFixture]
    public class GroundWeaponLoadoutTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[w1-loadout] " + m);

        // Two base-mod ground weapons with DIFFERENT hex ranges (verified in installations.json):
        //   default-design-ground-rifle  → Range 1   ·   default-design-ground-cannon → Range 3.
        private const string Frame = "default-design-human-frame";
        private const string Rifle = "default-design-ground-rifle";   // range 1
        private const string Cannon = "default-design-ground-cannon";  // range 3

        [Test]
        [Description("A unit assembled from a rifle (range 1) + a cannon (range 3) carries a TWO-mount loadout with the "
                   + "two distinct ranges; the mounts snapshot onto the raised unit; and Σ mount.Attack == unit.Attack, "
                   + "Max(mount.RangeHexes) == unit.Range (the byte-identity invariant that reproduces the old collapse).")]
        public void AssembledUnit_CarriesAPerWeaponLoadout_ThatReproducesTheCollapse()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            ComponentDesign Part(string p) => (ComponentDesign)faction.IndustryDesigns[p];

            // ── assemble: a frame + two DIFFERENT-range weapons ──
            var design = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "w1-two-gun", "Two-Gun Squad",
                Part(Frame),
                new List<(ComponentDesign, int)> { (Part(Rifle), 1), (Part(Cannon), 1) });

            Assert.That(design.WeaponLoadout, Is.Not.Null);
            Assert.That(design.WeaponLoadout.Count, Is.EqualTo(2), "the assembled DESIGN carries one mount per weapon");
            // the collapse invariant holds on the design too
            Assert.That(design.WeaponLoadout.Sum(m => m.Attack), Is.EqualTo(design.Attack).Within(1e-6),
                "Σ mount.Attack == design.Attack (byte-identity)");
            Assert.That(design.WeaponLoadout.Max(m => m.RangeHexes), Is.EqualTo(design.Range),
                "Max(mount.RangeHexes) == design.Range (reach = the longest weapon)");

            // ── raise: the loadout snapshots onto the unit ──
            var unit = GroundForces.RaiseUnit(s.StartingBody, design, s.Faction.Id, 0);
            Assert.That(unit.WeaponLoadout.Count, Is.EqualTo(2), "the raised UNIT snapshots the two-mount loadout");
            var ranges = unit.WeaponLoadout.Select(m => m.RangeHexes).OrderBy(r => r).ToList();
            Assert.That(ranges, Is.EqualTo(new List<int> { 1, 3 }), "the two mounts keep their distinct hex ranges (1 and 3)");
            Assert.That(unit.WeaponLoadout.Sum(m => m.Attack), Is.EqualTo(unit.Attack).Within(1e-6),
                "Σ mount.Attack == unit.Attack — the collapse is reproducible (byte-identity)");
            Assert.That(unit.WeaponLoadout.Max(m => m.RangeHexes), Is.EqualTo(unit.Range),
                "Max(mount.RangeHexes) == unit.Range");
            Log($"two-gun: loadout ranges [{string.Join(",", ranges)}], Σatk {unit.WeaponLoadout.Sum(m => m.Attack):0} == unit.Attack {unit.Attack:0}, reach {unit.Range}");
        }

        [Test]
        [Description("A single-weapon assembled unit carries exactly ONE loadout mount whose Attack/Range == the unit's "
                   + "collapsed values — so the loadout is a strict superset of the old model, never a behaviour change.")]
        public void SingleWeaponUnit_CarriesExactlyOneMount()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            ComponentDesign Part(string p) => (ComponentDesign)faction.IndustryDesigns[p];

            var design = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "w1-one-gun", "One-Gun Squad",
                Part(Frame),
                new List<(ComponentDesign, int)> { (Part(Rifle), 1) });

            var unit = GroundForces.RaiseUnit(s.StartingBody, design, s.Faction.Id, 0);
            Assert.That(unit.WeaponLoadout.Count, Is.EqualTo(1), "one weapon → one mount");
            Assert.That(unit.WeaponLoadout[0].Attack, Is.EqualTo(unit.Attack).Within(1e-6));
            Assert.That(unit.WeaponLoadout[0].RangeHexes, Is.EqualTo(unit.Range));
            Log($"one-gun: 1 mount, atk {unit.Attack:0}, range {unit.Range}");
        }

        [Test]
        [Description("The GroundUnit copy-ctor DEEP-COPIES the loadout (save-safety): a clone carries an equal-but-distinct "
                   + "mount list, so a save/load round-trip can't share or drop it.")]
        public void GroundUnitClone_DeepCopiesTheLoadout()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            ComponentDesign Part(string p) => (ComponentDesign)faction.IndustryDesigns[p];

            var design = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "w1-clone", "Clone Squad",
                Part(Frame),
                new List<(ComponentDesign, int)> { (Part(Rifle), 1), (Part(Cannon), 1) });
            var unit = GroundForces.RaiseUnit(s.StartingBody, design, s.Faction.Id, 0);

            var clone = new GroundUnit(unit);
            Assert.That(clone.WeaponLoadout, Is.Not.SameAs(unit.WeaponLoadout), "the clone gets its OWN list (deep copy)");
            Assert.That(clone.WeaponLoadout.Count, Is.EqualTo(unit.WeaponLoadout.Count));
            Assert.That(clone.WeaponLoadout.Select(m => m.RangeHexes).OrderBy(r => r),
                Is.EqualTo(unit.WeaponLoadout.Select(m => m.RangeHexes).OrderBy(r => r)), "same mount ranges survive the clone");
            Assert.That(clone.WeaponLoadout[0], Is.Not.SameAs(unit.WeaponLoadout[0]), "each mount is a distinct object (deep copy)");
            Log("clone: loadout deep-copied (own list, own mounts, same ranges)");
        }
    }
}
