using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Datablobs;   // OrderableDB lives here (namespace ≠ folder — it's under Engine/Orders/)
using Pulsar4X.Engine;
using Pulsar4X.Engine.Orders;
using Pulsar4X.Factions;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Ships;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-5c — the commit-branch order + "a branched site waits for the player" (docs/SITE-ENGINE-DESIGN.md
    /// §4). Proves the whole "study → THEN choose" decision end to end: a branched site keeps accruing understanding but
    /// does NOT auto-resolve (it waits), the <see cref="CommitSiteBranchOrder"/> resolves it down the CHOSEN branch and
    /// delivers that branch's yield, and a plain branchless site still auto-resolves exactly as SE-1 (byte-identical).
    /// Drives the processor + order directly (deterministic — the colony harness doesn't reliably auto-fire hotloops).
    /// </summary>
    [TestFixture]
    public class SiteCommitBranchTests
    {
        private static Entity SpawnWorker(TestScenario s, out Vector3 workerPos)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "SE-5c Worker");
            workerPos = ship.GetDataBlob<PositionDB>().AbsolutePosition;
            return ship;
        }

        // A worked anomaly with a worker parked on it, authored with two branches (Seal req 50 / Ally req 150).
        private static (Entity site, FieldSiteDB db) BranchedAnomalyWithWorker(TestScenario s)
        {
            SpawnWorker(s, out var workerPos);
            var site = FieldSiteFactory.CreateAnomalySite(s.StartingSystem, workerPos, "Branched Anomaly");
            var db = site.GetDataBlob<FieldSiteDB>();
            db.Branches = new List<SiteBranch>
            {
                new SiteBranch { Name = "Seal", UnderstandingRequired = 50,  Yield = SiteYield.Nothing,       ResultStatus = SiteStatus.Depleted },
                new SiteBranch { Name = "Ally", UnderstandingRequired = 150, Yield = SiteYield.StrategicAsset, ResultStatus = SiteStatus.Persistent },
            };
            return (site, db);
        }

        [Test]
        [Description("SE-5c: the site factory gives the site an OrderableDB so it can receive the commit-branch order.")]
        public void Factory_SiteIsOrderable()
        {
            var s = TestScenario.CreateWithColony();
            var site = FieldSiteFactory.CreateAnomalySite(s.StartingSystem, new Vector3(1e14, 0, 0), "Orderable Anomaly");
            Assert.That(site.HasDataBlob<OrderableDB>(), Is.True, "the site must be orderable to receive a branch commit");

            var surface = FieldSiteFactory.CreateSurfaceSite(s.StartingSystem, s.StartingBody, 1, 0, 0, "Orderable Ruin");
            Assert.That(surface.HasDataBlob<OrderableDB>(), Is.True, "a surface site must be orderable too");
        }

        [Test]
        [Description("SE-5c: a branched site accrues understanding (branches unlock) but does NOT auto-resolve — it waits for the player's commit.")]
        public void BranchedSite_DoesNotAutoResolve_ButKeepsAccruing()
        {
            var s = TestScenario.CreateWithColony();
            var (site, db) = BranchedAnomalyWithWorker(s);

            // 20 days of work → understanding 100 (> Seal's 50). A branchless site WOULD resolve at 100; this one must not.
            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 20 * 86400);

            Assert.That(db.Understanding, Is.GreaterThanOrEqualTo(50.0), "understanding kept accruing");
            Assert.That(SiteMachine.AnyBranchUnlocked(db), Is.True, "at least the cheap branch unlocked");
            Assert.That(db.Status, Is.EqualTo(SiteStatus.Worked), "a branched site does NOT auto-resolve — it waits for the choice");
            Assert.That(db.YieldDelivered, Is.False, "nothing paid out until a branch is committed");
            Assert.That(db.CommittedBranchIndex, Is.EqualTo(-1), "no branch committed yet");
        }

        [Test]
        [Description("SE-5c: committing an unlocked branch (via the order) resolves the site to that branch's outcome and pays it once.")]
        public void CommitOrder_ResolvesChosenBranch()
        {
            var s = TestScenario.CreateWithColony();
            var (site, db) = BranchedAnomalyWithWorker(s);
            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 20 * 86400); // understanding 100 → Seal (0) unlocked

            var order = CommitSiteBranchOrder.CreateCommand(site, 0); // commit "Seal" (Depleted outcome)
            Assert.That(order.IsValidCommand(s.Game), Is.True, "a real branch on a branched, unresolved site is a valid order");

            order.Execute(s.StartingSystem.StarSysDateTime);

            Assert.That(db.CommittedBranchIndex, Is.EqualTo(0), "the choice is recorded");
            Assert.That(db.Status, Is.EqualTo(SiteStatus.Depleted), "resolved to the chosen branch's outcome");
            Assert.That(db.YieldDelivered, Is.True, "the branch's yield was paid once");
            Assert.That(order.IsFinished(), Is.True, "the order is finished");
        }

        [Test]
        [Description("SE-5c: a branchless site still auto-resolves and pays out exactly as SE-1 (byte-identical fallback).")]
        public void BranchlessSite_StillAutoResolves()
        {
            var s = TestScenario.CreateWithColony();
            SpawnWorker(s, out var workerPos);
            var site = FieldSiteFactory.CreateAnomalySite(s.StartingSystem, workerPos, "Plain Anomaly"); // no branches
            var db = site.GetDataBlob<FieldSiteDB>();

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 30 * 86400); // understanding 150 > 100 threshold

            Assert.That(db.HasBranches, Is.False);
            Assert.That(db.Status, Is.EqualTo(SiteStatus.Depleted), "a branchless one-shot auto-resolves as before");
            Assert.That(db.YieldDelivered, Is.True, "and pays out automatically");
        }

        [Test]
        [Description("SE-5c guards: the order is invalid on a branchless site, and committing a still-locked branch leaves the site waiting.")]
        public void CommitOrder_Guards()
        {
            var s = TestScenario.CreateWithColony();

            // Invalid on a branchless site.
            var plain = FieldSiteFactory.CreateAnomalySite(s.StartingSystem, new Vector3(2e14, 0, 0), "Plain");
            Assert.That(CommitSiteBranchOrder.CreateCommand(plain, 0).IsValidCommand(s.Game), Is.False,
                "a site with no branches offers no choice to commit");

            // A locked branch (understanding not high enough) leaves the site Worked and the order unfinished (retries).
            var (site, db) = BranchedAnomalyWithWorker(s);
            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 20 * 86400); // understanding 100 → Ally (req 150) still locked

            var early = CommitSiteBranchOrder.CreateCommand(site, 1); // "Ally" not unlocked yet
            early.Execute(s.StartingSystem.StarSysDateTime);
            Assert.That(db.Status, Is.EqualTo(SiteStatus.Worked), "a locked branch can't resolve the site");
            Assert.That(db.YieldDelivered, Is.False, "nothing paid");
            Assert.That(early.IsFinished(), Is.False, "the order stays pending to retry once understanding fills");
        }
    }
}
