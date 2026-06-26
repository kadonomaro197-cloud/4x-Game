using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Sensors;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// MVP combat spine, step 4 — the in-game battle trigger (<see cref="CombatEngagement"/> +
    /// <see cref="BattleTriggerProcessor"/>).
    ///
    /// These drive <see cref="CombatEngagement"/> directly (no clock advance), which exercises the exact
    /// detection + engagement + resolution logic the hotloop processor runs each tick — deterministically. The
    /// processor itself is a 3-line wrapper that calls <c>CombatEngagement.Tick</c>; the engine arms every hotloop
    /// processor at manager init, and <c>GameLoopSmokeTests</c> proves it runs during a clock advance without
    /// throwing, so live auto-triggering is covered without an AdvanceTime test here.
    ///
    /// Why no AdvanceTime: a bare <c>CreateBasicFaction</c> test enemy can't build a hull, so its ships are built
    /// under the player faction and have FactionOwnerID flipped (combat only reads that int). Those flipped ships
    /// don't survive movement processing across a clock advance — a TEST artifact (real NPC factions are set up
    /// fully), not a combat issue. Driving Tick directly avoids it. Each ship is stamped with a known
    /// <see cref="ShipCombatValueDB"/> so outcomes are deterministic.
    /// </summary>
    [TestFixture]
    public class BattleTriggerTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[battle-trigger] " + m);

        private static Entity MakeFleet(TestScenario s, Entity faction, string name)
            => FleetFactory.Create(s.StartingSystem, faction.Id, name);

        private static Entity AddShip(TestScenario s, Entity faction, Entity fleet, double firepower, double toughness, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name); // build under player faction
            ship.FactionOwnerID = faction.Id;                                            // then assign true owner
            ship.SetDataBlob(new ShipCombatValueDB(firepower, toughness, 1.0));
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return ship;
        }

        /// <summary>Like <see cref="AddShip"/>, but built from the SENSOR-capable capital design (it carries a
        /// passive sensor receiver AND a reactor signature, so it can detect and be detected) — the fog-of-war
        /// test needs ships that both fight and show up on a sensor scan.</summary>
        private static Entity AddSensingShip(TestScenario s, Entity faction, Entity fleet, string name)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            var design = designs.TryGetValue("default-ship-design-test-capital", out var cap) ? cap : designs.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.FactionOwnerID = faction.Id;
            ship.SetDataBlob(new ShipCombatValueDB(50_000, 1_000_000, 1.0));
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return ship;
        }

        /// <summary>Fire the sensor scan once on every sensor-bearing entity — what <c>Game.PostNewGameInitialization</c>
        /// does at New Game, reproduced because the colony harness doesn't schedule it. Populates the track tables.</summary>
        private static void RunSensorScan(TestScenario s)
        {
            foreach (var e in s.StartingSystem.GetAllEntitiesWithDataBlob<SensorAbilityDB>())
                s.Game.ProcessorManager.GetInstanceProcessor(nameof(SensorScan)).ProcessEntity(e, s.Game.TimePulse.GameGlobalDateTime);
        }

        /// <summary>
        /// A New Game start spawns the colony's own fleets (colony-earth gives 3 — Freight/Military/Science,
        /// all player-owned; see <see cref="StartFleetTests"/>). A Tick-based trigger test needs a CONTROLLED
        /// matchup, so we clear those first: otherwise the enemy fleet engages whichever hostile player fleet
        /// the iteration reaches first (a colony fleet) and consumes itself before the test's own player fleet
        /// is paired — the engine is behaving correctly, the scenario just has extra fleets. Destroy() flips
        /// IsValid synchronously, and CombatEngagement.Tick skips !IsValid fleets, so they drop out at once;
        /// the orphaned ships have no FleetDB, so fleet pairing never sees them.
        /// </summary>
        private static void ClearExistingFleets(TestScenario s)
        {
            foreach (var fleet in s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>().ToList())
                fleet.Destroy();
        }

        [Test]
        [Description("Driven directly: a 3-ship fleet wipes a lone unarmed enemy, takes no losses, and the engagement state clears on both fleets.")]
        public void Engagement_StrongerFleet_WipesWeaker()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var strongFleet = MakeFleet(s, enemyFaction, "Red Fleet");
            AddShip(s, enemyFaction, strongFleet, 50_000, 1_000_000, "Red 1");
            AddShip(s, enemyFaction, strongFleet, 50_000, 1_000_000, "Red 2");
            AddShip(s, enemyFaction, strongFleet, 50_000, 1_000_000, "Red 3");

            var weakFleet = MakeFleet(s, s.Faction, "Blue Fleet");
            var weakShip = AddShip(s, s.Faction, weakFleet, 0, 1_000_000, "Blue 1"); // unarmed -> loses

            Assert.That(CombatEngagement.GetFleetShips(strongFleet).Count, Is.EqualTo(3), "strong fleet should have 3 ships");
            Assert.That(CombatEngagement.GetFleetShips(weakFleet).Count, Is.EqualTo(1), "weak fleet should have 1 ship");

            CombatEngagement.StartEngagement(weakFleet, strongFleet);
            Assert.That(weakFleet.HasDataBlob<FleetCombatStateDB>(), Is.True, "engagement should have started");

            int steps = 0;
            for (; steps < 1000 && weakFleet.HasDataBlob<FleetCombatStateDB>(); steps++)
                CombatEngagement.StepEngagement(weakFleet, strongFleet, 5);

            Log($"resolved in {steps} steps; weak={CombatEngagement.GetFleetShips(weakFleet).Count} strong={CombatEngagement.GetFleetShips(strongFleet).Count}");

            Assert.That(weakFleet.HasDataBlob<FleetCombatStateDB>(), Is.False, "engagement should have ended");
            Assert.That(strongFleet.HasDataBlob<FleetCombatStateDB>(), Is.False, "engagement should have ended on both sides");
            Assert.That(CombatEngagement.GetFleetShips(weakFleet).Count, Is.EqualTo(0), "the weaker fleet should be wiped");
            Assert.That(CombatEngagement.GetFleetShips(strongFleet).Count, Is.EqualTo(3), "the stronger fleet should take no losses from an unarmed enemy");
            Assert.That(weakShip.IsValid, Is.False, "the destroyed ship should be invalid");
        }

        [Test]
        [Description("CombatEngagement.Tick detects two hostile fleets in range, engages them, and resolves the fight — the unarmed fleet is wiped.")]
        public void Tick_DetectsEngagesAndResolves_HostileFleetsInRange()
        {
            var s = TestScenario.CreateWithColony();
            ClearExistingFleets(s); // drop the colony's own start fleets so this is a clean two-fleet matchup
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var enemyFleet = MakeFleet(s, enemyFaction, "Red Fleet");
            AddShip(s, enemyFaction, enemyFleet, 50_000, 1_000_000, "Red 1");
            AddShip(s, enemyFaction, enemyFleet, 50_000, 1_000_000, "Red 2");
            AddShip(s, enemyFaction, enemyFleet, 50_000, 1_000_000, "Red 3");

            var playerFleet = MakeFleet(s, s.Faction, "Blue Fleet");
            var playerShip = AddShip(s, s.Faction, playerFleet, 0, 1_000_000, "Blue 1");

            Log($"setup: factions enemy={enemyFaction.Id} player={s.Faction.Id}; fleetOwners enemy={enemyFleet.FactionOwnerID} player={playerFleet.FactionOwnerID}; " +
                $"validFleets={s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>().Count(f => f.IsValid)}; " +
                $"ships enemy={CombatEngagement.GetFleetShips(enemyFleet).Count} player={CombatEngagement.GetFleetShips(playerFleet).Count}");

            // First tick: detect the hostile pair in range and start an engagement.
            CombatEngagement.Tick(s.StartingSystem, 5);
            Assert.That(playerFleet.HasDataBlob<FleetCombatStateDB>(), Is.True, "the trigger should have engaged the hostile fleets");
            Assert.That(enemyFleet.HasDataBlob<FleetCombatStateDB>(), Is.True);

            // Keep ticking; the fight resolves and wipes the unarmed player fleet.
            int ticks = 1;
            for (; ticks < 1000 && playerShip.IsValid; ticks++)
                CombatEngagement.Tick(s.StartingSystem, 5);

            Log($"resolved in {ticks} ticks; playerValid={playerShip.IsValid} enemyShips={CombatEngagement.GetFleetShips(enemyFleet).Count}");

            Assert.That(playerShip.IsValid, Is.False, "the trigger should resolve the fight and destroy the unarmed player ship");
            Assert.That(CombatEngagement.GetFleetShips(enemyFleet).Count, Is.EqualTo(3), "the armed enemy fleet should take no losses");
            Assert.That(enemyFleet.HasDataBlob<FleetCombatStateDB>(), Is.False, "the engagement should have ended");
        }

        [Test]
        [Description("Combat interrupt: with InterruptTimeOnNewEngagement on, a NEW battle requests a time halt (CombatInterruptPending) so the clock stops at first contact instead of resolving the whole fight inside one step. Off by default, so headless tests stay deterministic.")]
        public void Tick_NewEngagement_RequestsCombatHalt_WhenInterruptEnabled()
        {
            var s = TestScenario.CreateWithColony();
            ClearExistingFleets(s); // clean two-fleet matchup
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var enemyFleet = MakeFleet(s, enemyFaction, "Red Fleet");
            AddShip(s, enemyFaction, enemyFleet, 50_000, 1_000_000, "Red 1");
            var playerFleet = MakeFleet(s, s.Faction, "Blue Fleet");
            AddShip(s, s.Faction, playerFleet, 50_000, 1_000_000, "Blue 1");

            Assert.That(CombatEngagement.InterruptTimeOnNewEngagement, Is.False, "the interrupt must default OFF so every other combat test advances deterministically");

            s.Game.TimePulse.CombatInterruptPending = false;
            CombatEngagement.InterruptTimeOnNewEngagement = true;
            try
            {
                Assert.That(s.Game.TimePulse.CombatInterruptPending, Is.False, "no interrupt before any combat");

                CombatEngagement.Tick(s.StartingSystem, 5); // detects the hostile pair, engages -> EnsureInCombat -> halt

                Assert.That(playerFleet.HasDataBlob<FleetCombatStateDB>(), Is.True, "the trigger should have engaged the fleets");
                Assert.That(s.Game.TimePulse.CombatInterruptPending, Is.True,
                    "a NEW engagement should request a combat halt so the clock stops at first contact");
            }
            finally
            {
                CombatEngagement.InterruptTimeOnNewEngagement = false; // never leak the static flag to other tests
                s.Game.TimePulse.CombatInterruptPending = false;
            }
        }

        [Test]
        [Description("NewEngagementImminent (the time-loop's fine-step gate): two hostile, un-engaged fleets in range read TRUE, so the clock sub-steps and the auto-pause lands at first contact.")]
        public void NewEngagementImminent_TwoHostileUnengagedInRange_ReturnsTrue()
        {
            var s = TestScenario.CreateWithColony();
            ClearExistingFleets(s); // clean matchup — drop the colony's own start fleets
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var enemyFleet = MakeFleet(s, enemyFaction, "Red Fleet");
            AddShip(s, enemyFaction, enemyFleet, 50_000, 1_000_000, "Red 1");
            var playerFleet = MakeFleet(s, s.Faction, "Blue Fleet");
            AddShip(s, s.Faction, playerFleet, 50_000, 1_000_000, "Blue 1");

            Assert.That(CombatEngagement.NewEngagementImminent(s.StartingSystem), Is.True,
                "two hostile un-engaged fleets in range mean a NEW engagement is about to fire");
        }

        [Test]
        [Description("NewEngagementImminent: once BOTH hostiles are already engaged, the ongoing fight reads FALSE — so the player gets their chosen step size back for the exchange, not a forced 5s.")]
        public void NewEngagementImminent_BothAlreadyEngaged_ReturnsFalse()
        {
            var s = TestScenario.CreateWithColony();
            ClearExistingFleets(s);
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var enemyFleet = MakeFleet(s, enemyFaction, "Red Fleet");
            AddShip(s, enemyFaction, enemyFleet, 50_000, 1_000_000, "Red 1");
            var playerFleet = MakeFleet(s, s.Faction, "Blue Fleet");
            AddShip(s, s.Faction, playerFleet, 50_000, 1_000_000, "Blue 1");

            CombatEngagement.StartEngagement(playerFleet, enemyFleet); // both now hold FleetCombatStateDB
            Assert.That(CombatEngagement.NewEngagementImminent(s.StartingSystem), Is.False,
                "an ONGOING fight (both already engaged) is not a NEW engagement — combat runs at the player's set speed");
        }

        [Test]
        [Description("NewEngagementImminent: one side already fighting, a hostile in range NOT yet in combat -> TRUE. This is the JOIN/round-2 case — a fresh fleet entering the fight re-arms the auto-pause.")]
        public void NewEngagementImminent_OneEngagedOneJoining_ReturnsTrue()
        {
            var s = TestScenario.CreateWithColony();
            ClearExistingFleets(s);
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var enemyFleet = MakeFleet(s, enemyFaction, "Red Fleet");
            AddShip(s, enemyFaction, enemyFleet, 50_000, 1_000_000, "Red 1");
            var playerFleet = MakeFleet(s, s.Faction, "Blue Fleet");
            AddShip(s, s.Faction, playerFleet, 50_000, 1_000_000, "Blue 1");

            CombatEngagement.StartEngagement(playerFleet, enemyFleet); // both engaged...
            CombatEngagement.EndEngagement(enemyFleet);                // ...then the enemy drops its state (player still in)
            Assert.That(CombatEngagement.NewEngagementImminent(s.StartingSystem), Is.True,
                "a hostile in range that is not yet in combat with the engaged fleet is a NEW entry about to fire");
        }

        [Test]
        [Description("NewEngagementImminent: only friendly fleets present -> false, so peacetime fast-forward keeps taking full Ticklength steps (no needless fine-stepping).")]
        public void NewEngagementImminent_SameFactionFleetsOnly_ReturnsFalse()
        {
            var s = TestScenario.CreateWithColony();
            ClearExistingFleets(s);

            var fleet1 = MakeFleet(s, s.Faction, "Home Guard A");
            AddShip(s, s.Faction, fleet1, 50_000, 1_000_000, "A1");
            var fleet2 = MakeFleet(s, s.Faction, "Home Guard B");
            AddShip(s, s.Faction, fleet2, 50_000, 1_000_000, "B1");

            Assert.That(CombatEngagement.NewEngagementImminent(s.StartingSystem), Is.False,
                "same-faction fleets are not hostile, so no engagement is imminent");
        }

        [Test]
        [Description("NewEngagementImminent: an empty system (no fleets) -> false. The gate must be cheap-and-quiet so it never forces fine-stepping where there is nothing to fight.")]
        public void NewEngagementImminent_NoFleets_ReturnsFalse()
        {
            var s = TestScenario.CreateWithColony();
            ClearExistingFleets(s);

            Assert.That(CombatEngagement.NewEngagementImminent(s.StartingSystem), Is.False,
                "no fleets means no engagement, imminent or otherwise");
        }

        [Test]
        [Description("Two fleets of the SAME faction never engage when the trigger runs.")]
        public void Tick_SameFactionFleets_DoNotEngage()
        {
            var s = TestScenario.CreateWithColony();
            ClearExistingFleets(s); // isolate to just the two friendly fleets under test

            var fleet1 = MakeFleet(s, s.Faction, "Home Guard A");
            AddShip(s, s.Faction, fleet1, 50_000, 1_000_000, "A1");
            var fleet2 = MakeFleet(s, s.Faction, "Home Guard B");
            AddShip(s, s.Faction, fleet2, 50_000, 1_000_000, "B1");

            for (int i = 0; i < 5; i++)
                CombatEngagement.Tick(s.StartingSystem, 5);

            Assert.That(fleet1.HasDataBlob<FleetCombatStateDB>(), Is.False, "friendly fleets must not engage");
            Assert.That(fleet2.HasDataBlob<FleetCombatStateDB>(), Is.False, "friendly fleets must not engage");
            Assert.That(CombatEngagement.GetFleetShips(fleet1).Count, Is.EqualTo(1), "no friendly losses");
            Assert.That(CombatEngagement.GetFleetShips(fleet2).Count, Is.EqualTo(1), "no friendly losses");
        }

        [Test]
        [Description("Fog of war (RequireDetectionToEngage): two hostile fleets in range do NOT engage until they DETECT each other. No scan -> empty track tables -> no battle; fire the scan -> mutual contacts -> battle. The detection x weapons seam — slice 2. Off by default so every other combat test stays deterministic.")]
        public void Tick_RequireDetection_NoBattleUntilDetected()
        {
            var s = TestScenario.CreateWithColony();
            ClearExistingFleets(s); // clean two-fleet matchup
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var enemyFleet = MakeFleet(s, enemyFaction, "Red Fleet");
            AddSensingShip(s, enemyFaction, enemyFleet, "Red 1");
            var playerFleet = MakeFleet(s, s.Faction, "Blue Fleet");
            AddSensingShip(s, s.Faction, playerFleet, "Blue 1");

            Assert.That(CombatEngagement.RequireDetectionToEngage, Is.False, "fog of war must default OFF so other combat tests don't need sensors");

            CombatEngagement.RequireDetectionToEngage = true;
            try
            {
                // No scan has run -> the track tables are empty -> hostile + in range but UNSEEN -> no engagement.
                CombatEngagement.Tick(s.StartingSystem, 5);
                Assert.That(playerFleet.HasDataBlob<FleetCombatStateDB>(), Is.False, "undetected hostiles must NOT engage (fog of war)");
                Assert.That(enemyFleet.HasDataBlob<FleetCombatStateDB>(), Is.False);

                // Fire the sensor scan (as the live game does at New Game) -> mutual contacts -> now they engage.
                RunSensorScan(s);
                CombatEngagement.Tick(s.StartingSystem, 5);
                Assert.That(playerFleet.HasDataBlob<FleetCombatStateDB>(), Is.True, "once detected, hostiles engage — detection x weapons");
                Assert.That(enemyFleet.HasDataBlob<FleetCombatStateDB>(), Is.True);
            }
            finally
            {
                CombatEngagement.RequireDetectionToEngage = false; // never leak the static flag to other tests
            }
        }

        [Test]
        [Description("First-strike (detection slice 5): the side that sees first shoots first. Two EQUAL armed " +
                     "fleets, but the enemy is BLINDED (its sensors shot off — the grave-rung path), so the player " +
                     "detects it while it stays blind. With fog of war on, the player wipes it taking ZERO losses — " +
                     "the blind fleet never returns fire. Equal forces firing both ways would be mutual destruction, " +
                     "so 'player intact, enemy wiped' is the proof. Composes detection × grave-rung × weapons.")]
        public void FirstStrike_SeerWipesBlindEnemy_Unscathed()
        {
            var s = TestScenario.CreateWithColony();
            ClearExistingFleets(s); // clean two-fleet matchup
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            // EQUAL fleets: same firepower + toughness. The enemy CAN fire (it has the guns) — it just can't SEE.
            var playerFleet = MakeFleet(s, s.Faction, "Blue Fleet");
            var watcher = AddSensingShip(s, s.Faction, playerFleet, "Blue 1");
            var enemyFleet = MakeFleet(s, enemyFaction, "Red Fleet");
            var bogey = AddSensingShip(s, enemyFaction, enemyFleet, "Red 1");

            // Blind the enemy: shoot its sensor receivers off (the grave-rung path) so it cannot detect anyone.
            var comps = bogey.GetDataBlob<ComponentInstancesDB>();
            comps.TryGetComponentsByAttribute<SensorReceiverAtb>(out var receivers);
            foreach (var r in receivers.ToList())
                comps.RemoveComponentInstance(r);
            ReCalcProcessor.ReCalcAbilities(bogey);

            RunSensorScan(s);

            // Preconditions: the player DETECTS the enemy (so it can open fire), and the enemy is genuinely BLIND
            // (no working receivers — it still emits a signature, so it can be seen, but it can't see).
            Assert.That(s.StartingSystem.GetSensorContacts(s.Faction.Id).SensorContactExists(bogey.Id), Is.True,
                "the player must detect the blinded enemy (it still emits)");
            Assert.That(bogey.GetDataBlob<SensorAbilityDB>().InstanceStates.Count, Is.EqualTo(0),
                "the enemy must be blind — its sensor receivers were shot off");

            CombatEngagement.RequireDetectionToEngage = true;
            try
            {
                CombatEngagement.StartEngagement(playerFleet, enemyFleet);
                int steps = 0;
                for (; steps < 1000 && playerFleet.HasDataBlob<FleetCombatStateDB>(); steps++)
                    CombatEngagement.StepEngagement(playerFleet, enemyFleet, 5);

                Log($"first-strike resolved in {steps} steps; player={CombatEngagement.GetFleetShips(playerFleet).Count} " +
                    $"enemy={CombatEngagement.GetFleetShips(enemyFleet).Count}");

                Assert.That(CombatEngagement.GetFleetShips(enemyFleet).Count, Is.EqualTo(0),
                    "the blind enemy is wiped — it's being shot and can't see to shoot back");
                Assert.That(CombatEngagement.GetFleetShips(playerFleet).Count, Is.EqualTo(1),
                    "the player takes ZERO losses — first-strike: a blind fleet never returns fire (equal forces both firing would be mutual kill)");
                Assert.That(watcher.IsValid, Is.True, "the player's ship survives unscathed");
            }
            finally
            {
                CombatEngagement.RequireDetectionToEngage = false; // never leak the static flag to other tests
            }
        }
    }
}
