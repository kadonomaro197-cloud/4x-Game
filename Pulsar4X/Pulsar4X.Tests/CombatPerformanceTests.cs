using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Combat depth — performance at fleet scale (the developer's "how does the PC handle 100s of ships?"
    /// question). The dodge resolve aggregates outgoing fire by weapon CLASS (≤ a few entries) and computes each
    /// target's landed fraction once, so a step is O(ships), NOT O(ships²). This builds hundreds of ships a side
    /// and asserts the resolve loop stays fast. Engine-only -> runs in CI. (Only the RESOLVE is timed, not the
    /// ship-building setup.)
    /// </summary>
    [TestFixture]
    public class CombatPerformanceTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[combat-perf] " + m);

        private static void AddArmed(TestScenario s, Entity faction, Entity fleet, ShipDesign design, string name)
        {
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name); // build under player faction
            ship.FactionOwnerID = faction.Id;                                            // then assign true owner
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
        }

        [Test]
        [Description("Hundreds of ships a side resolve quickly — the dodge resolve is O(ships) per step (fire aggregated by weapon class), not O(ships^2).")]
        public void LargeFleetBattle_ResolvesQuickly()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();

            const int N = 100; // per side -> 200 real warships (each with beam profiles + evasion)
            var blue = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Blue Armada");
            var red = FleetFactory.Create(s.StartingSystem, enemyFaction.Id, "Red Armada");
            for (int i = 0; i < N; i++)
            {
                AddArmed(s, s.Faction, blue, design, "B" + i);
                AddArmed(s, enemyFaction, red, design, "R" + i);
            }
            int blue0 = CombatEngagement.GetFleetShips(blue).Count;
            int red0 = CombatEngagement.GetFleetShips(red).Count;

            // Time ONLY the resolve loop (not the ship-building above).
            CombatEngagement.StartEngagement(blue, red);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int steps = 0;
            while (blue.HasDataBlob<FleetCombatStateDB>() && steps < 6000)
            {
                CombatEngagement.StepEngagement(blue, red, 5);
                steps++;
            }
            sw.Stop();

            Log($"{blue0}v{red0} ships resolved in {steps} steps, {sw.ElapsedMilliseconds} ms " +
                $"(survivors blue={CombatEngagement.GetFleetShips(blue).Count} red={CombatEngagement.GetFleetShips(red).Count})");

            Assert.That(blue.HasDataBlob<FleetCombatStateDB>(), Is.False, "the battle terminated (didn't hang)");
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(4000),
                "hundreds of ships resolve fast — O(ships) per step, not O(ships^2). A blowup here means the fire mix stopped aggregating by class.");
        }
    }
}
