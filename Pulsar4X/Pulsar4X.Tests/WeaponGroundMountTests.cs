using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Blueprints;
using Pulsar4X.DataStructures;
using Pulsar4X.Modding;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Weapon-unification P1 — the ONE weapon designer's direct-fire weapons are now mountable on a GROUND CHASSIS.
    ///
    /// The locked call (docs/WEAPON-UNIFICATION-DESIGN.md §0): one weapon designer, full stop — "if a chassis can
    /// SUPPLY a weapon's requirements, it mounts it," ground or space. P1 is the first, purely-additive rung: the
    /// base-mod direct-fire weapon templates gain <see cref="ComponentMountType.GroundUnit"/> alongside their existing
    /// Ship/PDC/installation mounts, so the same design the fleet uses is offered for a ground unit. NO resolver change
    /// yet (that's P2 the supply gate + P3 ground-reads-the-profile) — this gauge just locks that the mount flag is set
    /// where it should be and, crucially, NOT set where it shouldn't (the missile launcher is an ordnance subsystem and
    /// the deflector is defensive, not a weapon — both are deliberately out of this weapons-only slice).
    ///
    /// The gotcha-10 sensor for the JSON: it reads the real shipped weapons.json through the mod loader, so a later edit
    /// that drops (or over-adds) the flag fails here instead of silently shipping.
    /// </summary>
    [TestFixture]
    [Description("Weapon unification P1 — direct-fire weapons carry the GroundUnit mount flag; ordnance/defensive don't.")]
    internal class WeaponGroundMountTests
    {
        private ModDataStore _baseMod;

        [SetUp]
        public void Setup()
        {
            _baseMod = new ModDataStore();
            new ModLoader().LoadModManifest("Data/basemod/modInfo.json", _baseMod);
        }

        // The five direct-fire weapons the one designer now offers on a ground chassis (beam/kinetic/saturation/exotic).
        private static readonly string[] GroundMountable =
            { "laser-weapon", "railgun-weapon", "flak-weapon", "disruptor-weapon", "plasma-repeater" };

        // Deliberately NOT ground-mountable in this slice: an ordnance launcher (ammo subsystem) and a defensive shield.
        private static readonly string[] NotGroundMountable =
            { "missile-launcher", "deflector-array" };

        [Test]
        [Description("Every direct-fire weapon template carries ComponentMountType.GroundUnit (and keeps its Ship mount).")]
        public void DirectFireWeapons_AreGroundMountable_AndStillShipMountable()
        {
            var failures = new List<string>();
            foreach (var id in GroundMountable)
            {
                if (!_baseMod.ComponentTemplates.TryGetValue(id, out var t))
                {
                    failures.Add($"{id}: template not found in base mod");
                    continue;
                }
                if (!t.MountType.HasFlag(ComponentMountType.GroundUnit))
                    failures.Add($"{id}: missing GroundUnit mount flag (MountType={t.MountType})");
                if (!t.MountType.HasFlag(ComponentMountType.ShipComponent))
                    failures.Add($"{id}: lost its ShipComponent mount — a weapon must still fit a ship (MountType={t.MountType})");
            }
            Assert.IsEmpty(failures, "Ground-mountable weapon check failed:\n" + string.Join("\n", failures));
        }

        [Test]
        [Description("The ordnance launcher and the defensive deflector are NOT ground-mountable in this weapons-only slice.")]
        public void OrdnanceAndDefensive_AreNotGroundMountable()
        {
            var failures = new List<string>();
            foreach (var id in NotGroundMountable)
            {
                if (!_baseMod.ComponentTemplates.TryGetValue(id, out var t))
                {
                    failures.Add($"{id}: template not found in base mod");
                    continue;
                }
                if (t.MountType.HasFlag(ComponentMountType.GroundUnit))
                    failures.Add($"{id}: has the GroundUnit flag but shouldn't in this slice (MountType={t.MountType})");
            }
            Assert.IsEmpty(failures, "Excluded-weapon check failed:\n" + string.Join("\n", failures));
        }
    }
}
