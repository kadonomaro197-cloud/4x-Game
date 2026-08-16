using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Hazards;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using System.Linq;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauges <see cref="CombatConditions"/> — the "environment a battle is fought in" bundle (OPERATION
    /// BLUEPRINT-TO-STEEL Phase E-env, slice 1). The design is docs/combat/ENVIRONMENT-CONDITIONS-DESIGN.md.
    /// SLICE 1 builds the value object + the translation from the live space-hazard query; nothing in the resolver
    /// reads it yet, so it is byte-identical. These tests pin the translation (so a later kernel wire can't
    /// silently change what "in a nebula" means) — the pure math needs no colony harness, and one integration test
    /// proves the reader end-to-end on a REAL gas cloud through the proven <see cref="SpaceHazardTools.CombinedAt"/>.
    /// </summary>
    [TestFixture]
    public class CombatConditionsTests
    {
        private static Entity FirstStar(TestScenario s)
            => s.StartingSystem.GetAllDataBlobsOfType<StarInfoDB>().First().OwningEntity;

        [Test]
        [Description("Clean space (no hazard) reads the featureless-void baseline: every multiplier 1.0, no cover, no DoT.")]
        public void CleanSpace_IsIdentity()
        {
            var c = CombatConditions.FromHazard(HazardModifiers.None);
            Assert.IsFalse(c.InHazard);
            Assert.IsFalse(c.Blind);
            Assert.AreEqual(1.0, c.Detection, 1e-9);
            Assert.AreEqual(1.0, c.Accuracy, 1e-9);
            Assert.AreEqual(1.0, c.Closing, 1e-9);
            Assert.AreEqual(1.0, c.Firepower, 1e-9);
            Assert.AreEqual(1.0, c.ShieldRegen, 1e-9);
            Assert.AreEqual(0.0, c.Cover, 1e-9);
            Assert.AreEqual(0.0, c.AmbientDoT_Jps, 1e-9);

            // The static Clean is the same baseline.
            var clean = CombatConditions.Clean;
            Assert.AreEqual(clean.Detection, c.Detection, 1e-9);
            Assert.AreEqual(clean.AmbientDoT_Jps, c.AmbientDoT_Jps, 1e-9);
        }

        [Test]
        [Description("A gas-cloud-shaped hazard (jam + drag + corrosion) maps to cut detection/accuracy/closing and an ambient DoT.")]
        public void GasCloudModifiers_MapToCutsAndDoT()
        {
            var mods = new HazardModifiers
            {
                InAnyHazard = true,
                SensorRangeMultiplier = 0.35,
                MoveSpeedMultiplier = 0.5,
                WarpSpeedMultiplier = 0.25,
                DamagePerSecond = 50.0,
                BlindsSensors = false,
            };

            var c = CombatConditions.FromHazard(mods);
            Assert.IsTrue(c.InHazard);
            Assert.IsFalse(c.Blind);
            Assert.AreEqual(0.35, c.Detection, 1e-9, "A sensor jam cuts detection.");
            Assert.AreEqual(0.35, c.Accuracy, 1e-9, "The same jam cuts accuracy (you can't hit what you can't see).");
            Assert.AreEqual(0.5, c.Closing, 1e-9, "Movement drag slows closing.");
            Assert.AreEqual(50.0, c.AmbientDoT_Jps, 1e-9, "Hazard damage becomes the ambient DoT.");
            // Slice-1 identity fields (an authored-environment read fills these later).
            Assert.AreEqual(1.0, c.Firepower, 1e-9);
            Assert.AreEqual(1.0, c.ShieldRegen, 1e-9);
            Assert.AreEqual(0.0, c.Cover, 1e-9);
        }

        [Test]
        [Description("A blinding hazard (a solar flare) reads Blind with zero detection and accuracy.")]
        public void BlindingHazard_ReadsBlindAndZeroDetection()
        {
            var mods = new HazardModifiers
            {
                InAnyHazard = true,
                SensorRangeMultiplier = 0.0,
                MoveSpeedMultiplier = 1.0,
                WarpSpeedMultiplier = 1.0,
                DamagePerSecond = 500.0,
                BlindsSensors = true,
            };

            var c = CombatConditions.FromHazard(mods);
            Assert.IsTrue(c.InHazard);
            Assert.IsTrue(c.Blind);
            Assert.AreEqual(0.0, c.Detection, 1e-9);
            Assert.AreEqual(0.0, c.Accuracy, 1e-9);
            Assert.AreEqual(500.0, c.AmbientDoT_Jps, 1e-9);
        }

        [Test]
        [Description("ReadAt is null-safe: a null system reads clean space (the hazard query returns None).")]
        public void ReadAt_NullSystem_IsClean()
        {
            var c = CombatConditions.ReadAt(null, Vector3.Zero);
            Assert.IsFalse(c.InHazard);
            Assert.AreEqual(1.0, c.Detection, 1e-9);
            Assert.AreEqual(0.0, c.AmbientDoT_Jps, 1e-9);
        }

        [Test]
        [Description("End-to-end: a REAL gas cloud in a system reads as in-hazard combat conditions at its centre and clean well outside.")]
        public void ReadAt_RealGasCloud_ReadsInHazardInsideAndCleanOutside()
        {
            var s = TestScenario.CreateWithColony();
            var star = FirstStar(s);
            var starPos = star.GetDataBlob<PositionDB>().AbsolutePosition;

            var offset = new Vector3(Distance.AuToMt(5), 0, 0);
            double radius = Distance.AuToMt(1.0);
            SpaceHazardFactory.CreateGasCloud(s.StartingSystem, star, offset, radius);
            var center = starPos + offset;

            var inside = CombatConditions.ReadAt(s.StartingSystem, center);
            Assert.IsTrue(inside.InHazard, "At the cloud centre the fight is inside a hazard.");
            Assert.Less(inside.Detection, 1.0, "Sensors/detection cut inside the cloud.");
            Assert.Less(inside.Accuracy, 1.0, "Accuracy cut inside the cloud.");
            Assert.Less(inside.Closing, 1.0, "Closing dragged inside the cloud.");
            Assert.Greater(inside.AmbientDoT_Jps, 0.0, "The cloud deals ambient damage.");

            var outside = CombatConditions.ReadAt(s.StartingSystem, center + new Vector3(Distance.AuToMt(5), 0, 0));
            Assert.IsFalse(outside.InHazard, "Well outside the cloud the fight is in clean space.");
            Assert.AreEqual(1.0, outside.Detection, 1e-9);
            Assert.AreEqual(1.0, outside.Closing, 1e-9);
            Assert.AreEqual(0.0, outside.AmbientDoT_Jps, 1e-9);
        }
    }
}
