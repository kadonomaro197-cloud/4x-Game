using System;
using NUnit.Framework;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Self-tests for the scenario harness (TestScenario). If these are green, the harness is a trustworthy
    /// foundation for the economy / combat / etc. feature tests that will build on it.
    ///
    /// The advance test is also the colony-INCLUSIVE game-loop coverage the suite never had:
    /// GameLoopSmokeTests runs on a colony-less generated universe (the in-code DefaultHumans start is broken),
    /// so until now nothing ever advanced a real colony through its mining / industry / population processors.
    /// The harness uses the live CreateFromBlueprint path, so it can.
    /// </summary>
    [TestFixture]
    public class ScenarioHarnessTests
    {
        [Test]
        [Description("The harness builds a real faction + colony start and hands back the key entities.")]
        public void Harness_BuildsColonyStart()
        {
            var scenario = TestScenario.CreateWithColony();

            Assert.That(scenario.Game, Is.Not.Null, "no game");
            Assert.That(scenario.Faction, Is.Not.Null, "no faction");
            Assert.That(scenario.StartingSystem, Is.Not.Null, "no starting system");
            Assert.That(scenario.StartingBody, Is.Not.Null, "no starting body");
            Assert.That(scenario.Colony, Is.Not.Null, "no colony");
        }

        [Test]
        [Description("Advance a colony-bearing universe a full game-year; no processor may throw. This is the "
                     + "colony/economy loop coverage the suite has been missing.")]
        public void Harness_AdvancesAGameYearWithColony_WithoutThrowing()
        {
            var scenario = TestScenario.CreateWithColony();

            Assert.DoesNotThrow(() => scenario.AdvanceTime(TimeSpan.FromDays(365)),
                "A processor threw while advancing one game-year on a colony-bearing universe — something in the "
                + "colony/economy loop (mining, industry, population, …) crashes during normal time advancement; "
                + "see the inner exception.");
        }
    }
}
