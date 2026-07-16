using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;    // CombatEngagement
using Pulsar4X.Engine;
using Pulsar4X.Factions;  // FactionInfoDB
using Pulsar4X.Fleets;    // FleetDB, FleetFactory, FleetTools
using Pulsar4X.Ships;     // ShipFactory

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Regression gauge for the FROZEN-CLOCK WEDGE the developer's SIM-STALL watchdog caught (2026-07-15): the combat
    /// hotloop (`BattleTriggerProcessor` → `CombatEngagement.Tick`) walks the fleet tree via `GetFleetShips` /
    /// `GetCombatShips`, and a MALFORMED tree (a sub-fleet reachable by two paths — a diamond — or a cycle) made those
    /// walks re-explore the same subtree combinatorially and WEDGE the sim thread (game-time froze, no throw). The fix
    /// visits each fleet node at most once (a seen-set), so the walk is O(nodes) and can't blow up. These build the
    /// exact bad shapes and prove the walks terminate and count each ship once. The `[Timeout]` turns any regression
    /// (a real hang) into a fast test FAILURE instead of a wedged CI shard.
    /// </summary>
    [TestFixture]
    public class CombatFleetTreeSafetyTests
    {
        private static Entity Fleet(TestScenario s, string name) => FleetFactory.Create(s.StartingSystem, s.Faction.Id, name);
        private static Entity Ship(TestScenario s, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            return ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
        }
        private static void AddChild(Entity fleet, Entity child) => fleet.GetDataBlob<FleetDB>().AddChild(child);

        [Test, Timeout(30000)]
        [Description("A DIAMOND (two sub-fleets sharing one grandchild sub-fleet) counts the shared ship ONCE, not once per path — the dedup that stops the exponential blow-up.")]
        public void DiamondFleetTree_CountsSharedShipOnce()
        {
            var s = TestScenario.CreateWithColony();
            var root = Fleet(s, "Root");
            var b1 = Fleet(s, "Wing1");
            var b2 = Fleet(s, "Wing2");
            var shared = Fleet(s, "Shared");
            var shipShared = Ship(s, "shipShared");

            AddChild(shared, shipShared);
            AddChild(b1, shared);          // Wing1 -> Shared
            AddChild(b2, shared);          // Wing2 -> Shared  (the diamond: two parents, one child)
            AddChild(root, b1);
            AddChild(root, b2);

            var ships = CombatEngagement.GetFleetShips(root);
            Assert.That(ships.Count(x => x == shipShared), Is.EqualTo(1), "the shared ship is collected exactly once");

            var combat = CombatEngagement.GetCombatShips(root);
            Assert.That(combat.Count, Is.EqualTo(1), "GetCombatShips also counts it once");

            var recursive = FleetTools.AllShipsRecursive(root);
            Assert.That(recursive.Count(x => x == shipShared), Is.EqualTo(1), "AllShipsRecursive counts it once too");
        }

        [Test, Timeout(30000)]
        [Description("A CYCLE (A is a sub-fleet of B and B is a sub-fleet of A) terminates and returns each ship once — no wedge.")]
        public void CyclicFleetTree_TerminatesAndReturnsEachShipOnce()
        {
            var s = TestScenario.CreateWithColony();
            var a = Fleet(s, "Alpha");
            var b = Fleet(s, "Bravo");
            var shipA = Ship(s, "shipA");
            var shipB = Ship(s, "shipB");

            AddChild(a, shipA);
            AddChild(b, shipB);
            AddChild(a, b);   // Alpha -> Bravo
            AddChild(b, a);   // Bravo -> Alpha  (the cycle)

            var ships = CombatEngagement.GetFleetShips(a);   // must TERMINATE (the wedge fix)
            Assert.That(ships, Has.Member(shipA));
            Assert.That(ships, Has.Member(shipB));
            Assert.That(ships.Distinct().Count(), Is.EqualTo(ships.Count), "no ship double-listed through the cycle");

            Assert.That(FleetTools.AllShipsRecursive(a).Distinct().Count(),
                Is.EqualTo(FleetTools.AllShipsRecursive(a).Count), "recursive walk also terminates + dedups");
        }
    }
}
