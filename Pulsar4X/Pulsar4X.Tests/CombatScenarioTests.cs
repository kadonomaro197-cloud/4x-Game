using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Extensions;
using Pulsar4X.Fleets;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauges the premade combat scenario (`CombatSandbox.SpawnCombatScenario`) — two well-rounded PLAYER task
    /// forces at Earth and a beefed-up, capital-led HOSTILE squadron — each its OWN rival faction — at Luna / Venus
    /// / Mercury / Mars, for generating rich live combat data. CI-side proof the spawn stands up real, combat-rated,
    /// MULTI-FACTION fleets; the live behaviour (the fights, the closing log) is the developer's play-test.
    /// </summary>
    [TestFixture]
    public class CombatScenarioTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[combat-scenario] " + m);

        [Test]
        [Description("SpawnCombatScenario stands up 2 player task forces at Earth + 4 hostile squadrons (Luna/Venus/" +
                     "Mercury/Mars), each its OWN faction and beefed-up (7 ships: capital + 2 beam + railgun + flak + " +
                     "2 fighters) and combat-rated.")]
        public void SpawnCombatScenario_StandsUpPlayerAndEnemyFleets()
        {
            var s = TestScenario.CreateWithColony();

            foreach (var bn in new[] { "Earth", "Luna", "Venus", "Mercury", "Mars" })
                Log($"body '{bn}' found in system: {CombatSandbox.FindBody(s.StartingSystem, bn) != null}");

            var enemies = CombatSandbox.SpawnCombatScenario(s.Game, s.StartingSystem, s.Faction);
            Assert.That(enemies, Is.Not.Null.And.Count.EqualTo(4), "the scenario returns FOUR distinct hostile factions (one per body)");
            Log($"hostile factions: {string.Join(", ", enemies.Select(e => e.GetDefaultName()))}");
            Assert.That(enemies.Select(e => e.Id).Distinct().Count(), Is.EqualTo(4), "each squadron is a DIFFERENT faction — multi-faction combat");
            var enemyIds = enemies.Select(e => e.Id).ToHashSet();

            // Four hostile squadrons — one per rival faction (filter by name so each faction's empty root fleet
            // isn't counted).
            var enemySquadrons = s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>()
                .Where(f => enemyIds.Contains(f.FactionOwnerID) && f.GetDefaultName().Contains("Squadron")).ToList();
            Log($"hostile squadrons spawned: {enemySquadrons.Count} ({string.Join(", ", enemySquadrons.Select(f => f.GetDefaultName()))})");
            Assert.That(enemySquadrons.Count, Is.EqualTo(4), "a hostile squadron at Luna, Venus, Mercury, and Mars");
            // Each squadron belongs to a DISTINCT faction (no two share an owner).
            Assert.That(enemySquadrons.Select(f => f.FactionOwnerID).Distinct().Count(), Is.EqualTo(4),
                "the four squadrons are owned by four different factions");

            foreach (var fleet in enemySquadrons)
            {
                var ships = fleet.GetDataBlob<FleetDB>().GetChildren().Where(c => c.IsValid && !c.HasDataBlob<FleetDB>()).ToList();
                Assert.That(ships.Count, Is.EqualTo(7), "beefed-up = capital + 2 beam + railgun + flak + 2 fighters");
                foreach (var ship in ships)
                    Assert.That(ship.HasDataBlob<ShipCombatValueDB>(), Is.True, "each ship is combat-rated at build");
            }

            // The squadrons carry a MIX of engagement postures (attack-first / return-fire / hold-fire) — the
            // material the first-shot/standoff mechanic needs to be worth testing.
            var postures = enemySquadrons.Select(f => FleetDoctrine.PostureOf(f)).ToList();
            var postureStr = string.Join(", ", enemySquadrons.Select(f => f.GetDefaultName() + "=" + FleetDoctrine.PostureOf(f)));
            Log("enemy postures: " + postureStr);
            Assert.That(postures.Distinct().Count(), Is.GreaterThanOrEqualTo(2), "the enemy fleets carry a MIX of postures, not all the same");
            Assert.That(postures, Does.Contain(EngagementPosture.WeaponsHold), "at least one enemy holds fire — the standoff case");
            Assert.That(postures, Does.Contain(EngagementPosture.ReturnFire), "at least one enemy only returns fire");

            // Two player task forces at Earth.
            var playerTaskForces = s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>()
                .Where(f => f.FactionOwnerID == s.Faction.Id && f.GetDefaultName().Contains("Task Force")).ToList();
            Assert.That(playerTaskForces.Count, Is.EqualTo(2), "two well-rounded player task forces");
            foreach (var fleet in playerTaskForces)
                Assert.That(fleet.GetDataBlob<FleetDB>().GetChildren().Count(c => c.IsValid && !c.HasDataBlob<FleetDB>()),
                    Is.EqualTo(5), "each player task force is well-rounded (5 ships)");

            // The task forces must be reachable the way the Fleet WINDOW enumerates them — as children of the
            // faction's root FleetDB. FleetFactory.Create alone leaves a fleet owned+placed but ORPHANED from the
            // tree, so it never appears in the Fleet window (this exact bug shipped: the scenario spawned ships but
            // showed no fleets). GetAllEntitiesWithDataBlob<FleetDB> (above) finds orphans too, which is why it hid
            // the bug — so assert the tree linkage directly.
            var playerRootChildIds = s.Faction.GetDataBlob<FleetDB>().GetChildren().Select(c => c.Id).ToHashSet();
            foreach (var tf in playerTaskForces)
                Assert.That(playerRootChildIds.Contains(tf.Id), Is.True,
                    $"task force '{tf.GetDefaultName()}' must be a child of the faction root fleet — else the Fleet window can't list it");
        }
    }
}
