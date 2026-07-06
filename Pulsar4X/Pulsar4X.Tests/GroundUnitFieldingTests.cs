using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// SLICE A — THE CONNECT: an ASSEMBLED unit design (frame + parts) is buildable through the normal industry rails
    /// and, when the build completes, a real unit is FIELDED on the colony's planet with the assembly's emergent stats.
    /// This is what makes the whole planetary-unit designer real end-to-end: design → build → a unit on the ground.
    ///
    /// The pieces already existed (`GroundUnitAssembly.ToGroundUnitDesign` builds the design; `GroundUnitDesign.
    /// OnConstructionComplete` raises the unit); this proves the FULL chain runs through the real industry queue.
    /// Uses the existing planetary parts (frame/rifle/plating) so the fielded unit has real combat stats today — the
    /// universal-weapon combat fidelity (and the reactor/magazine gates biting) lights up with the resolver merge (next
    /// branch). Engine-only → runs in CI. Design: docs/WEAPON-UNIFICATION-DESIGN.md.
    /// </summary>
    [TestFixture]
    public class GroundUnitFieldingTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[fielding] " + m);

        private static TestScenario _s;
        private static ComponentDesign Part(string id)
            => (ComponentDesign)_s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns[id];

        [Test]
        [Description("A: assemble a unit (frame+rifle+plating) -> register it as buildable -> build it through the industry queue -> a unit is FIELDED on the planet with the assembly's emergent stats.")]
        public void AssembledDesign_BuildsThroughIndustry_AndFieldsAUnit()
        {
            _s = TestScenario.CreateWithColony();
            var faction = _s.Faction.GetDataBlob<FactionInfoDB>();
            var body = _s.StartingBody;

            // 1+2) ASSEMBLE + REGISTER — the single connect API a designer UI calls: frame + parts -> a buildable design
            //       registered on the faction. Stats emerge from the parts.
            var design = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "test-assembled-trooper", "Assembled Trooper",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)>
                {
                    (Part("default-design-ground-rifle"), 1),
                    (Part("default-design-ground-plating"), 1),
                });
            Log($"assembled: atk={design.Attack:0} hp={design.HitPoints:0} costItems={design.ResourceCosts.Count} pts={design.IndustryPointCosts}");
            Assert.That(design.Attack, Is.EqualTo(40), "attack emerged from the rifle");
            Assert.That(design.HitPoints, Is.EqualTo(350), "HP emerged from frame(200) + plating(150)");
            Assert.That(design.ResourceCosts.Count, Is.GreaterThan(0), "costs summed from the parts (frame + rifle + plating)");
            Assert.That(design.IndustryTypeID, Is.EqualTo("installation-construction"), "rides the colony's construction line");
            Assert.That(faction.IndustryDesigns.ContainsKey(design.UniqueID), Is.True, "registered as buildable on the faction");

            // 3) BUILD through the real industry queue. Neutralise material cost so the queue->complete->raise MECHANIC is
            //    isolated from mineral stock (cost-summing itself is asserted above); a small point cost completes in the
            //    advanced window, exactly like the proven GroundForcesTests / ProductionBuildTests pattern.
            design.ResourceCosts = new Dictionary<string, long>();
            design.IndustryPointCosts = 100;

            int before = body.HasDataBlob<GroundForcesDB>() ? body.GetDataBlob<GroundForcesDB>().Units.Count : 0;
            _s.QueueProductionJob(design.UniqueID);
            _s.AdvanceTime(System.TimeSpan.FromDays(180));

            // 4) A UNIT IS FIELDED on the planet, carrying the assembly's stats.
            Assert.That(body.HasDataBlob<GroundForcesDB>(), Is.True, "the build created the planet's ground-forces roster");
            var units = body.GetDataBlob<GroundForcesDB>().Units;
            Assert.That(units.Count, Is.EqualTo(before + 1), "exactly one unit was fielded by the completed build");
            var fielded = units.Last(u => u.DesignId == design.UniqueID);
            Log($"fielded: '{fielded.Name}' atk={fielded.Attack:0} hp={fielded.MaxHealth:0} region={fielded.RegionIndex}");
            Assert.That(fielded.Attack, Is.EqualTo(design.Attack), "the fielded unit carries the assembly's attack");
            Assert.That(fielded.MaxHealth, Is.EqualTo(design.HitPoints), "the fielded unit carries the assembly's HP");
        }
    }
}
