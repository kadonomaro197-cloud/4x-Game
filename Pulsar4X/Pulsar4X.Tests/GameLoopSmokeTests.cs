using NUnit.Framework;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// End-to-end smoke test of the running game loop. Advancing the clock fires the hotloop processors
    /// (orbits, sensors, movement, system scheduling, …) on real generated systems and entities, so a crash
    /// in the core simulation during normal time advancement is caught regardless of which processor causes
    /// it. Broad by design: it asserts the loop does not throw, not specific values.
    ///
    /// Scope note: this uses TestingUtilities.CreateTestUniverse (generated systems + a faction) — the same
    /// setup the passing ActivityStateTests use. It does NOT build the starting colony, because the only
    /// in-code "full default start" helper, DefaultStartFactory.DefaultHumans, is currently broken: it loads
    /// Sol via the legacy LoadSystemFromJson path (Data/basemod/sol/systemInfo.json), but that data was
    /// reorganized to ScenarioFiles/systems/sol/sol.json and the live game now builds the colony from JSON via
    /// ColonyFactory.CreateFromBlueprint. Extending this to cover the colony/economy/industry processors is
    /// tracked as follow-up (modernize DefaultHumans, or build the colony via CreateFromBlueprint in the test).
    /// </summary>
    [TestFixture]
    public class GameLoopSmokeTests
    {
        // One TimeStep == MasterTimePulse.Ticklength == 1 game-hour. 72 steps == 3 game-days, which fires the
        // sub-hourly, hourly, and daily processors at least once while staying fast and bounded.
        private const int GameHoursToSimulate = 72;

        [Test]
        [Description("Advance the simulation clock 3 game-days on a generated universe; no processor may throw.")]
        public void GameLoop_AdvancesClockWithoutThrowing()
        {
            var game = TestingUtilities.CreateTestUniverse(1, generateDefaultHumans: false);
            game.Settings.EnforceSingleThread = true; // deterministic, and surfaces processor exceptions on this thread

            Assert.DoesNotThrow(() =>
            {
                for (int hour = 0; hour < GameHoursToSimulate; hour++)
                    game.TimePulse.TimeStep();
            },
            $"A processor threw while advancing the simulation by {GameHoursToSimulate} game-hours — " +
            "something in the game loop crashes during normal time advancement; see the inner exception.");
        }
    }
}
