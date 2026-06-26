using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Movement;
using Pulsar4X.Ships;
using Pulsar4X.Storage;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The CI gauge for the DevTools "Spawn Hostile Fleet" tool (<see cref="CombatSandbox"/>). It proves two things
    /// the live button needs:
    ///   (1) PERSISTENCE — the spawned hostiles (built under the player faction, owner-flipped to a new faction)
    ///       still exist after a REAL game-clock advance. The other combat fixtures avoid a clock advance because a
    ///       BARE enemy faction's flipped ships "didn't survive movement processing"; the sandbox's fuller faction
    ///       setup (KnownSystems + ShipDesigns) is what this checks.
    ///   (2) ENGAGEABLE — the spawned hostiles are REAL enemies the battle trigger fights: driving
    ///       <see cref="CombatEngagement.Tick"/> over the system (the same proven path <c>BattleTriggerTests</c>
    ///       uses) engages them and they destroy an unarmed player ship.
    /// We drive Tick directly for (2) rather than rely on the clock advance because the lightweight colony test
    /// harness does not auto-schedule hotloop processors on <c>TimeStep</c> the way a full generated game does — the
    /// log line records whether the system clock even moved, so a future reader can tell harness-quirk from a real
    /// "trigger never fires" bug. Engine-only -> runs in CI.
    /// </summary>
    [TestFixture]
    public class CombatSandboxTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[combat-sandbox] " + m);

        [Test]
        [Description("CombatSandbox.SpawnHostileFleet produces hostiles that PERSIST through a real clock advance and are ENGAGEABLE: the battle trigger fights them and they destroy an unarmed player ship. Proves the DevTools 'Spawn Hostile Fleet' button stands up a working enemy.")]
        public void SpawnHostileFleet_PersistsThroughClockAdvance_AndIsEngageable()
        {
            var s = TestScenario.CreateWithColony();
            // Prefer the Leviathan (an NTR ship burning 'ntp', a fuel a fresh start may not have unlocked) so the
            // fuel gauge below exercises the trickiest fuel path; fall back to any design if it isn't loaded.
            var allDesigns = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            var design = allDesigns.TryGetValue("default-ship-design-test-capital", out var capital) ? capital : allDesigns.Values.First();

            // Weak player fleet: one UNARMED ship (firepower 0), so the only way it can die is a real battle.
            var playerFleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Home Fleet");
            var playerShip = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Blue 1");
            playerShip.SetDataBlob(new ShipCombatValueDB(0, 100_000, 1.0));
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(s.Faction.Id, playerFleet, playerShip));

            // Spawn the hostile fleet at the same body, then stamp its ships strong for a deterministic outcome.
            var enemyFleet = CombatSandbox.SpawnHostileFleet(s.Game, s.StartingSystem, s.Faction, design, 3, s.StartingBody, "Reds");
            foreach (var es in CombatEngagement.GetFleetShips(enemyFleet))
                es.SetDataBlob(new ShipCombatValueDB(50_000, 1_000_000, 1.0));
            Assert.That(CombatEngagement.GetFleetShips(enemyFleet).Count, Is.EqualTo(3), "the sandbox should spawn 3 hostile ships");

            // Fuel gauge (sensor for SpawnHostileFleet fuelling, added 2026-06-25): a spawned ship that has a
            // thruster AND a tank bay for its fuel must come out fuelled — TotalFuel_kg is set by AddCargoItems
            // -> UpdateMassFuelAndDeltaV. Asserts only for fuel-capable ships, so a design with no fuel bay
            // can't falsely fail it. ShipFactory leaves tanks empty; this proves the sandbox fills them.
            var faction = s.Faction.GetDataBlob<FactionInfoDB>();
            int fuelCapable = 0, fuelled = 0;
            foreach (var es in CombatEngagement.GetFleetShips(enemyFleet))
            {
                if (!es.TryGetDataBlob<NewtonThrustAbilityDB>(out var thr) || string.IsNullOrEmpty(thr.FuelType))
                    continue;
                // The engine's FuelType must resolve to a real fuel MATERIAL (unlocked OR locked). If it
                // doesn't (e.g. it stored a fuel *category* like "ntr" instead of the material "ntp"), the
                // fuelling silently no-ops — fail loudly so that mapping bug can't hide behind a skipped assert.
                var fuelDef = faction.Data.CargoGoods.GetAny(thr.FuelType) ?? faction.Data.LockedCargoGoods.GetAny(thr.FuelType);
                Assert.That(fuelDef, Is.Not.Null, $"thruster FuelType '{thr.FuelType}' should resolve to a defined fuel material");
                Log($"fuel: ship id={es.Id} fuelType='{thr.FuelType}' TotalFuel_kg={thr.TotalFuel_kg:0}");
                if (es.TryGetDataBlob<CargoStorageDB>(out var cargo) && cargo.TypeStores.ContainsKey(fuelDef.CargoTypeID))
                {
                    fuelCapable++;
                    if (thr.TotalFuel_kg > 0) fuelled++;
                }
            }
            Log($"fuel: {fuelled}/{fuelCapable} fuel-capable hostiles fuelled (TotalFuel_kg>0)");
            if (fuelCapable > 0)
                Assert.That(fuelled, Is.EqualTo(fuelCapable), "spawned hostiles that can store their fuel should be fuelled");

            // (1) PERSISTENCE — mark the system observed (what the client does when you watch it) and advance the
            // real clock. The spawned hostiles must still be there afterward. Log whether the system clock moved.
            s.StartingSystem.IncrementExternalObserver(true);
            var before = s.StartingSystem.StarSysDateTime;
            for (int i = 0; i < 3; i++) s.Game.TimePulse.TimeStep();
            var after = s.StartingSystem.StarSysDateTime;
            int survived = CombatEngagement.GetFleetShips(enemyFleet).Count;
            Log($"persistence: enemy={survived}/3, playerAlive={playerShip.IsValid}, systemClockAdvanced={(after - before).TotalHours:0.##}h");
            Assert.That(survived, Is.EqualTo(3), "the spawned hostiles persist through a real clock advance");

            // (2) ENGAGEABLE — drive the trigger over the system (the proven Tick path). The hostiles are real
            // enemies: the trigger finds both fleets, engages, and they destroy the unarmed player ship. Tick
            // returns the fleet count it saw (a diagnostic that both fleets share the manager).
            int fleetsSeen = 0;
            for (int i = 0; i < 30 && playerShip.IsValid; i++)
                fleetsSeen = CombatEngagement.Tick(s.StartingSystem, 5);
            Log($"engagement: fleetsSeen={fleetsSeen}, playerAlive={playerShip.IsValid}, enemy={CombatEngagement.GetFleetShips(enemyFleet).Count}/3");
            Assert.That(playerShip.IsValid, Is.False,
                "the spawned hostiles are real enemies — the battle trigger engages them and they destroy the unarmed player ship");
        }
    }
}
