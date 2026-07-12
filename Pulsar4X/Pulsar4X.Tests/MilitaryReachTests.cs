using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Datablobs;
using Pulsar4X.Energy;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.JumpPoints;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// P-3 military-reach gauge, slice 3 (docs/AI-BRAIN-BUILD-TRACKER.md — the last deferred "can my fleet GET THERE?"
    /// muscle). Proves <see cref="MilitaryReach"/> — the perception that replaces MilitaryTarget's coarse near/far
    /// PROXY with a real read off the jump graph + the fleet's fuel/range. Four reads:
    ///   (a) a target in the fleet's OWN system reads reachable + cheap + (charged fleet) READY;
    ///   (b) a fuel-drained fleet reads the same route but NOT ready (no range);
    ///   (c) a target one KNOWN jump away reads reachable but costlier (a discounted reach factor, one hop);
    ///   (d) a target behind an UNDISCOVERED gate reads unreachable (an unknown jump isn't a route to the AI).
    /// Pure read, no warp/order surface — byte-identical.
    /// </summary>
    [TestFixture]
    public class MilitaryReachTests
    {
        // ---- helpers for the same-system / fuel reads (a real charged strike fleet, like MilitaryCompositionTests) ----

        private static ShipDesign FirstWarshipDesign(Entity faction)
        {
            foreach (var d in faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values)
                if (d.TryGetComponentsByAttribute<Pulsar4X.Weapons.GenericBeamWeaponAtb>(out _)
                    || d.TryGetComponentsByAttribute<Pulsar4X.Weapons.RailgunWeaponAtb>(out _))
                    return d;
            return null;
        }

        /// <summary>Mass a fuelled + CHARGED strike fleet at the home body, then advance a day so the ships resolve
        /// into the fleet (the CombatSandbox recipe mirrors the start fleet — reactors charged, so it has warp range).</summary>
        private static Entity MassAChargedFleet(TestScenario s, int count)
        {
            var armed = FirstWarshipDesign(s.Faction);
            Assert.That(armed, Is.Not.Null, "the start faction has an armed design to mass");
            var designs = new List<ShipDesign>();
            for (int i = 0; i < count; i++) designs.Add(armed);
            var fleet = CombatSandbox.SpawnMixedFleet(s.Game, s.StartingSystem, s.Faction, s.Faction, designs, s.StartingBody, "Strike Group");
            s.AdvanceTime(TimeSpan.FromDays(1));
            return fleet;
        }

        [Test]
        [Description("A target in the strike fleet's OWN system reads SameSystem, reachable, cheapest reach factor, and (charged) READY.")]
        public void SameSystemTarget_ReadsReachableCheapAndReady()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = MassAChargedFleet(s, MilitaryComposition.StrikeGroupMinWarships);

            // A bare enemy world sitting in the fleet's own star system — a direct warp.
            var targetBody = Entity.Create();
            s.StartingSystem.AddEntity(targetBody);

            var reach = MilitaryReach.Assess(fleet, targetBody);

            Assert.That(reach.Tier, Is.EqualTo(MilitaryReach.ReachTier.SameSystem), "same-system target is a direct warp");
            Assert.That(reach.IsReachable, Is.True);
            Assert.That(reach.Hops, Is.EqualTo(0), "no jumps to a same-system target");
            Assert.That(reach.ReachFactor, Is.EqualTo(MilitaryReach.SameSystemReach), "cheapest reach — the near prize");
            Assert.That(reach.HasRange, Is.True, "a CombatSandbox-charged fleet has the stored energy to warp");
            Assert.That(reach.IsReady, Is.True, "reachable + fuelled = ready to sail");
        }

        [Test]
        [Description("A fuel-drained fleet reads the SAME same-system route but NOT ready — no stored energy for a warp bubble.")]
        public void FuelShortFleet_ReadsReachableButNotReady()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = MassAChargedFleet(s, MilitaryComposition.StrikeGroupMinWarships);

            var targetBody = Entity.Create();
            s.StartingSystem.AddEntity(targetBody);

            // Drain every ship's stored energy — the exact thing WarpMoveCommand blocks on (bubble > stored).
            foreach (var ship in FleetCombat.Ships(fleet))
                if (ship != null && ship.IsValid && ship.TryGetDataBlob<EnergyGenAbilityDB>(out var power))
                    power.EnergyStored.Clear();

            var reach = MilitaryReach.Assess(fleet, targetBody);

            Assert.That(reach.Tier, Is.EqualTo(MilitaryReach.ReachTier.SameSystem), "the route is still same-system");
            Assert.That(reach.IsReachable, Is.True, "the target is still routable");
            Assert.That(reach.HasRange, Is.False, "a drained fleet can't spin up a warp bubble");
            Assert.That(reach.IsReady, Is.False, "reachable but out of range = not ready (keep charging)");
        }

        // ---- helpers for the jump-graph reads (a small multi-system universe with a hand-linked gate) ----

        private const int Fid = 4242;     // an arbitrary faction id for the discovery gate

        /// <summary>Create a linked pair of jump-point entities, one in each system (mirrors JPFactory's linkage:
        /// each side's DestinationId names the other's entity). Returns the gate in <paramref name="fromSystem"/>.</summary>
        private static Entity LinkGate(StarSystem fromSystem, StarSystem toSystem)
        {
            var here = Entity.Create();
            var there = Entity.Create();
            fromSystem.AddEntity(here, new List<BaseDataBlob> { new JumpPointDB(there.Id) });
            toSystem.AddEntity(there, new List<BaseDataBlob> { new JumpPointDB(here.Id) });
            return here;
        }

        [Test]
        [Description("A target one KNOWN jump away reads OneJump — reachable but costlier (a discounted reach factor, one hop).")]
        public void KnownOneJumpTarget_ReadsReachableButCostlier()
        {
            var game = TestingUtilities.CreateTestUniverse(2, new DateTime(2100, 1, 1), false);
            var systems = game.Systems.Distinct().ToArray();
            var systemA = systems[0];
            var systemB = systems[1];

            var gate = LinkGate(systemA, systemB);
            gate.GetDataBlob<JumpPointDB>().IsDiscovered.Add(Fid);   // the faction has DISCOVERED this gate — a real route

            var targetBody = Entity.Create();
            systemB.AddEntity(targetBody);

            var reach = MilitaryReach.AssessRoute(systemA, targetBody, Fid);

            Assert.That(reach.Tier, Is.EqualTo(MilitaryReach.ReachTier.OneJump), "a discovered gate links the two systems");
            Assert.That(reach.IsReachable, Is.True, "one known jump is reachable");
            Assert.That(reach.Hops, Is.EqualTo(1), "exactly one jump to the target");
            Assert.That(reach.ReachFactor, Is.EqualTo(MilitaryReach.OneJumpReach), "costlier than same-system");
            Assert.That(reach.ReachFactor, Is.LessThan(MilitaryReach.SameSystemReach),
                "a jump-away prize is discounted vs a same-system one — the reach model that lets a near world win");
        }

        [Test]
        [Description("A target behind an UNDISCOVERED gate reads Unreachable — an unknown jump isn't a route to the planner.")]
        public void UndiscoveredGateTarget_ReadsUnreachable()
        {
            var game = TestingUtilities.CreateTestUniverse(2, new DateTime(2100, 1, 1), false);
            var systems = game.Systems.Distinct().ToArray();
            var systemA = systems[0];
            var systemB = systems[1];

            var gate = LinkGate(systemA, systemB);
            gate.GetDataBlob<JumpPointDB>().IsDiscovered.Add(9999);   // discovered by a STRANGER, not our faction

            var targetBody = Entity.Create();
            systemB.AddEntity(targetBody);

            var reach = MilitaryReach.AssessRoute(systemA, targetBody, Fid);

            Assert.That(reach.Tier, Is.EqualTo(MilitaryReach.ReachTier.Unreachable),
                "a gate our faction hasn't discovered doesn't count as a route");
            Assert.That(reach.IsReachable, Is.False);
            Assert.That(reach.Hops, Is.EqualTo(-1));
            Assert.That(reach.ReachFactor, Is.EqualTo(MilitaryReach.UnreachableReach), "unreachable scores no reach");
        }
    }
}
