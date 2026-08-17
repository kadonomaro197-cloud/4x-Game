using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;
using System.Linq;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL B4b (Ram) — the ENGINE half of "order a fleet to RAM a hostile fleet." A ram is a
    /// deliberate suicide charge: MUTUAL ship-for-ship kinetic annihilation (each rammer destroys ITSELF and one enemy
    /// ship), so both sides lose <c>min(A, B)</c> ships — distinct from <see cref="CombatEngagement.OrderAttack"/>
    /// where the stronger fleet wins with survivors. Casualties are WHOLE-SHIP <c>Entity.Destroy()</c> (the auto-resolve
    /// casualty model), NOT the per-pixel damage sim (which the resolver avoids and which deposits ~0 for ship hulls —
    /// routing a ram through it would be a hollow no-op). The desperation guarantee: a weak fleet can take N of a
    /// stronger enemy down with it. <see cref="CombatEngagement.OrderRamNearestHostile"/> is the button/AI convenience.
    /// </summary>
    [TestFixture]
    public class RamOrderTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[ram] " + m);

        private static Entity MakeFleetWithShips(TestScenario s, Entity faction, string name, int count)
        {
            var fleet = FleetFactory.Create(s.StartingSystem, faction.Id, name);
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First(); // build under player faction (proven pattern)
            for (int i = 0; i < count; i++)
            {
                var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name + " ship " + i);
                ship.FactionOwnerID = faction.Id;   // then flip to the true owner (combat reads FactionOwnerID)
                s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            }
            return fleet;
        }

        // Live ship count — a Destroy()'d ship flips IsValid=false at once (the auto-resolve casualty contract), and
        // GetFleetShips/AllShipsRecursive both skip !IsValid, so this is the honest "how many survived."
        private static int ValidShips(Entity fleet) => FleetTools.AllShipsRecursive(fleet).Count(x => x.IsValid);

        [Test]
        [Description("Equal fleets ramming: 3 vs 3 → BOTH wiped (each ship destroys itself and one enemy, ship-for-ship).")]
        public void OrderRam_EqualFleets_WipesBoth()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var mine = MakeFleetWithShips(s, s.Faction, "Mine", 3);
            var enemy = MakeFleetWithShips(s, reds, "Enemy", 3);
            Assume.That(ValidShips(mine), Is.EqualTo(3), "setup: mine has 3 ships");
            Assume.That(ValidShips(enemy), Is.EqualTo(3), "setup: enemy has 3 ships");

            CombatEngagement.OrderRam(mine, enemy);
            Log($"after 3v3 ram: mine={ValidShips(mine)} enemy={ValidShips(enemy)}");
            Assert.That(ValidShips(mine), Is.EqualTo(0), "the rammer's ships all die in a suicide charge");
            Assert.That(ValidShips(enemy), Is.EqualTo(0), "an equal enemy is wiped ship-for-ship");
        }

        [Test]
        [Description("Unequal fleets: a small rammer (2) charging a larger enemy (4) is WIPED and takes an equal count " +
                     "(2 of the 4) with it — the desperation guarantee; the enemy's surplus (2) survives.")]
        public void OrderRam_SmallerRammer_IsWipedAndTakesEqualCount()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var mine = MakeFleetWithShips(s, s.Faction, "Mine", 2);
            var enemy = MakeFleetWithShips(s, reds, "Enemy", 4);
            Assume.That(ValidShips(mine), Is.EqualTo(2));
            Assume.That(ValidShips(enemy), Is.EqualTo(4));

            CombatEngagement.OrderRam(mine, enemy);
            Log($"after 2v4 ram: mine={ValidShips(mine)} enemy={ValidShips(enemy)}");
            Assert.That(ValidShips(mine), Is.EqualTo(0), "the smaller rammer is fully consumed");
            Assert.That(ValidShips(enemy), Is.EqualTo(2), "the larger fleet loses an equal count (2); its surplus survives");
        }

        [Test]
        [Description("A ram on a FRIENDLY (same-faction) fleet is a no-op — the AreHostile guard; no ships lost on either side.")]
        public void OrderRam_FriendlyTarget_NoOp()
        {
            var s = TestScenario.CreateWithColony();
            var mine = MakeFleetWithShips(s, s.Faction, "Mine", 3);
            var friendly = MakeFleetWithShips(s, s.Faction, "Friendly", 3);

            CombatEngagement.OrderRam(mine, friendly);
            Assert.That(ValidShips(mine), Is.EqualTo(3), "no ramming your own — mine intact");
            Assert.That(ValidShips(friendly), Is.EqualTo(3), "friendly intact");
        }

        [Test]
        [Description("OrderRamNearestHostile finds + rams the nearest detectable hostile (the button/AI convenience); " +
                     "returns null with nothing destroyed when no hostile is present.")]
        public void OrderRamNearestHostile_FindsAndRams_NullWhenNone()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var mine = MakeFleetWithShips(s, s.Faction, "Mine", 2);
            var enemy = MakeFleetWithShips(s, reds, "Enemy", 2);

            var rammed = CombatEngagement.OrderRamNearestHostile(mine);
            Assert.That(rammed, Is.EqualTo(enemy), "the nearest (only) hostile is the ram target");
            Assert.That(ValidShips(mine), Is.EqualTo(0), "the ram happened — mine wiped");
            Assert.That(ValidShips(enemy), Is.EqualTo(0), "…and the equal enemy wiped ship-for-ship");

            // A lone fleet with no hostile present → null, no throw, nothing destroyed.
            var solo = MakeFleetWithShips(s, s.Faction, "Solo", 1);
            Assert.That(CombatEngagement.OrderRamNearestHostile(solo), Is.Null, "no hostile → null");
            Assert.That(ValidShips(solo), Is.EqualTo(1), "…and no ship lost");
        }
    }
}
