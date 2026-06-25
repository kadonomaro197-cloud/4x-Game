using System.Collections.Generic;
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
    /// Combat — MULTI-PARTY engagements. A battle is no longer locked to two fleets: any number of fleets can
    /// fight on either side, and a fleet can JOIN a fight already underway just by coming into range. Sides are
    /// factions (v1). The developer's framing: "all combat can be multi party at anytime… I can send in another
    /// fleet to assist." These prove the three things that makes real:
    ///   • ASSIST    — two fleets ganging up beat a lone equal enemy (combined fire wins).
    ///   • JOIN      — a reinforcement arriving mid-battle (through the real <see cref="CombatEngagement.Tick"/>
    ///                 trigger) is pulled in and tips the fight.
    ///   • FIRE-SPLIT— one fleet facing two enemies DIVIDES its fire across them (conserves firepower — it can't
    ///                 kill both in the time it could kill one), and the fire still reaches BOTH.
    /// Each ship is stamped with a known <see cref="ShipCombatValueDB"/> (no weapon profiles -> always-hit beam),
    /// so the count math is exact. Engine-only -> runs in CI.
    /// </summary>
    [TestFixture]
    public class MultiPartyEngagementTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[combat-multiparty] " + m);

        private static Entity MakeFleet(TestScenario s, Entity faction, string name)
            => FleetFactory.Create(s.StartingSystem, faction.Id, name);

        /// <summary>Build a real, destroyable ship under the player faction, flip it to its true owner, stamp a
        /// known firepower/toughness, and assign it to the fleet (the BattleTrigger/Retreat fixtures' pattern).</summary>
        private static Entity AddWarship(TestScenario s, Entity faction, Entity fleet, double firepower, double toughness, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.FactionOwnerID = faction.Id;
            ship.SetDataBlob(new ShipCombatValueDB(firepower, toughness, 1.0));
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return ship;
        }

        /// <summary>Drop the colony's own start fleets so a Tick-driven test sees only the controlled matchup
        /// (same reason as <c>BattleTriggerTests.ClearExistingFleets</c>).</summary>
        private static void ClearExistingFleets(TestScenario s)
        {
            foreach (var fleet in s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>().ToList())
                fleet.Destroy();
        }

        [Test]
        [Description("ASSIST: two equal fleets ganging up on a lone equal enemy win on combined fire — the outnumbered fleet is destroyed, both attackers survive, and the engagement ends for everyone.")]
        public void TwoFleetsAssisting_BeatALoneEqualEnemy()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            // One enemy fleet vs two player fleets — every ship identical, so only the numbers differ.
            var enemy = MakeFleet(s, enemyFaction, "Lone Raider");
            var enemyShip = AddWarship(s, enemyFaction, enemy, 1000, 10_000, "Raider");

            var ally1 = MakeFleet(s, s.Faction, "Home Fleet");
            var ally1Ship = AddWarship(s, s.Faction, ally1, 1000, 10_000, "Defender");
            var ally2 = MakeFleet(s, s.Faction, "Relief Fleet");
            var ally2Ship = AddWarship(s, s.Faction, ally2, 1000, 10_000, "Reliever");

            // Put all three in one engagement (the enemy is hostile to both player fleets; the two player fleets
            // are allied). Then run the multi-party resolve directly.
            CombatEngagement.EnsureInCombat(enemy, ally1.Id);
            CombatEngagement.EnsureInCombat(ally1, enemy.Id);
            CombatEngagement.EnsureInCombat(ally2, enemy.Id);

            var group = new List<Entity> { enemy, ally1, ally2 };
            int steps = 0;
            while (enemy.HasDataBlob<FleetCombatStateDB>() && steps < 2000)
            {
                CombatEngagement.StepEngagementGroup(group, 5);
                steps++;
            }

            Log($"assist: resolved in {steps} steps; enemyAlive={enemyShip.IsValid} ally1Alive={ally1Ship.IsValid} ally2Alive={ally2Ship.IsValid}");

            Assert.That(enemyShip.IsValid, Is.False, "the outnumbered enemy eats the combined fire of both fleets and dies");
            Assert.That(ally1Ship.IsValid, Is.True, "an assisting fleet survives — it only takes a fraction of the divided enemy fire");
            Assert.That(ally2Ship.IsValid, Is.True, "the second assisting fleet survives too");
            Assert.That(ally1.HasDataBlob<FleetCombatStateDB>(), Is.False, "the engagement ends once no enemy remains");
            Assert.That(ally2.HasDataBlob<FleetCombatStateDB>(), Is.False, "both allied fleets are released");
        }

        [Test]
        [Description("JOIN: a 1-v-1 grind is evenly matched, then a reinforcement arrives in range. The real Tick trigger pulls it into the fight (it gains combat state) and the now 2-v-1 destroys the lone enemy.")]
        public void AReinforcementJoinsMidBattle_AndTipsTheFight()
        {
            var s = TestScenario.CreateWithColony();
            ClearExistingFleets(s);
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            // Evenly matched 1-v-1: high toughness so it grinds (neither dies quickly).
            var enemy = MakeFleet(s, enemyFaction, "Red Fleet");
            var enemyShip = AddWarship(s, enemyFaction, enemy, 1000, 100_000, "Red 1");
            var defender = MakeFleet(s, s.Faction, "Blue Fleet");
            var defenderShip = AddWarship(s, s.Faction, defender, 1000, 100_000, "Blue 1");

            // Let the even fight run a while through the real trigger; both engage and grind, neither dies.
            for (int t = 0; t < 5; t++)
                CombatEngagement.Tick(s.StartingSystem, 5);
            Assert.That(enemy.HasDataBlob<FleetCombatStateDB>(), Is.True, "the 1-v-1 engaged");
            Assert.That(defender.HasDataBlob<FleetCombatStateDB>(), Is.True);
            Assert.That(enemyShip.IsValid && defenderShip.IsValid, Is.True, "the even match is still grinding — nobody dead yet");

            // Reinforcement arrives in range. It is NOT in combat yet.
            var relief = MakeFleet(s, s.Faction, "Relief Fleet");
            var reliefShip = AddWarship(s, s.Faction, relief, 1000, 100_000, "Blue 2");
            Assert.That(relief.HasDataBlob<FleetCombatStateDB>(), Is.False, "the reinforcement hasn't joined yet");

            // One tick: the trigger sees it in range of the hostile enemy and pulls it into the battle.
            CombatEngagement.Tick(s.StartingSystem, 5);
            Assert.That(relief.HasDataBlob<FleetCombatStateDB>(), Is.True, "the reinforcement JOINED the engagement in range");

            // Now 2-v-1: keep ticking; the lone enemy eats both fleets' fire and dies, both blue fleets survive.
            int ticks = 0;
            while (enemyShip.IsValid && ticks < 4000)
            {
                CombatEngagement.Tick(s.StartingSystem, 5);
                ticks++;
            }

            Log($"join: enemy died after a further {ticks} ticks; defenderAlive={defenderShip.IsValid} reliefAlive={reliefShip.IsValid}");

            Assert.That(enemyShip.IsValid, Is.False, "the reinforcement tipped it — the lone enemy is destroyed");
            Assert.That(defenderShip.IsValid, Is.True, "the original defender survived once help arrived");
            Assert.That(reliefShip.IsValid, Is.True, "the reinforcement survived");
        }

        [Test]
        [Description("FIRE-SPLIT: a lone fleet facing two enemies divides its fire between them — its damage is conserved (it can't kill both in one step the way it could kill one), but the fire reaches BOTH (both fall on the next step).")]
        public void OneFleetFacingTwoEnemies_DividesItsFire_AndHitsBoth()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            // The lone gun: 100k dps, effectively unkillable for the test. Each target is tough enough that HALF
            // the gun's fire (one step) can't kill it, but its FULL fire (or two steps of half) can. So:
            //   • fire divided  -> 50k/s each -> 250k/step  -> neither dies on step 1, both die on step 2.
            //   • fire NOT split -> 100k/s each -> 500k/step -> both would die on step 1 (the bug we guard against).
            var gun = MakeFleet(s, enemyFaction, "Spinal Gun");
            var gunShip = AddWarship(s, enemyFaction, gun, 100_000, 1_000_000_000, "Gun");

            var target1 = MakeFleet(s, s.Faction, "Target One");
            var target1Ship = AddWarship(s, s.Faction, target1, 0, 300_000, "T1");
            var target2 = MakeFleet(s, s.Faction, "Target Two");
            var target2Ship = AddWarship(s, s.Faction, target2, 0, 300_000, "T2");

            CombatEngagement.EnsureInCombat(gun, target1.Id);
            CombatEngagement.EnsureInCombat(target1, gun.Id);
            CombatEngagement.EnsureInCombat(target2, gun.Id);
            var group = new List<Entity> { gun, target1, target2 };

            // Step 1: the gun's fire is split 50/50, so neither target has taken a lethal dose yet.
            CombatEngagement.StepEngagementGroup(group, 5);
            Log($"split step1: t1Alive={target1Ship.IsValid} t2Alive={target2Ship.IsValid} (both should still be alive — fire was divided)");
            Assert.That(target1Ship.IsValid, Is.True, "fire is DIVIDED — one step of half the gun can't kill a target (no double-counting)");
            Assert.That(target2Ship.IsValid, Is.True, "the second target also survives step 1");

            // Step 2: the second half-dose lands on each — both fall, proving the fire reached BOTH enemies.
            CombatEngagement.StepEngagementGroup(group, 5);
            Log($"split step2: t1Alive={target1Ship.IsValid} t2Alive={target2Ship.IsValid} gunAlive={gunShip.IsValid}");
            Assert.That(target1Ship.IsValid, Is.False, "two steps of divided fire kill the first target");
            Assert.That(target2Ship.IsValid, Is.False, "and the second — the gun's fire reached BOTH enemies, not just one");
            Assert.That(gunShip.IsValid, Is.True, "the unarmed targets never threatened the gun");
        }
    }
}
