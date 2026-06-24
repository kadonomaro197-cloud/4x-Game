using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The "does a New Game actually build the starting fleet?" gauge — the CI-checkable half of the
    /// "fleets aren't working" investigation (2026-06-24). The colony blueprint `colony-earth`
    /// (GameData/basemod/ScenarioFiles/systems/sol/earth.json) defines THREE fleets — Freight / Military /
    /// Science — with FIVE ships (freighter, two gunships, surveyor, sensor sat), and
    /// `ColonyFactory.CreateFromBlueprint` is supposed to build them from `colonyBlueprint.Fleets`.
    ///
    /// `TestScenario.CreateWithColony()` mirrors the live New Game (`NewGameMenu.CreateGameCore` ->
    /// `ColonyFactory.CreateFromBlueprint`), so this asserts the ENGINE side actually produces those fleets
    /// and ships. If this is GREEN, the start builds the fleet and any "I see no fleet" is a CLIENT problem
    /// (ships orbit the planet at 2x its radius = hidden at system zoom; or stale live mod data). If it is
    /// RED (0 fleets), the engine start path is the bug. The client half can only be checked by the dev's
    /// local build + the DevTools "Dump State" button.
    /// </summary>
    [TestFixture]
    public class StartFleetTests
    {
        [Test]
        [Description("A New Game (engine path) builds the colony blueprint's starting fleets and ships.")]
        public void Start_BuildsStartingFleetsAndShips()
        {
            var s = TestScenario.CreateWithColony();

            var fleets = s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>();
            var ships = s.StartingSystem.GetAllEntitiesWithDataBlob<ShipInfoDB>();

            // Readout first (prints even on failure) so CI shows the actual counts, not just pass/fail.
            TestContext.Progress.WriteLine($"[start] fleets={fleets.Count}, ships={ships.Count} (expected: 3 fleets, 5 ships from colony-earth)");
            foreach (var f in fleets)
                TestContext.Progress.WriteLine($"[start]   fleet id={f.Id} faction={f.FactionOwnerID}");
            foreach (var sh in ships)
                TestContext.Progress.WriteLine($"[start]   ship  id={sh.Id} faction={sh.FactionOwnerID}");

            Assert.That(ships.Count, Is.GreaterThan(0),
                "New Game built NO ships — the colony blueprint's Fleets block produced none.");
            Assert.That(fleets.Count, Is.GreaterThan(0),
                "New Game built NO fleets — ColonyFactory.CreateFromBlueprint didn't build colonyBlueprint.Fleets.");
        }
    }
}
