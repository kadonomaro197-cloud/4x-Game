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
        [Description("A blinding hazard reads Blind with zero detection and accuracy — and Blind FORCES zero even when the raw sensor multiplier is non-zero (so the guard is actually exercised, not masked by an already-0 multiplier).")]
        public void BlindingHazard_ReadsBlindAndZeroDetection()
        {
            var mods = new HazardModifiers
            {
                InAnyHazard = true,
                SensorRangeMultiplier = 0.5,   // deliberately NON-zero: proves Blind overrides the multiplier to 0
                MoveSpeedMultiplier = 1.0,
                WarpSpeedMultiplier = 1.0,
                DamagePerSecond = 500.0,
                BlindsSensors = true,
            };

            var c = CombatConditions.FromHazard(mods);
            Assert.IsTrue(c.InHazard);
            Assert.IsTrue(c.Blind);
            Assert.AreEqual(0.0, c.Detection, 1e-9, "Blind forces detection to 0 over the 0.5 multiplier.");
            Assert.AreEqual(0.0, c.Accuracy, 1e-9, "Blind forces accuracy to 0 over the 0.5 multiplier.");
            Assert.AreEqual(500.0, c.AmbientDoT_Jps, 1e-9);
        }

        [Test]
        [Description("The STRUCT-DEFAULT TRAP (a slice-2 guard): default(CombatConditions) is ALL ZEROS — blind, frozen, gunless — the INVERSE of Clean (all 1.0). A future FleetCombatStateDB field must be SEEDED to Clean at every ctor/deserialize point; an un-seeded field would read blind straight into the shared kernel. This gauge documents the trap so it can't be silently reintroduced.")]
        public void StructDefault_IsAllZeros_NotClean_TheSlice2SeedingTrap()
        {
            var def = default(CombatConditions);
            Assert.AreEqual(0.0, def.Detection, 1e-9, "default is all-zeros (blind), NOT Clean.");
            Assert.AreEqual(0.0, def.Closing, 1e-9);
            Assert.AreEqual(0.0, def.Firepower, 1e-9);
            Assert.AreNotEqual(CombatConditions.Clean.Detection, def.Detection,
                "Clean.Detection (1.0) MUST differ from default.Detection (0.0) — seed CombatConditions to Clean, never leave it at default.");
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

        [Test]
        [Description("E-env slice 2 — the accuracy coefficient on the shared CombatKernel.HitFraction: accuracy 1.0 (clean space) is byte-identical to omitting it; a lower value scales the landed fraction down (poor visibility = worse hits); 0 lands nothing; >1 is clamped to 1.")]
        public void HitFraction_AccuracyCoefficient_1IsByteIdentical_LowerReduces()
        {
            // A finite-velocity ballistic weapon so evasion bites and the hit sits between the floor and 1.
            var w = new WeaponProfile(1000, 200000, 0.3, 5);
            double baseHit = CombatKernel.HitFraction(w, 0.5, 0);
            Assert.Greater(baseHit, 0.1, "sanity: the test weapon lands above the saturation floor");
            Assert.Less(baseHit, 1.0, "sanity: and below 1");

            Assert.AreEqual(baseHit, CombatKernel.HitFraction(w, 0.5, 0, 1.0), 1e-12, "accuracy 1.0 must equal clean space (byte-identical hook).");
            Assert.AreEqual(baseHit * 0.5, CombatKernel.HitFraction(w, 0.5, 0, 0.5), 1e-9, "accuracy 0.5 (nebula-grade cut) halves the landed fraction.");
            Assert.AreEqual(0.0, CombatKernel.HitFraction(w, 0.5, 0, 0.0), 1e-9, "accuracy 0 (blind) lands nothing.");
            Assert.LessOrEqual(CombatKernel.HitFraction(w, 0.5, 0, 2.0), 1.0, "accuracy > 1 (deep-space clarity) is clamped — a landed fraction never exceeds 1.");
        }

        [Test]
        [Description("E-env slice 2 — the struct-default trap guard: FleetCombatStateDB.Conditions defaults to Clean (all 1.0), NOT default(struct) all-zeros (blind/frozen), and the copy ctor carries a set value.")]
        public void FleetCombatState_Conditions_DefaultsToClean_AndCopies()
        {
            var state = new FleetCombatStateDB();
            Assert.AreEqual(1.0, state.Conditions.Detection, 1e-9, "a fresh combat state reads CLEAN conditions, not the blind all-zeros default(struct).");
            Assert.AreEqual(1.0, state.Conditions.Accuracy, 1e-9);
            Assert.IsFalse(state.Conditions.InHazard);

            state.Conditions = CombatConditions.FromHazard(new HazardModifiers
            {
                InAnyHazard = true, SensorRangeMultiplier = 0.4, MoveSpeedMultiplier = 1.0,
                WarpSpeedMultiplier = 1.0, DamagePerSecond = 0.0, BlindsSensors = false,
            });
            var copy = new FleetCombatStateDB(state);
            Assert.AreEqual(0.4, copy.Conditions.Accuracy, 1e-9, "the copy ctor carries Conditions.");
        }
    }
}
