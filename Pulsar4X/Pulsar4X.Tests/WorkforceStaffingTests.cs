using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Datablobs;   // ComponentInstancesDB
using Pulsar4X.Engine;
using Pulsar4X.Extensions;  // GetTotalJobs
using Pulsar4X.Industry;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// TIER 2.6 gauge — the workforce→production STAFFING throttle (developer ruling 2026-08-10,
    /// docs/assembler/ENGINE-WIRING-BACKLOG-2026-08-06.md). Proves that when the flag is ON, a colony's build rate is
    /// paced by whether its available workforce can cover its facilities' total job demand — and that with the flag OFF
    /// (the engine-suite default) the throttle never fires, so the rest of the suite is byte-identical.
    ///
    /// <para>Measured on a REAL start colony (TestScenario), and calibration-independent: it asserts the RELATIONSHIP
    /// (full when manned, ~half at half the workforce, zero when starved, inert with no pool) against the colony's own
    /// live job total, so re-tuning populations or building crews cannot make it lie.</para>
    /// </summary>
    [TestFixture]
    public class WorkforceStaffingTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[staffing] " + m);

        // The flag is a process-global static — always restore it so it can't leak into the rest of the shard.
        [TearDown]
        public void ResetFlag() => IndustryTools.EnableWorkforceStaffing = false;

        [Test]
        [Description("With the staffing flag ON, a colony's production rate scales with min(1, available workforce / total facility job demand): full when manned, ~half at half the workforce, zero when starved; with the flag OFF it is always 1.0 (byte-identical).")]
        public void StaffingEfficiency_PacesProductionByWorkforce_FlagGated()
        {
            var s = TestScenario.CreateWithColony();
            var colony = s.Colony;

            long pop = colony.GetDataBlob<ColonyInfoDB>().Population.Values.Sum();
            long jobs = colony.GetDataBlob<ComponentInstancesDB>().GetTotalJobs();
            Assert.That(jobs, Is.GreaterThan(1), "the start colony's installed buildings declare crew (GetTotalJobs > 1)");

            // Ensure a manpower pool exists (a colony carries one; add a fresh one if not, so the math is deterministic).
            if (!colony.TryGetDataBlob<ColonyManpowerDB>(out var mp))
            {
                mp = new ColonyManpowerDB();
                colony.SetDataBlob(mp);
            }

            long availableNow = mp.AvailableBulk(pop);
            Log($"pop {pop:N0} · workforce {ColonyManpowerDB.Workforce(pop):N0} · available {availableNow:N0} · jobs {jobs:N0}");
            Assert.That(availableNow, Is.GreaterThan(jobs), "the homeworld starts able to fully man its facilities");

            // (1) Flag OFF → always 1.0, even fully drained — the byte-identical default.
            IndustryTools.EnableWorkforceStaffing = false;
            mp.CommitBulk(availableNow);
            Assert.That(IndustryTools.StaffingEfficiency(colony), Is.EqualTo(1.0).Within(1e-9), "flag off → no throttle");
            mp.ReleaseBulk(availableNow);

            // (2) Flag ON, fully manned (workforce >> demand) → 1.0.
            IndustryTools.EnableWorkforceStaffing = true;
            Assert.That(IndustryTools.StaffingEfficiency(colony), Is.EqualTo(1.0).Within(1e-9),
                "workforce covers the job demand → full rate");

            // (3) Drain the workforce down to exactly half the job demand → ~0.5.
            long avail = mp.AvailableBulk(pop);
            mp.CommitBulk(avail - jobs / 2);
            double half = IndustryTools.StaffingEfficiency(colony);
            Log($"drained to half demand → staffing {half:0.###}");
            Assert.That(half, Is.EqualTo(0.5).Within(0.05), "workforce at half the job demand → ~half rate");

            // (4) Drain the rest → 0 (a colony that can't man anything builds nothing).
            mp.CommitBulk(mp.AvailableBulk(pop));
            Assert.That(IndustryTools.StaffingEfficiency(colony), Is.EqualTo(0.0).Within(1e-9),
                "no available workforce → zero production rate");
        }

        [Test]
        [Description("A host with no manpower pool (the -1 sentinel from AvailableWorkforce — e.g. a station or a bare entity) is inert: the staffing throttle stays 1.0, exactly as the crew gate is inert there. AvailableWorkforce reads the real pool for a colony.")]
        public void StaffingEfficiency_NoManpowerPool_IsInert_AndAvailableWorkforceReadsThePool()
        {
            var s = TestScenario.CreateWithColony();
            IndustryTools.EnableWorkforceStaffing = true;

            // A bare (unmanaged) entity has no manpower pool AND no Manager — AvailableWorkforce must return -1 without
            // NRE-ing on TryGetDataBlob (the Manager==null guard), and the throttle must stay inert.
            var bare = Entity.Create();
            Assert.That(ManpowerTools.AvailableWorkforce(bare), Is.EqualTo(-1L), "no pool / unmanaged → -1 sentinel, no NRE");
            Assert.That(IndustryTools.StaffingEfficiency(bare), Is.EqualTo(1.0).Within(1e-9),
                "no manpower pool → no throttle (station-inert)");

            // The colony has a real pool: AvailableWorkforce is a real, non-negative count.
            Assert.That(ManpowerTools.AvailableWorkforce(s.Colony), Is.GreaterThanOrEqualTo(0L),
                "a colony reports its real available workforce");
        }
    }
}
