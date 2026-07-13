using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.GroundCombat;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-4b — the MENACE factory (docs/SITE-ENGINE-DESIGN.md §4). Proves an incident's hostile force can
    /// be stood up: a dedicated menace faction + its ground units raised on the site's region. Because the menace holds
    /// the region, it trips the SE-3d guardian gate — you must clear it before the site can be contained. Unwired in
    /// the live game → byte-identical.
    /// </summary>
    [TestFixture]
    public class MenaceFactoryTests
    {
        [Test]
        [Description("SE-4b: RaiseMenaceAt creates a registered menace faction and raises its units on the region.")]
        public void RaiseMenaceAt_CreatesFaction_AndUnitsOnTheRegion()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            const int region = 5;

            var menace = MenaceFactory.RaiseMenaceAt(s.Game, body, region, "Europa Bloom", unitCount: 3);

            Assert.That(menace, Is.Not.EqualTo(Entity.InvalidEntity), "a menace faction was created");
            Assert.That(s.Game.Factions.ContainsKey(menace.Id), Is.True, "it is a registered faction");
            Assert.That(menace.Id, Is.Not.EqualTo(s.Faction.Id), "and distinct from the player");

            var forces = body.GetDataBlob<GroundForcesDB>();
            int menaceUnits = forces.Units.Count(u => u.FactionOwnerID == menace.Id && u.RegionIndex == region && u.Health > 0);
            Assert.That(menaceUnits, Is.EqualTo(3), "3 menace units stand on the region");
        }

        [Test]
        [Description("SE-4b: a menace on a surface site's region blocks work (trips the SE-3d guardian gate) until cleared.")]
        public void Menace_BlocksSurfaceWork_UntilCleared()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            const int region = 6;

            var site = FieldSiteFactory.CreateSurfaceSite(s.StartingSystem, body, region, 0, 0, "Incident", hook: SiteHook.Contested);
            var db = site.GetDataBlob<FieldSiteDB>();

            MenaceFactory.RaiseMenaceAt(s.Game, body, region, "Menace", unitCount: 2);

            Assert.That(SiteWorkProcessor.RegionIsClearFor(s.StartingSystem, db, s.Faction.Id), Is.False,
                "the menace holds the region → the player can't work the site yet");

            // Clear the menace (the ground resolver's job in a live game).
            foreach (var u in body.GetDataBlob<GroundForcesDB>().Units.Where(u => u.RegionIndex == region))
                u.Health = 0;

            Assert.That(SiteWorkProcessor.RegionIsClearFor(s.StartingSystem, db, s.Faction.Id), Is.True,
                "with the menace dead, the region is clear");
        }
    }
}
