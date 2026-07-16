using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Industry;
using Pulsar4X.Storage;
using Pulsar4X.Datablobs;      // ComponentInstancesDB (namespace ≠ folder)
using Pulsar4X.DataStructures; // ComponentMountType
using Pulsar4X.Interfaces;     // ChassisBudgetKind, IChassisAtb
using Pulsar4X.Stations;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// STATION-as-assembly — the engine gauge for the build-then-deploy station buildable. Proves the three rungs the
    /// Entity Assembler needs next slice:
    ///  1) <see cref="StationDesign.RegisterStationDesign"/> assembles a chassis + modules into a buildable
    ///     <see cref="StationDesign"/> and registers it on the faction (rides the normal industry rails).
    ///  2) the base-mod <c>station-chassis</c> binds its <see cref="StationChassisAtb"/> from JSON (the gotcha-10
    ///     template→atb sensor, the <see cref="RailgunWeaponTests"/> equivalent) with the Structure budget currency.
    ///  3) completing a station build DEPLOYS a real station at the colony's body with its designed modules installed —
    ///     driven through the design's own <see cref="StationDesign.OnConstructionComplete"/>, the exact call
    ///     <c>IndustryTools</c> makes at completion (the deterministic "drive the completion hook directly" pattern,
    ///     Tests/CLAUDE.md gotcha 7).
    /// Engine-only → runs in CI.
    /// </summary>
    [TestFixture]
    public class StationDesignTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[station-design] " + m);

        private static ComponentDesign Part(TestScenario s, string id)
            => (ComponentDesign)s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns[id];

        [Test]
        [Description("The base-mod station-chassis loads onto the start faction and binds a StationChassisAtb from JSON " +
                     "(gotcha-10 template→atb), exposing the shared IChassisAtb with the Structure budget currency and " +
                     "the Station part mount.")]
        public void StationChassis_LoadsFromJson_BindsTheAtb_WithStructureBudget()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns;

            Assert.That(designs.ContainsKey("default-design-station-chassis"), Is.True,
                "the station chassis loads onto the faction (template + component design + earth.json entries wired up — the six-point chain)");
            var chassis = designs["default-design-station-chassis"] as ComponentDesign;
            Assert.That(chassis, Is.Not.Null, "the station chassis is a ComponentDesign (rides the shared designer)");

            Assert.That(chassis.HasAttribute<StationChassisAtb>(), Is.True,
                "the JSON stationChassisAtbArgs bound a StationChassisAtb (gotcha-10 path works)");
            var atb = chassis.GetAttribute<StationChassisAtb>();
            Log($"station-chassis: budget={atb.StructuralBudget:0} kind={atb.BudgetKind} mount={atb.PartMount}");

            // The shared IChassisAtb view — a station's frame budgets in Structure (the "reserved for station hosts" kind).
            IChassisAtb asChassis = atb;
            Assert.That(asChassis.BudgetKind, Is.EqualTo(ChassisBudgetKind.Structure), "a station chassis budgets in Structure");
            Assert.That(asChassis.StructuralBudget, Is.GreaterThan(0), "it carries a real structure budget from the template");
            Assert.That(asChassis.PartMount, Is.EqualTo(ComponentMountType.Station), "its parts mount as Station modules");
            Assert.That(chassis.ComponentMountType.HasFlag(ComponentMountType.Station), Is.True,
                "the chassis component itself is a Station-mount part (the assembler lists it as a frame)");
        }

        [Test]
        [Description("RegisterStationDesign assembles a chassis + modules into a buildable StationDesign registered on " +
                     "the faction — the assembler entry point (mirror of GroundUnitAssembly.RegisterAssembledDesign).")]
        public void RegisterStationDesign_MakesABuildable_OnTheFaction()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();

            var design = StationDesign.RegisterStationDesign(
                faction, "test-station-research", "Test Research Station",
                Part(s, "default-design-station-chassis"),
                new List<(ComponentDesign, int)>
                {
                    (Part(s, "default-design-reactor-2t"), 1),
                    (Part(s, "default-design-research-lab"), 1),
                });

            Log($"registered: id={design.UniqueID} valid={design.IsValid} line={design.IndustryTypeID} costItems={design.ResourceCosts.Count} parts={design.ComponentDesignIds.Count}");
            Assert.That(faction.IndustryDesigns.ContainsKey("test-station-research"), Is.True, "registered as a buildable on the faction");
            Assert.That(design.IsValid, Is.True, "the design is valid (has a UniqueID)");
            Assert.That(design.IndustryTypeID, Is.EqualTo("installation-construction"),
                "rides the colony's construction line (the line a colony provides, from the chassis)");
            Assert.That(design.ResourceCosts.Count, Is.GreaterThan(0), "costs summed from the chassis + modules");
            Assert.That(design.ComponentDesignIds.ContainsKey("default-design-station-chassis"), Is.True, "the chassis is in the component list");
            Assert.That(design.ComponentDesignIds.ContainsKey("default-design-research-lab"), Is.True, "the research module is in the component list");
        }

        [Test]
        [Description("Completing a station build DEPLOYS a real station at the colony's body with its designed modules " +
                     "installed — driven through OnConstructionComplete directly (deterministic; the generic queue is " +
                     "covered by ProductionBuildTests). Never throws.")]
        public void StationBuild_OnCompletion_DeploysAStation_WithModulesInstalled()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();

            var design = StationDesign.RegisterStationDesign(
                faction, "test-station-deploy", "Test Deploy Station",
                Part(s, "default-design-station-chassis"),
                new List<(ComponentDesign, int)>
                {
                    (Part(s, "default-design-reactor-2t"), 1),
                    (Part(s, "default-design-research-lab"), 1),
                });

            int stationsBefore = faction.Stations.Count;

            // Drive the completion hook directly — the exact call IndustryTools makes when a build finishes.
            var storage = s.Colony.GetDataBlob<CargoStorageDB>();
            var job = new IndustryJob(faction, design.UniqueID);
            Assert.DoesNotThrow(() => design.OnConstructionComplete(s.Colony, storage, "line", job, design),
                "the deploy hook never throws in the industry hotloop (gotcha L4)");

            Assert.That(faction.Stations.Count, Is.EqualTo(stationsBefore + 1), "exactly one station was deployed by the completed build");
            var station = faction.Stations.Last();

            // The station is anchored at the colony's body and carries the shared station chassis.
            Assert.That(station.HasDataBlob<StationInfoDB>(), Is.True, "the deployed entity is a real station");
            Assert.That(station.GetDataBlob<StationInfoDB>().HostingBodyEntity.Id, Is.EqualTo(s.StartingBody.Id),
                "the station is deployed at the building colony's body");

            // The designed modules were installed on the new station.
            var comps = station.GetDataBlob<ComponentInstancesDB>();
            Log($"deployed station: modules installed = {comps.AllComponents.Count}");
            Assert.That(comps.AllComponents.Count, Is.GreaterThanOrEqualTo(3),
                "the chassis + reactor + research-lab modules were installed on the deployed station");
        }
    }
}
