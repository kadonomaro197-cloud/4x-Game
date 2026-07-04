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
    }
}
