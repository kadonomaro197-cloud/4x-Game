using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Passive performance sensor.
    ///
    /// Why it exists: every star system's scheduler (ManagerSubPulse) ALREADY times each processor on every
    /// pulse via the built-in PerformanceStopwatch and keeps a history — but nothing logs that outside the live
    /// game's debug window. This sensor advances the clock, then reads that already-collected timing data and
    /// prints a per-processor breakdown into the test output, so every CI run shows "which watch station ran
    /// hottest." It also sanity-checks the numbers (finite, non-negative).
    ///
    /// It is strictly read-only — it reads the stopwatch history the engine produced on its own; it changes
    /// nothing and adds no instrumentation to the hot loop.
    ///
    /// Deliberately NO wall-clock budget assertion: shared CI runners make absolute timings noisy, and a flaky
    /// red alarm is worse than no alarm. Once we know typical numbers, a generous per-processor budget can be
    /// added here to catch a processor that suddenly goes quadratic.
    /// </summary>
    [TestFixture]
    public class PerformanceReadoutSmokeTests
    {
        private const int GameHoursToSimulate = 72;

        [Test]
        [Description("Read the engine's per-processor stopwatch after advancing the clock; report it and sanity-check the numbers.")]
        public void ProcessorTimings_AreCollectedAndSane()
        {
            var game = TestingUtilities.CreateTestUniverse(1, generateDefaultHumans: false);
            game.Settings.EnforceSingleThread = true;

            for (int hour = 0; hour < GameHoursToSimulate; hour++)
                game.TimePulse.TimeStep();

            var totalByProcessor = new Dictionary<string, double>();
            int intervalsObserved = 0;

            foreach (var system in game.Systems)
            {
                // GetHistory() returns the stopwatch's recorded intervals (one per ProcessSystem call).
                foreach (PerformanceStopwatch.PerformanceData data in system.ManagerSubpulses.Performance.GetHistory())
                {
                    intervalsObserved++;
                    Assert.That(double.IsFinite(data.FullIntervalTime) && data.FullIntervalTime >= 0,
                        $"Recorded full-interval time was not a sane value: {data.FullIntervalTime}");

                    foreach (var entry in data.TimesById)
                    {
                        double ms = entry.Value.sum;
                        Assert.That(double.IsFinite(ms) && ms >= 0,
                            $"Recorded time for processor '{entry.Key}' was not a sane value: {ms}");

                        totalByProcessor.TryGetValue(entry.Key, out double running);
                        totalByProcessor[entry.Key] = running + ms;
                    }
                }
            }

            Assert.That(intervalsObserved, Is.GreaterThan(0),
                "The performance stopwatch recorded no intervals after advancing the clock — the timing sensor is not being driven.");

            TestContext.WriteLine($"Processor time across {intervalsObserved} recorded interval(s) over {GameHoursToSimulate} game-hours "
                                  + "(total ms, hottest first):");
            foreach (var entry in totalByProcessor.OrderByDescending(e => e.Value))
                TestContext.WriteLine($"  {entry.Key,-55} {entry.Value,12:F3} ms");
        }
    }
}
