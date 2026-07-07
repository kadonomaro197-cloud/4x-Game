using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Extensions;
using Pulsar4X.Colonies;
using Pulsar4X.GroundCombat;
using Pulsar4X.Galaxy;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauges the MARS BEACHHEAD — the piece that turns the fleet-only Martian Directorate into a world you can
    /// actually TAKE. After the combat scenario spawns, Mars must carry (a) an enemy COLONY (the capture target —
    /// clearing its garrison flips ownership) and (b) a defending ground GARRISON, both owned by a rival faction; and
    /// Mars must stay FOGGED (the enemy colony reveals the world on creation; we re-fog it so the player still has to
    /// send a survey ship). This is the engine half of "an AI to fight against, centered on Mars"; the space fight +
    /// the bombard->land->capture chain are covered by CombatScenarioTests / TakeAPlanetIntegrationTests.
    /// </summary>
    [TestFixture]
    public class MarsBeachheadTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[mars-beachhead] " + m);

        [Test]
        [Description("SpawnCombatScenario gives Mars an enemy colony + a defending garrison (both rival-owned) so it's a capture target, and Mars stays fogged (re-fogged after the colony revealed it) so the player must still survey it.")]
        public void SpawnCombatScenario_GivesMars_AnEnemyColonyAndGarrison_StillFogged()
        {
            var s = TestScenario.CreateWithColony();
            // The beachhead is OFF by default (no Earth-Mars war in a normal game); enable it for this gauge, and
            // reset in a finally so the static flag never leaks into another fixture in the same process.
            List<Entity> enemies;
            bool prev = CombatSandbox.SpawnMarsBeachhead;
            CombatSandbox.SpawnMarsBeachhead = true;
            try { enemies = CombatSandbox.SpawnCombatScenario(s.Game, s.StartingSystem, s.Faction); }
            finally { CombatSandbox.SpawnMarsBeachhead = prev; }
            var enemyIds = enemies.Select(e => e.Id).ToHashSet();
            int playerId = s.Faction.Id;

            var mars = CombatSandbox.FindBody(s.StartingSystem, "Mars");
            Assert.That(mars, Is.Not.Null, "Sol has Mars");

            // (a) A DEFENDING GARRISON on Mars, owned by a rival faction (not the player).
            Assert.That(mars.TryGetDataBlob<GroundForcesDB>(out var gf), Is.True, "Mars carries a ground-forces roster");
            var garrison = gf.Units.Where(u => u.FactionOwnerID != playerId).ToList();
            Assert.That(garrison.Count, Is.GreaterThan(0), "a defending enemy garrison stands on Mars");
            Assert.That(garrison.All(u => enemyIds.Contains(u.FactionOwnerID)), Is.True, "the garrison belongs to a rival faction");
            Log($"Mars garrison: {garrison.Count} units, owner {garrison[0].FactionOwnerID}");

            // (b) A CAPTURABLE enemy COLONY on Mars (the capture target).
            var marsColony = s.StartingSystem.GetAllEntitiesWithDataBlob<ColonyInfoDB>()
                .FirstOrDefault(c => c.GetDataBlob<ColonyInfoDB>().PlanetEntity.Id == mars.Id);
            Assert.That(marsColony, Is.Not.Null, "there is a colony on Mars");
            Assert.That(marsColony.FactionOwnerID, Is.Not.EqualTo(playerId), "the Mars colony is enemy-owned (a capture prize)");
            Assert.That(enemyIds, Does.Contain(marsColony.FactionOwnerID), "the Mars colony belongs to a rival faction");

            // (c) Mars stays FOGGED — the enemy colony revealed it on creation, but we re-fogged so the player must scan it.
            Assert.That(mars.TryGetDataBlob<PlanetRegionsDB>(out var regions), Is.True, "Mars has a region layer");
            Assert.That(regions.Regions.All(r => !r.Surveyed), Is.True, "Mars is re-fogged: the player must still survey it to see the surface");
            Log($"Mars colony owner {marsColony.FactionOwnerID}; surveyed {regions.Regions.Count(r => r.Surveyed)}/{regions.Regions.Count} regions (fogged)");
        }
    }
}
