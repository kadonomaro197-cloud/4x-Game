using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Colonies;
using Pulsar4X.Factions;
using Pulsar4X.Industry;
using Pulsar4X.Storage;
using Pulsar4X.DataStructures;
using Pulsar4X.Hazards;
using Pulsar4X.GroundCombat;
using Pulsar4X.Components;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Ground combat, slice 5a — RAISE A UNIT. A ground unit is a buildable design (`GroundUnitDesign :
    /// IConstructableDesign`) that rides the existing industry rails; when a build completes it's placed on the
    /// colony's planet in a region (`GroundForcesDB`), stamped with owner + region + combat stats. These gauges
    /// prove the place-primitive, the build→place hook, and persistence. Design: docs/GROUND-COMBAT-MAP-DESIGN.md.
    /// </summary>
    [TestFixture]
    public class GroundForcesTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[ground] " + m);

        private static GroundUnitDesign MakeInfantryDesign() => new GroundUnitDesign
        {
            UniqueID = "test-ground-infantry",
            Name = "Test Rifles",
            UnitType = GroundUnitType.Infantry,
            Attack = 100,
            Defense = 10,
            HitPoints = 500,
            IndustryPointCosts = 100,
            IndustryTypeID = "installation",
            ResourceCosts = new Dictionary<string, long>(),
        };

        [Test]
        [Description("5a: RaiseUnit places a ground unit onto a body's region, creating the GroundForcesDB on demand, stamped with the owning faction + region + the design's combat stats (a full-health snapshot).")]
        public void RaiseUnit_PlacesAUnit_InARegion()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var design = MakeInfantryDesign();

            var unit = GroundForces.RaiseUnit(body, design, s.Faction.Id, regionIndex: 1);

            Assert.That(body.HasDataBlob<GroundForcesDB>(), Is.True, "raising a unit creates the ground-forces roster on the body");
            var forces = body.GetDataBlob<GroundForcesDB>();
            Assert.That(forces.Units.Count, Is.EqualTo(1));
            Assert.That(unit.FactionOwnerID, Is.EqualTo(s.Faction.Id));
            Assert.That(unit.RegionIndex, Is.EqualTo(1));
            Assert.That(unit.UnitType, Is.EqualTo(GroundUnitType.Infantry));
            Assert.That(unit.MaxHealth, Is.EqualTo(500));
            Assert.That(unit.Health, Is.EqualTo(500), "a fresh unit starts at full health");
            Log($"raised '{unit.Name}' ({unit.UnitType}) in region {unit.RegionIndex}: atk {unit.Attack}, hp {unit.Health}/{unit.MaxHealth}");
        }

        [Test]
        [Description("5a cradle-to-grave rung: completing the BUILD of a GroundUnitDesign at a colony places the unit on that colony's planet — the OnConstructionComplete hook the industry processor calls, exercised directly with a real IndustryJob.")]
        public void BuildingAGroundUnit_PlacesItOnTheColonysPlanet()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var design = MakeInfantryDesign();
            factionInfo.IndustryDesigns[design.UniqueID] = design;   // register so the job + faction can resolve it

            var body = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;
            int before = body.TryGetDataBlob<GroundForcesDB>(out var f0) ? f0.Units.Count : 0;

            // NumberOrdered = 2 so the job-lifecycle removal branch is skipped (no real production line needed here).
            var job = new IndustryJob(factionInfo, design.UniqueID) { NumberOrdered = 2 };
            var storage = s.Colony.GetDataBlob<CargoStorageDB>();

            design.OnConstructionComplete(s.Colony, storage, "ground-line", job, design);

            Assert.That(job.NumberCompleted, Is.EqualTo(1), "the batch records one unit completed");
            Assert.That(body.HasDataBlob<GroundForcesDB>(), Is.True, "the completed build created the planet's ground roster");
            var forces = body.GetDataBlob<GroundForcesDB>();
            Assert.That(forces.Units.Count, Is.EqualTo(before + 1), "a completed build placed exactly one unit on the planet");
            Assert.That(forces.Units.Last().FactionOwnerID, Is.EqualTo(s.Colony.FactionOwnerID), "the unit belongs to the building colony's faction");
            Log($"built ground unit placed on the colony's planet: {forces.Units.Count} unit(s) total");
        }

        [Test]
        [Description("5a persistence: the ground-forces roster deep-clones (survives save/load + entity transfer) — mutating a clone must not touch the original. The discipline the old colony hex map lacked.")]
        public void GroundForces_ClonesDeeply()
        {
            var forces = new GroundForcesDB();
            forces.Units.Add(new GroundUnit
            {
                DesignId = "x", Name = "Clones", FactionOwnerID = 1, RegionIndex = 0,
                UnitType = GroundUnitType.Armor, Attack = 200, Defense = 20, MaxHealth = 100, Health = 100,
            });

            var clone = (GroundForcesDB)forces.Clone();
            Assert.That(clone.Units.Count, Is.EqualTo(1));
            clone.Units[0].Health = 50;

            Assert.That(forces.Units[0].Health, Is.EqualTo(100),
                "the unit list was deep-cloned, not shared — the roster would corrupt on transfer/save otherwise");
        }

        private const int InvaderFaction = 900001;

        [Test]
        [Description("5b MOVE: a unit ordered to an ADJACENT region enters transit for that region's crossing time, then arrives once the processor has advanced enough game-seconds. Units traverse the surface on the map's real travel-time edges.")]
        public void MoveUnit_ToAdjacentRegion_ArrivesAfterCrossingTime()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            var regions = body.GetDataBlob<PlanetRegionsDB>().Regions;
            var unit = GroundForces.RaiseUnit(body, MakeInfantryDesign(), s.Faction.Id, regionIndex: 0);

            int destination = regions[0].Neighbors[0];   // an adjacent region on the ring
            bool ordered = GroundForces.OrderMove(body, unit, destination);
            Assert.That(ordered, Is.True, "moving to a neighbouring region is a valid order");
            Assert.That(unit.MovingToRegion, Is.EqualTo(destination));
            Assert.That(unit.TransitSecondsRemaining, Is.EqualTo(regions[0].CrossingTimeSeconds));

            var proc = new GroundForcesProcessor();
            proc.ProcessEntity(body, (int)regions[0].CrossingTimeSeconds + 1);   // advance past the crossing time

            Assert.That(unit.RegionIndex, Is.EqualTo(destination), "the unit has arrived in the destination region");
            Assert.That(unit.MovingToRegion, Is.EqualTo(-1), "and is no longer in transit");
            Log($"unit marched region 0 → {destination} over {regions[0].CrossingTimeSeconds:N0}s");
        }

        [Test]
        [Description("5b bounds: a unit cannot jump to a NON-adjacent region (v1 is one hop at a time along the ring) — the order is rejected and the unit stays put.")]
        public void MoveUnit_ToNonAdjacentRegion_IsRejected()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            var unit = GroundForces.RaiseUnit(body, MakeInfantryDesign(), s.Faction.Id, regionIndex: 0);

            // Region 2 is opposite region 0 on a 4-slice ring (neighbours of 0 are 1 and 3) — not adjacent.
            bool ordered = GroundForces.OrderMove(body, unit, 2);
            Assert.That(ordered, Is.False, "region 2 is not a neighbour of region 0");
            Assert.That(unit.MovingToRegion, Is.EqualTo(-1), "the unit did not enter transit");
        }

        [Test]
        [Description("5c FIGHT + 5d region CAPTURE: two opposing garrisons in one region resolve by strength-math — the stronger (more total attack) wipes the weaker over successive salvos — and the surviving faction then OWNS the region.")]
        public void RegionCombat_StrongerGarrisonWins_AndTakesTheRegion()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            var design = MakeInfantryDesign();

            // Defender (the player) fields 3 units in region 0; the invader fields 1 — same stats, so numbers decide.
            GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);
            GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);
            GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);
            GroundForces.RaiseUnit(body, design, InvaderFaction, 0);

            var proc = new GroundForcesProcessor();
            for (int i = 0; i < 8; i++) proc.ProcessEntity(body, 3600);   // 8 salvos

            var forces = body.GetDataBlob<GroundForcesDB>();
            Assert.That(forces.Units.Any(u => u.FactionOwnerID == InvaderFaction), Is.False, "the weaker invader garrison is wiped");
            Assert.That(forces.Units.Any(u => u.FactionOwnerID == s.Faction.Id), Is.True, "the stronger defender survives");
            Assert.That(body.GetDataBlob<PlanetRegionsDB>().Regions[0].OwnerFactionID, Is.EqualTo(s.Faction.Id),
                "the surviving faction owns the contested region");
            Log($"region 0 held by faction {s.Faction.Id}; {forces.Units.Count} defender unit(s) survived");
        }

        [Test]
        [Description("5d WHOLE-PLANET CAPTURE (the 'you can take a planet' moment): when every region of a world is held by a single invader, the planet's colony flips to that faction.")]
        public void WholePlanetCapture_FlipsTheColony_WhenAllRegionsHeld()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;
            var regions = body.GetDataBlob<PlanetRegionsDB>().Regions;

            Assert.That(s.Colony.FactionOwnerID, Is.EqualTo(s.Faction.Id), "precondition: the colony starts owned by the player");

            // The invader holds every region (an uncontested landing across the whole surface), with a unit present
            // so the ground processor runs on this body.
            foreach (var r in regions) r.OwnerFactionID = InvaderFaction;
            GroundForces.RaiseUnit(body, MakeInfantryDesign(), InvaderFaction, 0);

            new GroundForcesProcessor().ProcessEntity(body, 3600);

            Assert.That(s.Colony.FactionOwnerID, Is.EqualTo(InvaderFaction),
                "with every region held by the invader, the colony (the planet) is taken");
            Log($"planet taken — colony now faction {s.Colony.FactionOwnerID}");
        }

        // ───────────────────────── 5f/5g — TERRAIN + the ground triangle ─────────────────────────

        [Test]
        [Description("5g: the classic ground triangle Armor ▸ Infantry ▸ Artillery ▸ Armor — the winning attacker deals more, the reverse pairing less, same type neutral.")]
        public void Triangle_ClassicCycle_ArmorBeatsInfantryBeatsArtilleryBeatsArmor()
        {
            Assert.That(GroundTerrain.TriangleMult(GroundUnitType.Armor, GroundUnitType.Infantry), Is.GreaterThan(1.0));
            Assert.That(GroundTerrain.TriangleMult(GroundUnitType.Infantry, GroundUnitType.Artillery), Is.GreaterThan(1.0));
            Assert.That(GroundTerrain.TriangleMult(GroundUnitType.Artillery, GroundUnitType.Armor), Is.GreaterThan(1.0));
            // the reverse pairings are the disadvantage
            Assert.That(GroundTerrain.TriangleMult(GroundUnitType.Infantry, GroundUnitType.Armor), Is.LessThan(1.0));
            Assert.That(GroundTerrain.TriangleMult(GroundUnitType.Artillery, GroundUnitType.Infantry), Is.LessThan(1.0));
            Assert.That(GroundTerrain.TriangleMult(GroundUnitType.Armor, GroundUnitType.Artillery), Is.LessThan(1.0));
            // same type is neutral
            Assert.That(GroundTerrain.TriangleMult(GroundUnitType.Infantry, GroundUnitType.Infantry), Is.EqualTo(1.0));
        }

        [Test]
        [Description("5f: terrain classifies from a region's dominant feature, and the combat dials mirror it — rough terrain gives the defender more cover, armour fights worse in the rough, artillery gains from high ground.")]
        public void Terrain_ClassifiesFeatures_AndCoverAndAffinityFollow()
        {
            var mountains = new Region(); mountains.Features.Add(new RegionFeature(RegionFeatureType.Mountains, 1.0));
            var plains = new Region(); plains.Features.Add(new RegionFeature(RegionFeatureType.Plains, 1.0));
            var forest = new Region(); forest.Features.Add(new RegionFeature(RegionFeatureType.Forest, 1.0));

            Assert.That(GroundTerrain.Classify(mountains), Is.EqualTo(GroundTerrainClass.Rough));
            Assert.That(GroundTerrain.Classify(plains), Is.EqualTo(GroundTerrainClass.Open));
            Assert.That(GroundTerrain.Classify(forest), Is.EqualTo(GroundTerrainClass.Cover));

            Assert.That(GroundTerrain.CoverDefenseMult(GroundTerrainClass.Rough),
                Is.GreaterThan(GroundTerrain.CoverDefenseMult(GroundTerrainClass.Open)), "rough terrain protects the defender more than open");
            Assert.That(GroundTerrain.TerrainAttackMult(GroundUnitType.Armor, GroundTerrainClass.Rough),
                Is.LessThan(GroundTerrain.TerrainAttackMult(GroundUnitType.Armor, GroundTerrainClass.Open)), "armour is worse in the rough than the open");
            Assert.That(GroundTerrain.TerrainAttackMult(GroundUnitType.Artillery, GroundTerrainClass.Rough), Is.GreaterThan(1.0), "artillery gains from high ground");
        }

        [Test]
        [Description("5f INTEGRATION: the SAME fight favours the defender in rough terrain — a defender in the mountains takes less than the identical defender on the open plains.")]
        public void Terrain_RoughFavoursTheDefender_InAFight()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();   // isolate terrain from attrition
            var regions = body.GetDataBlob<PlanetRegionsDB>().Regions;

            regions[0].Features.Clear(); regions[0].Features.Add(new RegionFeature(RegionFeatureType.Mountains, 1.0));
            regions[1].Features.Clear(); regions[1].Features.Add(new RegionFeature(RegionFeatureType.Plains, 1.0));
            regions[0].OwnerFactionID = s.Faction.Id;
            regions[1].OwnerFactionID = s.Faction.Id;

            var design = MakeInfantryDesign();
            GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);
            GroundForces.RaiseUnit(body, design, InvaderFaction, 0);
            GroundForces.RaiseUnit(body, design, s.Faction.Id, 1);
            GroundForces.RaiseUnit(body, design, InvaderFaction, 1);

            var proc = new GroundForcesProcessor();
            for (int i = 0; i < 3; i++) proc.ProcessEntity(body, 3600);

            var forces = body.GetDataBlob<GroundForcesDB>();
            var mountainDefender = forces.Units.FirstOrDefault(u => u.FactionOwnerID == s.Faction.Id && u.RegionIndex == 0);
            var plainsDefender = forces.Units.FirstOrDefault(u => u.FactionOwnerID == s.Faction.Id && u.RegionIndex == 1);
            Assert.That(mountainDefender, Is.Not.Null, "the mountain defender is still standing");
            Assert.That(plainsDefender, Is.Not.Null, "the plains defender is still standing");
            Assert.That(mountainDefender.Health, Is.GreaterThan(plainsDefender.Health),
                "the same defender takes LESS damage in the mountains than on the open plains — terrain favours the defender");
            Log($"terrain: mountain defender {mountainDefender.Health:0} hp vs plains defender {plainsDefender.Health:0} hp after 3 salvos");
        }

        [Test]
        [Description("5h fortification is DESIGN-DRIVEN: a Bunker (GroundDefenseAtb) fortifies its region + projects to ADJACENT FRIENDLY regions; a plain building (no attribute) does nothing; an enemy-held neighbour doesn't project; the bonus is capped. Pure math via GroundFortification.DefenseMult with a stub resolver (no JSON needed).")]
        public void Fortification_DesignDriven_LocalPlusAdjacentCappedAndOwned()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            var regions = body.GetDataBlob<PlanetRegionsDB>().Regions;

            // A Bunker = +25% local / +12% adjacent; ids 1 & 2 are bunkers, id 9 is a plain (non-defence) building.
            var bunker = new GroundDefenseAtb(0.25, 0.12);
            System.Func<int, GroundDefenseAtb> resolve = id => (id == 1 || id == 2) ? bunker : null;

            int fac = s.Faction.Id;
            regions[0].InstallationIds.Clear(); regions[0].InstallationIds.Add(1); regions[0].InstallationIds.Add(9);
            regions[1].InstallationIds.Clear(); regions[1].InstallationIds.Add(2);
            regions[0].OwnerFactionID = fac;
            regions[1].OwnerFactionID = fac;   // a FRIENDLY neighbour → projects
            Assert.That(regions[0].Neighbors.Contains(1), Is.True, "ring adjacency 0↔1");

            // Local +0.25 (the plain building adds 0) + adjacent-friendly +0.12 = ×1.37.
            Assert.That(GroundFortification.DefenseMult(regions[0], regions, fac, resolve), Is.EqualTo(1.37).Within(1e-9),
                "local +25% + adjacent friendly +12%");

            // An ENEMY-held neighbour doesn't project → only local +25%.
            regions[1].OwnerFactionID = InvaderFaction;
            Assert.That(GroundFortification.DefenseMult(regions[0], regions, fac, resolve), Is.EqualTo(1.25).Within(1e-9),
                "an enemy neighbour's bunkers don't fortify you");

            // A plain (non-defence) building alone = no fortification.
            regions[2].InstallationIds.Clear(); regions[2].InstallationIds.Add(9);
            regions[2].OwnerFactionID = fac;
            Assert.That(GroundFortification.DefenseMult(regions[2], regions, fac, resolve), Is.EqualTo(1.0).Within(1e-9),
                "a non-defence building does not fortify");

            // Cap: pile many bunkers into region 3 → capped at 1 + Cap.
            regions[3].InstallationIds.Clear();
            for (int i = 100; i < 120; i++) regions[3].InstallationIds.Add(i);
            regions[3].OwnerFactionID = fac;
            System.Func<int, GroundDefenseAtb> allBunkers = id => bunker;
            Assert.That(GroundFortification.DefenseMult(regions[3], regions, fac, allBunkers), Is.EqualTo(1.0 + GroundFortification.Cap).Within(1e-9),
                "fortification is capped");

            Log($"design-driven fortification: Bunker local+adj ×1.37, enemy-neighbour local-only ×1.25, plain-building ×1.00");
        }

        [Test]
        [Description("5h fortification CRADLE-TO-GRAVE: build the base-mod BUNKER from the faction's designs (proves the JSON→GroundDefenseAtb binding), install it in a region, and a real fight favours the fortified region's defender over an identical unfortified one.")]
        public void Fortification_BaseModBunker_BuildsAndWinsTheFight()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();
            var regions = body.GetDataBlob<PlanetRegionsDB>().Regions;

            regions[0].Features.Clear(); regions[0].Features.Add(new RegionFeature(RegionFeatureType.Plains, 1.0));
            regions[1].Features.Clear(); regions[1].Features.Add(new RegionFeature(RegionFeatureType.Plains, 1.0));
            regions[0].OwnerFactionID = s.Faction.Id;
            regions[1].OwnerFactionID = s.Faction.Id;

            // The base-mod Bunker is a real ComponentDesign in the faction's designs, and it carries the attribute
            // (this asserts the six-point JSON registration + the AtbConstrArgs binding all worked).
            var fi = s.Faction.GetDataBlob<FactionInfoDB>();
            Assert.That(fi.IndustryDesigns.ContainsKey("default-design-bunker"), Is.True, "the bunker design is unlocked");
            var bunkerDesign = (ComponentDesign)fi.IndustryDesigns["default-design-bunker"];
            Assert.That(bunkerDesign.HasAttribute<GroundDefenseAtb>(), Is.True, "the JSON bound a GroundDefenseAtb onto the bunker");
            var atb = bunkerDesign.GetAttribute<GroundDefenseAtb>();
            Assert.That(atb.LocalFortify, Is.EqualTo(0.25).Within(1e-6), "bunker local +25%");
            Assert.That(atb.AdjacentProjection, Is.EqualTo(0.12).Within(1e-6), "bunker adjacent +12%");

            // Install a bunker in region 0 (the start colony sits on this body) and record it there.
            var colony = s.Colony;
            var instance = new ComponentInstance(bunkerDesign);
            colony.AddComponent(instance);
            regions[0].InstallationIds.Clear(); regions[0].InstallationIds.Add(instance.ID);
            regions[1].InstallationIds.Clear();

            var design = MakeInfantryDesign();
            GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);
            GroundForces.RaiseUnit(body, design, InvaderFaction, 0);
            GroundForces.RaiseUnit(body, design, s.Faction.Id, 1);
            GroundForces.RaiseUnit(body, design, InvaderFaction, 1);

            var proc = new GroundForcesProcessor();
            for (int i = 0; i < 3; i++) proc.ProcessEntity(body, 3600);

            var forces = body.GetDataBlob<GroundForcesDB>();
            var bunkered = forces.Units.FirstOrDefault(u => u.FactionOwnerID == s.Faction.Id && u.RegionIndex == 0);
            var open = forces.Units.FirstOrDefault(u => u.FactionOwnerID == s.Faction.Id && u.RegionIndex == 1);
            Assert.That(bunkered, Is.Not.Null, "the bunkered defender survives");
            Assert.That(open, Is.Not.Null, "the open-ground defender survives");
            Assert.That(bunkered.Health, Is.GreaterThan(open.Health),
                "a real Bunker fortifies its region — its defender takes less than the identical unfortified one");
            Log($"bunker cradle-to-grave: bunkered defender {bunkered.Health:0} hp vs open {open.Health:0} hp after 3 salvos");
        }

        [Test]
        [Description("#5 LOCKED principle: the start colony's existing installations get a home region at creation (ColonyFactory hook), so Region.InstallationIds is non-empty (they draw on the planet view + count for fortification). Located in the capital region; idempotent (re-running adds nothing).")]
        public void Installations_LocatedInCapitalRegion_AtColonyCreation()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            Assert.That(body.HasDataBlob<PlanetRegionsDB>(), Is.True, "the home body has a region layer");
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();

            int located = regionsDB.Regions.Sum(r => r.InstallationIds.Count);
            Assert.That(located, Is.GreaterThan(0),
                "the start colony's installations are located on the ground (so they draw on the map + count for fortification)");
            Assert.That(regionsDB.Regions[0].InstallationIds.Count, Is.GreaterThan(0), "they land in the capital region (0)");

            // Idempotent — re-running the locator places nothing new (map-placed buildings keep their own region).
            int again = GroundInstallations.LocateColonyInstallations(s.Colony);
            Assert.That(again, Is.EqualTo(0), "re-locating adds nothing (idempotent)");

            Log($"located {located} start installation(s) in the capital region at colony creation");
        }

        [Test]
        [Description("Default HOME GARRISON: RaiseForFactionColonies gives the faction's home colony a starting ground garrison in the capital region (the New-Game default so the tactical map isn't empty). NOT auto-raised by the harness (runs on the New-Game path only); idempotent.")]
        public void StartGarrison_RaisedOnFactionHomeColony()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            Assert.That(body.HasDataBlob<PlanetRegionsDB>(), Is.True, "the home body has a region layer");

            int expected = GroundStartGarrison.Composition.Sum(c => c.count);
            int raised = GroundStartGarrison.RaiseForFactionColonies(s.Game, s.Faction);
            Assert.That(raised, Is.EqualTo(expected), "the whole garrison composition is raised");

            var forces = body.GetDataBlob<GroundForcesDB>();
            int mine = forces.Units.Count(u => u.FactionOwnerID == s.Faction.Id);
            Assert.That(mine, Is.EqualTo(expected), "the garrison belongs to the player faction");
            Assert.That(forces.Units.Where(u => u.FactionOwnerID == s.Faction.Id).All(u => u.RegionIndex == 0), Is.True,
                "the garrison musters in the capital region (0)");

            // Idempotent — re-running raises nothing (already garrisoned).
            int again = GroundStartGarrison.RaiseForFactionColonies(s.Game, s.Faction);
            Assert.That(again, Is.EqualTo(0), "idempotent — no double garrison");

            Log($"home garrison: raised {raised} unit(s) in the capital region");
        }

        // ───────────────────────── E1/E2/E3 — planetary ENVIRONMENTS (the ground hazard layer) ─────────────────────────

        [Test]
        [Description("E1: the planet-environments layer deep-clones (survives save/load) — the persistence discipline, on the dynamic-hazard layer.")]
        public void PlanetEnvironments_ClonesDeeply()
        {
            var env = new PlanetEnvironmentsDB();
            env.Environments.Add(new RegionEnvironment(0, "Fire Tornadoes", HazardEffectType.HeatDamage, 40));
            var clone = (PlanetEnvironmentsDB)env.Clone();
            Assert.That(clone.Environments.Count, Is.EqualTo(1));
            clone.Environments[0].Magnitude = 999;
            Assert.That(env.Environments[0].Magnitude, Is.EqualTo(40), "the effect list was deep-cloned, not shared");
        }

        [Test]
        [Description("E2 — THE LOAD-BEARING GATE: a gas/ice giant has no surface, so the physics generator emits NO surface environments for it. Meanwhile a surface world can carry hazards.")]
        public void EnvironmentGeneration_GasGiantHasNoSurfaceHazards()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            PlanetEnvironmentFactory.GenerateForSystem(s.StartingSystem);   // idempotent

            var gasGiants = s.StartingSystem.GetAllEntitiesWithDataBlob<SystemBodyInfoDB>()
                .Where(b => b.TryGetDataBlob<SystemBodyInfoDB>(out var info)
                            && (info.BodyType == BodyType.GasGiant || info.BodyType == BodyType.IceGiant || info.BodyType == BodyType.GasDwarf))
                .ToList();
            Assert.That(gasGiants.Count, Is.GreaterThan(0), "Sol has gas/ice giants");
            Assert.That(gasGiants.All(b => !b.HasDataBlob<PlanetEnvironmentsDB>()), Is.True,
                "a gas giant has no surface → the generator emits no surface environments for it (the load-bearing gate)");
            Log($"gate: {gasGiants.Count} giant(s), 0 with surface environments");
        }

        [Test]
        [Description("E2: environments are a fingerprint of PHYSICS — force a world scorching and the generator gives it fire/thermal hazards (guaranteed in at least one region).")]
        public void EnvironmentGeneration_ScorchingWorld_GetsFireHazards()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;

            Assert.That(body.TryGetDataBlob<AtmosphereDB>(out var atmo), Is.True, "the start world has an atmosphere");
            atmo.SurfaceTemperature = 500f;                                        // make it scorching (internal set, via InternalsVisibleTo)
            if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();
            PlanetEnvironmentFactory.GenerateForSystem(s.StartingSystem);          // regenerate this now-hostile world

            Assert.That(body.HasDataBlob<PlanetEnvironmentsDB>(), Is.True);
            var envs = body.GetDataBlob<PlanetEnvironmentsDB>().Environments;
            Assert.That(envs.Any(e => e.Effect == HazardEffectType.HeatDamage), Is.True, "a 500°C world gets fire/thermal hazards");
            Log($"scorching world → {envs.Count} environmental hazard(s), incl. {envs.First(e => e.Effect == HazardEffectType.HeatDamage).Name}");
        }

        [Test]
        [Description("E3: a unit STANDING in a region with a damaging environmental hazard bleeds health each tick — the ground twin of a ship taking damage inside a space hazard.")]
        public void Environment_DamagesAUnitStandingInIt()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;

            if (!body.TryGetDataBlob<PlanetEnvironmentsDB>(out var envDB))
            {
                envDB = new PlanetEnvironmentsDB();
                body.SetDataBlob(envDB);
            }
            envDB.Environments.Add(new RegionEnvironment(0, "Test Inferno", HazardEffectType.HeatDamage, 100.0));   // 100/hour

            var unit = GroundForces.RaiseUnit(body, MakeInfantryDesign(), s.Faction.Id, 0);
            double before = unit.Health;

            new GroundForcesProcessor().ProcessEntity(body, 3600);   // one hour

            Assert.That(unit.Health, Is.LessThan(before), "a unit in a fire hazard loses health");
            Assert.That(unit.Health, Is.EqualTo(before - 100.0).Within(1.0), "one hour of a 100/hr hazard = 100 attrition");
            Log($"environmental attrition: unit {before:0} → {unit.Health:0} hp after 1 hour in a fire hazard");
        }

        [Test]
        [Description("E4 cradle-to-grave counter: a unit built from a HEAT-SHIELDED design (EnvironmentalResistance {HeatDamage:0.8}) bleeds far less in a fire hazard than an unprotected one, and an IMMUNE (1.0) unit takes zero — the ground echo of a ship's HazardResistanceAtb. Resistance is snapshotted onto the raised unit and survives a clone.")]
        public void EnvironmentalGear_ReducesHazardAttrition()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;

            if (!body.TryGetDataBlob<PlanetEnvironmentsDB>(out var envDB))
            {
                envDB = new PlanetEnvironmentsDB();
                body.SetDataBlob(envDB);
            }
            envDB.Environments.Add(new RegionEnvironment(0, "Test Inferno", HazardEffectType.HeatDamage, 100.0));   // 100/hour

            var plain = GroundForces.RaiseUnit(body, MakeInfantryDesign(), s.Faction.Id, 0);

            var shielded = MakeInfantryDesign();
            shielded.UniqueID = "test-ground-infantry-heatshield";
            shielded.EnvironmentalResistance = new Dictionary<HazardEffectType, double> { { HazardEffectType.HeatDamage, 0.8 } };
            var geared = GroundForces.RaiseUnit(body, shielded, s.Faction.Id, 0);

            var immuneDesign = MakeInfantryDesign();
            immuneDesign.UniqueID = "test-ground-infantry-immune";
            immuneDesign.EnvironmentalResistance = new Dictionary<HazardEffectType, double> { { HazardEffectType.HeatDamage, 1.0 } };
            var immune = GroundForces.RaiseUnit(body, immuneDesign, s.Faction.Id, 0);

            // Resistance is snapshotted onto the unit (and must survive a clone of the roster).
            Assert.That(geared.ResistanceTo(HazardEffectType.HeatDamage), Is.EqualTo(0.8).Within(1e-9), "gear snapshots onto the unit");
            var rosterClone = (GroundForcesDB)body.GetDataBlob<GroundForcesDB>().Clone();
            Assert.That(rosterClone.Units.Find(u => u.DesignId == "test-ground-infantry-heatshield").ResistanceTo(HazardEffectType.HeatDamage),
                Is.EqualTo(0.8).Within(1e-9), "gear survives a clone");

            double plain0 = plain.Health, geared0 = geared.Health, immune0 = immune.Health;
            new GroundForcesProcessor().ProcessEntity(body, 3600);   // one hour

            Assert.That(plain0 - plain.Health, Is.EqualTo(100.0).Within(1.0), "unprotected: full 100/hr attrition");
            Assert.That(geared0 - geared.Health, Is.EqualTo(20.0).Within(1.0), "0.8 heat-shield negates 80% → 20 attrition");
            Assert.That(immune0 - immune.Health, Is.EqualTo(0.0).Within(1e-6), "1.0 resistance = immune, zero attrition");
            Assert.That(geared.Health, Is.GreaterThan(plain.Health), "geared unit outlasts the unprotected one");
            Log($"E4 gear: plain -{plain0 - plain.Health:0}, shielded(0.8) -{geared0 - geared.Health:0}, immune(1.0) -{immune0 - immune.Health:0} hp/hr in fire");
        }

        // ───────────────────────── 5h — FORMATIONS (the ground echo of fleet grouping) ─────────────────────────

        [Test]
        [Description("5h formations mirror the fleet's SHAPE: create a named formation, assign units (first = leader/flagship), move the whole block with ONE order, and on the leader's death leadership reassigns to a survivor (fleet-like, no penalty). Membership + leader survive a clone.")]
        public void Formation_MirrorsFleet_CreateAssignMoveAndLeaderReassign()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();   // no attrition during the long move tick

            var design = MakeInfantryDesign();
            var u1 = GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);
            var u2 = GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);
            var u3 = GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);

            // Units get stable ids (the ground echo of entity ids).
            Assert.That(new[] { u1.UnitId, u2.UnitId, u3.UnitId }.Distinct().Count(), Is.EqualTo(3), "each unit gets a unique id");

            // Create + assign (first assigned = leader, the flagship default).
            var formation = GroundForces.CreateFormation(body, s.Faction.Id, "1st Armoured");
            Assert.That(GroundForces.AssignUnit(formation, u1), Is.True);
            Assert.That(GroundForces.AssignUnit(formation, u2), Is.True);
            Assert.That(GroundForces.AssignUnit(formation, u3), Is.True);

            var forces = body.GetDataBlob<GroundForcesDB>();
            Assert.That(GroundFormationTools.MemberCount(forces, formation), Is.EqualTo(3), "all three joined");
            Assert.That(formation.LeaderUnitId, Is.EqualTo(u1.UnitId), "the first unit assigned is the leader (flagship default)");
            Assert.That(GroundFormationTools.Leader(forces, formation).UnitId, Is.EqualTo(u1.UnitId));

            // ONE order marches the whole block to an adjacent region.
            int moved = GroundForces.OrderFormationMove(body, formation, 1);
            Assert.That(moved, Is.EqualTo(3), "the formation moves as one — all three marched");
            Assert.That(forces.Units.Where(u => u.FormationId == formation.FormationId).All(u => u.MovingToRegion == 1), Is.True);

            var proc = new GroundForcesProcessor();
            proc.ProcessEntity(body, 100 * 24 * 3600);   // long tick so the march completes
            Assert.That(forces.Units.Where(u => u.FormationId == formation.FormationId).All(u => u.RegionIndex == 1), Is.True,
                "the whole formation arrived in region 1");

            // Leader dies → leadership reassigns to a surviving member (fleet-like).
            u1.Health = 0;
            proc.ProcessEntity(body, 3600);
            Assert.That(forces.Units.Any(u => u.UnitId == u1.UnitId), Is.False, "the dead leader is removed");
            Assert.That(formation.LeaderUnitId, Is.Not.EqualTo(-1), "the formation still has a leader");
            Assert.That(formation.LeaderUnitId, Is.AnyOf(u2.UnitId, u3.UnitId), "leadership passed to a survivor");
            Assert.That(GroundFormationTools.MemberCount(forces, formation), Is.EqualTo(2), "two members remain");

            // Membership + leader survive a clone (save-safety).
            var clone = (GroundForcesDB)forces.Clone();
            var clonedFormation = clone.Formations.Find(f => f.FormationId == formation.FormationId);
            Assert.That(clonedFormation, Is.Not.Null);
            Assert.That(clonedFormation.LeaderUnitId, Is.EqualTo(formation.LeaderUnitId), "leader survives clone");
            Assert.That(GroundFormationTools.MemberCount(clone, clonedFormation), Is.EqualTo(2), "membership survives clone");

            Log($"formation '1st Armoured': moved 3 as one, leader reassigned {u1.UnitId}→{formation.LeaderUnitId} on death, 2 survive");
        }

        [Test]
        [Description("5h formation STANCE mirrors the fleet doctrine catalog: the moddable GroundStance catalog loads from JSON (±25% trade-off), TrySetStance applies the mults + honours the switch cooldown, and a DIG-IN (defensive) formation's defender takes less than an identical no-stance defender in the same fight.")]
        public void FormationStance_Catalog_TrySet_AndDefensiveSoaksLess()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();
            var regions = body.GetDataBlob<PlanetRegionsDB>().Regions;

            regions[0].Features.Clear(); regions[0].Features.Add(new RegionFeature(RegionFeatureType.Plains, 1.0));
            regions[1].Features.Clear(); regions[1].Features.Add(new RegionFeature(RegionFeatureType.Plains, 1.0));
            regions[0].OwnerFactionID = s.Faction.Id;
            regions[1].OwnerFactionID = s.Faction.Id;

            // The moddable catalog loaded from groundStances.json (the fleet-doctrine mirror).
            var stances = s.Game.StartingGameData.GroundStances;
            Assert.That(stances.ContainsKey("ground-offensive") && stances.ContainsKey("ground-defensive") && stances.ContainsKey("ground-balanced"),
                Is.True, "the ground stance catalog loaded from JSON");
            Assert.That(stances["ground-offensive"].AttackMult, Is.EqualTo(1.25).Within(1e-9), "offensive deals +25%");
            Assert.That(stances["ground-offensive"].DamageTakenMult, Is.EqualTo(1.25).Within(1e-9), "offensive takes +25%");
            Assert.That(stances["ground-defensive"].DamageTakenMult, Is.EqualTo(0.75).Within(1e-9), "defensive takes -25%");

            var design = MakeInfantryDesign();
            var d0 = GroundForces.RaiseUnit(body, design, s.Faction.Id, 0);
            GroundForces.RaiseUnit(body, design, InvaderFaction, 0);
            GroundForces.RaiseUnit(body, design, s.Faction.Id, 1);
            GroundForces.RaiseUnit(body, design, InvaderFaction, 1);
            var forces = body.GetDataBlob<GroundForcesDB>();

            // Form up region-0 defender and set DIG IN; the cooldown then blocks an immediate re-switch.
            var formation = GroundForces.CreateFormation(body, s.Faction.Id, "Home Guard");
            GroundForces.AssignUnit(formation, d0);
            var now = s.Game.TimePulse.GameGlobalDateTime;
            Assert.That(GroundFormationDoctrine.TrySetStance(formation, stances["ground-defensive"], now), Is.True, "stance applied");
            Assert.That(formation.DamageTakenMult, Is.EqualTo(0.75).Within(1e-9), "the mult is cached on the formation");
            Assert.That(GroundFormationDoctrine.TrySetStance(formation, stances["ground-offensive"], now), Is.False, "cooldown blocks an immediate switch");

            var proc = new GroundForcesProcessor();
            for (int i = 0; i < 3; i++) proc.ProcessEntity(body, 3600);

            var dug = forces.Units.FirstOrDefault(u => u.FactionOwnerID == s.Faction.Id && u.RegionIndex == 0);
            var open = forces.Units.FirstOrDefault(u => u.FactionOwnerID == s.Faction.Id && u.RegionIndex == 1);
            Assert.That(dug, Is.Not.Null, "the dug-in defender survives");
            Assert.That(open, Is.Not.Null, "the no-stance defender survives");
            Assert.That(dug.Health, Is.GreaterThan(open.Health),
                "the Dig-In (defensive) formation takes LESS than the identical no-stance defender in the same fight");
            Log($"stance: dig-in defender {dug.Health:0} hp vs no-stance defender {open.Health:0} hp after 3 salvos");
        }

        // ───────────────────────── HEX MOVEMENT + PATHFINDING (H2) ─────────────────────────
        //
        // A ground unit stands on a HEX within its region (H1's Region.Hexes); the pathfinder plots a terrain-weighted
        // A* path across those hexes, crossing region borders at the patch-edge gates (seam-free ring). Cost depends on
        // the unit's MovementDomain (a tank bogs in mountains and can't cross ocean; an aircraft flies straight).

        /// <summary>Build a region as a hex DISK of one uniform terrain, with the ring's west/east neighbour indices.</summary>
        private static Region MakeDiskRegion(int index, int radius, RegionFeatureType fill, double crossing, int westNbr, int eastNbr)
        {
            var reg = new Region { Index = index, CrossingTimeSeconds = crossing, Surveyed = true };
            reg.Neighbors.Add(westNbr); // [0] = west  (PlanetRegionsFactory convention)
            reg.Neighbors.Add(eastNbr); // [1] = east
            reg.Features.Add(new RegionFeature(fill, 1.0));
            for (int q = -radius; q <= radius; q++)
            {
                int rLo = System.Math.Max(-radius, -q - radius);
                int rHi = System.Math.Min(radius, -q + radius);
                for (int r = rLo; r <= rHi; r++)
                    reg.Hexes.Add(new GroundHex(q, r, fill));
            }
            return reg;
        }

        private static void SetHexTerrain(Region reg, int q, int r, RegionFeatureType t)
        {
            foreach (var h in reg.Hexes) if (h.Q == q && h.R == r) { h.Terrain = t; return; }
        }

        [Test]
        [Description("H2: the developer-approved terrain cost scheme is UNIT-dependent — land is slowed by rough ground and stopped by ocean; water floats only on ocean/coast; air flies over everything flat.")]
        public void HexMovement_TerrainCost_MatchesApprovedScheme_PerDomain()
        {
            // LAND
            Assert.That(HexMovement.TerrainCost(MovementDomain.Land, RegionFeatureType.Plains), Is.EqualTo(1.0), "open ×1");
            Assert.That(HexMovement.TerrainCost(MovementDomain.Land, RegionFeatureType.Forest), Is.EqualTo(1.6), "vegetated ×1.6");
            Assert.That(HexMovement.TerrainCost(MovementDomain.Land, RegionFeatureType.Mountains), Is.EqualTo(2.5), "elevated ×2.5");
            Assert.That(HexMovement.TerrainCost(MovementDomain.Land, RegionFeatureType.Ice), Is.EqualTo(2.0), "ice ×2");
            Assert.That(double.IsPositiveInfinity(HexMovement.TerrainCost(MovementDomain.Land, RegionFeatureType.Ocean)), Is.True, "ocean impassable to land");
            // WATER
            Assert.That(HexMovement.TerrainCost(MovementDomain.Water, RegionFeatureType.Ocean), Is.EqualTo(1.0), "water on ocean ×1");
            Assert.That(HexMovement.TerrainCost(MovementDomain.Water, RegionFeatureType.Coast), Is.EqualTo(1.0), "coast is water-passable");
            Assert.That(double.IsPositiveInfinity(HexMovement.TerrainCost(MovementDomain.Water, RegionFeatureType.Plains)), Is.True, "water can't go on land");
            // AIR
            Assert.That(HexMovement.TerrainCost(MovementDomain.Air, RegionFeatureType.Mountains), Is.EqualTo(1.0), "air flies over mountains flat");
            Assert.That(HexMovement.TerrainCost(MovementDomain.Air, RegionFeatureType.Ocean), Is.EqualTo(1.0), "air flies over ocean flat");
            // COAST is passable to both land and water (the boundary).
            Assert.That(HexMovement.IsPassable(MovementDomain.Land, RegionFeatureType.Coast), Is.True);
            Assert.That(HexMovement.IsPassable(MovementDomain.Water, RegionFeatureType.Coast), Is.True);
            Log("terrain cost: land plains 1 / forest 1.6 / mtn 2.5 / ocean ∞; water ocean 1 / land ∞; air flat 1");
        }

        [Test]
        [Description("H2: a raised unit musters at its region's hex-patch centre (0,0) and snapshots its design's movement domain (Land by default); the position + domain survive a clone.")]
        public void RaiseUnit_StampsHexCentre_AndDomain()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var land = GroundForces.RaiseUnit(body, MakeInfantryDesign(), s.Faction.Id, regionIndex: 0);
            Assert.That(land.HexQ, Is.EqualTo(0));
            Assert.That(land.HexR, Is.EqualTo(0), "a fresh unit musters at the patch centre");
            Assert.That(land.Domain, Is.EqualTo(MovementDomain.Land), "Infantry is a land unit");

            var airDesign = MakeInfantryDesign();
            airDesign.Domain = MovementDomain.Air;
            var air = GroundForces.RaiseUnit(body, airDesign, s.Faction.Id, regionIndex: 0);
            Assert.That(air.Domain, Is.EqualTo(MovementDomain.Air), "domain snapshotted from the design");

            var forces = body.GetDataBlob<GroundForcesDB>();
            var clone = (GroundForcesDB)forces.Clone();
            var airClone = clone.Units.First(u => u.UnitId == air.UnitId);
            Assert.That(airClone.Domain, Is.EqualTo(MovementDomain.Air), "domain deep-copied");
            Log($"raise: land unit hex ({land.HexQ},{land.HexR}) domain {land.Domain}; air unit domain {air.Domain}");
        }

        [Test]
        [Description("H2: A* over one region's hexes finds a straight open path, and the total time ≈ the region's coarse CrossingTimeSeconds (the fine map stays consistent with the strategic one).")]
        public void FindPath_WithinRegion_StraightAcross_TotalsTheCrossingTime()
        {
            double crossing = 8000;
            var reg = MakeDiskRegion(0, 2, RegionFeatureType.Plains, crossing, westNbr: -1, eastNbr: -1);
            var regions = new PlanetRegionsDB(new List<Region> { reg });

            var path = HexPathfinder.FindPath(regions, 0, -2, 0, 0, 2, 0, MovementDomain.Land);
            Assert.That(path.Count, Is.EqualTo(4), "west edge to east edge of a radius-2 disk is 4 hex steps");
            Assert.That(path.Last().Q, Is.EqualTo(2), "arrives at the goal hex");
            double total = path.Sum(st => st.Seconds);
            Assert.That(total, Is.EqualTo(crossing).Within(1e-6), "crossing the fine hexes edge-to-edge ≈ the coarse region crossing time");
            Log($"straight path: {path.Count} steps, {total:0}s (region crossing {crossing:0}s)");
        }

        [Test]
        [Description("H2: A* routes AROUND impassable terrain — a land unit detours through the one gap in an ocean wall and never steps on an ocean hex.")]
        public void FindPath_Land_RoutesAroundAnImpassableWall()
        {
            var reg = MakeDiskRegion(0, 3, RegionFeatureType.Plains, 8000, -1, -1);
            // Ocean wall down the q=0 column (fully separates west from east) EXCEPT one gap at (0,3).
            for (int r = -3; r <= 3; r++) SetHexTerrain(reg, 0, r, RegionFeatureType.Ocean);
            SetHexTerrain(reg, 0, 3, RegionFeatureType.Plains); // the gap the land unit must find
            var regions = new PlanetRegionsDB(new List<Region> { reg });

            var path = HexPathfinder.FindPath(regions, 0, -3, 0, 0, 3, 0, MovementDomain.Land);
            Assert.That(path.Count, Is.GreaterThan(0), "a route exists through the gap");
            Assert.That(path.Any(st => st.Q == 0 && st.R == 3), Is.True, "the path threads the gap hex (0,3)");
            // no step lands on an ocean hex
            foreach (var st in path)
            {
                var terr = reg.Hexes.First(h => h.Q == st.Q && h.R == st.R).Terrain;
                Assert.That(terr, Is.Not.EqualTo(RegionFeatureType.Ocean), "the land unit never enters ocean");
            }
            Log($"detour: {path.Count} steps around the ocean wall, through the (0,3) gap");
        }

        [Test]
        [Description("H2: cost is UNIT-dependent — a full ocean wall STOPS a land unit (no path) but an air unit flies straight across it.")]
        public void FindPath_ImpassableWall_BlocksLand_ButAirCrosses()
        {
            var reg = MakeDiskRegion(0, 3, RegionFeatureType.Plains, 8000, -1, -1);
            for (int r = -3; r <= 3; r++) SetHexTerrain(reg, 0, r, RegionFeatureType.Ocean); // full wall, no gap
            var regions = new PlanetRegionsDB(new List<Region> { reg });

            var land = HexPathfinder.FindPath(regions, 0, -3, 0, 0, 3, 0, MovementDomain.Land);
            Assert.That(land.Count, Is.EqualTo(0), "the ocean wall fully blocks the land unit");
            var air = HexPathfinder.FindPath(regions, 0, -3, 0, 0, 3, 0, MovementDomain.Air);
            Assert.That(air.Count, Is.GreaterThan(0), "the air unit flies over the ocean");
            Assert.That(air.Any(st => st.Q == 0), Is.True, "the air path crosses the ocean column");
            Log($"land blocked ({land.Count} steps); air crosses ({air.Count} steps)");
        }

        [Test]
        [Description("H2: A* crosses a REGION border — a path from region 0 to the adjacent region 1 transits through region 0's east gate into region 1's west gate (the seam-free patch-edge crossing), and arrives.")]
        public void FindPath_CrossesRegionBorder_ViaEdgeGates()
        {
            // A 4-region ring, radius-2 plains patches. Region i: west=(i+3)%4, east=(i+1)%4.
            var list = new List<Region>();
            for (int i = 0; i < 4; i++)
                list.Add(MakeDiskRegion(i, 2, RegionFeatureType.Plains, 8000, (i + 3) % 4, (i + 1) % 4));
            var regions = new PlanetRegionsDB(list);

            var path = HexPathfinder.FindPath(regions, 0, 0, 0, 1, 0, 0, MovementDomain.Land);
            Assert.That(path.Count, Is.GreaterThan(0), "a cross-border route exists");
            Assert.That(path.Any(st => st.RegionIndex == 1), Is.True, "the path enters region 1");
            var last = path.Last();
            Assert.That((last.RegionIndex, last.Q, last.R), Is.EqualTo((1, 0, 0)), "arrives at region 1's centre");

            // The crossing: region 0 east gate (2,0) immediately followed by region 1 west gate (-2,0).
            int gateIdx = path.FindIndex(st => st.RegionIndex == 0 && st.Q == 2 && st.R == 0);
            Assert.That(gateIdx, Is.GreaterThanOrEqualTo(0), "reaches region 0's east gate");
            Assert.That((path[gateIdx + 1].RegionIndex, path[gateIdx + 1].Q, path[gateIdx + 1].R),
                Is.EqualTo((1, -2, 0)), "then bridges into region 1's west gate");
            Log($"cross-border: {path.Count} steps, region0 east gate (2,0) → region1 west gate (-2,0) → (1,0,0)");
        }

        [Test]
        [Description("H2: pathfinding is deterministic (same inputs → identical path) and trivial cases (same hex / invalid region) return an empty path.")]
        public void FindPath_Deterministic_AndTrivialCasesEmpty()
        {
            var reg = MakeDiskRegion(0, 3, RegionFeatureType.Plains, 8000, -1, -1);
            var regions = new PlanetRegionsDB(new List<Region> { reg });

            var a = HexPathfinder.FindPath(regions, 0, -3, 1, 0, 2, -1, MovementDomain.Land);
            var b = HexPathfinder.FindPath(regions, 0, -3, 1, 0, 2, -1, MovementDomain.Land);
            Assert.That(a.Count, Is.EqualTo(b.Count), "same length every run");
            for (int i = 0; i < a.Count; i++)
                Assert.That((a[i].RegionIndex, a[i].Q, a[i].R), Is.EqualTo((b[i].RegionIndex, b[i].Q, b[i].R)), "identical step sequence");

            Assert.That(HexPathfinder.FindPath(regions, 0, 0, 0, 0, 0, 0, MovementDomain.Land).Count, Is.EqualTo(0), "already there → empty");
            Assert.That(HexPathfinder.FindPath(regions, 0, 0, 0, 9, 0, 0, MovementDomain.Land).Count, Is.EqualTo(0), "invalid region → empty");
            Log($"deterministic path length {a.Count}; trivial cases empty");
        }

        [Test]
        [Description("H2b: a formation ordered to a target HEX in an ADJACENT region transits hex-by-hex across the border over MULTIPLE ticks (the London→Paris behaviour) and arrives standing on the target hex — it does not teleport, and the whole block moves as one.")]
        public void OrderFormationMove_ToHex_TransitsHexByHex_AcrossRegions()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;

            // Replace the home world's regions with a clean 2-region ring of all-plains radius-2 patches, so the march
            // is deterministic (no random ocean to route around). r0.east = r1, r1.west = r0 (the shared border).
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            double crossing = 6000;
            regionsDB.Regions.Clear();
            regionsDB.Regions.Add(MakeDiskRegion(0, 2, RegionFeatureType.Plains, crossing, westNbr: 1, eastNbr: 1));
            regionsDB.Regions.Add(MakeDiskRegion(1, 2, RegionFeatureType.Plains, crossing, westNbr: 0, eastNbr: 0));

            var design = MakeInfantryDesign();
            var u1 = GroundForces.RaiseUnit(body, design, s.Faction.Id, regionIndex: 0);
            var u2 = GroundForces.RaiseUnit(body, design, s.Faction.Id, regionIndex: 0);
            var formation = GroundForces.CreateFormation(body, s.Faction.Id, "Expeditionary");
            GroundForces.AssignUnit(formation, u1);
            GroundForces.AssignUnit(formation, u2);

            // London (region 0 centre) → Paris (region 1 centre).
            int moved = GroundForces.OrderFormationMove(body, formation, toRegion: 1, toQ: 0, toR: 0);
            Assert.That(moved, Is.EqualTo(2), "both members received a hex route to the target");
            Assert.That(u1.Path, Is.Not.Null, "the leader has a plotted route");
            Assert.That(u1.Path.Count, Is.GreaterThan(3), "the route is several hexes + the border crossing, not a single hop");
            Assert.That(u1.MovingToRegion, Is.EqualTo(1), "flagged in-transit toward the destination region");

            var proc = new GroundForcesProcessor();
            int ticks = 0;
            while (u1.MovingToRegion >= 0 && ticks < 500) { proc.ProcessEntity(body, 3600); ticks++; }

            Assert.That(ticks, Is.GreaterThan(1), "the march took MULTIPLE ticks — it transited hex-by-hex, it did not teleport");
            Assert.That(u1.RegionIndex, Is.EqualTo(1), "arrived in the destination region");
            Assert.That((u1.HexQ, u1.HexR), Is.EqualTo((0, 0)), "standing on the target hex (Paris)");
            Assert.That(u1.Path == null || u1.Path.Count == 0, Is.True, "the route was consumed on arrival");
            Assert.That(u2.RegionIndex, Is.EqualTo(1), "the whole formation moved as one");
            Log($"London→Paris: formation of 2 crossed region 0→1 to the target hex in {ticks} ticks ({u1.Path?.Count ?? 0} steps left)");
        }

        [Test]
        [Description("V1: movement time depends on the UNIT's speed — a 2.0-speed unit arrives in HALF the time of a standard 1.0-speed unit on the same region hop, so a tick between the two arrival times lands the fast one and leaves the standard one still marching.")]
        public void MovementSpeed_FasterUnitArrivesSooner()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            regionsDB.Regions.Clear();
            regionsDB.Regions.Add(MakeDiskRegion(0, 2, RegionFeatureType.Plains, 8000, 1, 1));
            regionsDB.Regions.Add(MakeDiskRegion(1, 2, RegionFeatureType.Plains, 8000, 0, 0));

            var slowDesign = MakeInfantryDesign(); slowDesign.MovementSpeed = 1.0;
            var fastDesign = MakeInfantryDesign(); fastDesign.UniqueID = "test-fast"; fastDesign.MovementSpeed = 2.0;
            var slow = GroundForces.RaiseUnit(body, slowDesign, s.Faction.Id, 0);
            var fast = GroundForces.RaiseUnit(body, fastDesign, s.Faction.Id, 0);

            Assert.That(GroundForces.OrderMove(body, slow, 1), Is.True);
            Assert.That(GroundForces.OrderMove(body, fast, 1), Is.True);
            Assert.That(slow.TransitSecondsRemaining, Is.EqualTo(8000).Within(1e-6), "standard unit: full crossing time");
            Assert.That(fast.TransitSecondsRemaining, Is.EqualTo(4000).Within(1e-6), "2× speed: half the crossing time");

            var proc = new GroundForcesProcessor();
            proc.ProcessEntity(body, 5000);   // > the fast unit's 4000 s, < the standard unit's 8000 s

            Assert.That(fast.RegionIndex, Is.EqualTo(1), "the fast unit arrived");
            Assert.That(fast.MovingToRegion, Is.EqualTo(-1));
            Assert.That(slow.RegionIndex, Is.EqualTo(0), "the standard unit is still marching");
            Assert.That(slow.MovingToRegion, Is.EqualTo(1));
            Log($"speed: fast arrived (region {fast.RegionIndex}); standard still en route ({slow.TransitSecondsRemaining:0}s left)");
        }
    }
}
