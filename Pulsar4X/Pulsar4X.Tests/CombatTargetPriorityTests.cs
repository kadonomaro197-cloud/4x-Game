using System.Linq;
using NUnit.Framework;
using Pulsar4X.Blueprints;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase 5 ROE — TARGET PRIORITY (docs/AUTO-RESOLVER-GROUND-TRUTH-2026-07-29.md §16). A doctrine's
    /// <see cref="TargetPriority"/> now decides WHO a force shoots first, wired into the resolver's casualty step
    /// (<c>CombatEngagement.ApplyCasualties</c>) — the fix for the enum's own complaint that fire always spread by
    /// current health, so "a cripple is never finished."
    ///
    /// The whole-or-dead aggregate model can express two of the six modes today:
    ///   • <see cref="TargetPriority.BiggestThreat"/> — concentrate on the highest-firepower enemy first.
    ///   • <see cref="TargetPriority.Heaviest"/>       — concentrate on the toughest enemy first.
    /// The other three (<see cref="TargetPriority.FinishWounded"/> needs per-ship health = the parked degrade-on-
    /// damage model; <see cref="TargetPriority.Closest"/> / <see cref="TargetPriority.Backfield"/> need per-target
    /// position) fall back to Balanced — flagged, not faked. Balanced itself is byte-identical to the legacy order,
    /// which is why every other combat fixture (all un-doctrined / Balanced) is unchanged.
    ///
    /// Engine-only → runs in CI. Mirrors the <see cref="FleetComponentTests"/> harness.
    /// </summary>
    [TestFixture]
    public class CombatTargetPriorityTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[target-priority] " + m);

        private static Entity AddShip(TestScenario s, Entity faction, Entity fleet, double fp, double tough, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name); // build under player faction
            ship.FactionOwnerID = faction.Id;                                            // then assign true owner
            ship.SetDataBlob(new ShipCombatValueDB(fp, tough, 1.0));
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return ship;
        }

        /// <summary>An attacking doctrine that only sets the target priority (firepower/toughness neutral, no cooldown),
        /// so the ONLY thing under test is who dies first.</summary>
        private static CombatDoctrineBlueprint Priority(string targetPriority)
            => new CombatDoctrineBlueprint
            {
                UniqueID = "test-" + targetPriority, DisplayName = "Test " + targetPriority, Family = "Offensive",
                FirepowerMult = 1.0, ToughnessMult = 1.0, CooldownSeconds = 0, TargetPriority = targetPriority
            };

        /// <summary>Fight one attacker (indestructible) against a two-ship defender until the FIRST defender ship
        /// dies, and return the ship that was destroyed first.</summary>
        private static Entity FirstToDie(TestScenario s, string attackerPriority, out Entity alpha, out Entity bravo)
        {
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var attacker = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Attacker");
            AddShip(s, s.Faction, attacker, 100_000, 100_000_000, "Gun"); // huge toughness → it never dies in-window

            var defender = FleetFactory.Create(s.StartingSystem, enemyFaction.Id, "Defender");
            // Alpha = high firepower, THIN.   Bravo = low firepower, TANKY.  Different toughness → different buckets,
            // so the two priorities pick opposite ships: BiggestThreat→Alpha (fp), Heaviest→Bravo (toughness).
            alpha = AddShip(s, enemyFaction, defender, 50_000, 300_000, "Alpha-HighThreat-Thin");
            bravo = AddShip(s, enemyFaction, defender, 1_000, 900_000, "Bravo-LowThreat-Tanky");

            Assert.That(FleetDoctrine.TrySetDoctrine(attacker, Priority(attackerPriority), attacker.StarSysDateTime), Is.True);

            CombatEngagement.StartEngagement(attacker, defender);
            for (int i = 0; i < 400; i++)
            {
                CombatEngagement.StepEngagement(attacker, defender, 5);
                bool aDead = !alpha.IsValid, bDead = !bravo.IsValid;
                if (aDead ^ bDead) { Log($"{attackerPriority}: first to die = {(aDead ? "Alpha" : "Bravo")} after {i + 1} steps"); return aDead ? alpha : bravo; }
                if (aDead && bDead) break; // both died in one step — the pool was too big to observe order
            }
            return null;
        }

        [Test]
        [Description("BiggestThreat concentrates fire on the highest-firepower enemy first — the thin high-threat ship dies before the tanky low-threat one, the reverse of what raw toughness would give.")]
        public void BiggestThreat_KillsHighestFirepowerFirst()
        {
            var s = TestScenario.CreateWithColony();
            var first = FirstToDie(s, "BiggestThreat", out var alpha, out var bravo);
            Assert.That(first, Is.Not.Null, "one defender ship should die before the other");
            Assert.That(first.Id, Is.EqualTo(alpha.Id), "BiggestThreat should finish the high-firepower ship first");
            Assert.That(bravo.IsValid, Is.True, "the low-threat tanky ship should still be alive");
        }

        [Test]
        [Description("Heaviest concentrates fire on the toughest enemy first — the tanky ship dies before the thin one, the opposite target of BiggestThreat on the same two ships.")]
        public void Heaviest_KillsToughestFirst()
        {
            var s = TestScenario.CreateWithColony();
            var first = FirstToDie(s, "Heaviest", out var alpha, out var bravo);
            Assert.That(first, Is.Not.Null, "one defender ship should die before the other");
            Assert.That(first.Id, Is.EqualTo(bravo.Id), "Heaviest should finish the toughest ship first");
            Assert.That(alpha.IsValid, Is.True, "the thin high-threat ship should still be alive");
        }

        [Test]
        [Description("Balanced (the default, and the effective value of every base doctrine's not-yet-expressible priority) leaves the casualty resolve working exactly as before — the fight still resolves to a clean win.")]
        public void Balanced_ResolvesNormally_ByteIdenticalDefault()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var attacker = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Attacker");
            AddShip(s, s.Faction, attacker, 100_000, 100_000_000, "Gun");

            var defender = FleetFactory.Create(s.StartingSystem, enemyFaction.Id, "Defender");
            AddShip(s, enemyFaction, defender, 10_000, 300_000, "Def 1"); // no attacker doctrine → priority Balanced

            CombatEngagement.StartEngagement(attacker, defender);
            for (int i = 0; i < 400 && CombatEngagement.GetFleetShips(defender).Count > 0; i++)
                CombatEngagement.StepEngagement(attacker, defender, 5);

            Assert.That(CombatEngagement.GetFleetShips(defender).Count, Is.EqualTo(0), "the un-doctrined fight still resolves");
        }
    }
}
