using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;      // ColonyInfoDB
using Pulsar4X.Engine;        // Entity
using Pulsar4X.Factions;      // FactionState, ConquerResolver, Diplomacy, MilitaryTarget, StrategicObjective(DB)
using Pulsar4X.Fleets;        // FleetDB (clear the start fleets)
using Pulsar4X.Galaxy;        // SystemBodyInfoDB (a real body for the enemy colony)
using Pulsar4X.Industry;      // IndustryAbilityDB, IndustryJob, IndustryTools
using Pulsar4X.Ships;         // ShipDesign, ShipDesignFromJson

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Operation Earthfall P4.1 gauge (findings/A4 cause 1 — the sealift chain died at Rung 2): the CONQUER resolver's
    /// BUILD-TRANSPORT rung (Rung 2) used to gate ONLY on "own no transport" + a free line, so on a MULTI-LINE shipyard
    /// colony (Mars fielded 4 ship-assembly lines) it queued a heavy troop transport on EVERY free line in successive
    /// monthly cycles — 4 redundant troopers strangling Mars industry before a single one finished. The fix adds an
    /// "and none already QUEUED" clause (<see cref="ConquerResolver.FactionHasTransportQueued"/>).
    ///
    /// These tests prove: (1) the resolver builds exactly ONE transport, then the guard makes a second cycle FALL
    /// THROUGH to the next rung (no redundant second transport); and (2) the helper reads a queued trooper across the
    /// faction's production lines. Pure decisions driven directly through the resolver (no sim advance).
    /// </summary>
    [TestFixture]
    public class EfSealiftQueueGuardTests
    {
        /// <summary>Register the base-mod "Lander Troop Transport" ship design onto the start faction and return it.
        /// The base-mod colony stocks the troop-bay COMPONENT and every part the trooper needs, but no trooper SHIP
        /// design is registered on it by default, so the resolver's Rung 2 has nothing to build until we add one.</summary>
        private static ShipDesign RegisterTrooper(TestScenario s)
        {
            var info = s.Faction.GetDataBlob<FactionInfoDB>();
            var trooperBp = s.Game.StartingGameData.ShipDesigns["default-ship-design-trooper"];
            return ShipDesignFromJson.Create(s.Faction, info.Data, trooperBp);   // Initialise() registers it into IndustryDesigns
        }

        /// <summary>Put a rival colony (at war with the player) on a real body in the player's own system, so
        /// <see cref="MilitaryTarget.BestEnemyTarget"/> scores it a valid strike target (the resolver's war rungs run).</summary>
        private static void GivePlayerAWarTarget(TestScenario s)
        {
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            Diplomacy.DeclareWar(s.Faction, reds, CasusBelli.ConfrontRival, s.Game.TimePulse.GameGlobalDateTime);

            var enemyBody = s.StartingSystem.GetAllEntitiesWithDataBlob<SystemBodyInfoDB>()
                .FirstOrDefault(b => b.Id != s.StartingBody.Id);
            Assert.That(enemyBody, Is.Not.Null, "the start system needs a second body to stand in for the enemy world");

            var rivalColony = Entity.Create();
            rivalColony.FactionOwnerID = reds.Id;
            s.StartingSystem.AddEntity(rivalColony);
            rivalColony.SetDataBlob(new ColonyInfoDB(new Dictionary<int, long> { { 1, 500_000 } }, enemyBody));
            reds.GetDataBlob<FactionInfoDB>().Colonies.Add(rivalColony);
        }

        [Test]
        [Description("P4.1: Rung 2 builds ONE troop transport, then the already-queued guard makes the next cycle fall "
                   + "through to the next rung instead of queuing a redundant second transport (findings/A4 cause 1).")]
        public void RungTwo_QueuesOneTransport_ThenGuardBlocksASecond()
        {
            var s = TestScenario.CreateWithColony();
            var trooper = RegisterTrooper(s);
            Assert.That(ConquerResolver.IsTroopTransport(trooper), Is.True, "the registered design is a troop transport");

            // Clear the colony's own start fleets so no massed strike fleet can preempt the BUILD-TRANSPORT rung — this
            // isolates Rung 2 (mirrors ConquerResolverTests.Conquer_SailsTheLoadedTransport).
            foreach (var fleet in s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>().ToList())
                fleet.Destroy();

            GivePlayerAWarTarget(s);
            Assert.That(MilitaryTarget.BestEnemyTarget(s.Faction).IsValid, Is.True, "precondition: a valid war target");

            // ── first cycle: no transport owned, none queued → Rung 2 builds the invasion carrier ─────────────────────
            var state1 = FactionState.Snapshot(s.Faction);
            Assert.That(ConquerResolver.FactionHasTransportQueued(state1), Is.False, "nothing queued yet");

            var action1 = new ConquerResolver().Resolve(state1, new StrategicObjectiveDB { Objective = StrategicObjective.Conquer });
            Assert.That(action1.Kind, Is.EqualTo("BuildTransport"),
                "with a war target, no owned transport, and none queued, Rung 2 builds the transport");

            action1.Execute();
            Assert.That(ConquerResolver.FactionHasTransportQueued(FactionState.Snapshot(s.Faction)), Is.True,
                "after Execute a troop-transport job is now in production");

            // ── second cycle: a transport is already queued → the guard blocks another; the resolver falls through ────
            var state2 = FactionState.Snapshot(s.Faction);
            var action2 = new ConquerResolver().Resolve(state2, new StrategicObjectiveDB { Objective = StrategicObjective.Conquer });
            Assert.That(action2.Kind, Is.Not.EqualTo("BuildTransport"),
                "the already-queued guard stops a redundant second transport — the resolver moves to the next rung "
                + "(the fix for the 4x-redundant-queue that strangled Mars)");
        }

        [Test]
        [Description("P4.1: FactionHasTransportQueued reads the faction's production lines — false with nothing queued, "
                   + "true once a troop-transport design has a job on a line (isolates the helper from feasibility).")]
        public void FactionHasTransportQueued_TrueOnlyWhenATrooperIsQueued()
        {
            var s = TestScenario.CreateWithColony();
            var info = s.Faction.GetDataBlob<FactionInfoDB>();
            var trooper = RegisterTrooper(s);

            Assert.That(ConquerResolver.FactionHasTransportQueued(FactionState.Snapshot(s.Faction)), Is.False,
                "no job queued yet → the guard reads clear");

            // Hand-queue the trooper on a free ship-assembly line (bypasses the resolver/feasibility — isolates the read).
            var industry = s.Colony.GetDataBlob<IndustryAbilityDB>();
            string line = industry.ProductionLines
                .First(l => l.Value.IndustryTypeRates.ContainsKey(trooper.IndustryTypeID) && l.Value.Jobs.Count == 0).Key;
            IndustryTools.AddJob(s.Colony, line, new IndustryJob(info, trooper.UniqueID));

            Assert.That(ConquerResolver.FactionHasTransportQueued(FactionState.Snapshot(s.Faction)), Is.True,
                "a queued troop-transport design is detected across the faction's production lines");
        }
    }
}
