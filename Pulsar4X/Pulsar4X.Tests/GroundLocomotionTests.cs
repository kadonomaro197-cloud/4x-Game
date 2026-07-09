using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Units-as-entities (Option A) — PARAMETRIC LOCOMOTION: locomotion is a designable COMPONENT you tweak (speed /
    /// rough-terrain handling / amphibious), not a fixed menu — so a player can build any drive for any environment.
    /// Its stats fall out of the unit's component store (like the radar), and the designed drive overrides the chassis's
    /// coarse Locomotion enum. Design: docs/GROUND-UNITS-AS-ENTITIES-DESIGN.md.
    /// </summary>
    [TestFixture]
    public class GroundLocomotionTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[locomotion] " + m);

        [Test]
        [Description("A designed locomotion component drives the unit's speed + rough handling, overriding the chassis enum — tweak the dial, get any drive.")]
        public void DesignedLocomotion_DrivesSpeedAndHandling()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            // A designed drive: fast (×3), poor rough handling (wheels-like), not amphibious.
            var loco = new ComponentDesign { UniqueID = "test-loco", Name = "Fast Wheels" };
            loco.AttributesByType[typeof(GroundLocomotionAtb)] = new GroundLocomotionAtb(3.0, 0.2, 0);
            faction.IndustryDesigns["test-loco"] = loco;

            var design = GroundUnitAssembly.RegisterAssembledDesign(faction, "test-wheeled-unit", "Wheeled Unit",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)> { (loco, 1) });

            var unit = GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);

            Assert.That(GroundMobility.SpeedMultForUnit(body, unit), Is.EqualTo(3.0),
                "the designed locomotion's speed factor drives the unit, overriding the chassis Foot enum (1.0)");
            Assert.That(GroundMobility.RoughHandlingForUnit(body, unit), Is.EqualTo(0.2).Within(1e-9),
                "rough-terrain handling reads off the designed locomotion");
            Log($"designed drive → speed ×{GroundMobility.SpeedMultForUnit(body, unit)}, rough handling {GroundMobility.RoughHandlingForUnit(body, unit):0.0}");
        }

        [Test]
        [Description("A unit with NO locomotion component falls back to the chassis Locomotion enum (Foot = 1.0) — additive, existing units unchanged.")]
        public void NoLocomotionComponent_FallsBackToChassis()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            var design = GroundUnitAssembly.RegisterAssembledDesign(faction, "test-footonly", "Foot Only",
                Part("default-design-human-frame"), new List<(ComponentDesign, int)>());
            var unit = GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);
            Assert.That(GroundMobility.SpeedMultForUnit(body, unit), Is.EqualTo(1.0), "Foot chassis baseline");
        }
    }
}
