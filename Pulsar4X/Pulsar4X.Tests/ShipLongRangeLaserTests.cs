using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;
using Pulsar4X.Weapons;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Weapons ⚙1 ▸ ENERGY — the RANGE dial, cradle-to-grave (S8). The laser designer's Range knob is a real dial:
    /// its ceiling RISES with the "Beam Focusing Range" tech (<c>tech-beam-range</c> = 10000 × 2^level, so 10,000 m
    /// at starting tech vs the 5,000 m default). This proves turning that knob on a NEW design actually reaches the
    /// gun: the base-mod <c>default-design-long-range-laser</c> overrides Range to the 10,000 m starting-tech ceiling
    /// (and Focal Length to 5,000 m for a wide full-damage band), mounts on the new Longbow standoff cruiser, and the
    /// ship's beam reach — read the SAME way the firing path enforces it (<see cref="WeaponUtils.GetMaxBeamRange_m"/>
    /// → <c>GenericBeamWeaponAtb.MaxRange</c>) — comes out at double the reach of a default-laser Aegis.
    ///
    /// Cradle-to-grave: JSON design (Range override) → NCalc <c>genericBeamWpnAtbArgs</c> → GenericBeamWeaponAtb.MaxRange
    /// → the range the closing-combat trigger and the firing processor both read. Additive / byte-identical (new design
    /// + new ship; no existing fixture perturbed — the default laser and every ship using it are untouched). Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipLongRangeLaserTests
    {
        private const string Longbow = "default-ship-design-test-longbow";
        private const string Aegis = "default-ship-design-test-warship";
        private static void Log(string m) => TestContext.Progress.WriteLine("[long-range-laser] " + m);

        private static Entity Build(TestScenario s, string designId, string name)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            return ShipFactory.CreateShip(designs[designId], s.Faction, s.StartingBody, name);
        }

        [Test]
        [Description("Cradle-to-grave: the Longbow's long-range lasers reach ~10,000 m (the starting-tech ceiling), read through the real GetMaxBeamRange_m/GenericBeamWeaponAtb.MaxRange path — the JSON Range override propagated to the gun.")]
        public void TheLongbow_ReachesTheTechCeiling()
        {
            var s = TestScenario.CreateWithColony();
            var ship = Build(s, Longbow, "Longbow");
            double reach = WeaponUtils.GetMaxBeamRange_m(ship);
            Log($"Longbow beam reach = {reach:0} m (long-range laser Range dial = 10,000)");
            Assert.That(reach, Is.EqualTo(10000).Within(1e-6),
                "the Range dial override reaches the gun (JSON design → GenericBeamWeaponAtb.MaxRange)");
        }

        [Test]
        [Description("The dial makes a DIFFERENCE: the Longbow's long-range laser out-reaches a default-laser Aegis by 2x (10,000 m vs 5,000 m) — the Range knob is real, and the default ship is untouched (byte-identical).")]
        public void TheRangeDial_DoublesReach_VsTheDefaultLaser()
        {
            var s = TestScenario.CreateWithColony();
            double longbow = WeaponUtils.GetMaxBeamRange_m(Build(s, Longbow, "Longbow"));
            double aegis = WeaponUtils.GetMaxBeamRange_m(Build(s, Aegis, "Aegis"));
            Log($"Longbow reach {longbow:0} m vs default-laser Aegis {aegis:0} m");
            Assert.That(aegis, Is.EqualTo(5000).Within(1e-6),
                "the default laser is untouched at its 5,000 m default (byte-identical)");
            Assert.That(longbow, Is.EqualTo(aegis * 2).Within(1e-6),
                "the long-range design out-reaches the default laser by the Range dial's doubling");
        }
    }
}
