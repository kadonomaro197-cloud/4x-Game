using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// MVP combat spine, step 3 — the auto-resolve loop (<see cref="AutoResolve"/>).
    ///
    /// Proves the salvo math: a stronger fleet beats and wipes a weaker one, a fight neither side can win is a
    /// stalemate, and combatants die before utility hulls. Each test builds REAL (valid) ship entities, then
    /// overwrites their <see cref="ShipCombatValueDB"/> with KNOWN values, so the outcome is deterministic and
    /// independent of whatever the starting designs happen to rate. Engine-only -> runs in CI.
    /// </summary>
    [TestFixture]
    public class AutoResolveTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[auto-resolve] " + m);

        /// <summary>Build a real, destroyable ship and stamp it with a known firepower/toughness/role.</summary>
        private static Entity MakeShip(TestScenario s, double firepower, double toughness, double role, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.SetDataBlob(new ShipCombatValueDB(firepower, toughness, role));
            return ship;
        }

        [Test]
        [Description("Three equal warships beat one identical warship and take no losses doing it.")]
        public void StrongerFleet_Wins_AndWipesTheWeaker()
        {
            var s = TestScenario.CreateWithColony();
            var sideA = new List<Entity>
            {
                MakeShip(s, 1000, 1_000_000, 1.0, "A1"),
                MakeShip(s, 1000, 1_000_000, 1.0, "A2"),
                MakeShip(s, 1000, 1_000_000, 1.0, "A3"),
            };
            var sideB = new List<Entity> { MakeShip(s, 1000, 1_000_000, 1.0, "B1") };

            var result = AutoResolve.Resolve(sideA, sideB);
            Log($"3v1: outcome={result.Outcome} rounds={result.RoundsElapsed} survA={result.SurvivorsA} survB={result.SurvivorsB} killA={result.DestroyedA.Count} killB={result.DestroyedB.Count}");

            Assert.That(result.Outcome, Is.EqualTo(BattleOutcome.SideAVictory));
            Assert.That(result.SurvivorsB, Is.EqualTo(0), "the lone enemy should be destroyed");
            Assert.That(result.SurvivorsA, Is.EqualTo(3), "the 3-ship fleet should kill B before losing anyone");
            Assert.That(result.DestroyedB.Count, Is.EqualTo(1));
            Assert.That(result.DestroyedA.Count, Is.EqualTo(0));
        }

        [Test]
        [Description("Two fleets that can't hurt each other (zero firepower) end in a stalemate with no losses.")]
        public void NoFirepower_OnEitherSide_IsAStalemate()
        {
            var s = TestScenario.CreateWithColony();
            var sideA = new List<Entity> { MakeShip(s, 0, 1_000_000, 0.25, "A1") };
            var sideB = new List<Entity> { MakeShip(s, 0, 1_000_000, 0.25, "B1") };

            var result = AutoResolve.Resolve(sideA, sideB);
            Log($"stalemate: outcome={result.Outcome} rounds={result.RoundsElapsed}");

            Assert.That(result.Outcome, Is.EqualTo(BattleOutcome.Stalemate));
            Assert.That(result.SurvivorsA, Is.EqualTo(1));
            Assert.That(result.SurvivorsB, Is.EqualTo(1));
            Assert.That(result.DestroyedA, Is.Empty);
            Assert.That(result.DestroyedB, Is.Empty);
        }

        [Test]
        [Description("When a fleet takes losses, its combatants die before its utility/transport hulls.")]
        public void Combatants_DieBefore_UtilityHulls()
        {
            var s = TestScenario.CreateWithColony();

            // Side A can't shoot back (firepower 0), so it loses both ships in order.
            var combatant = MakeShip(s, 0, 1_000_000, 1.0, "A-Combatant");
            var utility = MakeShip(s, 0, 1_000_000, ShipCombatValueDB.UtilityRoleWeight, "A-Utility");
            var sideA = new List<Entity> { utility, combatant }; // deliberately utility-first in the input list
            var sideB = new List<Entity> { MakeShip(s, 250_000, 1_000_000, 1.0, "B-Gun") };

            var result = AutoResolve.Resolve(sideA, sideB);
            Log($"ordering: outcome={result.Outcome} killA=[{string.Join(",", result.DestroyedA.Select(e => e.Id))}] combatantId={combatant.Id} utilityId={utility.Id}");

            Assert.That(result.Outcome, Is.EqualTo(BattleOutcome.SideBVictory));
            Assert.That(result.DestroyedA.Count, Is.EqualTo(2), "both A ships should be lost");
            Assert.That(result.DestroyedA[0], Is.SameAs(combatant),
                "the combatant should be destroyed before the utility hull regardless of input order");
            Assert.That(result.DestroyedA[1], Is.SameAs(utility));
        }
    }
}
