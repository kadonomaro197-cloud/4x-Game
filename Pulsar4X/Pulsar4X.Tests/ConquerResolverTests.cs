using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Datablobs;   // OrderableDB
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;
using Pulsar4X.GroundCombat;
using Pulsar4X.Industry;
using Pulsar4X.Movement;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.8 P-3 gauge (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md — the means-ends planner): the CONQUER resolver
    /// (Ambition tier), the SIXTH and last objective. v1 proves the NPC MASSES a strike fleet — queues an armed hull
    /// on a free ship line — when pursuing Conquer, and that Conquer is now registered (so every objective resolves).
    /// The actual attack (target-selection / reach / fuel / strike) is the deferred P-3 military sub-subsystem.
    /// Resolve is a pure decision (no side effect until Execute).
    ///
    /// B5-b (the LAND rung, 2026-07-15) adds the invasion KEYSTONE gauge: with a loaded transport sitting over a won
    /// enemy world, the CONQUER resolver's top rung LANDS the troops and the ground processor CAPTURES the region —
    /// the "take a planet" payoff, driven through the real resolver + order + ground paths, no sim advance.
    /// </summary>
    [TestFixture]
    public class ConquerResolverTests
    {
        private static int TotalJobs(Entity colony)
        {
            var ind = colony.GetDataBlob<IndustryAbilityDB>();
            int n = 0;
            foreach (var line in ind.ProductionLines.Values) n += line.Jobs.Count;
            return n;
        }

        [Test]
        [Description("Under Conquer the NPC masses a strike fleet — queues a warship; Execute queues it; Resolve is pure.")]
        public void Conquer_MassesAStrikeFleet()
        {
            var s = TestScenario.CreateWithColony();
            var state = FactionState.Snapshot(s.Faction);
            var objective = new StrategicObjectiveDB { Objective = StrategicObjective.Conquer };

            int before = TotalJobs(s.Colony);
            var action = new ConquerResolver().Resolve(state, objective);

            Assert.That(action.Kind, Is.EqualTo("QueueWarship"), "Conquer builds a warship to mass a strike fleet");
            Assert.That(TotalJobs(s.Colony), Is.EqualTo(before), "Resolve is a pure decision — nothing queued until Execute");

            action.Execute();
            Assert.That(TotalJobs(s.Colony), Is.GreaterThan(before), "Execute queues the warship build (+ any sub-jobs)");
        }

        [Test]
        [Description("The registry now resolves Conquer — the sixth and final objective; the planner is complete.")]
        public void Registry_ResolvesConquer()
        {
            Assert.That(ObjectiveResolvers.TryGet(StrategicObjective.Conquer, out var r), Is.True);
            Assert.That(r.Handles, Is.EqualTo(StrategicObjective.Conquer));
        }

        // ── B5-b — THE LAND RUNG (the invasion keystone) ────────────────────────────────────────────────────────────

        /// <summary>Give <paramref name="rival"/> a colony on a REAL regioned body in the attacker's own system (reach
        /// 1.0, so MilitaryTarget picks it), region 0 owned by the rival — the world to take. Uses a real body (not a
        /// synthetic one) so it carries a PositionDB (a ship can orbit it), a MassVolumeDB, and a full region surface
        /// with adjacency + hex grid — everything GroundForcesProcessor needs to run cleanly to the capture step (the
        /// same setup as TakeAPlanetIntegrationTests). Returns the body.</summary>
        private static Entity GiveRivalAColonyWorld(TestScenario s, Entity rival)
        {
            PlanetRegionsFactory.GenerateForSystem(s.StartingSystem, surveyed: true);
            var body = s.StartingSystem.GetAllEntitiesWithDataBlob<PlanetRegionsDB>()
                .FirstOrDefault(b => b.Id != s.StartingBody.Id && b.GetDataBlob<PlanetRegionsDB>().Regions.Count > 0);
            Assert.That(body, Is.Not.Null, "the start system needs a second regioned body to stand in for the enemy world");

            var regionsDB = body.GetDataBlob<PlanetRegionsDB>();
            regionsDB.Regions[0].OwnerFactionID = rival.Id;   // the rival holds the world (region 0 = the capital)

            var colony = Entity.Create();
            colony.FactionOwnerID = rival.Id;
            s.StartingSystem.AddEntity(colony);
            colony.SetDataBlob(new ColonyInfoDB(new Dictionary<int, long> { { 1, 500_000 } }, body));
            rival.GetDataBlob<FactionInfoDB>().Colonies.Add(colony);
            return body;
        }

        /// <summary>A transport owned by the attacker, sitting AT <paramref name="atBody"/>, carrying one ground unit —
        /// the "won the orbit, troops aboard" state the LAND rung acts on. Hand-built (rather than via ShipFactory) for
        /// direct control of the loaded state: ShipInfoDB (so it's found), PositionDB parented to the body (at body),
        /// OrderableDB (so the instant land order executes through the lane), GroundTransportDB with the loaded unit.</summary>
        private static (Entity ship, GroundUnit unit) PlaceLoadedTransportAt(TestScenario s, Entity atBody)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = Entity.Create();
            s.StartingSystem.AddEntity(ship);
            ship.FactionOwnerID = s.Faction.Id;
            ship.SetDataBlob(new ShipInfoDB(design));
            ship.SetDataBlob(new PositionDB(atBody));   // parent = the target body → GroundTransport.ShipIsAtBody true
            ship.SetDataBlob(new OrderableDB());          // the land order runs through the OrderableProcessor lane

            var unit = new GroundUnit
            {
                UnitId = 7,
                Name = "Invasion Rifles",
                FactionOwnerID = s.Faction.Id,
                UnitType = GroundUnitType.Infantry,
                Attack = 100, Defense = 10, MaxHealth = 500, Health = 500,
            };
            var transport = new GroundTransportDB();
            transport.LoadedUnits.Add(unit);
            ship.SetDataBlob(transport);
            return (ship, unit);
        }

        [Test]
        [Description("B5-b keystone: with a loaded transport holding the orbit over a war target, the CONQUER resolver's "
                   + "top rung LANDS the troops (a pure LandInvasion decision until Execute), and the ground processor "
                   + "then CAPTURES the region — take-a-planet driven end-to-end through resolver + order + ground paths.")]
        public void Conquer_LandsTheInvasion_AndCapturesTheRegion()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            Diplomacy.DeclareWar(s.Faction, reds, CasusBelli.ConfrontRival, s.Game.TimePulse.GameGlobalDateTime);

            var enemyBody = GiveRivalAColonyWorld(s, reds);
            var (transport, unit) = PlaceLoadedTransportAt(s, enemyBody);
            var regionsDB = enemyBody.GetDataBlob<PlanetRegionsDB>();
            Assert.That(regionsDB.Regions[0].OwnerFactionID, Is.EqualTo(reds.Id), "precondition: the rival holds region 0");

            var state = FactionState.Snapshot(s.Faction);
            var objective = new StrategicObjectiveDB { Objective = StrategicObjective.Conquer };
            var action = new ConquerResolver().Resolve(state, objective);

            // The top rung fires because a loaded transport holds the orbit over the target world.
            Assert.That(action.Kind, Is.EqualTo("LandInvasion"),
                "with troops aboard a transport over a won enemy world, CONQUER lands them (the highest-priority rung)");

            // Resolve is a pure decision — nothing has landed yet.
            Assert.That(transport.GetDataBlob<GroundTransportDB>().LoadedUnits, Has.Count.EqualTo(1),
                "Resolve is pure — the unit is still aboard the transport until Execute");
            Assert.That(enemyBody.HasDataBlob<GroundForcesDB>() &&
                        enemyBody.GetDataBlob<GroundForcesDB>().Units.Any(u => u.FactionOwnerID == s.Faction.Id), Is.False,
                "no invader unit is on the surface before Execute");

            // Execute the one step: the land order fires synchronously through the order handler.
            action.Execute();

            Assert.That(transport.GetDataBlob<GroundTransportDB>().LoadedUnits, Has.Count.EqualTo(0),
                "after Execute the unit has left the transport (it landed)");
            var forces = enemyBody.GetDataBlob<GroundForcesDB>();
            Assert.That(forces.Units.Any(u => u.FactionOwnerID == s.Faction.Id && u.RegionIndex == 0), Is.True,
                "the invader unit is now standing in the target world's region 0");

            // The ground processor runs (no sim advance): the sole live unit in region 0 is the invader → CAPTURE.
            new GroundForcesProcessor().ProcessEntity(enemyBody, 3600);

            Assert.That(regionsDB.Regions[0].OwnerFactionID, Is.EqualTo(s.Faction.Id),
                "with the invader holding the undefended region, it flips to the attacker — the AI took the ground");
        }
    }
}
