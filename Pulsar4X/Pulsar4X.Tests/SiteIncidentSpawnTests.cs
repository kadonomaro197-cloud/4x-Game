using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.GroundCombat;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-4d — the incident SPAWN/SPREAD engine (docs/SITE-ENGINE-DESIGN.md §4). While a Shape.Incident site
    /// is live, each interval its menace GROWS (a fresh unit at the region) and CREEPS (one unit into an adjacent
    /// region). Proves a fire grows the menace and spreads it, and that a CONTAINED incident stops growing (the grave
    /// rung). Drives the processor directly (deterministic); inert until a live incident is scheduled → byte-identical.
    /// </summary>
    [TestFixture]
    public class SiteIncidentSpawnTests
    {
        private const int Region = 1;

        private static int MenaceCount(Entity body, int menaceId)
            => body.GetDataBlob<GroundForcesDB>().Units.Count(u => u.FactionOwnerID == menaceId && u.Health > 0);

        // A live incident at Region with a 2-unit menace already standing on it.
        private static (Entity site, FieldSiteDB db, int menaceId) LiveIncident(TestScenario s)
        {
            var body = s.StartingBody;
            var site = FieldSiteFactory.CreateSurfaceSite(s.StartingSystem, body, Region, 0, 0, "Incident",
                shape: SiteShape.Incident);
            var db = site.GetDataBlob<FieldSiteDB>();
            db.SpawnIntervalDays = 30;
            var menace = MenaceFactory.RaiseMenaceAt(s.Game, body, Region, "Menace", unitCount: 2);
            db.MenaceFactionId = menace.Id;
            return (site, db, menace.Id);
        }

        [Test]
        [Description("SE-4d: firing a live incident raises a fresh menace unit at its region (it grows if you ignore it).")]
        public void Fire_GrowsTheMenace()
        {
            var s = TestScenario.CreateWithColony();
            var (site, _, menaceId) = LiveIncident(s);

            int before = MenaceCount(s.StartingBody, menaceId);
            new SiteIncidentProcessor().ProcessEntity(site, s.StartingSystem.StarSysDateTime);
            int after = MenaceCount(s.StartingBody, menaceId);

            Assert.That(after, Is.EqualTo(before + 1), "the menace reinforced by one unit");
        }

        [Test]
        [Description("SE-4d grave rung: a CONTAINED incident (Depleted) no longer grows when fired.")]
        public void ContainedIncident_DoesNotGrow()
        {
            var s = TestScenario.CreateWithColony();
            var (site, db, menaceId) = LiveIncident(s);
            db.Status = SiteStatus.Depleted; // contained

            int before = MenaceCount(s.StartingBody, menaceId);
            new SiteIncidentProcessor().ProcessEntity(site, s.StartingSystem.StarSysDateTime);
            int after = MenaceCount(s.StartingBody, menaceId);

            Assert.That(after, Is.EqualTo(before), "a contained incident spawns nothing");
        }

        [Test]
        [Description("SE-4d: firing a live incident pushes one menace unit toward an adjacent region (the creep).")]
        public void Fire_SpreadsToNeighbor()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var (site, _, menaceId) = LiveIncident(s);

            Assume.That(body.HasDataBlob<PlanetRegionsDB>(), Is.True, "needs a region layer to spread");
            var regions = body.GetDataBlob<PlanetRegionsDB>().Regions;
            Assume.That(Region < regions.Count && regions[Region].Neighbors.Count > 0, Is.True,
                "the incident region needs a valid neighbour");

            new SiteIncidentProcessor().ProcessEntity(site, s.StartingSystem.StarSysDateTime);

            bool spreading = body.GetDataBlob<GroundForcesDB>().Units
                .Any(u => u.FactionOwnerID == menaceId && u.MovingToRegion >= 0);
            Assert.That(spreading, Is.True, "a menace unit is now marching to an adjacent region");
        }
    }
}
