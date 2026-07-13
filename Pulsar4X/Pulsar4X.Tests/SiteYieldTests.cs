using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Ships;
using Pulsar4X.Sites;
using Pulsar4X.Technology;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-1c — resolve + the research yield, end to end (docs/SITE-ENGINE-DESIGN.md §3 Yield dial).
    /// Proves the PAYOFF half of the spine: when a worked anomaly's understanding fills, the site RESOLVES and its
    /// banked Progress is paid ONCE into the working faction's research (an existing consumer system — the Prime
    /// Directive "connect"). Gauged target-agnostically: the faction's total research progress climbs by exactly the
    /// delivered points (no assumption about which tech the "nearest breakthrough" rule picks).
    /// </summary>
    [TestFixture]
    public class SiteYieldTests
    {
        // Sum of accumulated research progress across all still-researchable techs — the delivery gauge.
        private static long SumResearchProgress(FactionInfoDB info)
        {
            long sum = 0;
            foreach (var tech in info.Data.Techs.Values)
                if (tech.Level < tech.MaxLevel)
                    sum += tech.ResearchProgress;
            return sum;
        }

        private static Entity SpawnWorker(TestScenario s, out Vector3 workerPos)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "SE-1c Worker");
            workerPos = ship.GetDataBlob<PositionDB>().AbsolutePosition;
            return ship;
        }

        [Test]
        [Description("SE-1c: DeliverResearch pays points into the working faction's research (total progress rises by exactly that).")]
        public void DeliverResearch_PaysPointsIntoFactionResearch()
        {
            var s = TestScenario.CreateWithColony();
            var info = s.Faction.GetDataBlob<FactionInfoDB>();

            Assume.That(info.Data.Techs.Values.Any(t => t.Level < t.MaxLevel), Is.True,
                "the start faction should have at least one researchable tech");

            long before = SumResearchProgress(info);
            bool landed = SiteYields.DeliverResearch(s.Game, s.Faction.Id, 100);

            Assert.That(landed, Is.True, "a faction with a researchable tech should accept the points");
            Assert.That(SumResearchProgress(info) - before, Is.EqualTo(100),
                "exactly the delivered points should land (100 < any tech cost → no level-up)");
        }

        [Test]
        [Description("SE-1c: an unknown / neutral faction id is a safe no-op (no throw, nothing delivered).")]
        public void DeliverResearch_UnknownFaction_IsNoOp()
        {
            var s = TestScenario.CreateWithColony();
            Assert.That(SiteYields.DeliverResearch(s.Game, -12345, 100), Is.False);
            Assert.That(SiteYields.DeliverResearch(s.Game, s.Faction.Id, 0), Is.False,
                "zero points is a no-op");
        }

        [Test]
        [Description("SE-1c: a worked anomaly whose understanding fills RESOLVES and delivers its research yield end to end.")]
        public void Processor_ResolvesAndDeliversResearch_EndToEnd()
        {
            var s = TestScenario.CreateWithColony();
            var info = s.Faction.GetDataBlob<FactionInfoDB>();
            SpawnWorker(s, out var workerPos);

            // A low understanding threshold so one day of work unlocks the branch (UnderstandingPerDay = 5).
            var site = FieldSiteFactory.CreateAnomalySite(
                s.StartingSystem, workerPos, "Resolving Anomaly", understandingToResolve: 5.0);
            var siteDB = site.GetDataBlob<FieldSiteDB>();

            long before = SumResearchProgress(info);
            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400); // one day: work=10, understanding=5

            Assert.That(siteDB.Status, Is.EqualTo(SiteStatus.Depleted), "a filled one-shot anomaly resolves (spent)");
            Assert.That(siteDB.YieldDelivered, Is.True, "the yield was paid out");
            Assert.That(SumResearchProgress(info) - before, Is.EqualTo((long)siteDB.Progress),
                "the banked Progress (10) landed as research points for the working faction");
            Assert.That(siteDB.Progress, Is.EqualTo(SiteWorkProcessor.WorkPerDay).Within(1e-6));
        }

        [Test]
        [Description("SE-1c: the yield is delivered exactly ONCE — extra ticks on a resolved site add no more research.")]
        public void ResolvedSite_DeliversYieldOnlyOnce()
        {
            var s = TestScenario.CreateWithColony();
            var info = s.Faction.GetDataBlob<FactionInfoDB>();
            SpawnWorker(s, out var workerPos);

            var site = FieldSiteFactory.CreateAnomalySite(
                s.StartingSystem, workerPos, "Once Anomaly", understandingToResolve: 5.0);
            var siteDB = site.GetDataBlob<FieldSiteDB>();

            long before = SumResearchProgress(info);
            var proc = new SiteWorkProcessor();
            proc.ProcessManager(s.StartingSystem, 86400); // resolves + delivers here
            long afterFirst = SumResearchProgress(info);

            proc.ProcessManager(s.StartingSystem, 86400); // resolved → ignored
            proc.ProcessManager(s.StartingSystem, 86400); // resolved → ignored

            Assert.That(afterFirst - before, Is.EqualTo((long)siteDB.Progress), "the first tick delivered the yield");
            Assert.That(SumResearchProgress(info), Is.EqualTo(afterFirst), "no further research is delivered after resolution");
            Assert.That(siteDB.Status, Is.EqualTo(SiteStatus.Depleted));
        }
    }
}
