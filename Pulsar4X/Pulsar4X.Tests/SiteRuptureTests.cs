using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-5d — the RUPTURED edge (docs/SITE-ENGINE-DESIGN.md §4): a resolved PERSISTENT site (a standing
    /// faucet) carrying a rupture chance can blow into a fresh CRISIS — "the reward carried the risk." Proves the whole
    /// escalation: a persistent site with a rupture dial ruptures on a tick (Status → Ruptured, the faucet stops) and
    /// births a live Shape.Incident crisis (a menace + pressure) at its own body/region, while a persistent site with
    /// the default 0 chance never ruptures (byte-identical) and a ruptured site never ruptures again (no cascade).
    /// Drives the processor directly (deterministic — the colony harness doesn't reliably auto-fire hotloops).
    /// </summary>
    [TestFixture]
    public class SiteRuptureTests
    {
        // A persistent surface faucet on the start body's region 1 (a real body with a region layer).
        private static (Entity site, FieldSiteDB db) PersistentFaucet(TestScenario s, double ruptureChancePerDay)
        {
            var site = FieldSiteFactory.CreateSurfaceSite(s.StartingSystem, s.StartingBody, 1, 0, 0,
                "Persistent Faucet", shape: SiteShape.Persistent);
            var db = site.GetDataBlob<FieldSiteDB>();
            db.Status = SiteStatus.Persistent;                 // a resolved standing stream
            db.RuptureChancePerDay = ruptureChancePerDay;
            return (site, db);
        }

        private static int SiteCount(TestScenario s)
            => s.StartingSystem.GetAllEntitiesWithDataBlob<FieldSiteDB>().Count;

        [Test]
        [Description("SE-5d byte-identity: a persistent site with the default 0 rupture chance never ruptures and spawns no crisis.")]
        public void ZeroChance_NeverRuptures()
        {
            var s = TestScenario.CreateWithColony();
            var (_, db) = PersistentFaucet(s, ruptureChancePerDay: 0.0);
            int before = SiteCount(s);

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 30 * 86400); // 30 days in one tick

            Assert.That(db.Status, Is.EqualTo(SiteStatus.Persistent), "a safe faucet keeps running");
            Assert.That(SiteCount(s), Is.EqualTo(before), "no crisis site was spawned");
        }

        [Test]
        [Description("SE-5d: a persistent site with a rupture chance ruptures into a fresh live Incident crisis (menace + pressure) at its region.")]
        public void Ruptures_IntoALiveCrisis()
        {
            var s = TestScenario.CreateWithColony();
            var (_, db) = PersistentFaucet(s, ruptureChancePerDay: 1.0); // certain this tick
            int before = SiteCount(s);

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400); // one day

            Assert.That(db.Status, Is.EqualTo(SiteStatus.Ruptured), "the faucet ruptured (it stops being a safe stream)");
            Assert.That(SiteCount(s), Is.EqualTo(before + 1), "a new crisis site was born");

            var crisis = s.StartingSystem.GetAllEntitiesWithDataBlob<FieldSiteDB>()
                .Select(e => e.GetDataBlob<FieldSiteDB>())
                .First(d => d.Shape == SiteShape.Incident);
            Assert.That(SiteMachine.IsIncidentLive(crisis), Is.True, "the crisis is a LIVE incident");
            Assert.That(crisis.MenaceFactionId, Is.GreaterThanOrEqualTo(0), "a menace force holds the crisis");
        }

        [Test]
        [Description("SE-5d no cascade: once ruptured, the site takes no more work and never ruptures again.")]
        public void RupturedSite_DoesNotRuptureAgain()
        {
            var s = TestScenario.CreateWithColony();
            var (_, db) = PersistentFaucet(s, ruptureChancePerDay: 1.0);

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400); // ruptures
            Assert.That(db.Status, Is.EqualTo(SiteStatus.Ruptured));
            int afterRupture = SiteCount(s);

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400); // another tick

            Assert.That(db.Status, Is.EqualTo(SiteStatus.Ruptured), "stays Ruptured");
            Assert.That(SiteCount(s), Is.EqualTo(afterRupture), "no further crisis is spawned from the ruptured site");
        }
    }
}
