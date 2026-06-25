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
    /// The CI gauge for the DevTools "Spawn Hostile Fleet" tool (<see cref="CombatSandbox"/>). The other combat
    /// fixtures drive <c>CombatEngagement.Tick</c> DIRECTLY and never advance the clock — because a bare
    /// enemy faction's owner-flipped ships "didn't survive movement processing across a clock advance." That left
    /// the live "spawn an enemy and press play" path unproven. This test ADVANCES THE REAL GAME CLOCK (the full
    /// per-tick processor sweep) and asserts the spawned hostile fleet survives it and is auto-engaged by the
    /// battle trigger — so the live button is proven in CI, not on the developer's machine. Engine-only -> runs in CI.
    /// </summary>
    [TestFixture]
    public class CombatSandboxTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[combat-sandbox] " + m);

        [Test]
        [Description("Spawned hostiles SURVIVE a real game-clock advance and auto-engage: an unarmed player ship is destroyed only because the spawned enemy persisted through the full processor sweep, engaged, and won. Proves the live 'Spawn Hostile Fleet' button works end-to-end (clock advance, not just a direct Tick).")]
        public void SpawnHostileFleet_SurvivesClockAdvance_AndAutoEngages()
        {
            var s = TestScenario.CreateWithColony();
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();

            // Weak player fleet: one UNARMED ship (firepower 0), so the only way it can die is a real battle
            // against a hostile fleet that actually survived the clock advance and engaged it.
            var playerFleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Home Fleet");
            var playerShip = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Blue 1");
            playerShip.SetDataBlob(new ShipCombatValueDB(0, 100_000, 1.0));
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, playerFleet, playerShip));

            // Spawn the hostile fleet at the SAME body (so it's in range), then stamp its ships strong so the
            // outcome is deterministic (the engine helper itself leaves the ships' real, design-derived values).
            var enemyFleet = CombatSandbox.SpawnHostileFleet(s.Game, s.StartingSystem, s.Faction, design, 3, s.StartingBody, "Reds");
            foreach (var es in CombatEngagement.GetFleetShips(enemyFleet))
                es.SetDataBlob(new ShipCombatValueDB(50_000, 1_000_000, 1.0));

            Assert.That(CombatEngagement.GetFleetShips(enemyFleet).Count, Is.EqualTo(3), "the sandbox should spawn 3 hostile ships");

            // Advance the REAL clock — the full processor sweep (movement / sensors / orders / the 5s battle
            // trigger). One TimeStep is a game-hour, so the trigger runs hundreds of times per step: plenty to
            // engage and resolve. This is the step the bare-flip test pattern could not survive.
            for (int i = 0; i < 5; i++) s.Game.TimePulse.TimeStep();

            int enemyLeft = CombatEngagement.GetFleetShips(enemyFleet).Count;
            Log($"after clock advance: playerAlive={playerShip.IsValid}, enemyShips={enemyLeft}/3");

            Assert.That(playerShip.IsValid, Is.False,
                "the spawned hostiles survived the clock advance, auto-engaged, and destroyed the unarmed player ship");
            Assert.That(enemyLeft, Is.GreaterThan(0), "the winning hostile fleet still has ships");
        }
    }
}
