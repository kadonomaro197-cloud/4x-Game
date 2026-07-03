using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Stations;
using Pulsar4X.Datablobs;
using Pulsar4X.Storage;
using Pulsar4X.Industry;
using Pulsar4X.Names;
using Pulsar4X.Colonies;
using Pulsar4X.Components;
using Pulsar4X.Extensions;
using Pulsar4X.Galaxy;
using Pulsar4X.Technology;
using Pulsar4X.Damage;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The station-host foundation gauge. A space station is the deliberate PARALLEL to a colony
    /// (docs/SPACE-STATIONS-DESIGN.md): its OWN chassis, but carrying the SAME shared component-equipment
    /// layer a colony does. These tests assert StationFactory wires that chassis correctly — the same blob
    /// set a colony gets, registered on the faction's parallel Stations registry — which is the precondition
    /// for the economy processors (which discover work by ability component, not by host type) to process a
    /// station for free in the next slice.
    /// </summary>
    [TestFixture]
    public class StationFactoryTests
    {
        [Test]
        [Description("A station built on a body carries the shared infrastructure chassis and is registered on the faction.")]
        public void CreateStation_WiresSharedChassis_AndRegistersOnFaction()
        {
            var s = TestScenario.CreateWithColony();

            var station = StationFactory.CreateStation(s.Faction, s.StartingBody);

            Assert.That(station, Is.Not.Null);
            Assert.That(station, Is.Not.EqualTo(Entity.InvalidEntity));

            // Core station host
            Assert.That(station.HasDataBlob<StationInfoDB>(), Is.True, "station missing StationInfoDB host");
            var info = station.GetDataBlob<StationInfoDB>();
            Assert.That(info.HostingBodyEntity, Is.EqualTo(s.StartingBody),
                "station's hosting body should be the body it was built on");

            // The SHARED equipment chassis — same blobs a colony carries, so the economy processors
            // (which key off these ability blobs, not the host type) can act on a station.
            Assert.That(station.HasDataBlob<NameDB>(), Is.True, "missing NameDB");
            Assert.That(station.HasDataBlob<ComponentInstancesDB>(), Is.True, "missing ComponentInstancesDB (the equipment layer)");
            Assert.That(station.HasDataBlob<CargoStorageDB>(), Is.True, "missing CargoStorageDB");
            Assert.That(station.HasDataBlob<MiningDB>(), Is.True, "missing MiningDB");
            Assert.That(station.HasDataBlob<InfrastructureDB>(), Is.True, "missing InfrastructureDB");

            // Registered as a station on the faction (the parallel registry to Colonies)
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            Assert.That(factionInfo.Stations, Does.Contain(station), "station not registered in FactionInfoDB.Stations");
            Assert.That(factionInfo.Colonies, Does.Not.Contain(station), "a station must NOT land in the Colonies registry");
            Assert.That(station.FactionOwnerID, Is.EqualTo(s.Faction.Id), "station not owned by the faction");

            // It lives in the same star-system manager as the body, like a colony does.
            Assert.That(station.Manager, Is.EqualTo(s.StartingBody.Manager));
        }

        [Test]
        [Description("A manned station carries population; an automated one defaults to empty.")]
        public void CreateStation_PopulationOptional()
        {
            var s = TestScenario.CreateWithColony();

            var automated = StationFactory.CreateStation(s.Faction, s.StartingBody);
            Assert.That(automated.GetDataBlob<StationInfoDB>().Population, Is.Empty,
                "an automated platform should start unmanned");

            var manned = StationFactory.CreateStation(s.Faction, s.StartingBody, 5000, s.Species);
            var pop = manned.GetDataBlob<StationInfoDB>().Population;
            Assert.That(pop.ContainsKey(s.Species.Id), Is.True, "manned station should house the given species");
            Assert.That(pop[s.Species.Id], Is.EqualTo(5000));
        }

        [Test]
        [Description("StationInfoDB clones deeply so it survives entity transfer / save-load.")]
        public void StationInfoDB_ClonesDeeply()
        {
            var hostingBody = Entity.Create();
            var original = new StationInfoDB(hostingBody);
            original.Population[42] = 1234;
            original.ComponentStockpile["widget"] = 7;

            var clone = (StationInfoDB)original.Clone();
            Assert.That(clone.HostingBodyEntity, Is.EqualTo(hostingBody));
            Assert.That(clone.Population[42], Is.EqualTo(1234));
            Assert.That(clone.ComponentStockpile["widget"], Is.EqualTo(7));

            // Deep copy: mutating the clone must not touch the original's collections.
            clone.Population[42] = 9999;
            Assert.That(original.Population[42], Is.EqualTo(1234), "Population dictionary was shared, not cloned");
        }

        [Test]
        [Description("The host-agnostic mining seam: TryGetMiningBody resolves a colony's planet and a station's hosting body, and rejects a plain body.")]
        public void TryGetMiningBody_ResolvesColonyAndStation()
        {
            var s = TestScenario.CreateWithColony();
            var planet = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;

            Assert.That(MiningHelper.TryGetMiningBody(s.Colony, out var colonyBody), Is.True);
            Assert.That(colonyBody, Is.EqualTo(planet), "a colony's mining body is its planet");

            var station = StationFactory.CreateStation(s.Faction, planet);
            Assert.That(MiningHelper.TryGetMiningBody(station, out var stationBody), Is.True);
            Assert.That(stationBody, Is.EqualTo(planet), "a station's mining body is its hosting body");

            // A bare body (neither colony nor station) is not a mining host.
            Assert.That(MiningHelper.TryGetMiningBody(planet, out _), Is.False);
        }

        [Test]
        [Description("A station carrying the colony's own mine + cargo modules mines its hosting body — the economy comes for free, no station-specific code (task #17).")]
        public void Station_WithMiningModule_MinesItsHostingBody()
        {
            var s = TestScenario.CreateWithColony();
            var planet = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;
            Assert.That(planet.HasDataBlob<MineralsDB>(), Is.True, "precondition: the colony's planet has minerals to mine");

            // Reuse the SAME mine + cargo-store designs the starting colony already runs — proves a station
            // rides the identical component chassis with no station-aware economy code.
            var colonyComponents = s.Colony.GetDataBlob<ComponentInstancesDB>();
            Assert.That(colonyComponents.TryGetComponentsByAttribute<MineResourcesAtbDB>(out var mineInstances), Is.True,
                "precondition: the starting colony has a mine to copy");
            Assert.That(colonyComponents.TryGetComponentsByAttribute<CargoStorageAtb>(out var cargoInstances), Is.True,
                "precondition: the starting colony has cargo storage to copy");
            var mineDesign = mineInstances.First().Design;
            var cargoDesign = cargoInstances.First().Design;

            var station = StationFactory.CreateStation(s.Faction, planet);
            station.AddComponent(cargoDesign); // a hold to mine into (a bare station has zero storage)
            station.AddComponent(mineDesign);  // the mine — AddComponent triggers ReCalc → sets ActualMiningRate

            var stockpile = station.GetDataBlob<CargoStorageDB>();
            double before = stockpile.TotalStoredMass;

            s.AdvanceTime(TimeSpan.FromDays(60));

            Assert.That(stockpile.TotalStoredMass, Is.GreaterThan(before),
                "a station with a mine + hold should have extracted minerals off its hosting body");
        }

        [Test]
        [Description("Deploy-bare-build-in-situ (Model 2): installing a constructor module makes a station a BUILDER — it gains a production line that handles the same industry type a colony uses to build installations, and accepts a build job set to install ON the station. The fabricate→install step (ComponentDesign.OnConstructionComplete → InstallOn.AddComponent) is host-agnostic, already proven by the colony ProductionBuildTests + the station mining test above.")]
        public void Station_WithConstructorModule_IsAnInSituBuilder()
        {
            var s = TestScenario.CreateWithColony();
            var planet = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();

            var colonyComps = s.Colony.GetDataBlob<ComponentInstancesDB>();

            // A refinery is the canonical installation the colony build path (ProductionBuildTests) already builds.
            var refineryDesign = factionInfo.IndustryDesigns["default-design-refinery"];

            // Copy the colony's FULL set of constructor/industry modules onto the station. The colony has several
            // (factory, refinery, shipyard…), each providing a DIFFERENT industry-type line; a refinery is built by
            // the installation-construction line specifically, so grabbing just one IndustryAtb component can miss it.
            var industryDesigns = colonyComps.GetDesignsByType(typeof(IndustryAtb));
            Assert.That(industryDesigns, Is.Not.Empty, "precondition: the colony has constructor/industry modules to copy");

            var station = StationFactory.CreateStation(s.Faction, planet);
            Assert.That(station.HasDataBlob<IndustryAbilityDB>(), Is.False, "a bare station has no production line yet");

            foreach (var design in industryDesigns)
                station.AddComponent(design); // install the constructors → the in-situ build capability

            Assert.That(station.HasDataBlob<IndustryAbilityDB>(), Is.True,
                "installing a constructor module must give the station a production line (IndustryAbilityDB)");

            var industry = station.GetDataBlob<IndustryAbilityDB>();
            string lineId = industry.ProductionLines
                .FirstOrDefault(l => l.Value.IndustryTypeRates.ContainsKey(refineryDesign.IndustryTypeID)).Key;
            Assert.That(lineId, Is.Not.Null,
                "the station's constructor should handle the same industry type the colony uses to build installations");

            // The build job queues on the station, set to install the finished module ON the station (in-situ).
            var job = new IndustryJob(factionInfo, "default-design-refinery");
            job.InitialiseJob(1, false);
            job.InstallOn = station;
            Assert.DoesNotThrow(() => IndustryTools.AddJob(station, lineId, job),
                "a station with a constructor should accept an in-situ build job");
            Assert.That(industry.ProductionLines[lineId].Jobs, Does.Contain(job),
                "the in-situ build job should be queued on the station's own production line");
        }

        [Test]
        [Description("Station population is gated by LIFE SUPPORT (the sealed-habitat model): a manned station with no habitat modules loses population over time — where a native-world colony would grow un-capped. Proves StationPopulationProcessor runs and applies the station-specific cap semantic (task #17, population half).")]
        public void MannedStation_WithNoLifeSupport_LosesPopulation()
        {
            var s = TestScenario.CreateWithColony();

            var manned = StationFactory.CreateStation(s.Faction, s.StartingBody, 5000, s.Species);
            Assert.That(manned.HasDataBlob<ColonyMoraleDB>(), Is.True,
                "a station should carry the shared morale valve");

            long before = manned.GetDataBlob<StationInfoDB>().Population[s.Species.Id];
            Assert.That(before, Is.EqualTo(5000));

            s.AdvanceTime(TimeSpan.FromDays(120)); // ~4 monthly population ticks

            long after = manned.GetDataBlob<StationInfoDB>().Population[s.Species.Id];
            Assert.That(after, Is.LessThan(before),
                "a sealed-habitat station with no life-support modules should starve (lose population), not grow");
        }

        [Test]
        [Description("The void habitat (default-design-space-habitat) supports station population in MICROGRAVITY where Earth-toleranced infrastructure provides nothing — and a manned station carrying it GROWS toward the habitat cap. This is the data that makes an asteroid station viable (task #18 groundwork).")]
        public void SpaceHabitat_SupportsPopulationInMicrogravity_WhereEarthInfraCannot()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();

            // Both infrastructure designs are unlocked at start (added to the colony blueprint).
            Assert.That(factionInfo.IndustryDesigns.ContainsKey("default-design-space-habitat"), Is.True,
                "the space habitat design should be unlocked at start");
            var habDesign = (ComponentDesign)factionInfo.IndustryDesigns["default-design-space-habitat"];
            var earthInfra = (ComponentDesign)factionInfo.IndustryDesigns["default-design-infrastructure"];

            // A real low-gravity body (asteroid / dwarf) in Sol to station between.
            Entity lowGravBody = null;
            foreach (var e in s.StartingSystem.GetAllEntitiesWithDataBlob<SystemBodyInfoDB>())
            {
                if (e.GetDataBlob<SystemBodyInfoDB>().Gravity < 1.0 && e.HasDataBlob<MassVolumeDB>() && e.HasDataBlob<NameDB>())
                {
                    lowGravBody = e;
                    break;
                }
            }
            Assert.That(lowGravBody, Is.Not.Null, "Sol should have a low-gravity body (asteroid/dwarf) to station on");

            // Earth-toleranced infrastructure supports NO ONE in microgravity (out of gravity/pressure tolerance)…
            var earthStation = StationFactory.CreateStation(s.Faction, lowGravBody, 100, s.Species);
            earthStation.AddComponent(earthInfra);
            Assert.That(earthStation.GetDataBlob<ComponentInstancesDB>().GetPopulationSupportValue(lowGravBody), Is.EqualTo(0),
                "Earth-toleranced infrastructure provides no life support in microgravity");

            // …but the void-rated space habitat does.
            var habStation = StationFactory.CreateStation(s.Faction, lowGravBody, 100, s.Species);
            habStation.AddComponent(habDesign);
            Assert.That(habStation.GetDataBlob<ComponentInstancesDB>().GetPopulationSupportValue(lowGravBody), Is.GreaterThan(0),
                "the void-rated space habitat supports population in microgravity");

            // And a manned, habitat-supported station GROWS toward the cap (where a bare station would starve).
            long before = habStation.GetDataBlob<StationInfoDB>().Population[s.Species.Id];
            s.AdvanceTime(TimeSpan.FromDays(120));
            long after = habStation.GetDataBlob<StationInfoDB>().Population[s.Species.Id];
            Assert.That(after, Is.GreaterThan(before),
                "a habitat-supported station should grow population, not starve");
        }

        [Test]
        [Description("A RESEARCH STATION, cradle-to-grave: a station carrying a research-lab module spawns a ResearcherDB and accrues research points toward a queued tech (paid from faction funds) — proving research is host-agnostic and a station can BE a research flavor (task #18).")]
        public void ResearchStation_AccruesResearchTowardAQueuedTech()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var planet = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;

            var labDesign = (ComponentDesign)factionInfo.IndustryDesigns["default-design-research-lab"];

            var station = StationFactory.CreateStation(s.Faction, planet);
            station.AddComponent(labDesign); // installing the lab spawns a ResearcherDB on a new entity tied to the station

            // The lab spawns its ResearcherDB on a SEPARATE entity, tagged with LocationId = the station.
            Entity labEntity = null;
            foreach (var e in station.Manager.GetAllEntitiesWithDataBlob<ResearcherDB>())
            {
                if (e.GetDataBlob<ResearcherDB>().LocationId == station.Id) { labEntity = e; break; }
            }
            Assert.That(labEntity, Is.Not.Null, "installing a research lab on a station should spawn a ResearcherDB tied to it");
            var researcherDB = labEntity.GetDataBlob<ResearcherDB>();
            Assert.That(researcherDB.PointsPerDay.GetValue(), Is.GreaterThan(0), "the station's lab should produce research points");

            // Queue a researchable tech (the faction starts with ample funds to pay the research cost).
            string techId = null;
            Tech tech = null;
            foreach (var kv in factionInfo.Data.Techs)
            {
                if (factionInfo.Data.IsResearchable(kv.Key)) { techId = kv.Key; tech = kv.Value; break; }
            }
            Assert.That(techId, Is.Not.Null, "the faction should have at least one researchable tech to queue");

            int levelBefore = tech.Level;
            int progressBefore = tech.ResearchProgress;
            ResearchProcessor.AssignTech(researcherDB, techId);

            s.AdvanceTime(TimeSpan.FromDays(120));

            Assert.That(tech.ResearchProgress > progressBefore || tech.Level > levelBefore, Is.True,
                "the research station should have accrued research points toward the queued tech");
        }

        // A bombardment hit packet. A station never runs the per-pixel wavelength sim (it has no
        // EntityDamageProfileDB), so only Energy is read — Position/Signature are set for shape only.
        private static DamageFragment Hit(double energy) => new DamageFragment
        {
            Energy = energy,
            Position = (0, 0),
            Signature = DamageSignature.Kinetic,
        };

        [Test]
        [Description("GRAVE RUNG (Slice B): a station is no longer a 'ghost target'. A weapon hit routes through DamageProcessor.OnStationDamage, deals real damage (> 0, closing the return-0 hole), and drains the station's structural-integrity pool — but a survivable hit does NOT destroy it.")]
        public void Station_TakesDamage_DrainsStructuralIntegrity_ButSurvivesASmallHit()
        {
            var s = TestScenario.CreateWithColony();
            var station = StationFactory.CreateStation(s.Faction, s.StartingBody);

            double integrityBefore = station.GetDataBlob<StationInfoDB>().StructuralIntegrity;
            Assert.That(integrityBefore, Is.EqualTo(StationInfoDB.BaseStructuralIntegrity),
                "a fresh station starts at full structural integrity");

            // A modest hit: 1e10 J → damageStrength 100, well under the 500 pool.
            var result = DamageProcessor.OnTakingDamage(station, Hit(1e10));

            Assert.That(result.Damage, Is.GreaterThan(0),
                "a station must take REAL damage now (the ghost-target return-0 hole is closed)");
            Assert.That(result.Destroyed, Is.False, "a small hit should not destroy the station");
            Assert.That(station.GetDataBlob<StationInfoDB>().StructuralIntegrity, Is.LessThan(integrityBefore),
                "the hit should have drained the structural-integrity pool");
            Assert.That(station.IsValid, Is.True, "a surviving station is still a live entity");
        }

        [Test]
        [Description("GRAVE RUNG (Slice B): a hit that exhausts the structural pool DESTROYS the station and unregisters it cleanly — removed from FactionInfoDB.Stations and un-owned (no dangling reference), the inverse of CreateStation.")]
        public void Station_DestroyedWhenStructuralIntegrityExhausted_AndUnregisters()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var station = StationFactory.CreateStation(s.Faction, s.StartingBody);
            Assert.That(factionInfo.Stations, Does.Contain(station), "precondition: registered on the faction");

            // An overwhelming hit: 1e12 J → damageStrength 10,000, far above the 500 pool.
            var result = DamageProcessor.OnTakingDamage(station, Hit(1e12));

            Assert.That(result.Destroyed, Is.True, "an overwhelming hit should destroy the station");
            Assert.That(factionInfo.Stations, Does.Not.Contain(station),
                "a destroyed station must be removed from the faction's Stations registry (no dangling ref)");
            Assert.That(station.FactionOwnerID, Is.EqualTo(-1),
                "a destroyed station must be un-owned (FactionOwnerDB.RemoveEntity)");
        }

        [Test]
        [Description("GRAVE RUNG (Slice B): destroying a station tears down its SPAWNED sub-entities. A research station's ResearcherDB lives on a separate entity that RemoveComponentInstance never cleans up — so without the teardown a dead station keeps researching. DestroyStation must kill the researcher.")]
        public void DestroyedStation_TearsDownSpawnedResearcher()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var planet = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;
            var labDesign = (ComponentDesign)factionInfo.IndustryDesigns["default-design-research-lab"];

            var station = StationFactory.CreateStation(s.Faction, planet);
            station.AddComponent(labDesign);

            Entity labEntity = null;
            foreach (var e in station.Manager.GetAllEntitiesWithDataBlob<ResearcherDB>())
            {
                if (e.GetDataBlob<ResearcherDB>().LocationId == station.Id) { labEntity = e; break; }
            }
            Assert.That(labEntity, Is.Not.Null, "precondition: the lab spawned a researcher tied to the station");
            Assert.That(labEntity.IsValid, Is.True);

            StationFactory.DestroyStation(station);

            Assert.That(labEntity.IsValid, Is.False,
                "the destroyed station's spawned researcher must be torn down (no orphan researching from the grave)");
        }

        [Test]
        [Description("GRAVE RUNG (Slice B): orbital bombardment of a MANNED station kills people (the population half of the grave rung), the same 250k/unit casualty math a colony takes.")]
        public void MannedStation_Bombardment_KillsPopulation()
        {
            var s = TestScenario.CreateWithColony();
            // A large population + a small hit → PARTIAL casualties (250k dead), so the station survives to be measured.
            var station = StationFactory.CreateStation(s.Faction, s.StartingBody, 30_000_000, s.Species);
            long before = station.GetDataBlob<StationInfoDB>().Population[s.Species.Id];

            var result = DamageProcessor.OnTakingDamage(station, Hit(1e8)); // damageStrength 1 → 250k casualties

            Assert.That(result.Destroyed, Is.False, "the station should survive a single small hit");
            long after = station.GetDataBlob<StationInfoDB>().Population[s.Species.Id];
            Assert.That(after, Is.LessThan(before), "a bombardment hit should kill some of the station's population");
        }

        [Test]
        [Description("FRONT DOOR (Slice A2): a station is deployed from a CONSTRUCTION SHIP at wherever it is parked — including in orbit of a STAR, the canonical research-station target you'd never colonize. The station anchors to the ship's SOI body, carries a starter constructor (in-situ builder), and the reusable vessel survives to deploy again.")]
        public void ConstructionShip_DeploysStation_AtAStar_AndSurvives()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();

            // The STAR — a place you'd never colonize; the whole reason a station beats a planet here.
            Entity star = s.StartingSystem.GetAllEntitiesWithDataBlob<StarInfoDB>().First();

            // A construction/hauler ship (has a cargo hold), created parked at the STAR.
            Entity ship = null;
            foreach (var kv in factionInfo.ShipDesigns)
            {
                var candidate = ShipFactory.CreateShip(kv.Value, s.Faction, star, "Constructor");
                if (candidate.HasDataBlob<CargoStorageDB>()) { ship = candidate; break; }
                candidate.Destroy();
            }
            Assert.That(ship, Is.Not.Null, "precondition: a start ship design with a cargo hold to act as the constructor vessel");

            var anchor = ship.GetSOIParentEntity();
            Assert.That(anchor, Is.Not.Null, "a ship parked at the star is in the star's SOI");

            int stationsBefore = factionInfo.Stations.Count;
            var command = DeployStationOrder.CreateCommand(ship);
            Assert.That(command.IsValidCommand(s.Game), Is.True, "a hauler parked at a body is a valid construction vessel");
            command.Execute(s.Game.TimePulse.GameGlobalDateTime);

            Assert.That(factionInfo.Stations.Count, Is.EqualTo(stationsBefore + 1),
                "deploying should register exactly one new station on the faction");
            var station = factionInfo.Stations[factionInfo.Stations.Count - 1];
            Assert.That(station.GetDataBlob<StationInfoDB>().HostingBodyEntity.Id, Is.EqualTo(anchor.Id),
                "the station should anchor to the body the construction ship was parked at (the star / its SOI) — NOT a colonized planet");
            Assert.That(station.HasDataBlob<IndustryAbilityDB>(), Is.True,
                "the deployed platform should carry a starter constructor (an in-situ build line)");
            Assert.That(ship.IsValid, Is.True, "a reusable constructor vessel survives the deploy and can deploy again");
        }

        [Test]
        [Description("COST CURVE (Slice C): a station bills a monthly operating cost to its faction that RISES with function-diversity — the 'cheap while focused, expensive as a planet-replacement' gradient. A multi-function station costs more than a focused one, and upkeep drains faction funds over time.")]
        public void StationUpkeep_DrainsFunds_AndScalesWithFunctionDiversity()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var planet = s.Colony.GetDataBlob<ColonyInfoDB>().PlanetEntity;

            // Several DISTINCT module designs from the colony, to give stations different function-diversity.
            var colonyComps = s.Colony.GetDataBlob<ComponentInstancesDB>();
            var industryDesigns = colonyComps.GetDesignsByType(typeof(IndustryAtb));
            Assert.That(industryDesigns.Count, Is.GreaterThan(1),
                "precondition: the colony has multiple distinct constructor designs to vary function-diversity");

            // A FOCUSED station carries ONE module; a GENERAL station carries many DISTINCT modules.
            var focused = StationFactory.CreateStation(s.Faction, planet);
            focused.AddComponent(industryDesigns[0]);

            var general = StationFactory.CreateStation(s.Faction, planet);
            foreach (var d in industryDesigns)
                general.AddComponent(d);

            // The gradient: a do-more station costs more to run than a focused one.
            decimal focusedCost = StationEconomyDB.OperatingCost(focused);
            decimal generalCost = StationEconomyDB.OperatingCost(general);
            Assert.That(generalCost, Is.GreaterThan(focusedCost),
                "a station with more distinct functions should have a higher operating cost (the cost gradient)");

            // Upkeep actually draws down the faction's funds over time (monthly billing).
            decimal fundsBefore = factionInfo.Money.GetCurrentFunds();
            s.AdvanceTime(TimeSpan.FromDays(90)); // ~3 monthly upkeep billings
            decimal fundsAfter = factionInfo.Money.GetCurrentFunds();
            Assert.That(fundsAfter, Is.LessThan(fundsBefore),
                "station upkeep should draw down the faction's funds each month");
        }
    }
}
