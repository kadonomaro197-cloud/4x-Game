using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Movement;
using Pulsar4X.Names;
using Pulsar4X.Orbital;
using Pulsar4X.Ships;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-1b — the anomaly factory + presence detection + the site work processor
    /// (docs/SITE-ENGINE-DESIGN.md, the anomaly-first build). Proves the located half of the spine:
    /// <see cref="FieldSiteFactory"/> drops a neutral anomaly into a real star system, and
    /// <see cref="SiteWorkProcessor"/> banks progress ONLY while a worker ship is parked on it — no worker, no
    /// advance (agency-preserving, no timer). Drives the processor directly (the colony harness doesn't reliably
    /// auto-fire hotloops — Pulsar4X.Tests/CLAUDE.md gotcha), plus a no-throw time-advance smoke.
    /// </summary>
    [TestFixture]
    public class SiteWorkTests
    {
        // Spawn a real faction ship at the colony body — a valid worker — and return it with its exact position.
        private static Entity SpawnWorker(TestScenario s, out Vector3 workerPos)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "SE-1b Worker");
            workerPos = ship.GetDataBlob<PositionDB>().AbsolutePosition;
            return ship;
        }

        [Test]
        [Description("SE-1b: the factory drops a neutral anomaly site (name + fixed position + FieldSiteDB) into the system.")]
        public void Factory_CreatesNeutralAnomaly_InTheSystem()
        {
            var s = TestScenario.CreateWithColony();
            int before = s.StartingSystem.GetAllEntitiesWithDataBlob<FieldSiteDB>().Count;

            var site = FieldSiteFactory.CreateAnomalySite(
                s.StartingSystem, new Vector3(1e14, 0, 0), "Test Anomaly");

            Assert.That(site.Manager, Is.SameAs(s.StartingSystem), "the site didn't land in the system");
            Assert.That(site.FactionOwnerID, Is.EqualTo(Game.NeutralFactionId), "an unworked site belongs to nobody");
            Assert.That(site.HasDataBlob<FieldSiteDB>(), Is.True);
            Assert.That(site.HasDataBlob<PositionDB>(), Is.True, "no position → can't be located/worked");
            Assert.That(site.HasDataBlob<NameDB>(), Is.True);
            Assert.That(site.GetDataBlob<FieldSiteDB>().Status, Is.EqualTo(SiteStatus.Discovered));
            Assert.That(s.StartingSystem.GetAllEntitiesWithDataBlob<FieldSiteDB>().Count, Is.EqualTo(before + 1));
        }

        [Test]
        [Description("SE-1b: a worker parked ON the site is detected; the processor begins the study and banks a day's work.")]
        public void WorkerPresent_ProcessorBanksADaysWork()
        {
            var s = TestScenario.CreateWithColony();
            SpawnWorker(s, out var workerPos);

            // Co-locate the anomaly exactly with the worker → present.
            var site = FieldSiteFactory.CreateAnomalySite(s.StartingSystem, workerPos, "Co-located Anomaly");
            var siteDB = site.GetDataBlob<FieldSiteDB>();

            Assert.That(SiteWorkProcessor.TryFindWorker(s.StartingSystem, workerPos, out var found), Is.True,
                "the parked ship should be detected as an on-site worker");
            Assert.That(found.FactionOwnerID, Is.EqualTo(s.Faction.Id), "the on-site worker is our faction's ship");

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400); // one game-day

            Assert.That(siteDB.Status, Is.EqualTo(SiteStatus.Worked), "the first worker begins the study");
            Assert.That(siteDB.Progress, Is.EqualTo(SiteWorkProcessor.WorkPerDay).Within(1e-6), "a day banks WorkPerDay");
            Assert.That(siteDB.Understanding, Is.EqualTo(SiteWorkProcessor.UnderstandingPerDay).Within(1e-6));
            Assert.That(siteDB.WorkedByFactionId, Is.EqualTo(s.Faction.Id), "the working faction is recorded (yield routes here)");
        }

        [Test]
        [Description("SE-1b: with no worker within range, the site does not advance (no timer — pressure, not a clock).")]
        public void NoWorker_SiteDoesNotAdvance()
        {
            var s = TestScenario.CreateWithColony();

            // Far from the colony and its start fleet — nobody is on-site.
            var farAway = new Vector3(1e15, 1e15, 1e15);
            var site = FieldSiteFactory.CreateAnomalySite(s.StartingSystem, farAway, "Remote Anomaly");
            var siteDB = site.GetDataBlob<FieldSiteDB>();

            Assert.That(SiteWorkProcessor.TryFindWorker(s.StartingSystem, farAway, out _), Is.False,
                "no ship should be within presence range of the remote anomaly");

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400);

            Assert.That(siteDB.Status, Is.EqualTo(SiteStatus.Discovered), "an unworked site stays Discovered");
            Assert.That(siteDB.Progress, Is.EqualTo(0.0), "no worker → no progress banked");
            Assert.That(siteDB.Understanding, Is.EqualTo(0.0));
        }

        [Test]
        [Description("SE-1b: a resolved (Depleted) site is ignored by the processor even with a worker present.")]
        public void ResolvedSite_IgnoredByProcessor()
        {
            var s = TestScenario.CreateWithColony();
            SpawnWorker(s, out var workerPos);

            var site = FieldSiteFactory.CreateAnomalySite(s.StartingSystem, workerPos, "Spent Anomaly");
            var siteDB = site.GetDataBlob<FieldSiteDB>();

            // Drive it to a terminal state via the pure machine, then confirm the processor won't re-work it.
            SiteMachine.Accrue(siteDB, siteDB.UnderstandingToResolve, siteDB.UnderstandingToResolve);
            Assert.That(SiteMachine.Resolve(siteDB), Is.True);
            Assert.That(siteDB.Status, Is.EqualTo(SiteStatus.Depleted));
            double progressBefore = siteDB.Progress;

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400);

            Assert.That(siteDB.Progress, Is.EqualTo(progressBefore), "a spent site takes no more work");
            Assert.That(siteDB.Status, Is.EqualTo(SiteStatus.Depleted));
        }

        [Test]
        [Description("SE-1b: advancing the clock with a live anomaly + parked worker in the system never throws.")]
        public void TimeAdvance_WithAnomaly_DoesNotThrow()
        {
            var s = TestScenario.CreateWithColony();
            SpawnWorker(s, out var workerPos);
            FieldSiteFactory.CreateAnomalySite(s.StartingSystem, workerPos, "Live Anomaly");

            Assert.DoesNotThrow(() => s.AdvanceTime(TimeSpan.FromDays(5)),
                "advancing time with a FieldSite + its work processor active threw");
        }
    }
}
