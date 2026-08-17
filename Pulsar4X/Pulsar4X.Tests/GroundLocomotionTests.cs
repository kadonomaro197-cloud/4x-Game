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
    /// coarse Locomotion enum. Design: docs/economy/COMPONENT-DESIGNER-CATEGORIES.md.
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

        [Test]
        [Description("OPERATION BLUEPRINT-TO-STEEL C-mobility = MULTIPLY (developer ruling 2026-08-17): a designed drive " +
                     "MULTIPLIES the frame's locomotion mode instead of erasing it — the SAME ×1.5 drive reads ×4.5 on a " +
                     "Hover (×3) frame but ×1.5 on a Foot (×1) frame, so the frame TYPE and the drive POWER compound and " +
                     "the frame choice stays a real decision. (Byte-identical for a Foot-frame unit — the existing gauges.)")]
        public void DriveOnANonFootFrame_MultipliesTheFrameMode()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            // The SAME designed drive (×1.5) mounted on two different frames.
            var drive = new ComponentDesign { UniqueID = "test-mob-drive", Name = "Drive x1.5" };
            drive.AttributesByType[typeof(GroundLocomotionAtb)] = new GroundLocomotionAtb(1.5, 0.5, 0);
            faction.IndustryDesigns["test-mob-drive"] = drive;

            // A NON-Foot (Hover ×3) frame as a designed chassis (base mod ships only the Foot human-frame).
            var hoverFrame = new ComponentDesign { UniqueID = "test-mob-hover-frame", Name = "Hover Frame" };
            hoverFrame.AttributesByType[typeof(GroundChassisAtb)] =
                new GroundChassisAtb(100, 200, 10, (double)(int)GroundLocomotion.Hover, (double)(int)GroundCarryClass.Personnel);
            faction.IndustryDesigns["test-mob-hover-frame"] = hoverFrame;

            var hoverDesign = GroundUnitAssembly.RegisterAssembledDesign(faction, "test-mob-hover-unit", "Hover Unit",
                hoverFrame, new List<(ComponentDesign, int)> { (drive, 1) });
            var footDesign = GroundUnitAssembly.RegisterAssembledDesign(faction, "test-mob-foot-unit", "Foot Unit",
                Part("default-design-human-frame"), new List<(ComponentDesign, int)> { (drive, 1) });

            var hoverUnit = GroundForces.RaiseUnit(body, hoverDesign, s.Faction.Id, 0);
            var footUnit = GroundForces.RaiseUnit(body, footDesign, s.Faction.Id, 0);

            double hoverSpeed = GroundMobility.SpeedMultForUnit(body, hoverUnit);
            double footSpeed = GroundMobility.SpeedMultForUnit(body, footUnit);
            Log($"same x1.5 drive: hover-frame x{hoverSpeed} vs foot-frame x{footSpeed} (HoverSpeed={GroundMobility.HoverSpeed})");

            // MULTIPLY: Hover ×3 × drive ×1.5 = ×4.5 — the frame and the drive COMPOUND.
            Assert.That(hoverSpeed, Is.EqualTo(GroundMobility.HoverSpeed * 1.5).Within(1e-9),
                "the drive MULTIPLIES the Hover frame mode (x3 * x1.5 = x4.5)");
            Assert.That(hoverSpeed, Is.GreaterThan(1.5).And.GreaterThan(GroundMobility.HoverSpeed),
                "compounding — faster than the drive alone AND faster than the frame alone");
            // Foot (×1) is neutral, so the same drive reads ×1.5 (byte-identical to the old 'drive wins' result) — but a
            // Hover-frame unit is genuinely faster than a Foot-frame unit with the IDENTICAL drive: the frame matters again.
            Assert.That(footSpeed, Is.EqualTo(1.5).Within(1e-9), "on a Foot frame the x1.5 drive reads x1.5 (byte-identical)");
            Assert.That(hoverSpeed, Is.GreaterThan(footSpeed), "the FRAME CHOICE matters — Hover beats Foot with the same drive");
        }
    }
}
