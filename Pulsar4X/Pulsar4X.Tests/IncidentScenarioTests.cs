using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.GroundCombat;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-4e — the discovery/authoring wire (docs/SITE-ENGINE-DESIGN.md §4). The first slice that can put a
    /// LIVE incident into a game: SpawnIncidentAt composes the SE-4 parts (site + menace + dials + armed spread), and
    /// the New-Game hook is behind the default-OFF AutoSpawnIncident flag. Proves the composed incident is live and
    /// bleeds, the flag defaults off (New Game byte-identical), and flipping it on seeds a home-world incident.
    /// </summary>
    [TestFixture]
    public class IncidentScenarioTests
    {
        private static GroundUnitDesign Infantry() => new GroundUnitDesign
        {
            UniqueID = "se4e-infantry", Name = "Rifles", UnitType = GroundUnitType.Infantry,
            Attack = 100, Defense = 10, HitPoints = 500,
            IndustryPointCosts = 100, IndustryTypeID = "installation",
            ResourceCosts = new Dictionary<string, long>(),
        };

        [Test]
        [Description("SE-4e: SpawnIncidentAt authors a live incident — a Shape.Incident site + a menace holding the region + it bleeds your unit there.")]
        public void SpawnIncidentAt_CreatesALiveBleedingIncident()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            const int region = 1;

            var site = IncidentScenario.SpawnIncidentAt(s.Game, s.StartingSystem, body, region,
                pressurePerDay: 20, spawnIntervalDays: 60, menaceUnits: 2);
            var db = site.GetDataBlob<FieldSiteDB>();

            Assert.That(db.Shape, Is.EqualTo(SiteShape.Incident));
            Assert.That(SiteMachine.IsIncidentLive(db), Is.True, "the incident is live");
            Assert.That(db.MenaceFactionId, Is.GreaterThanOrEqualTo(0), "a menace faction holds it");
            Assert.That(SiteWorkProcessor.RegionIsClearFor(s.StartingSystem, db, s.Faction.Id), Is.False,
                "the menace blocks work until cleared");

            // Your unit standing on the incident bleeds its pressure.
            var unit = GroundForces.RaiseUnit(body, Infantry(), s.Faction.Id, region);
            new SiteWorkProcessor().ProcessManager(s.StartingSystem, 86400); // one day
            Assert.That(unit.Health, Is.EqualTo(480).Within(1e-6), "the incident bled 20 health this day");
        }

        [Test]
        [Description("SE-4e byte-identity: AutoSpawnIncident defaults OFF, so a New Game seeds no incident.")]
        public void AutoSpawnIncident_DefaultsOff_NoIncidentInNewGame()
        {
            var s = TestScenario.CreateWithColony();

            Assert.That(IncidentScenario.AutoSpawnIncident, Is.False, "the New-Game auto-spawn is off by default");
            Assert.That(IncidentScenario.MaybeSpawnForNewGame(s.Game), Is.EqualTo(0), "flag off → nothing spawned");
            Assert.That(s.StartingSystem.GetAllEntitiesWithDataBlob<FieldSiteDB>().Count, Is.EqualTo(0),
                "no incident site exists in a byte-identical New Game");
        }

        [Test]
        [Description("SE-4e: with the flag ON, the New-Game hook seeds a live incident on the home world.")]
        public void AutoSpawnIncident_On_SeedsHomeWorldIncident()
        {
            var s = TestScenario.CreateWithColony();
            try
            {
                IncidentScenario.AutoSpawnIncident = true;
                int spawned = IncidentScenario.MaybeSpawnForNewGame(s.Game);

                Assert.That(spawned, Is.GreaterThanOrEqualTo(1), "the player's home world got an incident");
                var incidents = s.StartingSystem.GetAllEntitiesWithDataBlob<FieldSiteDB>()
                    .Select(e => e.GetDataBlob<FieldSiteDB>())
                    .Where(d => d.Shape == SiteShape.Incident && SiteMachine.IsIncidentLive(d))
                    .ToList();
                Assert.That(incidents.Count, Is.GreaterThanOrEqualTo(1), "a live incident is on the map");
            }
            finally
            {
                IncidentScenario.AutoSpawnIncident = false; // never leak the flag to other tests
            }
        }
    }
}
