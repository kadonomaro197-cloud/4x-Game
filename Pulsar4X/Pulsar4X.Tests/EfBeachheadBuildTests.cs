using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;
using Pulsar4X.Galaxy;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION EARTHFALL — G1.2: the COLONY-FREE ON-SITE BUILD (the beachhead / FOB). Continues G1.1: a landed COMBAT
    /// ENGINEER (a chassis carrying a <see cref="GroundConstructorAtb"/>, G1.1) standing on FRIENDLY-HELD, enemy-free
    /// ground with landed footprint parts (<see cref="GroundParts"/>, G1.1) now ERECTS a footprint building on site over
    /// ground ticks — with NO colony present — hosted in the invader's beachhead OUTPOST (a bare component store, the
    /// same one a ground unit's backing entity uses). The placed bunker FORTIFIES the region (the same
    /// <see cref="GroundDefenseAtb"/> path a colony's Bunker uses), is a BOMBARD target (the grave rung), and marks the
    /// region a FOB resupply point (consumed in G2).
    ///
    /// Gauges (engine-only → CI): (1) an engineer + parts on a held region builds a bunker OVER ticks (not before the
    /// work is done), it fortifies, it's hosted colony-free, and it's a resupply point + a bombard target; (2) no build
    /// with no parts, an enemy in the region, or ground you don't hold. Byte-identical: <see cref="GroundBeachhead.TickBuilds"/>
    /// is a no-op until an engineer unit exists and lands.
    /// </summary>
    [TestFixture]
    public class EfBeachheadBuildTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[beachhead] " + m);

        private const int oneDay = 86400;

        [Test]
        [Description("A landed combat engineer on friendly-held, enemy-free ground with landed bunker parts erects the bunker OVER ticks; it fortifies the region (colony-free, via a beachhead outpost), is a resupply point, and is a bombard target (the grave rung).")]
        public void Engineer_BuildsAndFortifiesABeachhead_ColonyFree_AndItCanBeBombed()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            int fid = s.Faction.Id;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            // the surface hexes + cylinder grid exist for the war-map assertions (colony creation does this; be explicit)
            PlanetHexFactory.EnsureHexesForBody(body);
            PlanetGridFactory.EnsureGridForBody(body);
            var regions = body.GetDataBlob<PlanetRegionsDB>();
            Assert.That(regions.Regions.Count, Is.GreaterThanOrEqualTo(2), "the world has regions to build in");

            // Build in a NON-capital region (region 1). The start colony's own footprints sit on region 0's centre hex,
            // so building/bombing there would collide with them; region 1 has NO colony buildings, so the beachhead
            // bunker is the only thing on its hex — a clean colony-free build + a clean grave-rung bombard.
            const int rgn = 1;
            // A beachhead is built AFTER capturing the region — the invader HOLDS region 1 (owner == its faction).
            regions.Regions[rgn].OwnerFactionID = fid;

            // Field a COMBAT ENGINEER — a chassis carrying the base-mod ground-constructor part. The build ability falls
            // out of its backing component store (units-as-entities), exactly like a radar scout.
            var engDesign = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "test-combat-engineer", "Combat Engineer",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)> { (Part("default-design-ground-constructor"), 1) });
            var engineer = GroundForces.RaiseUnit(body, engDesign, fid, rgn);
            Assert.That(engineer.BackingEntityId, Is.GreaterThanOrEqualTo(0), "the engineer carries the constructor on its backing store");
            Assert.That(engineer.Health, Is.GreaterThan(0), "precondition: the engineer is a live unit");

            double rate = Part("default-design-ground-constructor").GetAttribute<GroundConstructorAtb>().BuildRate;
            var bunker = Part("default-design-bunker");
            double required = Math.Max(GroundBeachhead.MinAssemblyEffort, bunker.IndustryPointCosts);
            Log($"engineer builds at {rate:0} bp/day; a bunker costs {required:0} bp → ~{required / rate:0} engineer-days");

            // Land ONE crated bunker (a footprint building) in the held region.
            Assert.That(GroundParts.AddParts(body, rgn, "default-design-bunker", 1), Is.EqualTo(1));

            var region = regions.Regions[rgn];
            var beforeIds = new HashSet<int>(region.InstallationIds);
            double multBefore = GroundFortification.DefenseMult(region, regions.Regions, fid, GroundFortification.BuildResolver(body));

            // ── OVER TICKS: one day of work does NOT finish a ~500-day bunker, but a site is now accruing ──
            GroundBeachhead.TickBuilds(body, oneDay);
            var forces = body.GetDataBlob<GroundForcesDB>();
            Assert.That(region.InstallationIds.Count, Is.EqualTo(beforeIds.Count), "one day of work hasn't raised the bunker yet");
            Assert.That(forces.BuildSites.Count, Is.EqualTo(1), "but a build site is now in progress");
            Assert.That(forces.BuildSites[0].ProgressPoints, Is.EqualTo(rate).Within(1e-6), "~1 day of build-points laid down");
            Assert.That(GroundParts.PartCount(body, rgn, "default-design-bunker"), Is.EqualTo(1), "the crate isn't consumed until the build finishes");

            // ── drive the remaining work to completion (Σ engineer BuildRate × days ≥ the bunker's cost) ──
            int fullDrive = (int)Math.Ceiling(required / rate) * oneDay;
            GroundBeachhead.TickBuilds(body, fullDrive);

            var added = region.InstallationIds.Where(id => !beforeIds.Contains(id)).ToList();
            Assert.That(added.Count, Is.EqualTo(1), "the bunker is now built in the held region — exactly one new installation");
            int newId = added[0];
            Assert.That(GroundParts.PartCount(body, rgn, "default-design-bunker"), Is.EqualTo(0), "the crate was consumed on completion");
            Assert.That(forces.BuildSites.Count, Is.EqualTo(0), "the finished site is cleared");

            // ── COLONY-FREE HOST: the bunker lives in the invader's beachhead OUTPOST, not the colony store ──
            var colonyStore = s.Colony.GetDataBlob<ComponentInstancesDB>();
            Assert.That(colonyStore.AllComponents.Values.Any(i => i.ID == newId), Is.False, "the beachhead building is NOT hosted in a colony");
            Assert.That(forces.OutpostEntityIds.Count, Is.GreaterThanOrEqualTo(1), "a beachhead outpost host was created");
            Assert.That(GroundBuildings.BodyComponentStores(body).Any(st => st.AllComponents.Values.Any(i => i.ID == newId)), Is.True,
                "it's hosted in a beachhead outpost store, found by the shared body-store index");

            // ── FORTIFICATION readable after: the new bunker resolves to its GroundDefenseAtb and raises the region's fort ──
            var resolve = GroundFortification.BuildResolver(body);
            Assert.That(resolve(newId), Is.Not.Null, "the on-site bunker resolves to its GroundDefenseAtb (fortification finds it colony-free)");
            Assert.That(resolve(newId).LocalFortify, Is.GreaterThan(0), "and it actually fortifies");
            double multAfter = GroundFortification.DefenseMult(region, regions.Regions, fid, resolve);
            Assert.That(multAfter, Is.GreaterThan(multBefore), "the beachhead bunker added fortification to the held region");
            Assert.That(multAfter, Is.GreaterThan(1.0), "the region is fortified");
            Log($"bunker #{newId} built colony-free; region {rgn} fortification {multBefore:0.00}x → {multAfter:0.00}x");

            // ── FOB RESUPPLY POINT (the marker G2 consumes) ──
            Assert.That(GroundBeachhead.HasBeachhead(body, fid, rgn), Is.True, $"region {rgn} is now a FOB resupply point");

            // ── GRAVE RUNG: the bunker is a real bombard target — proves the outpost is indexed for bombard/capture ──
            var centreHex = region.Hexes.First(h => h.Q == 0 && h.R == 0);
            Assert.That(centreHex.InstallationIds.Contains(newId), Is.True, "the beachhead bunker sits on the region's war-map hex");
            int destroyed = GroundBuildings.BombardHex(body, rgn, 0, 0, 2.0);   // 2.0 > 1 full building → destroyed
            Assert.That(destroyed, Is.EqualTo(1), "the on-site bunker is a real bombard target (the outpost store is indexed)");
            Assert.That(region.InstallationIds.Contains(newId), Is.False, "the destroyed bunker is gone from the region");
            Assert.That(centreHex.InstallationIds.Contains(newId), Is.False, "and gone from the war-map hex");
            Assert.That(GroundBeachhead.HasBeachhead(body, fid, rgn), Is.False, "the resupply point is gone once the bunker is destroyed");
            var resolveAfter = GroundFortification.BuildResolver(body);
            Assert.That(GroundFortification.DefenseMult(region, regions.Regions, fid, resolveAfter), Is.EqualTo(multBefore).Within(1e-9),
                "fortification returns to its pre-beachhead value once the bunker is destroyed");
            Log($"grave rung: bombarded bunker #{newId} — destroyed and de-fortified");
        }

        [Test]
        [Description("No on-site build when the conditions aren't met: ground you don't hold, an enemy contesting the region, or no parts landed. The would-be crates are left untouched.")]
        public void NoBuild_WithoutHeldGround_WithAnEnemy_OrWithoutParts()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var body = s.StartingBody;
            int fid = s.Faction.Id;
            const int enemyFid = 900042;
            ComponentDesign Part(string id) => (ComponentDesign)faction.IndustryDesigns[id];

            PlanetHexFactory.EnsureHexesForBody(body);
            var regions = body.GetDataBlob<PlanetRegionsDB>();
            Assert.That(regions.Regions.Count, Is.GreaterThanOrEqualTo(3), "the world has 3+ regions for the three cases");

            var engDesign = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "test-eng-negatives", "Combat Engineer",
                Part("default-design-human-frame"),
                new List<(ComponentDesign, int)> { (Part("default-design-ground-constructor"), 1) });

            // CASE 1 — NOT friendly-held: region 0 owned by nobody; engineer + bunker parts there.
            regions.Regions[0].OwnerFactionID = -1;
            GroundForces.RaiseUnit(body, engDesign, fid, 0);
            GroundParts.AddParts(body, 0, "default-design-bunker", 1);

            // CASE 2 — ENEMY present: region 1 held by the faction, engineer + parts, but an enemy unit contests it.
            regions.Regions[1].OwnerFactionID = fid;
            GroundForces.RaiseUnit(body, engDesign, fid, 1);
            GroundParts.AddParts(body, 1, "default-design-bunker", 1);
            GroundForces.RaiseUnit(body, EnemyInfantry(), enemyFid, 1);

            // CASE 3 — NO parts: region 2 held by the faction, engineer standing, but nothing landed to build.
            regions.Regions[2].OwnerFactionID = fid;
            GroundForces.RaiseUnit(body, engDesign, fid, 2);

            int[] before =
            {
                regions.Regions[0].InstallationIds.Count,
                regions.Regions[1].InstallationIds.Count,
                regions.Regions[2].InstallationIds.Count,
            };

            // Drive well past a bunker's build time — even so, none of the three conditions permit a build.
            int longDrive = (int)Math.Ceiling(Part("default-design-bunker").IndustryPointCosts / 100.0) * oneDay * 2;
            GroundBeachhead.TickBuilds(body, longDrive);

            Assert.That(regions.Regions[0].InstallationIds.Count, Is.EqualTo(before[0]), "no build on ground you don't hold");
            Assert.That(regions.Regions[1].InstallationIds.Count, Is.EqualTo(before[1]), "no build while an enemy contests the region");
            Assert.That(regions.Regions[2].InstallationIds.Count, Is.EqualTo(before[2]), "no build with no parts landed");
            Assert.That(GroundParts.PartCount(body, 0, "default-design-bunker"), Is.EqualTo(1), "the un-held region's crate is untouched");
            Assert.That(GroundParts.PartCount(body, 1, "default-design-bunker"), Is.EqualTo(1), "the contested region's crate is untouched");
            Assert.That(body.GetDataBlob<GroundForcesDB>().OutpostEntityIds.Count, Is.EqualTo(0), "no outpost host was created (no build happened)");

            // FLIP case 2 to prove the enemy was the only blocker: remove the enemy, then the same region builds.
            var forces = body.GetDataBlob<GroundForcesDB>();
            forces.Units.RemoveAll(u => u.FactionOwnerID == enemyFid);
            GroundBeachhead.TickBuilds(body, longDrive);
            Assert.That(regions.Regions[1].InstallationIds.Count, Is.EqualTo(before[1] + 1),
                "once the enemy is gone the held region builds — the enemy was the only blocker");
            Log("no-build gates hold (un-held / contested / no-parts); clearing the enemy unblocks the contested region");
        }

        /// <summary>A monolithic enemy infantry (no components → no backing → never a combat engineer), used to contest a
        /// region. Mirrors the invader in <c>TakeAPlanetIntegrationTests</c>.</summary>
        private static GroundUnitDesign EnemyInfantry() => new GroundUnitDesign
        {
            UniqueID = "test-beachhead-enemy",
            Name = "Enemy Rifles",
            UnitType = GroundUnitType.Infantry,
            Attack = 50, Defense = 5, HitPoints = 300, Range = 1,
            IndustryPointCosts = 100, IndustryTypeID = "installation",
            ResourceCosts = new Dictionary<string, long>(),
        };
    }
}
