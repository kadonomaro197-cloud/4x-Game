using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.DataStructures;
using Pulsar4X.Modding;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Weapon-unification P2a — the SUPPLY side of the "a Titan can, infantry can't" gate: reactors are now mountable
    /// on a GROUND CHASSIS, the same part a ship uses.
    ///
    /// The locked call (docs/WEAPON-UNIFICATION-DESIGN.md §0 + the 2026-07-06 P2 design chat): power on a ground unit is
    /// NOT a magic frame stat — it's a mounted reactor COMPONENT you research / build / lose, exactly like on a ship
    /// (the developer's full cradle-to-grave choice). P1 made the direct-fire weapons ground-mountable (the DEMAND);
    /// this makes the three fuel-burning generators — <c>reactor</c> / <c>rtg</c> / <c>steam-turbine-reactor</c> — carry
    /// <see cref="ComponentMountType.GroundUnit"/> too (the SUPPLY). P2b then teaches the ground assembler the gate:
    /// Σ weapon draw ≤ Σ reactor output, else the design is illegal.
    ///
    /// Deliberately generators-only: <c>battery-bank</c> is energy STORAGE (not a source) and <c>solarArray</c> is a
    /// different beast — both stay ship-only for this slice. Purely additive: the flag has no consumer until P2b, so
    /// nothing player-facing changes yet. NO new gameplay numbers. The gotcha-10 JSON sensor for the mount flag.
    /// </summary>
    [TestFixture]
    [Description("Weapon unification P2a — fuel-burning reactors carry the GroundUnit mount flag; storage/solar don't.")]
    internal class PowerPlantGroundMountTests
    {
        private ModDataStore _baseMod;

        [SetUp]
        public void Setup()
        {
            _baseMod = new ModDataStore();
            new ModLoader().LoadModManifest("Data/basemod/modInfo.json", _baseMod);
        }

        // The three fuel-burning generators the one designer now offers as a ground unit's power source.
        private static readonly string[] GroundMountable =
            { "reactor", "rtg", "steam-turbine-reactor" };

        // Deliberately NOT ground-mountable in this slice: energy STORAGE and the solar array (a source, but a
        // different model — no fuel/output pairing the P2b gate reads).
        private static readonly string[] NotGroundMountable =
            { "battery-bank", "solarArray" };

        [Test]
        [Description("Every fuel-burning reactor template carries ComponentMountType.GroundUnit (and keeps its ship mount).")]
        public void Reactors_AreGroundMountable_AndStillShipMountable()
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
                    failures.Add($"{id}: lost its ShipComponent mount — a reactor must still fit a ship (MountType={t.MountType})");
            }
            Assert.IsEmpty(failures, "Ground-mountable reactor check failed:\n" + string.Join("\n", failures));
        }

        [Test]
        [Description("Energy storage and the solar array are NOT ground-mountable in this generators-only slice.")]
        public void StorageAndSolar_AreNotGroundMountable()
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
            Assert.IsEmpty(failures, "Excluded power-component check failed:\n" + string.Join("\n", failures));
        }
    }
}
