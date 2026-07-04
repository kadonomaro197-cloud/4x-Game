using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Factions;
using Pulsar4X.Colonies;
using Pulsar4X.Datablobs;
using Pulsar4X.DataStructures;
using Pulsar4X.GeoSurveys;

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
        [Description("SURVEY MODEL: you KNOW the ground where you SETTLE. After a normal start, the home colony's world (Earth) is surveyed — revealed on colony creation — but an uncolonised sibling body in the same system starts as FOG. Nothing is pre-surveyed at generation; a colony reveals its own world, and everything else must be scanned. This is what makes Luna/Mars real survey targets.")]
        public void HomeColony_WorldSurveyed_SiblingsFogged()
        {
            var s = TestScenario.CreateWithColony();

            // Home world (where the colony sits) has known geography.
            Assert.That(s.StartingBody.HasDataBlob<PlanetRegionsDB>(), Is.True, "the home world has a region layer");
            Assert.That(s.StartingBody.GetDataBlob<PlanetRegionsDB>().Regions.All(r => r.Surveyed), Is.True,
                "the home colony's world is surveyed (revealed on colony creation)");

            // At least one OTHER body in the system has a region layer and is still fogged.
            var siblings = s.StartingSystem.GetAllEntitiesWithDataBlob<PlanetRegionsDB>()
                .Where(b => b.Id != s.StartingBody.Id).ToList();
            Assert.That(siblings.Count, Is.GreaterThan(0), "Sol has other major bodies with a region layer");
            bool anyFogged = siblings.Any(b => b.GetDataBlob<PlanetRegionsDB>().Regions.Any(r => !r.Surveyed));
            Assert.That(anyFogged, Is.True, "an uncolonised world starts as fog — the survey target (e.g. Luna)");
            Log($"survey model: home surveyed; {siblings.Count(b => b.GetDataBlob<PlanetRegionsDB>().Regions.Any(r => !r.Surveyed))} sibling world(s) fogged");
        }

        [Test]
        [Description("Moons get a region layer too — Luna/Ganymede are ground-combat places you can survey and fight over, not just planets. Confirms IsMajorBody includes Moon, so the developer's 'survey Luna' test is actually reachable.")]
        public void Moon_GetsRegionLayer_SoLunaIsSurveyable()
        {
            var s = TestScenario.CreateWithColony();
            var moons = s.StartingSystem.GetAllEntitiesWithDataBlob<SystemBodyInfoDB>()
                .Where(b => b.TryGetDataBlob<SystemBodyInfoDB>(out var info) && info.BodyType == BodyType.Moon)
                .ToList();
            Assert.That(moons.Count, Is.GreaterThan(0), "Sol has moons (Luna, …)");
            Assert.That(moons.Any(b => b.HasDataBlob<PlanetRegionsDB>()), Is.True,
                "a moon (Luna) gets a region layer, so it can be surveyed and fought over");
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

        [Test]
        [Description("SURVEY REVEAL (slice 4) — the reveal primitive: RevealAll flips every fogged region to KNOWN and reports it changed; a second call is a no-op (an already-known world isn't re-revealed). This is the exploration→map link's inner mechanism.")]
        public void SurveyReveal_RevealAll_FlipsFogToKnown_Idempotently()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var regionsDB = s.StartingBody.GetDataBlob<PlanetRegionsDB>();

            // Put the world into fog, as a procedurally-generated (unscanned) world starts.
            foreach (var r in regionsDB.Regions) r.Surveyed = false;   // internal set, reachable via InternalsVisibleTo

            Assert.That(regionsDB.RevealAll(), Is.True, "revealing a fogged world reports a change");
            Assert.That(regionsDB.Regions.All(r => r.Surveyed), Is.True, "every region is now known");
            Assert.That(regionsDB.RevealAll(), Is.False, "re-revealing an already-known world is a no-op (idempotent)");
        }

        [Test]
        [Description("SURVEY REVEAL (slice 4) — the CONNECTION: completing a geological survey of a fogged world reveals its regions. Driven through the REAL GeoSurveyProcessor completion path (a player fleet with enough survey speed), so the exploration system → ground map wire is exercised, not just the primitive.")]
        public void SurveyReveal_CompletingGeoSurvey_RevealsRegions()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var planet = s.StartingBody;
            var regionsDB = planet.GetDataBlob<PlanetRegionsDB>();

            // Precondition: put the world back into fog (as an unscanned procedural world would be).
            foreach (var r in regionsDB.Regions) r.Surveyed = false;
            Assert.That(regionsDB.Regions.All(r => !r.Surveyed), Is.True, "precondition: the world starts unsurveyed (fog)");

            // Make the body geo-surveyable with a low bar, cleared of any prior progress.
            if (!planet.TryGetDataBlob<GeoSurveyableDB>(out var geo))
            {
                geo = new GeoSurveyableDB { PointsRequired = 10 };
                planet.SetDataBlob(geo);
            }
            else
            {
                geo.PointsRequired = 10;
                geo.GeoSurveyStatus.Clear();
            }

            // A player-owned survey fleet with more than enough survey speed (100 ≥ 10 → completes in one pass).
            var fleet = Entity.Create(s.Faction.Id);
            s.StartingSystem.AddEntity(fleet, new List<BaseDataBlob> { new GeoSurveyAbilityDB { Speed = 100 } });

            var proc = new GeoSurveyProcessor(fleet, planet);
            proc.ProcessEntity(planet, s.Game.TimePulse.GameGlobalDateTime);

            Assert.That(regionsDB.Regions.All(r => r.Surveyed), Is.True,
                "completing the geo survey should reveal every region — fog → known (the exploration→map link)");
            Log($"survey reveal: {regionsDB.Regions.Count(r => r.Surveyed)}/{regionsDB.Regions.Count} regions now known after geo survey");
        }

        [Test]
        [Description("U1: completing a geo survey also GENERATES the world's fine HEX grid (the developer's call — hexes exist for every SURVEYED world, so the planet-view hex map is never empty on a world you've scanned). Driven through the REAL GeoSurveyProcessor completion path (beside the reveal).")]
        public void SurveyGen_CompletingGeoSurvey_GeneratesHexGrid()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var planet = s.StartingBody;
            var regionsDB = planet.GetDataBlob<PlanetRegionsDB>();

            // Precondition: an UNSCANNED world — fog AND no fine hexes yet. Clear the colony-gen hexes to simulate a
            // sibling world (Luna/Mars) that has coarse regions but was never a theatre, so the survey does the gen.
            foreach (var r in regionsDB.Regions) { r.Surveyed = false; r.Hexes.Clear(); }
            Assert.That(regionsDB.Regions.Sum(r => r.Hexes.Count), Is.EqualTo(0), "precondition: no fine hexes before survey");

            if (!planet.TryGetDataBlob<GeoSurveyableDB>(out var geo))
            {
                geo = new GeoSurveyableDB { PointsRequired = 10 };
                planet.SetDataBlob(geo);
            }
            else
            {
                geo.PointsRequired = 10;
                geo.GeoSurveyStatus.Clear();
            }

            var fleet = Entity.Create(s.Faction.Id);
            s.StartingSystem.AddEntity(fleet, new List<BaseDataBlob> { new GeoSurveyAbilityDB { Speed = 100 } });

            var proc = new GeoSurveyProcessor(fleet, planet);
            proc.ProcessEntity(planet, s.Game.TimePulse.GameGlobalDateTime);

            int total = regionsDB.Regions.Sum(r => r.Hexes.Count);
            Assert.That(total, Is.GreaterThan(0), "completing the geo survey generates the world's fine hex grid");
            Assert.That(regionsDB.Regions.All(r => r.Hexes.Count > 0), Is.True, "every region gets its hex patch on survey");
            Log($"survey hex-gen: {total} hexes across {regionsDB.Regions.Count} regions after geo survey");
        }

        // ───────────────────────── HEX layer (H1 — Planet → Region → Hex) ─────────────────────────

        [Test]
        [Description("H1 density math: the hex-patch radius scales linearly with planet radius (Earth → 12, clamped [2,24]); a radius-12 disk is 469 hexes — so Earth ≈ 4×469 ≈ 1876 hexes total (the 'Operational' density).")]
        public void HexDensity_ScalesWithPlanetRadius_Pure()
        {
            Assert.That(PlanetHexFactory.HexPatchRadiusFor(6.371e6), Is.EqualTo(12), "Earth-sized → radius-12 patch");
            Assert.That(PlanetHexFactory.HexPatchRadiusFor(3.39e6), Is.EqualTo(6), "Mars-sized → about half");
            Assert.That(PlanetHexFactory.HexPatchRadiusFor(1.0e5), Is.EqualTo(2), "tiny body clamps to the minimum");
            Assert.That(PlanetHexFactory.HexPatchRadiusFor(1.0e9), Is.EqualTo(24), "giant body clamps to the maximum");
            Assert.That(PlanetHexFactory.HexDiskCount(12), Is.EqualTo(469), "radius-12 disk = 3·144+3·12+1 = 469 hexes");
            Log($"hex density: Earth patch r=12 → {PlanetHexFactory.HexDiskCount(12)} hexes/region → ≈{4 * PlanetHexFactory.HexDiskCount(12)} total");
        }

        [Test]
        [Description("H1: the start colony's world gets its fine HEX patches at colony creation (lazy gen) — each region is filled with hexes (≈469 on Earth), every hex has a terrain drawn from the region's feature mix, and the patch is idempotent + clone-safe.")]
        public void HexPatches_GeneratedOnHomeWorld_TerrainAndCloneSafe()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            Assert.That(body.HasDataBlob<PlanetRegionsDB>(), Is.True, "home world has a region layer");
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();

            // ColonyFactory generated the patches at creation. Derive the expected size from the body's real radius so
            // the gauge holds whatever exact value earth.json uses.
            double radiusM = body.GetDataBlob<MassVolumeDB>().RadiusInM;
            int expectedR = PlanetHexFactory.HexPatchRadiusFor(radiusM);
            int expectedPerRegion = PlanetHexFactory.HexDiskCount(expectedR);
            Assert.That(regionsDB.Regions.All(r => r.Hexes.Count == expectedPerRegion), Is.True,
                $"every region is a radius-{expectedR} disk ({expectedPerRegion} hexes)");
            int total = regionsDB.Regions.Sum(r => r.Hexes.Count);
            Assert.That(total, Is.EqualTo(regionsDB.Regions.Count * expectedPerRegion), "total = regions × per-region");
            Assert.That(total, Is.GreaterThan(300), "an Earth-sized world has a real (Operational-density) hex map");

            // Every hex has a terrain, and it comes from the region's feature set (fine realization of the coarse map).
            var region0 = regionsDB.Regions[0];
            var featureTypes = region0.Features.Select(f => f.Type).ToHashSet();
            Assert.That(region0.Hexes.All(h => featureTypes.Contains(h.Terrain)), Is.True,
                "each hex's terrain is one of the region's features");

            // Idempotent — re-running generates nothing new.
            int before = total;
            PlanetHexFactory.EnsureHexesForBody(body);
            Assert.That(regionsDB.Regions.Sum(r => r.Hexes.Count), Is.EqualTo(before), "hex gen is idempotent");

            // Clone-safe (the old ColonyHexMapDB's fatal flaw — this one survives).
            var clone = (PlanetRegionsDB)regionsDB.Clone();
            Assert.That(clone.Regions.Sum(r => r.Hexes.Count), Is.EqualTo(before), "hexes survive a clone");
            Assert.That(clone.Regions[0].Hexes[0].Terrain, Is.EqualTo(region0.Hexes[0].Terrain), "hex data deep-copied");

            Log($"hex patches: {regionsDB.Regions.Count} regions, {total} hexes total; region0 terrains: {string.Join(",", region0.Hexes.Select(h => h.Terrain).Distinct())}");
        }
    }
}
