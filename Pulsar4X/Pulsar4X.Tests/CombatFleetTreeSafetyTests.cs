using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;    // CombatEngagement, FleetCombat, ShipCombatValueDB, FleetCombatStateDB
using Pulsar4X.Engine;    // MasterTimePulse
using Pulsar4X.Factions;  // FactionInfoDB, FactionFactory
using Pulsar4X.Fleets;    // FleetDB, FleetFactory, FleetTools, FleetOrder, FleetAssembly
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

        // Armed-ship helpers (lifted from BattleTriggerTests) for the fine-step gauge — a controlled hostile pair.
        private static Entity MakeFleet(TestScenario s, Entity faction, string name) => FleetFactory.Create(s.StartingSystem, faction.Id, name);
        private static Entity AddShip(TestScenario s, Entity faction, Entity fleet, double firepower, double toughness, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);   // build under the player faction
            ship.FactionOwnerID = faction.Id;                                             // then assign the true owner
            ship.SetDataBlob(new ShipCombatValueDB(firepower, toughness, 1.0));           // deterministic firepower
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return ship;
        }
        private static void ClearExistingFleets(TestScenario s)
        {
            foreach (var fleet in s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>().ToList())
                fleet.Destroy();
        }

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

        [Test, Timeout(30000)]
        [Description("TreeHierarchyDB.Root walks UP the parent chain; a CYCLIC chain must terminate (best-effort) instead of StackOverflow-crashing — the parent-direction twin of the fleet-walk fix.")]
        public void CyclicParentChain_RootTerminates_NoCrash()
        {
            var s = TestScenario.CreateWithColony();
            var a = Fleet(s, "A");
            var b = Fleet(s, "B");
            a.GetDataBlob<FleetDB>().SetParent(b);
            b.GetDataBlob<FleetDB>().SetParent(a);   // cycle: A.parent=B, B.parent=A

            Entity root = null;
            Assert.DoesNotThrow(() => root = a.GetDataBlob<FleetDB>().Root, "Root must not recurse forever on a cyclic parent chain");
            Assert.That(root, Is.Not.Null, "Root returns a best-effort node, not a crash");
        }

        // ── The GAP the fix closes: the walks above (GetFleetShips/GetCombatShips/AllShipsRecursive/Root) were already
        //    seen-set-guarded, but FleetCombat.Ships (the walker the WHOLE closing model + FleetAssembly.ArmedShipCount
        //    ride) was NOT — and it runs inside CombatEngagement.Tick. On a cycle it looped forever = the "StarInfoDB
        //    TRUE WEDGE"; on stacked diamonds it went exponential. These prove FleetCombat.* now terminate. ──

        [Test, Timeout(30000)]
        [Description("A CYCLE makes the FleetCombat readouts (Ships/FirepowerAtRange/SensorReach/WarpSpeedFloor) + FleetAssembly.ArmedShipCount TERMINATE — the walker used inside CombatEngagement.Tick that used to WEDGE the sim.")]
        public void CyclicFleetTree_FleetCombatReadouts_Terminate()
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

            var ships = FleetCombat.Ships(a);                                   // FleetCombat.Collect — must TERMINATE
            Assert.That(ships, Has.Member(shipA));
            Assert.That(ships, Has.Member(shipB));
            Assert.That(ships.Distinct().Count(), Is.EqualTo(ships.Count), "no ship double-listed through the cycle");
            Assert.DoesNotThrow(() => FleetCombat.FirepowerAtRange(a, 0));      // all ride the same walker
            Assert.DoesNotThrow(() => FleetCombat.SensorReach(a));
            Assert.DoesNotThrow(() => FleetCombat.WarpSpeedFloor(a));
            Assert.DoesNotThrow(() => FleetAssembly.ArmedShipCount(a));         // the AI's monthly sweep read
        }

        [Test, Timeout(30000)]
        [Description("A STACK of diamonds (2^N descents without a seen-set) makes FleetCombat.Ships TERMINATE and count the shared leaf ONCE — the exponential blow-up guard.")]
        public void StackedDiamondFleetTree_FleetCombatReadouts_TerminateAndCountOnce()
        {
            var s = TestScenario.CreateWithColony();
            const int N = 25;                                                   // 2^25 ≈ 33M unguarded descents → TIMEOUT pre-fix
            var top = new Entity[N + 1];
            for (int i = 0; i <= N; i++) top[i] = Fleet(s, "T" + i);
            for (int i = 0; i < N; i++)
            {
                var l = Fleet(s, "L" + i);
                var r = Fleet(s, "R" + i);
                AddChild(top[i], l); AddChild(top[i], r);                       // top[i] → L,R
                AddChild(l, top[i + 1]); AddChild(r, top[i + 1]);              // L,R both → top[i+1]  (a diamond per level)
            }
            var leaf = Ship(s, "leaf");
            AddChild(top[N], leaf);

            var ships = FleetCombat.Ships(top[0]);                             // O(nodes) post-fix
            Assert.That(ships.Count(x => x == leaf), Is.EqualTo(1), "the seen-set dedups the shared leaf, not once per path");
            Assert.DoesNotThrow(() => FleetCombat.FirepowerAtRange(top[0], 0));
            Assert.DoesNotThrow(() => FleetCombat.SensorReach(top[0]));
            Assert.That(FleetAssembly.ArmedShipCount(top[0]), Is.LessThanOrEqualTo(1), "shared leaf counted at most once");
        }

        [Test, Timeout(30000)]
        [Description("FleetOrder.ChangeParent REFUSES reparenting a fleet under its own descendant — the client drag-drop that used to build the 2-node parent cycle at the SOURCE. The tree is left well-formed.")]
        public void ChangeParent_UnderOwnDescendant_Refused_NoCycleFormed()
        {
            var s = TestScenario.CreateWithColony();
            var parent = Fleet(s, "Parent");
            var sub = Fleet(s, "Sub");
            parent.GetDataBlob<FleetDB>().SetParent(s.Faction);
            sub.GetDataBlob<FleetDB>().SetParent(parent);                       // Sub is a descendant of Parent

            // The player drags Parent onto its own Sub — this would make Parent.parent=Sub, Sub.parent=Parent (a cycle).
            s.Game.OrderHandler.HandleOrder(FleetOrder.ChangeParent(s.Faction.Id, parent, sub));

            Assert.That(parent.GetDataBlob<FleetDB>().Parent?.Id, Is.Not.EqualTo(sub.Id), "the reparent-under-descendant was refused — Parent is NOT under Sub");
            Assert.That(sub.GetDataBlob<FleetDB>().Parent?.Id, Is.EqualTo(parent.Id), "Sub stays under Parent (tree unchanged)");
            Assert.DoesNotThrow(() => FleetCombat.Ships(parent), "the tree is well-formed — the walk terminates");
        }

        [Test, Timeout(60000)]
        [Description("A STANDING hostile pair that reads imminent but never engages must not fine-step unboundedly across game-hours — the chronic 24-per-hour tax that crawled the clock (the SensorScan PERF freeze). The give-up now persists across advances.")]
        public void StandingHostilePair_DoesNotFineStepUnboundedlyAcrossAdvances()
        {
            var s = TestScenario.CreateWithColony();
            ClearExistingFleets(s);
            var enemy = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var red = MakeFleet(s, enemy, "Red"); AddShip(s, enemy, red, 50_000, 1_000_000, "Red 1");
            var blue = MakeFleet(s, s.Faction, "Blue"); AddShip(s, s.Faction, blue, 50_000, 1_000_000, "Blue 1");
            Assert.That(CombatEngagement.NewEngagementImminent(s.StartingSystem), Is.True, "precondition: the pair reads imminent");

            bool prevInterrupt = CombatEngagement.InterruptTimeOnNewEngagement;
            CombatEngagement.InterruptTimeOnNewEngagement = true;               // arm the fine-step machinery (mirrors the client)
            long before = MasterTimePulse.FineStepCount;
            try
            {
                const int advances = 6;
                for (int i = 0; i < advances; i++)
                    s.AdvanceTime(TimeSpan.FromHours(1), TimeSpan.FromHours(1)); // 6 SEPARATE SimulateTimeUntil calls
                long delta = MasterTimePulse.FineStepCount - before;
                Assert.That(blue.HasDataBlob<FleetCombatStateDB>(), Is.False, "the harness never fires the trigger → the pair stays un-engaged (the false-positive path)");
                // Pre-fix: ~24 fine-steps per advance × 6 = ~144 (the cap re-armed each game-hour). Post-fix: the give-up
                // persists across calls while the imminent read stands, so total ≈ one MaxConsecutiveFineSteps burst.
                Assert.That(delta, Is.LessThanOrEqualTo(MasterTimePulse.MaxConsecutiveFineSteps + 2),
                    "chronic fine-stepping: " + delta + " fine-steps over " + advances + " game-hours means the 24/hr tax re-armed each advance (the PERF freeze)");
            }
            finally
            {
                CombatEngagement.InterruptTimeOnNewEngagement = prevInterrupt;  // never leak the static flag
                s.Game.TimePulse.CombatInterruptPending = false;
            }
        }
    }
}
