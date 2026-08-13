using NUnit.Framework;
using Pulsar4X.Colonies;      // PopulationProcessor, ColonyMoraleDB
using Pulsar4X.Datablobs;     // ComponentInstancesDB
using Pulsar4X.Extensions;    // GetTotalJobs (extension)

namespace Pulsar4X.Tests
{
    /// <summary>
    /// OPERATION BLUEPRINT-TO-STEEL A1 (civic door / ENGINE-WIRING-BACKLOG TIER 2 #2) — feed the ±40 employment→morale
    /// term "that can never fire."
    ///
    /// The gap this closes: the whole employment-morale chain was welded EXCEPT the first rung — the CONSUMER
    /// (`PopulationProcessor` turns jobs÷workforce into a ±40 morale band), the aggregator (`GetTotalJobs`), and the
    /// attribute (`EmploymentAtbDB`) all existed, but NO template declared jobs, so `GetTotalJobs` summed to 0 and the
    /// employment term contributed exactly 0 in every colony, every game. A1 wires the PRODUCER: per the civic-door
    /// design, `GetTotalJobs` now sources each building's operating-CREW requirement (`ComponentDesign.CrewReq`) as its
    /// employment (an `EmploymentAtbDB.Jobs` override still wins where a template declares one).
    ///
    /// Because turning the term on is the single biggest live behaviour change in the backlog (morale feeds migration,
    /// tax income, legitimacy — and CrewReq-as-jobs reads heavy unemployment against a billions-pop workforce, a
    /// CALIBRATION the developer owns), the morale term is FLAG-GATED (`PopulationProcessor.EnableEmploymentMorale`,
    /// default off → byte-identical). These gauges prove: (1) the producer now reads a real non-zero jobs total on a
    /// staffed colony (it summed 0 before A1); and (2) the flag gates the morale term — OFF the employment factor is the
    /// neutral 0 (byte-identical, the `MoraleTests` homeworld baseline stays green), ON it fires. Flag reset in finally.
    /// </summary>
    [TestFixture]
    public class EmploymentMoraleTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[a1-employment] " + m);

        [Test]
        [Description("The PRODUCER: GetTotalJobs now sums installed buildings' CrewReq, so a staffed start colony reads a "
                   + "NON-ZERO jobs total (before A1 it summed EmploymentAtbDB.Jobs, which no template declares → 0 forever).")]
        public void GetTotalJobs_ReadsInstalledCrewReq_NonZeroOnAStaffedColony()
        {
            var s = TestScenario.CreateWithColony();
            long jobs = s.Colony.GetDataBlob<ComponentInstancesDB>().GetTotalJobs();
            Log($"start colony jobs total = {jobs}");
            Assert.That(jobs, Is.GreaterThan(0),
                "the start colony's installed infrastructure declares operating crew → jobs > 0 (the producer that was missing)");
        }

        [Test]
        [Description("The CONSUMER GATE: with EnableEmploymentMorale OFF (default) the employment morale factor is the "
                   + "neutral 0 (byte-identical); with it ON the term fires (a staffed colony vs its workforce reads a "
                   + "non-zero employment contribution). Flag reset in finally so it never leaks to another fixture.")]
        public void EmploymentMorale_FlagGatesTheMoraleTerm()
        {
            var s = TestScenario.CreateWithColony();
            var pop = new PopulationProcessor();
            var moraleDB = s.Colony.GetDataBlob<ColonyMoraleDB>();

            bool saved = PopulationProcessor.EnableEmploymentMorale;
            try
            {
                // OFF (default) → the employment term reads the -1.0 neutral sentinel → 0 contribution (byte-identical).
                PopulationProcessor.EnableEmploymentMorale = false;
                pop.GrowPopulation(s.Colony);
                double off = moraleDB.Factors.TryGetValue("employment", out var vo) ? vo : 0.0;
                Log($"employment factor (flag OFF) = {off}");
                Assert.That(off, Is.EqualTo(0.0).Within(1e-9),
                    "flag off → the employment term is neutral (byte-identical — the homeworld morale baseline is unchanged)");

                // ON → the term fires. On a billions-pop homeworld the installed-infra jobs are a tiny fraction of the
                // workforce, so the ratio reads heavy unemployment — a NON-ZERO (negative) employment contribution.
                PopulationProcessor.EnableEmploymentMorale = true;
                pop.GrowPopulation(s.Colony);
                double on = moraleDB.Factors.TryGetValue("employment", out var vn) ? vn : 0.0;
                Log($"employment factor (flag ON) = {on}");
                Assert.That(on, Is.Not.EqualTo(0.0).Within(1e-9),
                    "flag on → the employment term contributes to morale (the ±40 band is live)");
            }
            finally { PopulationProcessor.EnableEmploymentMorale = saved; }
        }
    }
}
