using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-6a — the "make it playable" wire (the sibling of <see cref="IncidentScenarioTests"/>). Proves the
    /// flag-gated demo-site spawner puts real, single-path Research sites into a game so the Site Engine can be
    /// experienced hands-on, while the default-OFF flag keeps a New Game byte-identical. Drives the engine helper
    /// directly (deterministic).
    /// </summary>
    [TestFixture]
    public class SiteScenarioTests
    {
        [Test]
        [Description("SE-6a byte-identity: AutoSpawnSites defaults OFF, so a New Game seeds no sites.")]
        public void AutoSpawnSites_DefaultsOff_NoSites()
        {
            var s = TestScenario.CreateWithColony();

            Assert.That(SiteScenario.AutoSpawnSites, Is.False, "the New-Game demo-site spawn is off by default");
            Assert.That(SiteScenario.MaybeSpawnForNewGame(s.Game), Is.EqualTo(0), "flag off → nothing spawned");
            Assert.That(s.StartingSystem.GetAllEntitiesWithDataBlob<FieldSiteDB>().Count, Is.EqualTo(0),
                "no site exists in a byte-identical New Game");
        }

        [Test]
        [Description("SE-6a: with the flag ON, the New-Game hook seeds demo sites at the home world.")]
        public void AutoSpawnSites_On_SeedsDemoSites()
        {
            var s = TestScenario.CreateWithColony();
            try
            {
                SiteScenario.AutoSpawnSites = true;
                int spawned = SiteScenario.MaybeSpawnForNewGame(s.Game);

                Assert.That(spawned, Is.GreaterThanOrEqualTo(1), "the player's home world got at least one demo site");
                Assert.That(s.StartingSystem.GetAllEntitiesWithDataBlob<FieldSiteDB>().Count, Is.GreaterThanOrEqualTo(1),
                    "a demo site exists on the map");
            }
            finally
            {
                SiteScenario.AutoSpawnSites = false; // never leak the flag to other tests
            }
        }

        [Test]
        [Description("SE-6a: SpawnDemoSitesAt creates workable single-path Research sites, including a surface ruin the player can work.")]
        public void SpawnDemoSitesAt_CreatesWorkableSites()
        {
            var s = TestScenario.CreateWithColony();

            int n = SiteScenario.SpawnDemoSitesAt(s.Game, s.StartingSystem, s.StartingBody);
            Assert.That(n, Is.GreaterThanOrEqualTo(1), "at least the surface ruin was created");

            var sites = s.StartingSystem.GetAllEntitiesWithDataBlob<FieldSiteDB>()
                .Select(e => e.GetDataBlob<FieldSiteDB>())
                .ToList();

            // The surface ruin (the readily-workable demo) — a single-path Science/Research one-shot on the ground.
            var ruin = sites.FirstOrDefault(d => d.IsSurfaceSite);
            Assert.That(ruin, Is.Not.Null, "a surface ruin was spawned on the home body");
            Assert.That(ruin.Role, Is.EqualTo(SiteRole.Science));
            Assert.That(ruin.Yield, Is.EqualTo(SiteYield.Research));
            Assert.That(ruin.HasBranches, Is.False, "a demo site is single-path (auto-resolves when worked — no UI needed)");
            Assert.That(ruin.Shape, Is.EqualTo(SiteShape.OneShot));
        }
    }
}
