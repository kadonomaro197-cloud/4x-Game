using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;      // ColonyInfoDB
using Pulsar4X.Components;    // ComponentDesign
using Pulsar4X.Datablobs;     // OrderableDB
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;        // PlanetRegionsFactory, PlanetRegionsDB, PlanetHexFactory, GroundHex
using Pulsar4X.GroundCombat;
using Pulsar4X.Movement;      // PositionDB, WarpAbilityDB
using Pulsar4X.Ships;
using Pulsar4X.Storage;       // CargoStorageDB

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION EARTHFALL — PW.1: the CONQUER resolver's GROUND CONQUER rungs (the resolver half of the invasion end-
    /// game), driven directly through <see cref="ConquerResolver.Resolve"/> the way <c>ConquerResolverTests</c> does —
    /// pure decision, then <c>Execute</c> for the one side effect. Three deliverables, each gauged here:
    ///
    ///   (c) BEACHHEAD — once the invaders HOLD a region on the enemy world AND a ship over it (holding the orbit) carries
    ///       crated FOOTPRINT parts, the resolver decides <c>LandBeachheadParts</c> and lands one crate onto the held
    ///       region; those parts then feed the ground tick's on-site combat-engineer build (a build site forms).
    ///
    ///   (d) BRAIN kickoff — the LAND rung now forms the just-landed unit into a BATTALION (so the ground tactical brain
    ///       has hands to command), regardless of the AutoFormUp gate.
    ///
    ///   (e) INFRA tasking — an OFFENSIVE battalion standing on an enemy building hex is tasked a DestroyInfrastructure
    ///       order (STANCE-AS-GATE: a defensive/holding battalion is left alone).
    ///
    /// Engine-only → CI (`rest` shard). Byte-identical: every new rung is gated on target.IsValid (a war) AND a landed/
    /// held/offensive state no default game builds; the resolver's side effects only run inside the gated EmitOrders.
    /// The existing byte-identity tripwires live in <c>ConquerResolverTests</c> (no war → still QueueWarship).
    /// </summary>
    [TestFixture]
    public class EfConquerGroundRungsTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[conquer-ground] " + m);

        private const string FootprintPart = "default-design-building-foundation";   // a haulable FOOTPRINT (GroundFootprintAtb + general-storage)

        // ── shared setup (mirrors ConquerResolverTests) ──────────────────────────────────────────────────────────────

        /// <summary>Give <paramref name="rival"/> a colony on a REAL regioned body in the attacker's own system (reach
        /// 1.0 → MilitaryTarget picks it), region 0 owned by the rival — the enemy world. Mirrors
        /// <c>ConquerResolverTests.GiveRivalAColonyWorld</c>.</summary>
        private static Entity GiveRivalAColonyWorld(TestScenario s, Entity rival)
        {
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingSystem.GetAllEntitiesWithDataBlob<PlanetRegionsDB>()
                .FirstOrDefault(b => b.Id != s.StartingBody.Id && b.GetDataBlob<PlanetRegionsDB>().Regions.Count > 0);
            Assert.That(body, Is.Not.Null, "the start system needs a second regioned body to stand in for the enemy world");

            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            regionsDB.Regions[0].OwnerFactionID = rival.Id;   // the rival holds region 0 (the capital)

            var colony = Entity.Create();
            colony.FactionOwnerID = rival.Id;
            s.StartingSystem.AddEntity(colony);
            colony.SetDataBlob(new ColonyInfoDB(new Dictionary<int, long> { { 1, 500_000 } }, body));
            rival.GetDataBlob<FactionInfoDB>().Colonies.Add(colony);
            return body;
        }

        private static Entity StartWar(TestScenario s, out Entity rival)
        {
            rival = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            Diplomacy.DeclareWar(s.Faction, rival, CasusBelli.ConfrontRival, s.Game.TimePulse.GameGlobalDateTime);
            return GiveRivalAColonyWorld(s, rival);
        }

        private static ConquerResolver Resolver() => new ConquerResolver();
        private static StrategicObjectiveDB ConquerObjective() => new StrategicObjectiveDB { Objective = StrategicObjective.Conquer };

        // ── (d) BRAIN kickoff — the LAND rung forms the landed unit into a battalion ─────────────────────────────────

        /// <summary>A transport owned by the attacker, sitting AT <paramref name="atBody"/>, carrying one ground unit —
        /// mirrors <c>ConquerResolverTests.PlaceLoadedTransportAt</c>.</summary>
        private static (Entity ship, GroundUnit unit) PlaceLoadedTransportAt(TestScenario s, Entity atBody)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = Entity.Create();
            s.StartingSystem.AddEntity(ship);
            ship.FactionOwnerID = s.Faction.Id;
            ship.SetDataBlob(new ShipInfoDB(design));
            ship.SetDataBlob(new PositionDB(atBody));
            ship.SetDataBlob(new OrderableDB());
            ship.SetDataBlob(new WarpAbilityDB { MaxSpeed = 10000 });

            var unit = new GroundUnit
            {
                UnitId = 7, Name = "Invasion Rifles", FactionOwnerID = s.Faction.Id,
                UnitType = GroundUnitType.Infantry, Attack = 100, Defense = 10, MaxHealth = 500, Health = 500,
            };
            var transport = new GroundTransportDB();
            transport.LoadedUnits.Add(unit);
            ship.SetDataBlob(transport);
            return (ship, unit);
        }

        [Test]
        [Description("PW.1 (d): the LAND rung's Execute lands the troops AND forms the just-landed unit into a BATTALION "
                   + "(so the ground tactical brain has hands to command) — regardless of the AutoFormUp gate.")]
        public void Conquer_LandsInvasion_FormsTheLandedUnitIntoABattalion()
        {
            var s = TestScenario.CreateWithColony();
            var enemyBody = StartWar(s, out _);
            PlaceLoadedTransportAt(s, enemyBody);

            bool priorAutoFormUp = GroundAssembly.AutoFormUp;
            try
            {
                GroundAssembly.AutoFormUp = false;   // prove the resolver forms up even with the AUTO gate OFF

                var state = FactionState.Snapshot(s.Faction);
                var action = Resolver().Resolve(state, ConquerObjective());
                Assert.That(action.Kind, Is.EqualTo("LandInvasion"), "a loaded transport over a won world → LAND");

                // Resolve is pure — no formation exists yet.
                Assert.That(enemyBody.HasDataBlob<GroundForcesDB>() &&
                            GroundFormationTools.FormationsFor(enemyBody.GetDataBlob<GroundForcesDB>(), s.Faction.Id).Count > 0,
                            Is.False, "no battalion before Execute (Resolve is a pure decision)");

                action.Execute();

                var forces = enemyBody.GetDataBlob<GroundForcesDB>();
                var landed = forces.Units.First(u => u.FactionOwnerID == s.Faction.Id);
                Assert.That(landed.RegionIndex, Is.EqualTo(0), "the unit landed into region 0");
                Assert.That(landed.FormationId, Is.GreaterThanOrEqualTo(0), "the landed unit was formed up into a battalion");
                var battalions = GroundFormationTools.FormationsFor(forces, s.Faction.Id);
                Assert.That(battalions.Count, Is.GreaterThanOrEqualTo(1), "a battalion now exists for the brain to command");
                Log($"landed + formed: unit {landed.UnitId} → battalion '{battalions[0].Name}' (AutoFormUp off)");
            }
            finally { GroundAssembly.AutoFormUp = priorAutoFormUp; }
        }

        // ── (c) BEACHHEAD — land footprint parts onto held ground, feeding the on-site build ─────────────────────────

        /// <summary>A start-faction ship carrying a cargo hold, parked at <paramref name="parent"/> (mirrors
        /// <c>EfGroundConstructorTests.CargoShip</c>).</summary>
        private static Entity CargoShip(TestScenario s, Entity parent, string name)
        {
            var fi = s.Faction.GetDataBlob<FactionInfoDB>();
            foreach (var kv in fi.ShipDesigns)
            {
                var candidate = ShipFactory.CreateShip(kv.Value, s.Faction, parent, name);
                if (candidate.HasDataBlob<CargoStorageDB>()) return candidate;
                candidate.Destroy();
            }
            return ShipFactory.CreateShip(fi.ShipDesigns.Values.First(), s.Faction, parent, name);
        }

        /// <summary>Put <paramref name="count"/> units of a built component into the ship's hold, mounting warehouses
        /// until there's room (mirrors <c>EfGroundConstructorTests.SeedComponentCargo</c>).</summary>
        private static void SeedComponentCargo(TestScenario s, Entity ship, ComponentDesign comp, long count)
        {
            var warehouse = (ComponentDesign)s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns["default-design-warehouse"];
            var hold = ship.GetDataBlob<CargoStorageDB>();
            int guard = 0;
            while (hold.GetFreeUnitSpace(comp, true) < count && guard++ < 100) ship.AddComponent(warehouse);
            hold = ship.GetDataBlob<CargoStorageDB>();
            hold.AddCargoByUnit(comp, count);
        }

        [Test]
        [Description("PW.1 (c): with the invaders HOLDING a region on the enemy world and a ship over it (holding the "
                   + "orbit) carrying crated FOOTPRINT parts, the resolver decides LandBeachheadParts and lands one crate "
                   + "onto the held region (Resolve is pure until Execute); a combat engineer's on-site build then starts "
                   + "consuming them — the resolver half of the G1 beachhead chain.")]
        public void Conquer_LandsBeachheadParts_WhenHoldingGroundWithFootprintPartsInOrbit()
        {
            var s = TestScenario.CreateWithColony();
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            var enemyBody = StartWar(s, out _);
            var regions = enemyBody.GetDataBlob<PlanetRegionsDB>();
            Assert.That(regions.Regions.Count, Is.GreaterThanOrEqualTo(2), "the enemy world needs a second region to hold");

            const int held = 1;
            regions.Regions[held].OwnerFactionID = s.Faction.Id;   // the invaders have TAKEN region 1

            // A ship over the enemy world (holds the orbit — no foreign ship present), carrying 2 crated foundations.
            var part = (ComponentDesign)faction.IndustryDesigns[FootprintPart];
            var ship = CargoShip(s, enemyBody, "Beachhead Lighter");
            SeedComponentCargo(s, ship, part, 2);
            Assert.That(GroundTransport.ShipIsAtBody(ship, enemyBody), Is.True, "precondition: the ship is over the enemy world");
            Assert.That(GroundTransport.HasOrbitalControl(ship, enemyBody), Is.True, "precondition: it holds the orbit");
            Assert.That(GroundBuildings.IsFootprint(part), Is.True, "precondition: the hauled part is a footprint building");

            var state = FactionState.Snapshot(s.Faction);

            // The finder picks the held region + the ship carrying footprint parts.
            var (fShip, fRegion, fDesign) = ConquerResolver.FindBeachheadHaul(state, enemyBody);
            Assert.That(fShip, Is.EqualTo(ship), "the beachhead finder picks the ship carrying footprint parts");
            Assert.That(fRegion, Is.EqualTo(held), "onto the region the invaders hold");
            Assert.That(fDesign, Is.EqualTo(FootprintPart), "landing the footprint part it carries");

            var action = Resolver().Resolve(state, ConquerObjective());
            Assert.That(action.Kind, Is.EqualTo("LandBeachheadParts"),
                "holding ground + footprint parts in orbit → the resolver lands beachhead parts");
            Assert.That(GroundParts.PartCount(enemyBody, held, FootprintPart), Is.EqualTo(0),
                "Resolve is pure — nothing landed until Execute");

            action.Execute();
            Assert.That(GroundParts.PartCount(enemyBody, held, FootprintPart), Is.EqualTo(1),
                "Execute landed one crate onto the held region");

            // The landed parts feed the G1 on-site build: a combat engineer on the held region starts a build site.
            var engDesign = GroundUnitAssembly.RegisterAssembledDesign(
                faction, "efcgr-engineer", "Combat Engineer",
                (ComponentDesign)faction.IndustryDesigns["default-design-human-frame"],
                new List<(ComponentDesign, int)> { ((ComponentDesign)faction.IndustryDesigns["default-design-ground-constructor"], 1) });
            GroundForces.RaiseUnit(enemyBody, engDesign, s.Faction.Id, held);
            GroundBeachhead.TickBuilds(enemyBody, 86400);   // one day of on-site work
            var forces = enemyBody.GetDataBlob<GroundForcesDB>();
            Assert.That(forces.BuildSites.Count, Is.EqualTo(1),
                "the landed beachhead parts are being assembled on site (the resolver's parts fed the G1 build step)");
            Log($"beachhead: landed 1 crate onto held region {held}; on-site build accruing ({forces.BuildSites[0].ProgressPoints:0} bp)");
        }

        // ── (e) INFRA tasking — task an OFFENSIVE battalion to raze an enemy building (STANCE-AS-GATE) ───────────────

        /// <summary>Field an OFFENSIVE attacker battalion on <paramref name="enemyBody"/> standing on region 0's centre
        /// hex, which holds an enemy building. Returns the formation + the enemy building's hex.</summary>
        private static (GroundFormation formation, GroundHex enemyHex) SetupOffensiveBattalionOnEnemyBuilding(
            TestScenario s, Entity enemyBody, Entity rival, string stanceFamily)
        {
            PlanetHexFactory.EnsureHexesForBody(enemyBody);
            var regions = enemyBody.GetDataBlob<PlanetRegionsDB>();
            var centre = regions.Regions[0].Hexes.First(h => h.Q == 0 && h.R == 0);
            centre.OwnerFactionID = rival.Id;      // an ENEMY-held hex ...
            centre.InstallationIds.Add(9001);      // ... carrying an enemy building (a fort/HQ footprint)

            var design = new GroundUnitDesign
            { UniqueID = "efcgr-raider", Name = "Raider", UnitType = GroundUnitType.Infantry, Attack = 1000, Defense = 10, HitPoints = 500, Range = 1 };
            var u = GroundForces.RaiseUnit(enemyBody, design, s.Faction.Id, 0);
            u.HexQ = 0; u.HexR = 0;   // co-located with the enemy building hex → in range
            var f = GroundForces.CreateFormation(enemyBody, s.Faction.Id, "1st Raiders");
            GroundForces.AssignUnit(f, u);
            f.StanceFamily = stanceFamily;   // the tactical-brain posture the resolver gates on
            return (f, centre);
        }

        [Test]
        [Description("PW.1 (e): an OFFENSIVE attacker battalion standing on an enemy building hex is tasked a "
                   + "DestroyInfrastructure order against that hex (Resolve pure until Execute) — the strategy→tactics seam.")]
        public void Conquer_TasksInfraDestroy_ForAnOffensiveBattalion_OnAnEnemyBuildingHex()
        {
            var s = TestScenario.CreateWithColony();
            var enemyBody = StartWar(s, out var rival);
            var (formation, enemyHex) = SetupOffensiveBattalionOnEnemyBuilding(s, enemyBody, rival, GroundTactics.Offensive);

            var state = FactionState.Snapshot(s.Faction);

            var (fBody, fFormation, fRegion, fQ, fR) = ConquerResolver.FindInfraTasking(state, enemyBody);
            Assert.That(fFormation, Is.EqualTo(formation), "the infra finder picks the offensive battalion on the enemy building");
            Assert.That(fRegion, Is.EqualTo(0), "targeting its own region");
            Assert.That((fQ, fR), Is.EqualTo((enemyHex.Q, enemyHex.R)), "and the enemy building's hex");

            var action = Resolver().Resolve(state, ConquerObjective());
            Assert.That(action.Kind, Is.EqualTo("TaskInfraDestroy"),
                "an offensive battalion on an enemy building → the resolver tasks a raze");
            Assert.That(formation.Orders, Is.Empty, "Resolve is pure — no order queued until Execute");

            action.Execute();
            Assert.That(formation.Orders, Has.Count.EqualTo(1), "Execute queued the raze order");
            var order = formation.Orders[0];
            Assert.That(order.Type, Is.EqualTo(GroundOrderType.DestroyInfrastructure), "it's a DestroyInfrastructure order");
            Assert.That(order.TargetRegion, Is.EqualTo(0));
            Assert.That((order.TargetQ, order.TargetR), Is.EqualTo((enemyHex.Q, enemyHex.R)), "aimed at the enemy building hex");
            Assert.That(order.Issuer, Is.EqualTo(GroundOrderIssuer.Player),
                "the strategic order carries the hands-off (Player) issuer so the per-tick tactical brain won't stomp it");
            Log($"infra: '{formation.Name}' (Offensive) tasked to raze region 1 hex ({order.TargetQ},{order.TargetR})");
        }

        [Test]
        [Description("PW.1 (e) STANCE-AS-GATE: a DEFENSIVE battalion on the SAME enemy building is NOT tasked to raze — "
                   + "only an offensive (pressing) battalion razes infrastructure (the FLAGGED aggression policy).")]
        public void Infra_StanceGate_ADefensiveBattalionIsNotTasked()
        {
            var s = TestScenario.CreateWithColony();
            var enemyBody = StartWar(s, out var rival);
            var (formation, _) = SetupOffensiveBattalionOnEnemyBuilding(s, enemyBody, rival, GroundTactics.Defensive);

            var state = FactionState.Snapshot(s.Faction);

            var (_, fFormation, _, _, _) = ConquerResolver.FindInfraTasking(state, enemyBody);
            Assert.That(fFormation, Is.Null, "a defensive battalion is not tasked to raze (stance-as-gate)");

            var action = Resolver().Resolve(state, ConquerObjective());
            Assert.That(action.Kind, Is.Not.EqualTo("TaskInfraDestroy"),
                "the resolver does NOT task a raze for a non-offensive battalion");
            Assert.That(formation.Orders, Is.Empty, "and no order was queued");
            Log($"stance-gate: a Defensive battalion is left alone (resolver chose '{action.Kind}')");
        }
    }
}
