using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Propulsion ⚙2 (Traction) — a unit's DESIGNED locomotion gives it a COMBAT edge on its preferred ground, not just
    /// a movement one. Terrain-in-combat was already wired by unit TYPE (`GroundTerrain.TerrainAttackMult`); this
    /// CONNECTS the designed drive: `GroundTerrain.LocomotionTerrainMult(roughHandling, terrain)` reads the unit's
    /// `GroundLocomotionAtb.RoughHandling` (via `GroundMobility.RoughHandlingForUnit`) so an all-terrain drive fights
    /// better on Rough/Cover ground and a road-bound one is bogged down. The resolver (`ResolveRegionCombat`) now
    /// multiplies each attacker's output by it.
    ///
    /// Byte-identical: it's centred on the neutral handling 0.5 → ×1.0, and a unit with NO designed locomotion reads
    /// 0.5, so every current unit (all the monolithic garrison/base-mod units) is untouched. Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class GroundLocomotionCombatTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[loco-combat] " + m);

        [Test]
        [Description("Pure term: neutral (×1.0) at rough-handling 0.5 (byte-identical) and on OPEN ground; an all-terrain drive (0.9) gets an edge on Rough/Cover, a road-bound one (0.1) a penalty.")]
        public void LocomotionTerrainMult_NeutralAtHalf_EdgeOnConstrainedGround()
        {
            // 0.5 handling is the neutral point — every current unit reads it → byte-identical.
            Assert.That(GroundTerrain.LocomotionTerrainMult(0.5, GroundTerrainClass.Rough), Is.EqualTo(1.0).Within(1e-9),
                "neutral handling on rough ground is ×1.0 — no unit without a designed drive is affected");
            Assert.That(GroundTerrain.LocomotionTerrainMult(0.5, GroundTerrainClass.Cover), Is.EqualTo(1.0).Within(1e-9),
                "neutral handling on cover is ×1.0");

            // Open ground is neutral for ALL handling — mobility doesn't gate the fight in the open.
            Assert.That(GroundTerrain.LocomotionTerrainMult(0.9, GroundTerrainClass.Open), Is.EqualTo(1.0),
                "even an all-terrain drive gets no combat edge in the OPEN (everyone moves freely there)");

            // Constrained ground: all-terrain fights better, road-bound worse.
            Assert.That(GroundTerrain.LocomotionTerrainMult(0.9, GroundTerrainClass.Rough), Is.GreaterThan(1.0),
                "an all-terrain drive (high rough-handling) fights BETTER on rough ground");
            Assert.That(GroundTerrain.LocomotionTerrainMult(0.1, GroundTerrainClass.Rough), Is.LessThan(1.0),
                "a road-bound drive (low rough-handling) is bogged down on rough ground");
        }

        [Test]
        [Description("Cradle-to-grave connection: a unit DESIGNED with an all-terrain locomotion (high rough-handling) yields a bigger rough-ground combat term than one designed road-bound — the term reads the designed drive (GroundLocomotionAtb → RoughHandlingForUnit → LocomotionTerrainMult), so the drive you design decides your rough-ground edge.")]
        public void ADesignedAllTerrainDrive_OutFightsARoadBoundOne_OnRoughGround()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            // Two designed drives, differing only in rough-terrain handling: all-terrain tracks vs fast road wheels.
            var allTerrain = new ComponentDesign { UniqueID = "test-loco-allterrain", Name = "All-Terrain Tracks" };
            allTerrain.AttributesByType[typeof(GroundLocomotionAtb)] = new GroundLocomotionAtb(1.5, 0.9, 0);
            faction.IndustryDesigns["test-loco-allterrain"] = allTerrain;
            var roadBound = new ComponentDesign { UniqueID = "test-loco-roadbound", Name = "Fast Road Wheels" };
            roadBound.AttributesByType[typeof(GroundLocomotionAtb)] = new GroundLocomotionAtb(3.0, 0.1, 0);
            faction.IndustryDesigns["test-loco-roadbound"] = roadBound;

            var atDesign = GroundUnitAssembly.RegisterAssembledDesign(faction, "test-at-unit", "All-Terrain Unit",
                Part("default-design-human-frame"), new List<(ComponentDesign, int)> { (allTerrain, 1) });
            var rbDesign = GroundUnitAssembly.RegisterAssembledDesign(faction, "test-rb-unit", "Road-Bound Unit",
                Part("default-design-human-frame"), new List<(ComponentDesign, int)> { (roadBound, 1) });

            var atUnit = GroundForces.RaiseUnit(body, atDesign, s.Faction.Id, 0);
            var rbUnit = GroundForces.RaiseUnit(body, rbDesign, s.Faction.Id, 0);

            double atRough = GroundTerrain.LocomotionTerrainMult(GroundMobility.RoughHandlingForUnit(body, atUnit), GroundTerrainClass.Rough);
            double rbRough = GroundTerrain.LocomotionTerrainMult(GroundMobility.RoughHandlingForUnit(body, rbUnit), GroundTerrainClass.Rough);
            Log($"rough-ground combat term: all-terrain unit ×{atRough:0.00}, road-bound unit ×{rbRough:0.00}");

            Assert.That(atRough, Is.GreaterThan(1.0), "the designed all-terrain unit gets a rough-ground combat edge");
            Assert.That(rbRough, Is.LessThan(1.0), "the designed road-bound unit is penalised on rough ground");
            Assert.That(atRough, Is.GreaterThan(rbRough), "the drive you design decides your rough-ground edge — the CONNECT");
        }
    }
}
