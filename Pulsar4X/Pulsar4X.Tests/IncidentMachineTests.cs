using NUnit.Framework;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-4a — the INCIDENT state + machine reads (docs/SITE-ENGINE-DESIGN.md §4, the "stop-the-bleed"
    /// shape). A Shape.Incident site bleeds steady PRESSURE and can grow a menace while it is LIVE (exists + not yet
    /// contained); containing it (resolve → Depleted) stops the bleed. Proves the pure reads + the new incident dials on
    /// FieldSiteDB survive Clone. Nothing is attached to an entity, so byte-identical.
    /// </summary>
    [TestFixture]
    public class IncidentMachineTests
    {
        private static FieldSiteDB Incident(double pressure = 5.0) => new FieldSiteDB
        {
            Shape = SiteShape.Incident,
            Role = SiteRole.Tactical,
            PressurePerDay = pressure,
            MenaceFactionId = 7,
            SpawnIntervalDays = 30,
            UnderstandingToResolve = 10.0,
            SurfaceBodyEntityId = 42,
            SurfaceRegionIndex = 3,
        };

        [Test]
        [Description("SE-4a: an Incident site is LIVE while Discovered/Worked and stops being live once contained (Depleted).")]
        public void Incident_IsLive_UntilContained()
        {
            var site = Incident();
            Assert.That(SiteMachine.IsIncidentLive(site), Is.True, "a fresh incident is live");

            SiteMachine.Accrue(site, work: 0, understanding: 10); // → Worked, understanding at threshold
            Assert.That(site.Status, Is.EqualTo(SiteStatus.Worked));
            Assert.That(SiteMachine.IsIncidentLive(site), Is.True, "still live while being worked");

            Assert.That(SiteMachine.Resolve(site), Is.True, "unlocked → contain it");
            Assert.That(site.Status, Is.EqualTo(SiteStatus.Depleted), "an incident resolves to contained (Depleted)");
            Assert.That(SiteMachine.IsIncidentLive(site), Is.False, "a contained incident is no longer live");
        }

        [Test]
        [Description("SE-4a: pressure flows at PressurePerDay while the incident is live, and drops to 0 once contained.")]
        public void Pressure_FlowsOnlyWhileLive()
        {
            var site = Incident(pressure: 5.0);
            Assert.That(SiteMachine.CurrentPressure(site), Is.EqualTo(5.0), "a live incident bleeds its PressurePerDay");

            SiteMachine.Accrue(site, work: 0, understanding: 10);
            SiteMachine.Resolve(site);
            Assert.That(SiteMachine.CurrentPressure(site), Is.EqualTo(0.0), "a contained incident bleeds nothing");
        }

        [Test]
        [Description("SE-4a: a non-Incident site (OneShot/Persistent) is never live and bleeds no pressure — byte-identical.")]
        public void NonIncident_HasNoPressure()
        {
            var oneShot = new FieldSiteDB { Shape = SiteShape.OneShot, PressurePerDay = 99 };
            Assert.That(SiteMachine.IsIncidentLive(oneShot), Is.False);
            Assert.That(SiteMachine.CurrentPressure(oneShot), Is.EqualTo(0.0), "only a Shape.Incident site bleeds");

            var persistent = new FieldSiteDB { Shape = SiteShape.Persistent, PressurePerDay = 99 };
            Assert.That(SiteMachine.IsIncidentLive(persistent), Is.False);
        }

        [Test]
        [Description("SE-4a: Clone deep-copies the incident dials (MenaceFactionId / PressurePerDay / SpawnIntervalDays).")]
        public void Clone_CopiesIncidentFields()
        {
            var site = Incident(pressure: 3.0);
            var copy = (FieldSiteDB)site.Clone();

            Assert.That(copy.MenaceFactionId, Is.EqualTo(7));
            Assert.That(copy.PressurePerDay, Is.EqualTo(3.0));
            Assert.That(copy.SpawnIntervalDays, Is.EqualTo(30));

            copy.PressurePerDay = 0;
            Assert.That(site.PressurePerDay, Is.EqualTo(3.0), "mutating the copy must not touch the original");
        }
    }
}
