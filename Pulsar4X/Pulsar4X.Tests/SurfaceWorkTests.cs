using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.GroundCombat;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Ships;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-3b — the ground-worker presence path (docs/SITE-ENGINE-DESIGN.md §6). A SURFACE site is worked by
    /// a friendly ground unit standing in its region (the surface twin of a ship parked at a space anomaly). Proves the
    /// unit-worker presence detection + accrual, that a neutral (guardian) unit doesn't count, and — the byte-identity
    /// tripwire — that the space-anomaly path still works after the space/surface branch split.
    /// </summary>
    [TestFixture]
    public class SurfaceWorkTests
    {
        private static GroundUnitDesign Infantry() => new GroundUnitDesign
        {
            UniqueID = "se3b-infantry",
            Name = "Test Rifles",
            UnitType = GroundUnitType.Infantry,
            Attack = 100,
            Defense = 10,
            HitPoints = 500,
            IndustryPointCosts = 100,
            IndustryTypeID = "installation",
            ResourceCosts = new Dictionary<string, long>(),
        };

        [Test]
        [Description("SE-3b: a friendly ground unit standing in the surface site's region is detected and works it (banks the flat rate for its faction).")]
        public void GroundUnitInRegion_WorksTheSurfaceSite()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            const int region = 5;

            var site = FieldSiteFactory.CreateSurfaceSite(s.StartingSystem, body, regionIndex: region, globalQ: 0, globalR: 0);
            var db = site.GetDataBlob<FieldSiteDB>();

            Assert.That(SiteWorkProcessor.TryFindGroundWorker(s.StartingSystem, db, out _), Is.False,
                "no unit stands on the site's region yet");

            GroundForces.RaiseUnit(body, Infantry(), s.Faction.Id, regionIndex: region);

            Assert.That(SiteWorkProcessor.TryFindGroundWorker(s.StartingSystem, db, out int fid), Is.True,
                "the landed unit is detected as the on-site worker");
            Assert.That(fid, Is.EqualTo(s.Faction.Id));

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400); // one day

            Assert.That(db.Status, Is.EqualTo(SiteStatus.Worked), "the unit begins the study");
            Assert.That(db.Progress, Is.EqualTo(SiteWorkProcessor.WorkPerDay).Within(1e-6), "a day banks the flat rate");
            Assert.That(db.WorkedByFactionId, Is.EqualTo(s.Faction.Id), "the working faction is recorded (yield routes here)");
        }

        [Test]
        [Description("SE-3b: with no ground unit in the site's region, the surface site does not advance.")]
        public void NoGroundUnit_SurfaceSite_DoesNotAdvance()
        {
            var s = TestScenario.CreateWithColony();
            var site = FieldSiteFactory.CreateSurfaceSite(s.StartingSystem, s.StartingBody, regionIndex: 6, globalQ: 0, globalR: 0);
            var db = site.GetDataBlob<FieldSiteDB>();

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400);

            Assert.That(db.Status, Is.EqualTo(SiteStatus.Discovered));
            Assert.That(db.Progress, Is.EqualTo(0.0), "no on-site unit → no progress");
        }

        [Test]
        [Description("SE-3b: a NEUTRAL unit (a guardian) in the region is not a worker — the site does not advance for it.")]
        public void NeutralUnit_DoesNotWorkTheSite()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            const int region = 7;

            var site = FieldSiteFactory.CreateSurfaceSite(s.StartingSystem, body, regionIndex: region, globalQ: 0, globalR: 0);
            var db = site.GetDataBlob<FieldSiteDB>();

            GroundForces.RaiseUnit(body, Infantry(), Game.NeutralFactionId, regionIndex: region); // a neutral guardian

            Assert.That(SiteWorkProcessor.TryFindGroundWorker(s.StartingSystem, db, out _), Is.False,
                "a neutral unit does not work the site");

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400);
            Assert.That(db.Progress, Is.EqualTo(0.0));
        }

        [Test]
        [Description("SE-3b byte-identity: the space-anomaly path still works after the space/surface branch split.")]
        public void SpaceAnomaly_StillWorkedByShip()
        {
            var s = TestScenario.CreateWithColony();
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "Space Worker");
            var pos = ship.GetDataBlob<PositionDB>().AbsolutePosition;

            var site = FieldSiteFactory.CreateAnomalySite(s.StartingSystem, pos, "Anomaly");
            var db = site.GetDataBlob<FieldSiteDB>();

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400);

            Assert.That(db.Status, Is.EqualTo(SiteStatus.Worked));
            Assert.That(db.Progress, Is.EqualTo(SiteWorkProcessor.WorkPerDay).Within(1e-6),
                "a parked ship still banks the flat rate on a space anomaly (unbroken by the surface branch)");
        }
    }
}
