using System.Collections.Generic;   // Dictionary (the ComputeMorale factors out-param)
using System.Linq;                   // Sum (colony population total)
using NUnit.Framework;
using Pulsar4X.Colonies;      // PopulationProcessor, ColonyMoraleDB, ColonyInfoDB
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

                // ON → the term fires. With the 2026-08-13 calibration the ratio is jobs ÷ (pop × JobsPerCapita), so a
                // fully-built homeworld reads a MILD deficit (near-neutral) — a NON-ZERO employment contribution either
                // way (the calibration BAND is pinned by EmploymentMorale_PerCapitaDemand_… below).
                PopulationProcessor.EnableEmploymentMorale = true;
                pop.GrowPopulation(s.Colony);
                double on = moraleDB.Factors.TryGetValue("employment", out var vn) ? vn : 0.0;
                Log($"employment factor (flag ON) = {on}");
                Assert.That(on, Is.Not.EqualTo(0.0).Within(1e-9),
                    "flag on → the employment term contributes to morale (the ±40 band is live)");
            }
            finally { PopulationProcessor.EnableEmploymentMorale = saved; }
        }

        /// <summary>The employment morale factor for a given ratio, read from the REAL math (ComputeMorale's factor
        /// out-param) with every other input neutral — so the assertions pin the shipped term, not a re-derivation.</summary>
        private static double EmploymentFactor(double ratio)
        {
            var factors = new Dictionary<string, double>();
            ColonyMoraleDB.ComputeMorale(0.0, 0.0, ratio, 0.0, 0.0, factors);
            return factors.TryGetValue("employment", out var e) ? e : 0.0;
        }

        [Test]
        [Description("THE CALIBRATION (developer-authorized 2026-08-13). The employment denominator is now a per-capita "
                   + "job DEMAND (pop × ColonyMoraleDB.JobsPerCapita), mirroring SustenanceProcessor — so it SCALES with "
                   + "population instead of pinning to −25 against a billions-strong workforce. Pins (a) the term SHAPE "
                   + "over known ratios and (b) that the fully-built homeworld, on its ENGINE-MEASURED installed-jobs "
                   + "total, reads a MILD employment deficit (near-neutral) — lifted off the −25 catastrophe and NOT the "
                   + "EARNED +15 full-employment bonus. Prints the real numbers so the coefficient can be fine-tuned.")]
        public void EmploymentMorale_PerCapitaDemand_LandsHomeworldNearNeutral()
        {
            // (a) THE SHAPE — discontinuous by design (ColonyMoraleDB): ratio<1 → a negative approaching 0⁻; ratio≥1
            //     jumps to the earned +15; the −1 sentinel ("no job data") → neutral 0. Calibration-independent.
            Assert.That(EmploymentFactor(0.95), Is.EqualTo(-1.25).Within(1e-6), "near-full employment → a small deficit");
            Assert.That(EmploymentFactor(0.5),  Is.EqualTo(-12.5).Within(1e-6), "half the demand met → half the −25 penalty");
            Assert.That(EmploymentFactor(0.0),  Is.EqualTo(-25.0).Within(1e-6), "no jobs for the demand → full unemployment penalty");
            Assert.That(EmploymentFactor(1.0),  Is.EqualTo(15.0).Within(1e-6),  "demand met → the EARNED full-employment bonus");
            Assert.That(EmploymentFactor(-1.0), Is.EqualTo(0.0).Within(1e-6),   "no job data (sentinel) → neutral");

            // (b) THE HOMEWORLD, on the ENGINE-MEASURED jobs total (what the coefficient is tuned against).
            var s = TestScenario.CreateWithColony();
            var pop = new PopulationProcessor();
            var moraleDB = s.Colony.GetDataBlob<ColonyMoraleDB>();
            long jobs = s.Colony.GetDataBlob<ComponentInstancesDB>().GetTotalJobs();
            long totalPop = s.Colony.GetDataBlob<ColonyInfoDB>().Population.Values.Sum();
            double jobDemand = totalPop * ColonyMoraleDB.JobsPerCapita;
            Log($"HOMEWORLD calibration: pop {totalPop:N0}, jobs {jobs:N0}, jobDemand {jobDemand:N0}, "
              + $"ratio {(jobDemand > 0 ? jobs / jobDemand : 0):0.000}, JobsPerCapita {ColonyMoraleDB.JobsPerCapita:0.0e-0}");

            bool saved = PopulationProcessor.EnableEmploymentMorale;
            try
            {
                PopulationProcessor.EnableEmploymentMorale = true;
                pop.GrowPopulation(s.Colony);
                double homeFactor = moraleDB.Factors.TryGetValue("employment", out var v) ? v : 0.0;
                Log($"HOMEWORLD employment factor = {homeFactor:0.00}, morale = {moraleDB.Morale:0.0}");
                // Strictly negative and mild: proves the calibration lifted the homeworld off the −25 catastrophe to
                // near-neutral, AND that its ratio is < 1 (it did NOT snap to the earned +15). The band tolerates the
                // ~52k jobs estimate wobbling; the printout above carries the exact landing for fine-tuning the coefficient.
                Assert.That(homeFactor, Is.GreaterThan(-9.0).And.LessThan(0.0),
                    "the calibrated homeworld reads a MILD employment deficit (near-neutral) — off the −25 catastrophe and "
                    + "below the EARNED +15 (a thriving, fully-employed world is earned by over-building industry)");
            }
            finally { PopulationProcessor.EnableEmploymentMorale = saved; }
        }
    }
}
