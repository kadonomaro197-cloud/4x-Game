using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Factions;
using Pulsar4X.Colonies;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The ground-map foundation gauge. A planet is no longer a dimensionless point — it carries a REGION layer
    /// (<see cref="PlanetRegionsDB"/>): v1, four longitude slices in a RING (topology-correct, so there's no seam
    /// and the "Pacific theatre" survives), each with real area, a crossing time (the distance datum movement will
    /// read), and a bundle of discovered-by-exploration features. These tests assert the generator builds that
    /// layer correctly, that it's persistent (deep-clones), and that features come out random-but-LOGICAL.
    /// Design: docs/GROUND-COMBAT-MAP-DESIGN.md.
    /// </summary>
    [TestFixture]
    public class PlanetRegionsTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[regions] " + m);

        [Test]
        [Description("A major body gets a 4-region RING surface map: 4 regions, ring adjacency that WRAPS (region 0 borders region 3 — no seam, the Pacific-theatre fix), each with real area, a crossing time, and at least one feature.")]
        public void Planet_GetsFourRegions_InARing()
        {
            var s = TestScenario.CreateWithColony();
            // Idempotent — ensure the layer exists regardless of which gen path built the start system.
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);

            Assert.That(s.StartingBody.HasDataBlob<PlanetRegionsDB>(), Is.True, "the start planet should have a region layer");
            var regions = s.StartingBody.GetDataBlob<PlanetRegionsDB>().Regions;
            Assert.That(regions.Count, Is.EqualTo(4), "v1 is a 4-slice ring");

            var r0 = regions[0];
            Assert.That(r0.Neighbors, Does.Contain(3), "region 0 wraps west to region 3 — the seam-free ring");
            Assert.That(r0.Neighbors, Does.Contain(1), "region 0 borders region 1 to the east");

            foreach (var r in regions)
            {
                Assert.That(r.Area_km2, Is.GreaterThan(0), "each region has real surface area (the true-size datum)");
                Assert.That(r.CrossingTimeSeconds, Is.GreaterThan(0), "each region has a traversal time (the distance datum)");
                Assert.That(r.Features.Count, Is.GreaterThan(0), "each region has at least one feature");
            }
            Log($"start planet regions={regions.Count}, r0 area={regions[0].Area_km2:N0} km², r0 features={regions[0].Features.Count}");
        }

        [Test]
        [Description("An authored world (Earth, via the blueprint start path) has KNOWN geography — its regions start surveyed. A procedurally generated world starts unsurveyed instead (that path is exercised live by CreateSystem's surveyed:false hook).")]
        public void AuthoredWorld_StartsSurveyed()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var regions = s.StartingBody.GetDataBlob<PlanetRegionsDB>().Regions;
            Assert.That(regions.All(r => r.Surveyed), Is.True, "Earth's geography is known — its regions start surveyed");
        }

        [Test]
        [Description("Features are random but LOGICAL: Earth is a wet world (hydrosphere 71%), so at least one region carries an Ocean feature.")]
        public void WetWorld_HasAnOceanFeature()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var regions = s.StartingBody.GetDataBlob<PlanetRegionsDB>().Regions;
            bool anyOcean = regions.Any(r => r.Features.Any(f => f.Type == RegionFeatureType.Ocean));
            Assert.That(anyOcean, Is.True, "a wet world (hydro 71%) should generate at least one ocean feature");
        }

        [Test]
        [Description("The region layer deep-clones so it survives save/load and entity transfer — mutating a clone must not touch the original. This is the persistence discipline the earlier colony hex map lacked.")]
        public void RegionLayer_ClonesDeeply()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var original = s.StartingBody.GetDataBlob<PlanetRegionsDB>();

            var clone = (PlanetRegionsDB)original.Clone();
            Assert.That(clone.Regions.Count, Is.EqualTo(original.Regions.Count));

            int beforeFeatures = original.Regions[0].Features.Count;
            clone.Regions[0].Features.Add(new RegionFeature(RegionFeatureType.Volcanic, 0.5));
            Assert.That(original.Regions[0].Features.Count, Is.EqualTo(beforeFeatures),
                "the features list was shared, not deep-cloned — the region layer would corrupt on transfer/save");
        }

        [Test]
        [Description("Region generation is idempotent — a body that already has a region layer is not regenerated or duplicated (it's hooked into New-Game-critical gen at several paths, so it must never double-build).")]
        public void Generation_IsIdempotent()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var first = s.StartingBody.GetDataBlob<PlanetRegionsDB>();
            int count = first.Regions.Count;

            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var second = s.StartingBody.GetDataBlob<PlanetRegionsDB>();
            Assert.That(second, Is.SameAs(first), "the same region-layer blob should remain (not replaced on a second pass)");
            Assert.That(second.Regions.Count, Is.EqualTo(count), "no duplicate regions added");
        }

        [Test]
        [Description("BUILD AT A REGION (slice 2): a colony builds an installation at a CHOSEN region of its planet, through the real order handler (the player path). The building is a normal colony installation (the economy sees it) that ALSO records itself in that region — the new located axis, without disturbing the existing economy.")]
        public void BuildInRegion_PlacesInstallation_ThroughTheOrderHandler()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var planet = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;
            var regions = planet.GetDataBlob<PlanetRegionsDB>().Regions;

            const int regionIdx = 2;
            const string designId = "default-design-infrastructure";
            Assert.That(factionInfo.IndustryDesigns.ContainsKey(designId), Is.True, "precondition: the installation design is unlocked");
            int regionBefore = regions[regionIdx].InstallationIds.Count;
            int colonyComponentsBefore = s.Colony.GetDataBlob<ComponentInstancesDB>().AllComponents.Count;

            var cmd = PlaceInstallationInRegionOrder.CreateCommand(s.Colony, regionIdx, designId);
            Assert.That(cmd.IsValidCommand(s.Game), Is.True, "building at a valid region of the colony's planet is a valid order");
            s.Game.OrderHandler.HandleOrder(cmd); // the real player submission path (Game.OrderHandler.HandleOrder)

            Assert.That(regions[regionIdx].InstallationIds.Count, Is.EqualTo(regionBefore + 1),
                "the installation should be recorded in the chosen region — the new located axis");
            Assert.That(s.Colony.GetDataBlob<ComponentInstancesDB>().AllComponents.Count, Is.EqualTo(colonyComponentsBefore + 1),
                "and it should also be a normal colony installation the economy sees (economy undisturbed, just located)");
        }

        [Test]
        [Description("BUILD AT A REGION — bounds: an out-of-range region index is not a valid order and builds nothing (the handler validates before executing).")]
        public void BuildInRegion_InvalidRegion_IsRejected()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);

            var cmd = PlaceInstallationInRegionOrder.CreateCommand(s.Colony, 99, "default-design-infrastructure");
            Assert.That(cmd.IsValidCommand(s.Game), Is.False, "region index 99 does not exist on a 4-slice ring");

            int before = s.Colony.GetDataBlob<ComponentInstancesDB>().AllComponents.Count;
            s.Game.OrderHandler.HandleOrder(cmd); // the handler validates → no-op
            Assert.That(s.Colony.GetDataBlob<ComponentInstancesDB>().AllComponents.Count, Is.EqualTo(before),
                "nothing is built for an invalid region");
        }
    }
}
