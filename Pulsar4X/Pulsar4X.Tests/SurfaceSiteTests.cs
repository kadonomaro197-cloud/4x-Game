using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-3a — the surface-site RECORD (docs/SITE-ENGINE-DESIGN.md §6). A surface site lives on a planet
    /// body's region/hex (the way a GroundUnit locates itself), NOT at a point in space, so it carries no PositionDB —
    /// its worker is a ground unit standing on it (SE-3b), not a parked ship. Proves the location fields + the
    /// space-vs-surface distinction, and that a surface site is INERT under the current (space-only) work path →
    /// byte-identical until SE-3b adds the ground-worker branch.
    /// </summary>
    [TestFixture]
    public class SurfaceSiteTests
    {
        [Test]
        [Description("SE-3a: CreateSurfaceSite records the body+region+hex location, carries no PositionDB, and is a neutral Discovered site.")]
        public void CreateSurfaceSite_RecordsLocation_NoPosition()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;

            var site = FieldSiteFactory.CreateSurfaceSite(s.StartingSystem, body, regionIndex: 2, globalQ: 5, globalR: 7,
                name: "Test Ruin");

            Assert.That(site.Manager, Is.SameAs(s.StartingSystem));
            Assert.That(site.FactionOwnerID, Is.EqualTo(Game.NeutralFactionId), "an unworked site belongs to nobody");
            Assert.That(site.HasDataBlob<PositionDB>(), Is.False, "a surface site is located by region/hex, not a space position");

            var db = site.GetDataBlob<FieldSiteDB>();
            Assert.That(db.IsSurfaceSite, Is.True);
            Assert.That(db.SurfaceBodyEntityId, Is.EqualTo(body.Id));
            Assert.That(db.SurfaceRegionIndex, Is.EqualTo(2));
            Assert.That(db.SurfaceGlobalQ, Is.EqualTo(5));
            Assert.That(db.SurfaceGlobalR, Is.EqualTo(7));
            Assert.That(db.Status, Is.EqualTo(SiteStatus.Discovered));
        }

        [Test]
        [Description("SE-3a: a space anomaly is NOT a surface site (its surface fields stay at the -1 defaults).")]
        public void SpaceAnomaly_IsNotASurfaceSite()
        {
            var s = TestScenario.CreateWithColony();
            var anomaly = FieldSiteFactory.CreateAnomalySite(s.StartingSystem, new Vector3(1e14, 0, 0));
            var db = anomaly.GetDataBlob<FieldSiteDB>();

            Assert.That(db.IsSurfaceSite, Is.False);
            Assert.That(db.SurfaceBodyEntityId, Is.EqualTo(-1));
            Assert.That(anomaly.HasDataBlob<PositionDB>(), Is.True, "a space anomaly DOES carry a position");
        }

        [Test]
        [Description("SE-3a byte-identity: a surface site (no PositionDB) is inert under the current space-only work processor.")]
        public void SurfaceSite_IsInert_UnderCurrentProcessor()
        {
            var s = TestScenario.CreateWithColony();
            var site = FieldSiteFactory.CreateSurfaceSite(s.StartingSystem, s.StartingBody, regionIndex: 0, globalQ: 0, globalR: 0);
            var db = site.GetDataBlob<FieldSiteDB>();

            // The start fleet's ships are in this system, but the surface site has no PositionDB, so the space work
            // path (SiteWorkProcessor) skips it entirely — no progress accrues (SE-3b adds the ground-worker branch).
            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400);

            Assert.That(db.Status, Is.EqualTo(SiteStatus.Discovered), "a surface site is not worked by the space path");
            Assert.That(db.Progress, Is.EqualTo(0.0));
            Assert.That(db.Understanding, Is.EqualTo(0.0));
        }

        [Test]
        [Description("SE-3a: Clone deep-copies the surface location fields.")]
        public void Clone_DeepCopies_SurfaceFields()
        {
            var db = new FieldSiteDB
            {
                SurfaceBodyEntityId = 42, SurfaceRegionIndex = 3, SurfaceGlobalQ = 9, SurfaceGlobalR = 11
            };
            var copy = (FieldSiteDB)db.Clone();

            Assert.That(copy.IsSurfaceSite, Is.True);
            Assert.That(copy.SurfaceBodyEntityId, Is.EqualTo(42));
            Assert.That(copy.SurfaceRegionIndex, Is.EqualTo(3));
            Assert.That(copy.SurfaceGlobalQ, Is.EqualTo(9));
            Assert.That(copy.SurfaceGlobalR, Is.EqualTo(11));

            copy.SurfaceRegionIndex = 99;
            Assert.That(db.SurfaceRegionIndex, Is.EqualTo(3), "mutating the copy must not touch the original");
        }
    }
}
