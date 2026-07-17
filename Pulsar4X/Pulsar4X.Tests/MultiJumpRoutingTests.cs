using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.JumpPoints;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// P-3 MULTI-JUMP STRIKE ROUTING gauge (docs/AI-BRAIN-BUILD-TRACKER.md — the deferred router MilitaryReach flagged).
    /// Proves <see cref="JumpRouter"/>, the breadth-first search over the DISCOVERED jump-point graph that lets the war
    /// brain sail a fleet ACROSS jump points to reach an enemy world in another star system (before this it could only
    /// strike a same-system target). A tiny synthetic multi-system jump graph is hand-linked (a chain A→B→C, mirroring
    /// JPFactory's paired-gate linkage), then:
    ///   (a) a 2-hop route A→C returns the correct FIRST gate to head for + a hop count of 2;
    ///   (b) an UNDISCOVERED link is NOT traversed (an unknown gate isn't a route to the AI) → no route;
    ///   (c) a disconnected target → the explicit "no route" result (never throws);
    ///   (d) a same-system target → 0 hops (a direct warp, no gate to cross);
    ///   and (e) <see cref="MilitaryReach"/> now reads a 2-hop target as MULTI-JUMP REACHABLE (it read Unreachable
    ///   before this slice — the headline behaviour change).
    /// Pure reads on the router + reach; no warp/order surface driven (the ConquerResolver StrikeJump wiring is covered
    /// by its byte-identity tests plus reads (a)/(e) here — a full multi-leg sail has no deterministic harness, the same
    /// reason the same-system StrikeFleet rung is gauged as a decision, not an actual sail).
    /// </summary>
    [TestFixture]
    public class MultiJumpRoutingTests
    {
        private const int Fid = 4242;       // our faction — the one whose discovered graph the router walks
        private const int Stranger = 9999;  // a DIFFERENT faction — a gate only it discovered is not our route

        /// <summary>Create a linked pair of jump-point entities, one in each system (mirrors JPFactory's linkage: each
        /// side's DestinationId names the OTHER side's entity). The <paramref name="discoveredBy"/> faction is marked
        /// as having discovered the NEAR (from-side) gate — the side the router checks when leaving <paramref
        /// name="fromSystem"/>. Returns the from-side gate entity.</summary>
        private static Entity LinkGate(StarSystem fromSystem, StarSystem toSystem, int discoveredBy)
        {
            var here = Entity.Create();
            var there = Entity.Create();
            var hereDB = new JumpPointDB(there.Id);
            hereDB.IsDiscovered.Add(discoveredBy);
            fromSystem.AddEntity(here, new List<BaseDataBlob> { hereDB });
            toSystem.AddEntity(there, new List<BaseDataBlob> { new JumpPointDB(here.Id) });
            return here;
        }

        private static StarSystem[] ThreeSystems(out Game game)
        {
            game = TestingUtilities.CreateTestUniverse(3, new DateTime(2100, 1, 1), false);
            return game.Systems.Distinct().ToArray();
        }

        [Test]
        [Description("A 2-hop discovered route A→(B)→C returns the FIRST gate (the A→B gate) to head for, and Hops == 2.")]
        public void TwoHopRoute_ReturnsFirstGateAndHopCount()
        {
            var systems = ThreeSystems(out _);
            var a = systems[0]; var b = systems[1]; var c = systems[2];

            var gateAB = LinkGate(a, b, Fid);   // A→B discovered by us
            LinkGate(b, c, Fid);                // B→C discovered by us

            var route = JumpRouter.FindRoute(a, c, Fid);

            Assert.That(route.Found, Is.True, "a discovered A→B→C chain is a route");
            Assert.That(route.Hops, Is.EqualTo(2), "two gate crossings from A to C");
            Assert.That(route.FirstGate, Is.Not.Null);
            Assert.That(route.FirstGate.Id, Is.EqualTo(gateAB.Id),
                "the fleet heads for the A→B gate FIRST (the leg to emit this cycle)");

            // The convenience accessor agrees.
            Assert.That(JumpRouter.NextGateToward(a, c, Fid).Id, Is.EqualTo(gateAB.Id));
        }

        [Test]
        [Description("An UNDISCOVERED link (discovered only by a stranger faction) is NOT traversed — so A→C via an "
                   + "undiscovered B→C leg has NO route, even though the A→B leg is discovered.")]
        public void UndiscoveredLink_IsNotTraversed()
        {
            var systems = ThreeSystems(out _);
            var a = systems[0]; var b = systems[1]; var c = systems[2];

            LinkGate(a, b, Fid);           // A→B discovered by us
            LinkGate(b, c, Stranger);      // B→C discovered ONLY by a stranger — not our route

            // A→B alone is a valid one-hop route (sanity: the graph is wired, just the last leg is unknown to us).
            Assert.That(JumpRouter.FindRoute(a, b, Fid).Hops, Is.EqualTo(1), "the discovered A→B leg is reachable");

            var route = JumpRouter.FindRoute(a, c, Fid);
            Assert.That(route.Found, Is.False, "the only path to C runs through a gate we haven't discovered");
            Assert.That(route.Hops, Is.EqualTo(-1), "no route → -1 hops");
            Assert.That(route.FirstGate.IsValid, Is.False, "no first gate to head for");
        }

        [Test]
        [Description("A target in a DISCONNECTED system (no discovered route) returns the explicit no-route result — never throws.")]
        public void NoRoute_ReturnsUnreachable()
        {
            var systems = ThreeSystems(out _);
            var a = systems[0]; var b = systems[1]; var c = systems[2];

            LinkGate(a, b, Fid);   // only A↔B is linked; C is stranded

            var route = JumpRouter.FindRoute(a, c, Fid);

            Assert.That(route.Found, Is.False, "C has no discovered link into the graph");
            Assert.That(route.Hops, Is.EqualTo(-1));
            Assert.That(route.FirstGate.IsValid, Is.False);
        }

        [Test]
        [Description("A target in the fleet's OWN system is 0 hops (a direct warp, no gate to cross).")]
        public void SameSystem_ReturnsZeroHops()
        {
            var systems = ThreeSystems(out _);
            var a = systems[0];

            var route = JumpRouter.FindRoute(a, a, Fid);

            Assert.That(route.Found, Is.True, "the target system IS the fleet's system");
            Assert.That(route.Hops, Is.EqualTo(0), "no jumps to a same-system target");
            Assert.That(route.FirstGate.IsValid, Is.False, "no gate to head for — it's a direct warp");
        }

        [Test]
        [Description("A null system on either end yields no route (defensive, never throws).")]
        public void NullSystem_ReturnsNoRoute()
        {
            var systems = ThreeSystems(out _);
            var a = systems[0];

            Assert.That(JumpRouter.FindRoute(null, a, Fid).Found, Is.False);
            Assert.That(JumpRouter.FindRoute(a, null, Fid).Found, Is.False);
            Assert.That(JumpRouter.FindRoute(null, null, Fid).Hops, Is.EqualTo(-1));
        }

        [Test]
        [Description("MilitaryReach now reads a 2-hop target as MULTI-JUMP and REACHABLE (it read Unreachable before "
                   + "this slice — the multi-jump router closes the OneJump=cap). The reach factor is discounted below "
                   + "a one-jump prize, so a far campaign weighs less than a nearer world of equal value.")]
        public void MilitaryReach_TwoHopTarget_ReadsMultiJumpReachable()
        {
            var systems = ThreeSystems(out _);
            var a = systems[0]; var b = systems[1]; var c = systems[2];

            LinkGate(a, b, Fid);
            LinkGate(b, c, Fid);

            var targetBody = Entity.Create();
            c.AddEntity(targetBody);

            var reach = MilitaryReach.AssessRoute(a, targetBody, Fid);

            Assert.That(reach.Tier, Is.EqualTo(MilitaryReach.ReachTier.MultiJump),
                "two discovered jumps → the multi-jump tier (was Unreachable before the router)");
            Assert.That(reach.IsReachable, Is.True, "a discovered multi-jump route is reachable");
            Assert.That(reach.Hops, Is.EqualTo(2), "two jump crossings to the target world");
            Assert.That(reach.ReachFactor, Is.EqualTo(MilitaryReach.MultiJumpReach), "the multi-jump discount");
            Assert.That(reach.ReachFactor, Is.LessThan(MilitaryReach.OneJumpReach),
                "a two-jump prize is discounted below a one-jump one — the reach model that lets a nearer world win");
        }
    }
}
