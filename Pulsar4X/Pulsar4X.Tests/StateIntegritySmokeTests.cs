using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Passive state-integrity sensor.
    ///
    /// Why it exists: the engine has NO NaN/Infinity guards and NO try/catch around its processors. So a
    /// processor that computes a garbage number — a NaN position, an infinite coordinate — does not crash.
    /// The bad value just propagates and the game keeps running on it. A "did it throw?" check (the game-loop
    /// smoke test) is completely blind to that failure mode. This sensor rounds the gauges: it advances the
    /// clock, then reads every entity's position in every system and asserts each coordinate is a finite real
    /// number.
    ///
    /// It is strictly read-only — it inspects game state and never mutates it, so it cannot affect the game.
    ///
    /// Scope today is PositionDB, the highest-value always-true invariant (a position is never legitimately
    /// NaN/Inf). Velocity, population >= 0, fuel >= 0, and cargo-mass conservation are the natural next
    /// additions to FindNonFinitePositions as those systems get a colony-bearing test universe to run on.
    /// </summary>
    [TestFixture]
    public class StateIntegritySmokeTests
    {
        // One TimeStep == 1 game-hour. 72 == 3 game-days: enough for the orbit/movement processors to move
        // every body many times, which is where a bad number would show up.
        private const int GameHoursToSimulate = 72;

        [Test]
        [Description("Every entity position must be a finite number before and after advancing the clock.")]
        public void Positions_StayFinite_AcrossClockAdvance()
        {
            var game = TestingUtilities.CreateTestUniverse(1, generateDefaultHumans: false);
            game.Settings.EnforceSingleThread = true; // deterministic; surfaces processor exceptions on this thread

            var startViolations = FindNonFinitePositions(game, out int positionsAtStart);
            Assert.That(startViolations, Is.Empty,
                "A position was already non-finite in the freshly generated universe (system generation produced garbage):\n"
                + string.Join("\n", startViolations));

            for (int hour = 0; hour < GameHoursToSimulate; hour++)
                game.TimePulse.TimeStep();

            var endViolations = FindNonFinitePositions(game, out int positionsAfter);

            TestContext.WriteLine($"State-integrity sensor: checked {positionsAtStart} positions at start, "
                                  + $"{positionsAfter} after {GameHoursToSimulate} game-hours; all finite.");

            Assert.That(endViolations, Is.Empty,
                $"A position became non-finite while advancing {GameHoursToSimulate} game-hours — a processor is "
                + "producing garbage numbers that do NOT throw (silent corruption):\n" + string.Join("\n", endViolations));
        }

        /// <summary>
        /// Sweeps every PositionDB in every system and returns one human-readable line for each position that is
        /// non-finite or that throws when read. Defensive: a read that throws is recorded as a violation rather
        /// than aborting the sweep, so one bad entity does not hide the rest.
        /// </summary>
        private static List<string> FindNonFinitePositions(Game game, out int positionsChecked)
        {
            var violations = new List<string>();
            int count = 0;
            int systemIndex = 0;

            foreach (var system in game.Systems)
            {
                foreach (var pos in system.GetAllDataBlobsOfType<PositionDB>())
                {
                    count++;
                    int entityId = pos.OwningEntity != null ? pos.OwningEntity.Id : -1;
                    try
                    {
                        var p = pos.AbsolutePosition;
                        if (!double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(p.Z))
                            violations.Add($"  system[{systemIndex}] entity {entityId}: AbsolutePosition = ({p.X}, {p.Y}, {p.Z})");
                    }
                    catch (Exception ex)
                    {
                        violations.Add($"  system[{systemIndex}] entity {entityId}: reading AbsolutePosition threw {ex.GetType().Name}: {ex.Message}");
                    }
                }
                systemIndex++;
            }

            positionsChecked = count;
            return violations;
        }
    }
}
