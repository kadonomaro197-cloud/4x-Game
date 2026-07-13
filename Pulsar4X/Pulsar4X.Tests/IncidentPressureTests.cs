using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.GroundCombat;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-4c — the steady PRESSURE of a live incident (docs/SITE-ENGINE-DESIGN.md §4, "stop-the-bleed").
    /// While a Shape.Incident surface site is live, it bleeds every NON-menace unit in its region each tick (the menace
    /// is the source, so it's spared). Proves the bleed applies to your holding force, spares the menace, and never
    /// touches a non-incident site (byte-identical).
    /// </summary>
    [TestFixture]
    public class IncidentPressureTests
    {
        private static GroundUnitDesign Infantry() => new GroundUnitDesign
        {
            UniqueID = "se4c-infantry", Name = "Rifles", UnitType = GroundUnitType.Infantry,
            Attack = 100, Defense = 10, HitPoints = 500,
            IndustryPointCosts = 100, IndustryTypeID = "installation",
            ResourceCosts = new Dictionary<string, long>(),
        };

        [Test]
        [Description("SE-4c: a live incident drains a unit in its region by PressurePerDay each day.")]
        public void LiveIncident_BleedsUnitsInRegion()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            const int region = 5;

            var site = FieldSiteFactory.CreateSurfaceSite(s.StartingSystem, body, region, 0, 0, "Incident",
                shape: SiteShape.Incident);
            site.GetDataBlob<FieldSiteDB>().PressurePerDay = 100;

            var unit = GroundForces.RaiseUnit(body, Infantry(), s.Faction.Id, region);
            Assert.That(unit.Health, Is.EqualTo(500));

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400); // one day

            Assert.That(unit.Health, Is.EqualTo(400).Within(1e-6), "the incident bled 100 health this day");
        }

        [Test]
        [Description("SE-4c: the menace is spared its own incident's pressure; your holding force bleeds.")]
        public void Menace_IsSpared_ByItsOwnPressure()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            const int region = 6;

            var site = FieldSiteFactory.CreateSurfaceSite(s.StartingSystem, body, region, 0, 0, "Incident",
                shape: SiteShape.Incident);
            var db = site.GetDataBlob<FieldSiteDB>();
            db.PressurePerDay = 100;

            var menace = MenaceFactory.RaiseMenaceAt(s.Game, body, region, "Menace", unitCount: 1);
            db.MenaceFactionId = menace.Id;
            var friendly = GroundForces.RaiseUnit(body, Infantry(), s.Faction.Id, region);

            // Find the menace unit's starting health.
            double menaceHealthBefore = 0;
            foreach (var u in body.GetDataBlob<GroundForcesDB>().Units)
                if (u.FactionOwnerID == menace.Id) menaceHealthBefore = u.Health;

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400);

            Assert.That(friendly.Health, Is.EqualTo(400).Within(1e-6), "your unit bleeds the incident's pressure");
            foreach (var u in body.GetDataBlob<GroundForcesDB>().Units)
                if (u.FactionOwnerID == menace.Id)
                    Assert.That(u.Health, Is.EqualTo(menaceHealthBefore).Within(1e-6), "the menace does not suffer its own pressure");
        }

        [Test]
        [Description("SE-4c byte-identity: a non-Incident surface site never bleeds its region.")]
        public void NonIncidentSite_NoPressure()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            const int region = 7;

            // A OneShot surface site — even with a PressurePerDay set, it's not an incident, so nothing bleeds.
            var site = FieldSiteFactory.CreateSurfaceSite(s.StartingSystem, body, region, 0, 0, "Ruin",
                shape: SiteShape.OneShot);
            site.GetDataBlob<FieldSiteDB>().PressurePerDay = 100;

            var unit = GroundForces.RaiseUnit(body, Infantry(), s.Faction.Id, region);

            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400);

            Assert.That(unit.Health, Is.EqualTo(500), "only a live Shape.Incident site bleeds — this one is untouched");
        }
    }
}
