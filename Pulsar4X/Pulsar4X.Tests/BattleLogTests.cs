using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The BattleLog — structured battle history that SURVIVES after a fight ends, so the client can show a
    /// "battle report" for a fight the player blinked and missed (the live <see cref="FleetCombatStateDB"/> is
    /// removed on disengage, so it can't be the source). This is the engine half of the combat-visibility feature
    /// ("I couldn't see the battle"); the marker/readout/report UI reads it. Engine-only -> runs in CI.
    /// </summary>
    [TestFixture]
    public class BattleLogTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[battlelog] " + m);

        private static Entity AddShip(TestScenario s, Entity faction, Entity fleet, ShipCombatValueDB cv, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name); // build under player faction
            ship.FactionOwnerID = faction.Id;                                            // then assign true owner
            ship.SetDataBlob(cv);                                                        // stamp the combat value we want
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return ship;
        }

        // A strong railgun attacker, and a defenceless hull that can't dodge — so the fight resolves in a few salvos.
        private static ShipCombatValueDB Slugger(double dps)
            => new ShipCombatValueDB(dps, 100_000, 1.0) { Weapons = { new WeaponProfile(WeaponClass.Railgun, dps, 50_000, 0.05, 5) } };
        private static ShipCombatValueDB SoftHull(double toughness)
            => new ShipCombatValueDB(0, toughness, 1.0) { Evasion = 0.0 };

        [Test]
        [Description("A resolved battle leaves a structured trail in BattleLog — the loser's Engaged, a Salvo loss, and a Disengaged event — and that trail SURVIVES after the fight (FleetCombatStateDB is gone). This is the data the persistent battle report reads.")]
        public void BattleLog_RecordsEngageSalvoAndDisengage_AndSurvivesTheFight()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var attacker = FleetFactory.Create(s.StartingSystem, enemyFaction.Id, "Slug Battery");
            AddShip(s, enemyFaction, attacker, Slugger(40_000), "Slugger");

            var defender = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Defenders");
            var victim = AddShip(s, s.Faction, defender, SoftHull(100_000), "Victim"); // can't dodge -> dies

            BattleLog.Clear();
            CombatEngagement.StartEngagement(attacker, defender);
            int steps = 0;
            while (defender.HasDataBlob<FleetCombatStateDB>() && steps < 2000)
            {
                CombatEngagement.StepEngagement(attacker, defender, 5);
                steps++;
            }

            // Filter to OUR two fleets so the assertions are robust if another fixture also feeds the global log.
            var mine = BattleLog.Recent()
                .Where(e => e.FleetId == attacker.Id || e.FleetId == defender.Id)
                .ToList();
            foreach (var e in mine)
                Log($"{e.FleetName} #{e.FleetId} {e.Type} lost={e.ShipsLost} left={e.ShipsLeft} step={e.Step} {e.Note}");

            Assert.That(victim.IsValid, Is.False, "the soft hull should have been destroyed (so a Salvo loss is recorded)");
            Assert.That(defender.HasDataBlob<FleetCombatStateDB>(), Is.False, "the fight ended — live combat state is gone, proving the report must come from BattleLog");

            Assert.That(mine.Any(e => e.FleetId == defender.Id && e.Type == BattleEventType.Engaged), Is.True,
                "the defender entering combat is recorded");
            Assert.That(mine.Any(e => e.FleetId == defender.Id && e.Type == BattleEventType.Salvo && e.ShipsLost > 0), Is.True,
                "the salvo that kills the defender's ship is recorded with the loss count");
            Assert.That(mine.Any(e => e.FleetId == defender.Id && e.Type == BattleEventType.Disengaged), Is.True,
                "the defender leaving the fight is recorded — and still readable AFTER FleetCombatStateDB is removed");
        }

        [Test]
        [Description("With narration on (as the live client runs), the casualty salvo records a detailed play-by-play " +
                     "note — which weapon CLASS fired, the hit-vs-dodge rate, the damage dealt, and the ship destroyed " +
                     "BY NAME (the developer's 'salvo means nothing — tell me which weapon / hit / damage / which ship').")]
        public void BattleLog_SalvoNote_NamesWeaponHitRateDamageAndDestroyedShip()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var attacker = FleetFactory.Create(s.StartingSystem, enemyFaction.Id, "Slug Battery");
            AddShip(s, enemyFaction, attacker, Slugger(40_000), "Slugger");
            var defender = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Defenders");
            AddShip(s, s.Faction, defender, SoftHull(100_000), "Victim"); // can't dodge -> dies

            bool prev = CombatEngagement.NarrateToLog;
            BattleLog.Clear();
            CombatEngagement.NarrateToLog = true;   // the client runs with this ON; tests default OFF
            try
            {
                CombatEngagement.StartEngagement(attacker, defender);
                int steps = 0;
                while (defender.HasDataBlob<FleetCombatStateDB>() && steps < 2000)
                { CombatEngagement.StepEngagement(attacker, defender, 5); steps++; }
            }
            finally { CombatEngagement.NarrateToLog = prev; }   // never leak the static

            var killNote = BattleLog.Recent()
                .Where(e => e.FleetId == defender.Id && e.Type == BattleEventType.Salvo && e.ShipsLost > 0)
                .Select(e => e.Note).FirstOrDefault();
            Log($"kill note: {killNote}");

            Assert.That(killNote, Is.Not.Null.And.Not.Empty, "the casualty salvo carries a detailed note");
            Assert.That(killNote, Does.Contain("Railgun"), "names the weapon CLASS that fired");
            Assert.That(killNote, Does.Contain("on target"), "reports the hit-vs-dodge rate");
            Assert.That(killNote, Does.Contain("dealt"), "reports the damage dealt");
            Assert.That(killNote, Does.Contain("'Victim'"), "names the ship that was destroyed");
        }

        [Test]
        [Description("BattleLog is capped (ring buffer) so a long campaign can't grow it without bound.")]
        public void BattleLog_IsCappedToMaxEvents()
        {
            BattleLog.Clear();
            for (int i = 0; i < BattleLog.MaxEvents + 50; i++)
                BattleLog.Record(new BattleEvent(default, i, "F" + i, 1, BattleEventType.Engaged, 0, 1, 0, ""));

            Assert.That(BattleLog.Count, Is.EqualTo(BattleLog.MaxEvents), "the log holds at most MaxEvents");
            // The OLDEST were trimmed: the most recent record (highest id) is still present, the first is gone.
            var recent = BattleLog.Recent();
            Assert.That(recent.Last().FleetId, Is.EqualTo(BattleLog.MaxEvents + 49), "newest event retained");
            Assert.That(recent.Any(e => e.FleetId == 0), Is.False, "oldest event trimmed");
        }
    }
}
