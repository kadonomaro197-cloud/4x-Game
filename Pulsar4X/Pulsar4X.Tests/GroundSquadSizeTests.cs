using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Components;    // ComponentDesign
using Pulsar4X.Factions;      // FactionInfoDB
using Pulsar4X.GroundCombat;  // GroundUnitAssembly, GroundForcesDB
using Pulsar4X.Industry;      // IndustryJob
using Pulsar4X.Storage;       // CargoStorageDB

namespace Pulsar4X.Tests
{
    /// <summary>
    /// SQUAD SIZE — the Entity Assembler's "units per build" dial (the developer's ask: a dial that dictates how many
    /// units are made when the design is built at a production facility). One build of a squad-of-N design raises N
    /// GroundUnits into the muster region, and the build COST + build-TIME scale with N so there's no free
    /// multiplication (CONVENTIONS §16). Default 1 → exactly one unit, byte-identical (every existing design + old save).
    /// Engine-only → CI. Drives <c>OnConstructionComplete</c> directly (the deterministic fielding idiom of
    /// PlayerGroundChainRailsTests / GroundUnitFieldingTests, gotcha 7).
    /// </summary>
    [TestFixture]
    public class GroundSquadSizeTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[squad-size] " + m);

        [Test]
        [Description("A squad-of-5 assembled design: one build raises FIVE units (default design raises ONE), and its "
                   + "build cost + time are exactly 5× the single-unit design's — the dial scales cost, no free units.")]
        public void SquadSize_OneBuildRaisesNUnits_AndCostsNTimes()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            ComponentDesign Part(string p) => (ComponentDesign)faction.IndustryDesigns[p];
            var frame = Part("default-design-human-frame");
            var parts = new List<(ComponentDesign, int)> { (Part("default-design-ground-rifle"), 1) };

            // the SAME parts assembled two ways: one unit per build, and a squad of five per build.
            var solo = GroundUnitAssembly.RegisterAssembledDesign(faction, "sq-solo", "Lone Trooper", frame, parts);      // default squad 1
            var squad = GroundUnitAssembly.RegisterAssembledDesign(faction, "sq-five", "Rifle Squad", frame, parts, 5);   // squad of five

            Assert.That(solo.UnitsPerBuild, Is.EqualTo(1), "default squad size is 1 (byte-identical)");
            Assert.That(squad.UnitsPerBuild, Is.EqualTo(5), "the dial sets the squad size");

            // COST scales with the squad — no free multiplication (CONVENTIONS §16).
            Assert.That(squad.IndustryPointCosts, Is.EqualTo(solo.IndustryPointCosts * 5), "build-time is 5× for a squad of 5");
            Assert.That(solo.ResourceCosts.Count, Is.GreaterThan(0), "precondition: the design has material costs to compare");
            foreach (var kv in solo.ResourceCosts)
                Assert.That(squad.ResourceCosts[kv.Key], Is.EqualTo(kv.Value * 5), "material " + kv.Key + " costs 5× for the squad");

            var body = s.StartingBody;
            var storage = s.Colony.GetDataBlob<CargoStorageDB>();

            // ONE build completion of the squad design raises FIVE units (RaiseUnit creates the roster on demand).
            squad.OnConstructionComplete(s.Colony, storage, "line", new IndustryJob(faction, squad.UniqueID), squad);
            var forces = body.GetDataBlob<GroundForcesDB>();
            int raised = forces.Units.Count(u => u.FactionOwnerID == s.Faction.Id && u.DesignId == squad.UniqueID);
            Assert.That(raised, Is.EqualTo(5), "one build of a squad-of-5 design fields five units into the muster region");

            // the default (squad 1) design raises exactly ONE — byte-identical to the pre-dial behaviour.
            solo.OnConstructionComplete(s.Colony, storage, "line", new IndustryJob(faction, solo.UniqueID), solo);
            int soloRaised = forces.Units.Count(u => u.FactionOwnerID == s.Faction.Id && u.DesignId == solo.UniqueID);
            Assert.That(soloRaised, Is.EqualTo(1), "a default design raises exactly one unit per build (byte-identical)");
            Log($"squad of 5 → {raised} units in one build (time {squad.IndustryPointCosts} = 5× {solo.IndustryPointCosts}); solo → {soloRaised}");
        }
    }
}
