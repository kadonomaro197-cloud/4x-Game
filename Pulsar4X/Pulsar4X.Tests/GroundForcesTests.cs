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
using Pulsar4X.Datablobs;   // ComponentInstancesDB lives here (namespace ≠ folder)

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

        // ───────────────────────── War-map layer (W1) — a building occupies an operational hex ─────────────────────────

        [Test]
        [Description("War-map W1 (data): a GroundHex's building list deep-copies on clone, so a hex-located base is save-safe like the rest of the hex.")]
        public void WarMap_GroundHexInstallations_SurviveClone()
        {
            var hex = new GroundHex(0, 0, RegionFeatureType.Plains);
            hex.InstallationIds.Add(42);
            var clone = new GroundHex(hex);
            Assert.That(clone.InstallationIds, Does.Contain(42), "the building ref copies onto the clone");
            clone.InstallationIds.Add(99);
            Assert.That(hex.InstallationIds, Does.Not.Contain(99), "the copy is DEEP — mutating the clone doesn't touch the original");
        }

        [Test]
        [Description("War-map W1 (the loop): the base-mod Bunker (now a GroundFootprintAtb strategic building) LANDS on its region's centre hex, CAPTURING the hex takes it, and BOMBING the hex destroys it (gone from the hex AND the colony). A non-footprint building is not a war-map target.")]
        public void WarMap_FootprintBuilding_LandsOnHex_CapturedAndBombed()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            Assert.That(body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB), Is.True);
            var region0 = regionsDB.Regions[0];
            Assert.That(region0.Hexes != null && region0.Hexes.Count > 0, Is.True, "the capital region carries a hex patch");
            var centre = region0.Hexes.First(h => h.Q == 0 && h.R == 0);

            // Clean slate for determinism (CreateWithColony already located the start colony's buildings).
            foreach (var h in region0.Hexes) h.InstallationIds.Clear();
            region0.InstallationIds.Clear();

            var fi = s.Faction.GetDataBlob<FactionInfoDB>();
            var bunkerDesign = (ComponentDesign)fi.IndustryDesigns["default-design-bunker"];
            Assert.That(GroundBuildings.IsFootprint(bunkerDesign), Is.True, "a Bunker is a war-map footprint building (carries GroundFootprintAtb)");

            var bunker = new ComponentInstance(bunkerDesign);
            s.Colony.AddComponent(bunker);
            region0.InstallationIds.Add(bunker.ID);

            // A non-footprint building on the same colony/region — it must NOT become a war-map target.
            var nonFootprint = fi.IndustryDesigns.Values.OfType<ComponentDesign>().FirstOrDefault(d => !GroundBuildings.IsFootprint(d));
            Assert.That(nonFootprint, Is.Not.Null, "the faction has at least one non-footprint installation design");
            var panel = new ComponentInstance(nonFootprint);
            s.Colony.AddComponent(panel);
            region0.InstallationIds.Add(panel.ID);

            // LOCATE — footprint lands on the region's centre hex; the non-footprint does not.
            int placed = GroundBuildings.LocateFootprintsOnHexes(s.Colony);
            Assert.That(placed, Is.EqualTo(1), "only the footprint building is placed on a hex");
            Assert.That(centre.InstallationIds, Does.Contain(bunker.ID), "the footprint building sits on the operational hex");
            Assert.That(centre.InstallationIds, Does.Not.Contain(panel.ID), "a non-footprint building is not a war-map target");
            Assert.That(GroundBuildings.LocateFootprintsOnHexes(s.Colony), Is.EqualTo(0), "re-locating adds nothing (idempotent)");

            // CAPTURE — taking the hex captures what's on it.
            int captured = GroundBuildings.CaptureRegionHexContents(regionsDB, 0, InvaderFaction);
            Assert.That(captured, Is.GreaterThanOrEqualTo(1));
            Assert.That(centre.OwnerFactionID, Is.EqualTo(InvaderFaction), "capturing the hex captures the base on it");

            // BOMBARD — bombing the hex destroys what's on it (the grave rung).
            int destroyed = GroundBuildings.BombardHex(body, 0, 0, 0, strength: 5.0);
            Assert.That(destroyed, Is.EqualTo(1), "the bombed base is destroyed");
            Assert.That(centre.InstallationIds, Is.Empty, "and is gone from the hex");
            Assert.That(s.Colony.GetDataBlob<ComponentInstancesDB>().AllComponents.Values.Any(c => c.ID == bunker.ID), Is.False,
                "and gone from the colony's components — a real loss, not just off the map");

            Log($"war-map: bunker placed on hex (0,0), captured by {InvaderFaction}, then bombed to rubble");
        }

        [Test]
        [Description("War-map W1 (processor wiring): taking a region THROUGH THE COMBAT PROCESSOR carries the strategic buildings on its hexes to the captor — capturing the region captures what's built on it.")]
        public void WarMap_RegionCapture_CarriesHexBuildings_ThroughProcessor()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();   // isolate from attrition
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            var region0 = regionsDB.Regions[0];
            var centre = region0.Hexes.First(h => h.Q == 0 && h.R == 0);
            foreach (var h in region0.Hexes) h.InstallationIds.Clear();
            region0.InstallationIds.Clear();
            region0.OwnerFactionID = s.Faction.Id;   // the defender initially holds the region + its base

            var fi = s.Faction.GetDataBlob<FactionInfoDB>();
            var bunker = new ComponentInstance((ComponentDesign)fi.IndustryDesigns["default-design-bunker"]);
            s.Colony.AddComponent(bunker);
            region0.InstallationIds.Add(bunker.ID);
            GroundBuildings.LocateFootprintsOnHexes(s.Colony);
            Assert.That(centre.InstallationIds, Does.Contain(bunker.ID), "the base is on the capital hex");
            Assert.That(centre.OwnerFactionID, Is.Not.EqualTo(InvaderFaction));

            // Only an INVADER stands in region 0 (no defender units) → the processor flips the region to the invader.
            GroundForces.RaiseUnit(body, MakeInfantryDesign(), InvaderFaction, 0);
            new GroundForcesProcessor().ProcessEntity(body, 3600);

            Assert.That(region0.OwnerFactionID, Is.EqualTo(InvaderFaction), "the invader takes the region");
            Assert.That(centre.OwnerFactionID, Is.EqualTo(InvaderFaction),
                "and the hex-building flips with it — capturing the region captures what's built on it");
            Log($"war-map capture via processor: region 0 + its bunker hex now held by {InvaderFaction}");
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

        // ───────────────────────── H2 — hex movement + pathfinding (docs/HEX-GROUND-AND-ORDERS-DESIGN.md) ─────────────────────────

        /// <summary>Build an open hex disk of the given radius (all one terrain) — the shape PlanetHexFactory generates.</summary>
        private static List<GroundHex> OpenDisk(int radius, RegionFeatureType fill = RegionFeatureType.Plains)
        {
            var list = new List<GroundHex>();
            for (int q = -radius; q <= radius; q++)
            {
                int rLo = System.Math.Max(-radius, -q - radius);
                int rHi = System.Math.Min(radius, -q + radius);
                for (int r = rLo; r <= rHi; r++)
                    list.Add(new GroundHex(q, r, fill));
            }
            return list;
        }

        [Test]
        [Description("H2: A* across an all-open patch is a straight line — its step count equals the hex distance from start to destination.")]
        public void HexPath_StraightLineAcrossOpenPatch_LengthEqualsDistance()
        {
            var disk = OpenDisk(2);
            var path = HexPathfinder.FindPath(disk, -2, 0, 2, 0);
            Assert.That(path.Count, Is.EqualTo(4), "distance from (-2,0) to (2,0) is 4 hexes, all open → a 4-step path");
            Assert.That(path[path.Count - 1].Q, Is.EqualTo(2));
            Assert.That(path[path.Count - 1].R, Is.EqualTo(0), "the path ends on the destination hex");
            Log($"open straight line: {path.Count} steps to (2,0)");
        }

        [Test]
        [Description("H2: terrain steers the route — A* detours around a wall of rough hexes rather than paying the ×2.5 to bull through.")]
        public void HexPath_RoutesAroundRough_AvoidsTheExpensiveWall()
        {
            var disk = OpenDisk(2);
            // Block the straight r=0 corridor with mountains (×2.5 each) — a cheaper open detour exists off-axis.
            for (int i = 0; i < disk.Count; i++)
                if (disk[i].R == 0 && (disk[i].Q == -1 || disk[i].Q == 0 || disk[i].Q == 1))
                    disk[i] = new GroundHex(disk[i].Q, disk[i].R, RegionFeatureType.Mountains);

            var path = HexPathfinder.FindPath(disk, -2, 0, 2, 0);
            Assert.That(path.Count, Is.GreaterThan(0), "a route exists");
            Assert.That(path.Any(h => h.Terrain == RegionFeatureType.Mountains), Is.False,
                "the cheaper open detour is taken — no mountain hex is on the path");
            Log($"rough-avoiding route: {path.Count} steps, 0 mountain hexes");
        }

        [Test]
        [Description("H2: pathfinder guard rails — an off-patch destination and a start-equals-destination both return an empty path (no move).")]
        public void HexPath_Guards_OffPatchAndAlreadyThere_ReturnEmpty()
        {
            var disk = OpenDisk(2);
            Assert.That(HexPathfinder.FindPath(disk, 0, 0, 9, 9).Count, Is.EqualTo(0), "(9,9) is outside the patch → no path");
            Assert.That(HexPathfinder.FindPath(disk, 1, 0, 1, 0).Count, Is.EqualTo(0), "already on the destination → no path");
        }

        [Test]
        [Description("H2b: OCEAN is impassable to ground units (the developer's call) — a march never steps onto a water hex, and a destination ON water is unreachable. Ice stays passable.")]
        public void HexPath_Ocean_IsImpassable_ButIceIsFine()
        {
            Assert.That(HexPathfinder.IsImpassable(RegionFeatureType.Ocean), Is.True, "ocean blocks ground units");
            Assert.That(HexPathfinder.IsImpassable(RegionFeatureType.Ice), Is.False, "ice is passable (rough)");
            Assert.That(HexPathfinder.HexMoveMult(RegionFeatureType.Ice), Is.EqualTo(2.5), "ice is rough-cost");

            // A patch with a wall of ocean along the r=0 corridor: the march must detour around it (never onto water).
            var disk = OpenDisk(2);
            for (int i = 0; i < disk.Count; i++)
                if (disk[i].R == 0 && (disk[i].Q == -1 || disk[i].Q == 0 || disk[i].Q == 1))
                    disk[i] = new GroundHex(disk[i].Q, disk[i].R, RegionFeatureType.Ocean);

            var around = HexPathfinder.FindPath(disk, -2, 0, 2, 0);
            Assert.That(around.Count, Is.GreaterThan(0), "an over-land route around the water exists");
            Assert.That(around.Any(h => h.Terrain == RegionFeatureType.Ocean), Is.False, "no step is ever on a water hex");

            // A destination that IS a water hex is unreachable — no march onto open water.
            var onWater = HexPathfinder.FindPath(disk, -2, 0, 0, 0);
            Assert.That(onWater.Count, Is.EqualTo(0), "can't order a march onto an ocean hex");
            Log($"ocean impassable: routed {around.Count} land hexes around the water; on-water dest rejected");
        }

        [Test]
        [Description("H2: the move-cost tiers are the developer's Moderate call (open ×1, cover ×1.5, rough ×2.5); per-hex base time is derived from the region's crossing-time datum, not a magic number.")]
        public void HexPath_CostModel_TiersAndDerivedBaseTime()
        {
            Assert.That(HexPathfinder.HexMoveMult(RegionFeatureType.Plains), Is.EqualTo(1.0));
            Assert.That(HexPathfinder.HexMoveMult(RegionFeatureType.Forest), Is.EqualTo(1.5));
            Assert.That(HexPathfinder.HexMoveMult(RegionFeatureType.Mountains), Is.EqualTo(2.5));

            var region = new Region { CrossingTimeSeconds = 1000.0, Hexes = OpenDisk(5) };
            // patch radius 5 → 10 hexes across → one open hex ≈ 1000/10 = 100s.
            Assert.That(HexPathfinder.PerHexBaseSeconds(region), Is.EqualTo(100.0).Within(0.001));
            Log("cost model: ×1/×1.5/×2.5, per-open-hex 100s from a 1000s region");
        }

        [Test]
        [Description("H2 WIRING: order a real garrisoned unit to a hex across its region and drive the ground processor — it walks the A* path hex-by-hex and arrives on the destination hex (the London→Paris transit-in-ticks behaviour).")]
        public void OrderMoveToHex_UnitWalksHexByHex_AndArrives()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;   // the home world — ColonyFactory generated its hex patches
            Assert.That(body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB), Is.True);
            var region0 = regionsDB.Regions[0];
            Assert.That(region0.Hexes.Count, Is.GreaterThan(0), "the home world's capital region carries a hex patch (lazy gen at colony creation)");

            var unit = GroundForces.RaiseUnit(body, MakeInfantryDesign(), s.Faction.Id, regionIndex: 0);
            Assert.That(unit.HexQ, Is.EqualTo(0));
            Assert.That(unit.HexR, Is.EqualTo(0), "a fresh unit stands at the patch centre");

            // Pick the farthest-out hex that's actually REACHABLE over land (ocean is impassable, and water could cut
            // a distant hex off) — the first candidate, by descending distance, that A* can actually reach.
            var origin = new Pulsar4X.Colonies.HexCoordinate(0, 0);
            GroundHex dest = null;
            foreach (var h in region0.Hexes
                         .Where(h => !(h.Q == 0 && h.R == 0))
                         .OrderByDescending(h => new Pulsar4X.Colonies.HexCoordinate(h.Q, h.R).DistanceTo(origin)))
            {
                if (HexPathfinder.FindPath(region0.Hexes, 0, 0, h.Q, h.R).Count > 0) { dest = h; break; }
            }
            Assert.That(dest, Is.Not.Null, "at least one hex in the patch is reachable over land");

            bool ordered = GroundForces.OrderMoveToHex(body, unit, dest.Q, dest.R);
            Assert.That(ordered, Is.True, "a reachable hex in the patch is a valid order");
            Assert.That(unit.HexPath, Is.Not.Null.And.Count.GreaterThan(0), "the unit has a stored hex path");
            int steps = unit.HexPath.Count;

            var proc = new GroundForcesProcessor();
            // Advance well past the whole march (each step ≤ base×2.5; a long tick clears them all).
            proc.ProcessEntity(body, 100 * 24 * 3600);

            Assert.That(unit.HexQ, Is.EqualTo(dest.Q));
            Assert.That(unit.HexR, Is.EqualTo(dest.R), "the unit arrived on the destination hex");
            Assert.That(unit.HexPath, Is.Null, "and is no longer hex-marching");

            // Save-safety: a mid-march unit's path survives a clone.
            var unit2 = GroundForces.RaiseUnit(body, MakeInfantryDesign(), s.Faction.Id, regionIndex: 0);
            GroundForces.OrderMoveToHex(body, unit2, dest.Q, dest.R);
            var clone = (GroundForcesDB)body.GetDataBlob<GroundForcesDB>().Clone();
            var clonedMarcher = clone.Units.First(u => u.UnitId == unit2.UnitId);
            Assert.That(clonedMarcher.HexPath, Is.Not.Null.And.Count.GreaterThan(0), "an in-transit hex path survives a clone (save-safe)");

            Log($"hex march: unit walked {steps} hexes to ({dest.Q},{dest.R}) and arrived; path clone-safe");
        }

        // ───────────────────────── G3 — units on the ONE continuous global grid (docs/GLOBAL-HEX-GRID-DESIGN.md) ─────────────────────────

        [Test]
        [Description("G3: a raised unit is ALSO placed on the global cylinder grid — at its region BAND's centre column (the global twin of the disk's (0,0) muster).")]
        public void RaiseUnit_MustersOnGlobalGrid_InItsBand()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var unit = GroundForces.RaiseUnit(body, MakeInfantryDesign(), s.Faction.Id, regionIndex: 2);

            Assert.That(body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB), Is.True);
            Assert.That(regionsDB.SurfaceGrid, Is.Not.Null, "raising a unit generated the body's global grid");
            Assert.That(unit.GlobalQ, Is.GreaterThanOrEqualTo(0), "the unit is placed on the grid");
            int band = PlanetGridFactory.RegionOfColumn(unit.GlobalQ, regionsDB.SurfaceGrid.Cols, regionsDB.Regions.Count);
            Assert.That(band, Is.EqualTo(2), "it musters in region 2's column band");
        }

        [Test]
        [Description("G3 THE POINT — a unit marches across a region BAND border on the continuous grid with NO edge gate: order a global move one band east, advance time, and it arrives with its region index updated (it just walked across the seam column, not through a stitch).")]
        public void OrderMoveToGlobalHex_MarchesAcrossBandBorder_NoGate()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var unit = GroundForces.RaiseUnit(body, MakeInfantryDesign(), s.Faction.Id, regionIndex: 0);
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            var grid = regionsDB.SurfaceGrid;
            Assert.That(grid, Is.Not.Null);

            // Make the whole world land so the march is deterministic (isolates the border-crossing from terrain routing).
            foreach (var h in grid.Hexes) h.Terrain = RegionFeatureType.Plains;

            int rc = regionsDB.Regions.Count;
            int bandWidth = grid.Cols / rc;
            int startBand = PlanetGridFactory.RegionOfColumn(unit.GlobalQ, grid.Cols, rc);
            int targetQ = (unit.GlobalQ + bandWidth) % grid.Cols;         // one full band east → an ADJACENT band
            int targetBand = PlanetGridFactory.RegionOfColumn(targetQ, grid.Cols, rc);
            Assert.That(targetBand, Is.Not.EqualTo(startBand), "the target is across a region-band border");

            Assert.That(GroundForces.OrderMoveToGlobalHex(body, unit, targetQ, unit.GlobalR), Is.True, "a reachable global hex is a valid order");
            Assert.That(unit.GlobalPath, Is.Not.Null.And.Count.GreaterThan(0));

            new GroundForcesProcessor().ProcessEntity(body, 100 * 24 * 3600);   // long tick clears the whole march

            Assert.That(unit.GlobalQ, Is.EqualTo(targetQ), "it arrived at the destination column");
            Assert.That(unit.GlobalR, Is.EqualTo(unit.GlobalR));
            Assert.That(unit.GlobalPath, Is.Null, "no longer marching");
            Assert.That(unit.RegionIndex, Is.EqualTo(targetBand), "its region followed it across the band border — no edge gate");
        }

        // ───────────────────────── H3 — range-based directed combat (docs/HEX-GROUND-AND-ORDERS-DESIGN.md) ─────────────────────────

        private static GroundUnitDesign MakeDesign(string id, string name, GroundUnitType type, int range, double hp = 1000) => new GroundUnitDesign
        {
            UniqueID = id, Name = name, UnitType = type, Range = range,
            Attack = 100, Defense = 10, HitPoints = hp,
            IndustryPointCosts = 100, IndustryTypeID = "installation", ResourceCosts = new Dictionary<string, long>(),
        };

        [Test]
        [Description("H3 defaults: strike range is a per-type default when a design leaves it unset — artillery (3) outranges infantry/armor (1); and a raised unit snapshots it.")]
        public void RangeDefaults_ArtilleryOutrangesInfantryAndArmor()
        {
            Assert.That(GroundRangeTools.DefaultRangeFor(GroundUnitType.Infantry), Is.EqualTo(1));
            Assert.That(GroundRangeTools.DefaultRangeFor(GroundUnitType.Armor), Is.EqualTo(1));
            Assert.That(GroundRangeTools.DefaultRangeFor(GroundUnitType.Artillery), Is.EqualTo(3));

            var s = TestScenario.CreateWithColony();
            var noRangeArty = new GroundUnitDesign
            {
                UniqueID = "arty-norange", Name = "Guns", UnitType = GroundUnitType.Artillery,
                Attack = 100, HitPoints = 100, IndustryTypeID = "installation", ResourceCosts = new Dictionary<string, long>(),
            };
            var u = GroundForces.RaiseUnit(s.StartingBody, noRangeArty, s.Faction.Id, 0);
            Assert.That(u.Range, Is.EqualTo(3), "an artillery design with no explicit range gets the type default (3)");
        }

        [Test]
        [Description("H3 THE HEADLINE (clone-vs-zerg): a longer-ranged unit hits a shorter-ranged one while it CLOSES, taking no fire back until the enemy reaches its own range — the ground echo of the space first-strike.")]
        public void RangeCombat_OutRangerHitsCloserUnitFirst_CloneVsZerg()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();   // isolate combat from attrition
            var regions = body.GetDataBlob<PlanetRegionsDB>().Regions;
            regions[0].OwnerFactionID = -1;   // neutral ground — no defender cover bias, so we measure pure range

            // The "clone trooper": long reach (3 hexes), holds at the patch centre. Same type as the zerg so the weapon
            // triangle is neutral — this isolates RANGE as the only variable.
            var clone = GroundForces.RaiseUnit(body, MakeDesign("clone", "Clone Trooper", GroundUnitType.Infantry, range: 3), s.Faction.Id, 0);
            clone.HexQ = 0; clone.HexR = 0;
            // The "zerg": melee reach (1 hex), starts 3 hexes away — out of its OWN range, inside the clone's.
            var zerg = GroundForces.RaiseUnit(body, MakeDesign("zerg", "Zergling", GroundUnitType.Infantry, range: 1), InvaderFaction, 0);
            zerg.HexQ = 3; zerg.HexR = 0;

            var proc = new GroundForcesProcessor();
            proc.ProcessEntity(body, 3600);

            Assert.That(clone.Health, Is.EqualTo(clone.MaxHealth),
                "the clone is untouched — the zerg is out of its own range and can't shoot back");
            Assert.That(zerg.Health, Is.LessThan(zerg.MaxHealth),
                "the zerg takes fire while it is still closing (the standoff advantage)");
            Log($"standoff (dist 3): clone {clone.Health:0}/{clone.MaxHealth:0} unhit, zerg {zerg.Health:0}/{zerg.MaxHealth:0} hit");

            // The zerg closes to its own melee range (1 hex) — now it fights back.
            zerg.HexQ = 1; zerg.HexR = 0;
            double cloneBefore = clone.Health;
            proc.ProcessEntity(body, 3600);
            Assert.That(clone.Health, Is.LessThan(cloneBefore),
                "once the zerg has closed into ITS range, the clone finally takes damage too");
            Log($"closed (dist 1): clone {clone.Health:0}, zerg {zerg.Health:0} — both now trading fire");
        }

        [Test]
        [Description("System ① — flat ARMOUR in a REAL fight (proves the resolver reads a unit's Defense): two identical infantry defenders each face an identical swarm of small-attack attackers; the ARMOURED one (high Defense) ends with far more health, because flat armour bounces most of each little volley. The wiring half of the pure-math armour gauge (GroundDamageMatrixTests).")]
        public void Armour_InAFight_ArmouredDefenderOutlastsUnarmoured_VsASwarm()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();   // isolate armour from attrition
            var regions = body.GetDataBlob<PlanetRegionsDB>().Regions;
            regions[0].OwnerFactionID = -1; regions[1].OwnerFactionID = -1;   // neutral ground → no cover bias, so we measure pure armour
            // identical, open terrain in both regions → the ONLY difference between the two fights is the defender's Defense
            regions[0].Features.Clear(); regions[0].Features.Add(new RegionFeature(RegionFeatureType.Plains, 1.0));
            regions[1].Features.Clear(); regions[1].Features.Add(new RegionFeature(RegionFeatureType.Plains, 1.0));

            GroundUnitDesign Defender(string id, double defense) => new GroundUnitDesign
            {
                UniqueID = id, Name = id, UnitType = GroundUnitType.Infantry,
                Attack = 0, Defense = defense, HitPoints = 1000, Range = 1,   // Attack 0 → a punching bag, so the swarm stays intact and we measure ONLY armour
                IndustryTypeID = "installation", ResourceCosts = new Dictionary<string, long>(),
            };
            GroundUnitDesign Attacker() => new GroundUnitDesign
            {
                UniqueID = "swarm", Name = "Swarm", UnitType = GroundUnitType.Infantry,
                Attack = 20, Defense = 0, HitPoints = 100, Range = 1,        // small per-source hits → exactly what flat armour bounces
                IndustryTypeID = "installation", ResourceCosts = new Dictionary<string, long>(),
            };

            // region 0: an ARMOURED defender vs a 4-unit swarm; region 1: an UNARMOURED defender vs the same swarm.
            var armoured = GroundForces.RaiseUnit(body, Defender("armoured", 30), s.Faction.Id, 0);
            var bare = GroundForces.RaiseUnit(body, Defender("bare", 0), s.Faction.Id, 1);
            for (int i = 0; i < 4; i++)
            {
                GroundForces.RaiseUnit(body, Attacker(), InvaderFaction, 0);
                GroundForces.RaiseUnit(body, Attacker(), InvaderFaction, 1);
            }

            var proc = new GroundForcesProcessor();
            for (int i = 0; i < 4; i++) proc.ProcessEntity(body, 3600);

            Assert.That(armoured.Health, Is.GreaterThan(bare.Health),
                "the armoured defender soaked the swarm's chip damage; the unarmoured one bled far more");
            Assert.That(armoured.Health, Is.GreaterThan(0.9 * armoured.MaxHealth),
                "flat armour bounces most of each little volley — the armoured unit is barely scratched");
            Log($"armour fight: armoured {armoured.Health:0}/{armoured.MaxHealth:0} vs bare {bare.Health:0}/{bare.MaxHealth:0} after 4 salvos");
        }

        [Test]
        [Description("H3 readout — the developer's insight made visible: a hex range converts to a real distance that DIFFERS by body (a hex on a big world covers more real km than on a small one). Combat stays in hex-space; this is only the gauge.")]
        public void RealReach_HexRangeToKm_VariesByBodyHexSize()
        {
            var big = new Region { Area_km2 = 1_000_000, Hexes = OpenDisk(2) };     // 19 hexes
            double pitchBig = GroundRangeTools.HexPitchKm(big);
            Assert.That(pitchBig, Is.GreaterThan(0));
            Assert.That(GroundRangeTools.RealReachKm(3, big), Is.EqualTo(3 * pitchBig).Within(1e-6));
            Assert.That(GroundRangeTools.RealReachKm(0, big), Is.EqualTo(0.0), "zero range = zero reach");

            var small = new Region { Area_km2 = 100_000, Hexes = OpenDisk(2) };
            Assert.That(GroundRangeTools.HexPitchKm(small), Is.LessThan(pitchBig),
                "the same hex range covers less real distance on a smaller world (1 hex ≠ the same distance everywhere)");
            Assert.That(GroundRangeTools.HexPitchKm(new Region()), Is.EqualTo(0.0), "no hex patch / no area → no readout");
            Log($"readout: a 3-hex gun ≈ {GroundRangeTools.RealReachKm(3, big):N0} km on the big region vs {GroundRangeTools.RealReachKm(3, small):N0} km on the small one");
        }

        // ───────────────────────── ROE — commander engagement rules (the space closing-model echo) ─────────────────────────

        private static void PaveRegionHexes(Entity body, PlanetRegionsDB regionsDB, int regionIndex)
        {
            Pulsar4X.Galaxy.PlanetHexFactory.EnsureHexesForBody(body);
            foreach (var h in regionsDB.Regions[regionIndex].Hexes) h.Terrain = RegionFeatureType.Plains;   // deterministic, passable
        }

        private static int HexDistBetween(GroundUnit a, GroundUnit b)
            => new Pulsar4X.Colonies.HexCoordinate(a.HexQ, a.HexR).DistanceTo(new Pulsar4X.Colonies.HexCoordinate(b.HexQ, b.HexR));

        [Test]
        [Description("ROE set/read: SetEngagementStance stores the maneuver intent on the formation; EngagementOf reads it (HoldGround if unformed).")]
        public void Roe_SetAndReadEngagementStance()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var u = GroundForces.RaiseUnit(body, MakeDesign("u", "U", GroundUnitType.Infantry, 3), s.Faction.Id, 0);
            var forces = body.GetDataBlob<GroundForcesDB>();

            Assert.That(GroundFormationDoctrine.EngagementOf(forces, u), Is.EqualTo(GroundEngagementStance.HoldGround), "an unformed unit holds ground by default");
            var f = GroundForces.CreateFormation(body, s.Faction.Id, "Recon");
            GroundForces.AssignUnit(f, u);
            GroundFormationDoctrine.SetEngagementStance(f, GroundEngagementStance.StandOff);
            Assert.That(GroundFormationDoctrine.EngagementOf(forces, u), Is.EqualTo(GroundEngagementStance.StandOff));
        }

        [Test]
        [Description("ROE StandOff (the auto-KITE): a Stand-off formation whose enemy is inside ITS reach auto-orders a step AWAY, opening the gap so the longer-ranged unit keeps hitting from beyond the enemy's range.")]
        public void Roe_StandOff_OrdersAStepAway_ToKeepRange()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            PaveRegionHexes(body, regionsDB, 0);

            var clone = GroundForces.RaiseUnit(body, MakeDesign("clone", "Clone", GroundUnitType.Infantry, range: 3), s.Faction.Id, 0);
            clone.HexQ = 3; clone.HexR = 0;
            var zerg = GroundForces.RaiseUnit(body, MakeDesign("zerg", "Zerg", GroundUnitType.Infantry, range: 1), InvaderFaction, 0);
            zerg.HexQ = 4; zerg.HexR = 0;   // distance 1 → inside the zerg's reach → the clone should back off

            var forces = body.GetDataBlob<GroundForcesDB>();
            var f = GroundForces.CreateFormation(body, s.Faction.Id, "Clones");
            GroundForces.AssignUnit(f, clone);
            GroundFormationDoctrine.SetEngagementStance(f, GroundEngagementStance.StandOff);

            int distBefore = HexDistBetween(clone, zerg);   // 1
            new GroundForcesProcessor().ProcessEntity(body, 1);   // tiny tick: issue the maneuver, don't complete the walk

            Assert.That(clone.HexPath, Is.Not.Null.And.Count.GreaterThan(0), "the stand-off clone was auto-ordered to reposition");
            var dest = clone.HexPath[clone.HexPath.Count - 1];
            int destDist = new Pulsar4X.Colonies.HexCoordinate(dest.Q, dest.R).DistanceTo(new Pulsar4X.Colonies.HexCoordinate(zerg.HexQ, zerg.HexR));
            Assert.That(destDist, Is.GreaterThan(distBefore), "it kites — the ordered hex is FARTHER from the enemy");
            Log($"ROE stand-off: clone auto-kites from dist {distBefore} → dist {destDist}");
        }

        [Test]
        [Description("ROE CloseToEngage (the auto-CLOSE): a Close formation whose enemy is beyond its reach auto-orders a step TOWARD, shrinking the gap to get into its own range (the zerg rush).")]
        public void Roe_CloseToEngage_OrdersAStepToward_ToShrinkTheGap()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            PaveRegionHexes(body, regionsDB, 0);

            var brawler = GroundForces.RaiseUnit(body, MakeDesign("brawler", "Zergling", GroundUnitType.Infantry, range: 1), s.Faction.Id, 0);
            brawler.HexQ = 0; brawler.HexR = 0;
            var target = GroundForces.RaiseUnit(body, MakeDesign("tgt", "Sniper", GroundUnitType.Infantry, range: 1), InvaderFaction, 0);
            target.HexQ = 4; target.HexR = 0;   // distance 4 → far beyond the brawler's reach 1 → it should close

            var f = GroundForces.CreateFormation(body, s.Faction.Id, "Rush");
            GroundForces.AssignUnit(f, brawler);
            GroundFormationDoctrine.SetEngagementStance(f, GroundEngagementStance.CloseToEngage);

            int distBefore = HexDistBetween(brawler, target);   // 4
            new GroundForcesProcessor().ProcessEntity(body, 1);

            Assert.That(brawler.HexPath, Is.Not.Null.And.Count.GreaterThan(0), "the closing brawler was auto-ordered to advance");
            var dest = brawler.HexPath[brawler.HexPath.Count - 1];
            int destDist = new Pulsar4X.Colonies.HexCoordinate(dest.Q, dest.R).DistanceTo(new Pulsar4X.Colonies.HexCoordinate(target.HexQ, target.HexR));
            Assert.That(destDist, Is.LessThan(distBefore), "it closes — the ordered hex is NEARER the enemy");
            Log($"ROE close: brawler auto-advances from dist {distBefore} → dist {destDist}");
        }

        [Test]
        [Description("A unit maneuvering on the battlefield (a fine HEX march) STILL FIGHTS — it fires from its current hex as it repositions (so a kiting unit shoots while backing away). Only a strategic REGION hop takes a unit out of the fight.")]
        public void HexMarchingUnit_StillFights()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            PaveRegionHexes(body, regionsDB, 0);
            regionsDB.Regions[0].OwnerFactionID = -1;

            var mover = GroundForces.RaiseUnit(body, MakeDesign("mv", "Mover", GroundUnitType.Infantry, range: 3), s.Faction.Id, 0);
            mover.HexQ = 0; mover.HexR = 0;
            var shooter = GroundForces.RaiseUnit(body, MakeDesign("sh", "Shooter", GroundUnitType.Infantry, range: 3), InvaderFaction, 0);
            shooter.HexQ = 0; shooter.HexR = 0;   // co-located → in range

            Assert.That(GroundForces.OrderMoveToHex(body, mover, 2, 0), Is.True, "the mover sets off on a hex march");
            Assert.That(mover.HexPath, Is.Not.Null.And.Count.GreaterThan(0));

            double before = mover.Health;
            new GroundForcesProcessor().ProcessEntity(body, 1);   // tiny tick: still mid-march, but combat runs
            Assert.That(mover.HexPath, Is.Not.Null.And.Count.GreaterThan(0), "still mid-march (the tick was too short to arrive)");
            Assert.That(mover.Health, Is.LessThan(before), "a hex-marching unit still takes fire — it fights while repositioning");
            Log($"hex-marcher fights: mover took {before - mover.Health:0} dmg while mid-march");
        }

        [Test]
        [Description("NORTH STAR — the closing fight END-TO-END (docs/RESOLVER-MERGE-DESIGN §7, the zergling/Titan acceptance test). A long-range unit (reach 3) and an EQUAL-stats short-range unit (reach 1) start 3 hexes apart; the short-range one RUSHES (CloseToEngage). The long-range unit fires across the whole approach while the rusher can't shoot back until it closes — so the range advantage, played out over the battle's DURATION tick by tick, decides it: the rusher ends far more damaged (or dead) than the kiter. Range is the ONLY difference between the two, so this isolates the closing advantage the auto-resolver simulates.")]
        public void ClosingFight_LongRangeWhittlesTheRusherDuringTheApproach()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();  // no attrition skew
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            PaveRegionHexes(body, regionsDB, 0);
            regionsDB.Regions[0].OwnerFactionID = -1;   // neutral ground → no fortification/cover skew for either side

            // Equal stats EXCEPT reach — range is the only variable, so the fight isolates the closing advantage.
            var kiter = GroundForces.RaiseUnit(body, MakeDesign("kiter", "LongGun", GroundUnitType.Infantry, range: 3), s.Faction.Id, 0);
            kiter.HexQ = 0; kiter.HexR = 0;
            var rusher = GroundForces.RaiseUnit(body, MakeDesign("rusher", "Zergling", GroundUnitType.Infantry, range: 1), InvaderFaction, 0);
            rusher.HexQ = 3; rusher.HexR = 0;   // distance 3: inside the kiter's reach, OUTSIDE the rusher's

            // The rusher's formation is ordered to CLOSE; the kiter has no formation, so it holds ground and fires.
            var f = GroundForces.CreateFormation(body, InvaderFaction, "Swarm");
            GroundForces.AssignUnit(f, rusher);
            GroundFormationDoctrine.SetEngagementStance(f, GroundEngagementStance.CloseToEngage);

            var forces = body.GetDataBlob<GroundForcesDB>();
            var proc = new GroundForcesProcessor();
            int startDist = HexDistBetween(kiter, rusher);
            // Play the battle out over many game-hours — the resolver closes the gap and trades fire tick by tick.
            for (int i = 0; i < 60 && forces.Units.Count > 1; i++)
                proc.ProcessEntity(body, 3600);

            // The unit objects persist even after a death removes them from the roster, so read their final Health directly.
            Log($"closing fight from dist {startDist}: kiter(reach 3) ends {kiter.Health:0} hp, rusher(reach 1) ends {rusher.Health:0} hp");
            Assert.That(rusher.Health, Is.LessThan(kiter.Health),
                "the long-range unit's free shots during the approach decide the fight — the rusher ends more damaged");
            Assert.That(kiter.Health, Is.GreaterThan(0),
                "the long-range unit survives: it whittled the rusher before it could close to trade evenly");
        }

        [Test]
        [Description("4b GROUND COMBAT INTERRUPT — the ground mirror of the space combat-pause: the first tick a NEW planetary battle forms, the processor calls RequestCombatHalt so the clock stops and the player is notified (the client's CombatInterruptPending banner, shared with space). Gated by a flag (default off, so headless advances deterministically); the WasInBattle latch means an ONGOING fight doesn't re-halt every tick.")]
        public void GroundCombatInterrupt_HaltsTheClockOnANewBattle_NotEveryTick()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            PaveRegionHexes(body, regionsDB, 0);
            regionsDB.Regions[0].OwnerFactionID = -1;

            // Two hostile units co-located (distance 0 → in range) → they fight, so a battle forms.
            var a = GroundForces.RaiseUnit(body, MakeDesign("a", "A", GroundUnitType.Infantry, range: 1), s.Faction.Id, 0);
            a.HexQ = 0; a.HexR = 0;
            var b = GroundForces.RaiseUnit(body, MakeDesign("b", "B", GroundUnitType.Infantry, range: 1), InvaderFaction, 0);
            b.HexQ = 0; b.HexR = 0;

            var tp = s.Game.TimePulse;
            var forces = body.GetDataBlob<GroundForcesDB>();
            Assert.That(GroundForcesProcessor.InterruptTimeOnNewBattle, Is.False, "the ground interrupt flag defaults OFF");

            // Flag OFF → the clock is never halted by a ground battle.
            tp.CombatInterruptPending = false;
            forces.WasInBattle = false;
            new GroundForcesProcessor().ProcessEntity(body, 3600);
            Assert.That(tp.CombatInterruptPending, Is.False, "flag off → a planetary battle does NOT halt the clock");

            try
            {
                GroundForcesProcessor.InterruptTimeOnNewBattle = true;

                // A NEW battle (fresh transition) halts the clock.
                forces.WasInBattle = false;
                tp.CombatInterruptPending = false;
                new GroundForcesProcessor().ProcessEntity(body, 3600);
                Assert.That(tp.CombatInterruptPending, Is.True, "a NEW planetary battle halts the clock (the player is notified)");

                // The SAME ongoing fight the next tick does NOT re-halt (the WasInBattle latch).
                tp.CombatInterruptPending = false;
                new GroundForcesProcessor().ProcessEntity(body, 3600);
                Assert.That(tp.CombatInterruptPending, Is.False, "an ongoing fight does not re-halt every tick");
            }
            finally { GroundForcesProcessor.InterruptTimeOnNewBattle = false; }
        }

        [Test]
        [Description("3c SHIELD as a DEPLETING POOL (routed through the shared kernel): a ground unit's shield is now a pool that DRAINS in combat (not the old permanent %), and the weapon-NATURE matchup falls out of the kernel — a KINETIC attacker is fully soaked by the shield while an ENERGY attacker half-bleeds through. Two identical shielded targets in separate regions, one hit by kinetic fire, one by energy: the energy-hit target takes MORE health damage, and both targets' shield pools drain.")]
        public void GroundShield_IsADepletingPool_EnergyBleedsWhereKineticIsSoaked()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            PaveRegionHexes(body, regionsDB, 0);
            PaveRegionHexes(body, regionsDB, 1);
            regionsDB.Regions[0].OwnerFactionID = -1;
            regionsDB.Regions[1].OwnerFactionID = -1;

            // Region 0: a KINETIC attacker vs a shielded target. Region 1: an ENERGY attacker vs an identical target.
            var kinetic = GroundForces.RaiseUnit(body, MakeDesign("kin", "Rifles", GroundUnitType.Infantry, range: 1), InvaderFaction, 0);
            var tgtK = GroundForces.RaiseUnit(body, MakeDesign("tk", "ShieldedK", GroundUnitType.Infantry, range: 1), s.Faction.Id, 0);
            var energy = GroundForces.RaiseUnit(body, MakeDesign("eng", "Lasers", GroundUnitType.Infantry, range: 1), InvaderFaction, 1);
            energy.DamageType = GroundWeaponMode.Energy;
            var tgtE = GroundForces.RaiseUnit(body, MakeDesign("te", "ShieldedE", GroundUnitType.Infantry, range: 1), s.Faction.Id, 1);
            foreach (var t in new[] { tgtK, tgtE }) { t.Shield = 300; t.CurrentShield = 300; }

            new GroundForcesProcessor().ProcessEntity(body, 3600);

            Log($"kinetic-hit target: {tgtK.Health:0} hp, shield {tgtK.CurrentShield:0}/300 | energy-hit target: {tgtE.Health:0} hp, shield {tgtE.CurrentShield:0}/300");
            Assert.That(tgtK.CurrentShield, Is.LessThan(300), "the shield is a POOL — kinetic fire drains it");
            Assert.That(tgtE.CurrentShield, Is.LessThan(300), "energy fire drains the pool too");
            Assert.That(tgtE.Health, Is.LessThan(tgtK.Health),
                "ENERGY bleeds through a shield where KINETIC is soaked — the nature matchup, from the kernel");
        }

        // ───────────────────────── O1 — formation order QUEUE (sequential waypoints) ─────────────────────────

        [Test]
        [Description("O1 queue API: Queue appends, Set replaces the whole plan with one order, Clear empties it.")]
        public void Orders_QueueApi_QueueSetClear()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            GroundForces.RaiseUnit(body, MakeDesign("u", "U", GroundUnitType.Infantry, 1), s.Faction.Id, 0);
            var f = GroundForces.CreateFormation(body, s.Faction.Id, "Column");

            Assert.That(GroundForces.QueueFormationOrder(f, GroundOrder.MoveHex(1, 0)), Is.True);
            GroundForces.QueueFormationOrder(f, GroundOrder.MoveHex(2, 0));
            Assert.That(f.Orders.Count, Is.EqualTo(2), "Queue appends");
            GroundForces.SetFormationOrder(f, GroundOrder.Hold(3600));
            Assert.That(f.Orders.Count, Is.EqualTo(1), "Set replaces the whole plan");
            Assert.That(f.Orders[0].Type, Is.EqualTo(GroundOrderType.HoldFor));
            GroundForces.ClearFormationOrders(f);
            Assert.That(f.Orders.Count, Is.EqualTo(0), "Clear empties the queue");
        }

        [Test]
        [Description("O1 THE HEADLINE — sequential waypoints: a formation queued 'move to hex A, THEN hex B' runs them in order and its leader ends on the final waypoint (the London→Paris→… chain the fleet lane-model can't express).")]
        public void Orders_SequentialWaypoints_LeaderEndsAtFinalHex()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            PaveRegionHexes(body, regionsDB, 0);

            var u = GroundForces.RaiseUnit(body, MakeDesign("u", "U", GroundUnitType.Infantry, 1), s.Faction.Id, 0);
            u.HexQ = 0; u.HexR = 0;
            var forces = body.GetDataBlob<GroundForcesDB>();
            var f = GroundForces.CreateFormation(body, s.Faction.Id, "Column");
            GroundForces.AssignUnit(f, u);

            GroundForces.QueueFormationOrder(f, GroundOrder.MoveHex(2, 0));   // waypoint 1
            GroundForces.QueueFormationOrder(f, GroundOrder.MoveHex(2, 2));   // waypoint 2
            Assert.That(f.Orders.Count, Is.EqualTo(2));

            var proc = new GroundForcesProcessor();
            for (int i = 0; i < 300 && f.Orders.Count > 0; i++) proc.ProcessEntity(body, 24 * 3600);   // a day per tick

            Assert.That(f.Orders.Count, Is.EqualTo(0), "both waypoints completed and popped");
            var leader = GroundFormationTools.Leader(forces, f);
            Assert.That(leader.HexQ, Is.EqualTo(2));
            Assert.That(leader.HexR, Is.EqualTo(2), "the leader finished at the FINAL waypoint (it ran the chain in order)");
            Log($"O1 waypoints: leader walked (0,0) → (2,0) → (2,2), queue drained");
        }

        [Test]
        [Description("O1 HoldFor: a queued hold counts down over game-time and pops when it elapses — the 'dig in for N hours' wait between waypoints.")]
        public void Orders_HoldFor_CountsDownThenPops()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            GroundForces.RaiseUnit(body, MakeDesign("u", "U", GroundUnitType.Infantry, 1), s.Faction.Id, 0);
            var f = GroundForces.CreateFormation(body, s.Faction.Id, "Guard");
            GroundForces.QueueFormationOrder(f, GroundOrder.Hold(7200));   // 2 game-hours

            var proc = new GroundForcesProcessor();
            proc.ProcessEntity(body, 3600);   // 1h elapses
            Assert.That(f.Orders.Count, Is.EqualTo(1), "still holding after 1 of 2 hours");
            proc.ProcessEntity(body, 3600);   // 2nd hour
            Assert.That(f.Orders.Count, Is.EqualTo(0), "the hold elapsed and popped");
        }

        [Test]
        [Description("O1 instant order: a queued SetEngagement applies the ROE and pops immediately — so 'move to Paris THEN stand off' switches posture on arrival.")]
        public void Orders_SetEngagement_AppliesInstantly()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            GroundForces.RaiseUnit(body, MakeDesign("u", "U", GroundUnitType.Infantry, 3), s.Faction.Id, 0);
            var forces = body.GetDataBlob<GroundForcesDB>();
            var f = GroundForces.CreateFormation(body, s.Faction.Id, "Snipers");
            GroundForces.QueueFormationOrder(f, GroundOrder.Roe(GroundEngagementStance.StandOff));

            new GroundForcesProcessor().ProcessEntity(body, 1);
            Assert.That(f.Engagement, Is.EqualTo(GroundEngagementStance.StandOff), "the queued ROE order applied");
            Assert.That(f.Orders.Count, Is.EqualTo(0), "and popped (instant order)");
        }

        [Test]
        [Description("O1 precedence: an explicit queued order OVERRIDES the auto-ROE — a Stand-off formation with a queued Hold does NOT auto-kite (the plan wins over the standing posture).")]
        public void Orders_QueuedPlan_SuppressesAutoRoe()
        {
            var s = TestScenario.CreateWithColony();
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingBody;
            if (body.HasDataBlob<PlanetEnvironmentsDB>()) body.RemoveDataBlob<PlanetEnvironmentsDB>();
            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            PaveRegionHexes(body, regionsDB, 0);

            var clone = GroundForces.RaiseUnit(body, MakeDesign("clone", "Clone", GroundUnitType.Infantry, range: 3), s.Faction.Id, 0);
            clone.HexQ = 3; clone.HexR = 0;
            var zerg = GroundForces.RaiseUnit(body, MakeDesign("zerg", "Zerg", GroundUnitType.Infantry, range: 1), InvaderFaction, 0);
            zerg.HexQ = 4; zerg.HexR = 0;   // dist 1 → StandOff alone would auto-kite away

            var f = GroundForces.CreateFormation(body, s.Faction.Id, "Clones");
            GroundForces.AssignUnit(f, clone);
            GroundFormationDoctrine.SetEngagementStance(f, GroundEngagementStance.StandOff);
            GroundForces.QueueFormationOrder(f, GroundOrder.Hold(7200));   // an explicit plan is running

            new GroundForcesProcessor().ProcessEntity(body, 1);
            Assert.That(clone.HexPath, Is.Null, "the queued Hold order suppresses the auto-kite — the plan overrides the posture");
            Log("O1 precedence: queued plan overrides auto-ROE (no auto-kite while executing orders)");
        }
    }
}
