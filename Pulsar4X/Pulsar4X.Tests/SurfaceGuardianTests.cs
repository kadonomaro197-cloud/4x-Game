using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.GroundCombat;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-3d — the GUARDIAN gate (docs/SITE-ENGINE-DESIGN.md §6, the Guardian hook). A surface site whose
    /// region still holds a foreign unit (a neutral/menace guardian, or a rival) can't be worked until it's cleared —
    /// you beat the defender first (the region combat + capture the ground layer already runs), then work the site.
    /// A region with only your own units is clear. Proves the gate blocks + unblocks, and that a clear region is
    /// byte-identical to SE-3b.
    /// </summary>
    [TestFixture]
    public class SurfaceGuardianTests
    {
        private static GroundUnitDesign Infantry() => new GroundUnitDesign
        {
            UniqueID = "se3d-infantry", Name = "Rifles", UnitType = GroundUnitType.Infantry,
            Attack = 100, Defense = 10, HitPoints = 500,
            IndustryPointCosts = 100, IndustryTypeID = "installation",
            ResourceCosts = new Dictionary<string, long>(),
        };

        [Test]
        [Description("SE-3d: a neutral guardian standing in the site's region blocks work even with a friendly worker present.")]
        public void Guardian_BlocksSurfaceWork()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            const int region = 5;

            var site = FieldSiteFactory.CreateSurfaceSite(s.StartingSystem, body, region, 0, 0, "Guardian Ruin", hook: SiteHook.Guardian);
            var db = site.GetDataBlob<FieldSiteDB>();

            GroundForces.RaiseUnit(body, Infantry(), s.Faction.Id, region);            // our worker
            GroundForces.RaiseUnit(body, Infantry(), Game.NeutralFactionId, region);   // the guardian

            Assert.That(SiteWorkProcessor.RegionIsClearFor(s.StartingSystem, db, s.Faction.Id), Is.False,
                "a guardian holds the region → not clear");

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400);

            Assert.That(db.Progress, Is.EqualTo(0.0), "work is blocked while the guardian stands");
            Assert.That(db.Status, Is.EqualTo(SiteStatus.Discovered));
        }

        [Test]
        [Description("SE-3d: once the guardian is cleared (dead), the region reads clear and the site is worked.")]
        public void ClearingGuardian_UnblocksSurfaceWork()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            const int region = 6;

            var site = FieldSiteFactory.CreateSurfaceSite(s.StartingSystem, body, region, 0, 0, "Guardian Ruin", hook: SiteHook.Guardian);
            var db = site.GetDataBlob<FieldSiteDB>();

            GroundForces.RaiseUnit(body, Infantry(), s.Faction.Id, region);
            var guardian = GroundForces.RaiseUnit(body, Infantry(), Game.NeutralFactionId, region);

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400);
            Assert.That(db.Progress, Is.EqualTo(0.0), "blocked while the guardian lives");

            guardian.Health = 0; // the guardian is defeated (the ground resolver's job in a live game)

            Assert.That(SiteWorkProcessor.RegionIsClearFor(s.StartingSystem, db, s.Faction.Id), Is.True,
                "a dead guardian no longer holds the region");

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400);
            Assert.That(db.Progress, Is.EqualTo(SiteWorkProcessor.WorkPerDay).Within(1e-6),
                "with the guardian cleared, the worker banks the flat rate");
            Assert.That(db.Status, Is.EqualTo(SiteStatus.Worked));
        }

        [Test]
        [Description("SE-3d: a region with only friendly units is clear — a Benign surface site works normally (byte-identical to SE-3b).")]
        public void NoGuardian_WorksNormally()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            const int region = 7;

            var site = FieldSiteFactory.CreateSurfaceSite(s.StartingSystem, body, region, 0, 0, "Open Ruin");
            var db = site.GetDataBlob<FieldSiteDB>();

            GroundForces.RaiseUnit(body, Infantry(), s.Faction.Id, region);

            Assert.That(SiteWorkProcessor.RegionIsClearFor(s.StartingSystem, db, s.Faction.Id), Is.True);

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400);
            Assert.That(db.Progress, Is.EqualTo(SiteWorkProcessor.WorkPerDay).Within(1e-6));
        }
    }
}
