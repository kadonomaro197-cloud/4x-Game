using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Galaxy;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Units-as-entities (Option A) — SLICE 4: unit SPEED falls out of the CHASSIS (the frame's existing
    /// <c>Locomotion</c>), not a new stat. Because a raised unit carries its components, march time reads the chassis
    /// speed off the backing store (like any ability) and divides the crossing time by it. Foot is the ×1.0 baseline;
    /// a faster frame (Hover/Tracked/Walker) crosses the same ground in less time. Design: docs/GROUND-UNITS-AS-ENTITIES-DESIGN.md.
    /// </summary>
    [TestFixture]
    public class GroundMobilityTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[mobility] " + m);

        [Test]
        [Description("A unit on a Hover frame marches quicker than one on a Foot frame over the same ground — speed falls out of the chassis.")]
        public void FastChassis_MarchesQuickerThanFoot()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            // A fast HOVER frame as a designed component (base mod ships only the Foot human-frame).
            var hoverFrame = new ComponentDesign { UniqueID = "test-hover-frame", Name = "Hover Frame" };
            hoverFrame.AttributesByType[typeof(GroundChassisAtb)] =
                new GroundChassisAtb(100, 200, 10, (double)(int)GroundLocomotion.Hover, (double)(int)GroundCarryClass.Personnel);
            faction.IndustryDesigns["test-hover-frame"] = hoverFrame;

            var hoverDesign = GroundUnitAssembly.RegisterAssembledDesign(faction, "test-hover-unit", "Hover Unit",
                hoverFrame, new List<(ComponentDesign, int)>());
            var footDesign = GroundUnitAssembly.RegisterAssembledDesign(faction, "test-foot-unit", "Foot Unit",
                Part("default-design-human-frame"), new List<(ComponentDesign, int)>());

            var hover = GroundForces.RaiseUnit(body, hoverDesign, s.Faction.Id, 0);
            var foot = GroundForces.RaiseUnit(body, footDesign, s.Faction.Id, 0);

            var regions = body.GetDataBlob<PlanetRegionsDB>();
            int neighbor = regions.Regions[hover.RegionIndex].Neighbors[0];
            Assert.That(GroundForces.OrderMove(body, hover, neighbor), Is.True, "hover unit marches to the neighbour");
            Assert.That(GroundForces.OrderMove(body, foot, neighbor), Is.True, "foot unit marches to the neighbour");

            Assert.That(hover.TransitSecondsRemaining, Is.LessThan(foot.TransitSecondsRemaining),
                "the hover frame crosses the same region in less time (speed off the chassis)");
            Log($"march time: hover {hover.TransitSecondsRemaining:0}s < foot {foot.TransitSecondsRemaining:0}s (×{GroundMobility.HoverSpeed} chassis speed)");
        }

        [Test]
        [Description("SpeedMultFor: Foot is the 1.0 baseline; Tracked / Walker / Hover are faster.")]
        public void SpeedMult_FootIsBaseline_OthersFaster()
        {
            Assert.That(GroundMobility.SpeedMultFor(GroundLocomotion.Foot), Is.EqualTo(1.0));
            Assert.That(GroundMobility.SpeedMultFor(GroundLocomotion.Hover), Is.GreaterThan(1.0));
            Assert.That(GroundMobility.SpeedMultFor(GroundLocomotion.Tracked), Is.GreaterThan(1.0));
            Assert.That(GroundMobility.SpeedMultFor(GroundLocomotion.Walker), Is.GreaterThan(1.0));
        }
    }
}
