using NUnit.Framework;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// End-to-end smoke test of the running game loop.
    ///
    /// Creating the default Sol/human start and advancing the clock fires every hotloop processor
    /// (orbits, sensors, movement, economy, mining, research, construction, damage, …) on real entities.
    /// So this catches a crash in ANY system during normal time advancement — including a system that a
    /// given change never touched, which is the case point-tests can't cover. It is deliberately broad:
    /// it asserts the loop does not throw, not specific values (a logic error that produces a wrong number
    /// without throwing is out of scope — that needs a targeted test that asserts the value).
    /// </summary>
    [TestFixture]
    public class GameLoopSmokeTests
    {
        // One TimeStep == MasterTimePulse.Ticklength == 1 game-hour. 72 steps == 3 game-days, which fires the
        // sub-hourly, hourly, and daily processors (mining offset 1h, research 0.5h, construction 6h, …) at
        // least once while staying fast and bounded. (The 30-day-cycle processors — population, NPC doctrine —
        // are not reached; extend this if you need to cover them.)
        private const int GameHoursToSimulate = 72;

        [Test]
        [Description("Create the default Sol/human start and run the clock forward 3 game-days; " +
                     "no processor may throw during normal time advancement.")]
        public void DefaultStart_AdvancesClockWithoutThrowing()
        {
            var game = TestingUtilities.CreateTestUniverse(1, generateDefaultHumans: true);
            game.Settings.EnforceSingleThread = true; // deterministic, and surfaces processor exceptions on this thread

            Assert.DoesNotThrow(() =>
            {
                for (int hour = 0; hour < GameHoursToSimulate; hour++)
                    game.TimePulse.TimeStep();
            },
            $"A processor threw while advancing the default start by {GameHoursToSimulate} game-hours. " +
            "Something in the game loop crashes during normal time advancement — see the inner exception for which system.");
        }
    }
}
