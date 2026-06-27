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
    /// Phase 3 of the closing-fight model (docs/FLEET-COMBAT-CLOSING-DESIGN.md): the FIRST-SHOT trigger. A battle no
    /// longer erupts on mere proximity — it erupts only if someone will RELEASE a shot (the first ROE knob,
    /// weapons-free vs weapons-hold). Two hostile fleets that are both holding fire sit in a tense STANDOFF. Behind
    /// <c>RequireWeaponsReleaseToEngage</c> (default off → proximity engages as before, since the default posture is
    /// WeaponsFree).
    /// </summary>
    [TestFixture]
    public class WeaponsReleaseTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[weapons-release] " + m);

        private static Entity MakeFleet(TestScenario s, Entity faction, string name)
            => FleetFactory.Create(s.StartingSystem, faction.Id, name);

        private static void AddShip(TestScenario s, Entity faction, Entity fleet, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.FactionOwnerID = faction.Id;
            ship.SetDataBlob(new ShipCombatValueDB(50_000, 1_000_000, 1.0));
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
        }

        /// <summary>Drop the colony's own start fleets so the matchup is a clean two-fleet pair (same idiom as
        /// BattleTriggerTests — Destroy() flips IsValid synchronously and Tick skips invalid fleets).</summary>
        private static void ClearExistingFleets(TestScenario s)
        {
            foreach (var fleet in s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>().ToList())
                fleet.Destroy();
        }

        [Test]
        [Description("Two hostile fleets in range BOTH holding fire don't fight — a tense standoff, no engagement. " +
                     "The moment one goes weapons-free it opens the battle and both sides enter. Default posture is " +
                     "WeaponsFree, so with the flag off proximity still engages (the pre-P3 behaviour).")]
        public void FirstShot_StandoffUntilSomeoneGoesWeaponsFree()
        {
            var s = TestScenario.CreateWithColony();
            ClearExistingFleets(s);
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var redFleet = MakeFleet(s, reds, "Red");
            AddShip(s, reds, redFleet, "Red 1");
            var blueFleet = MakeFleet(s, s.Faction, "Blue");
            AddShip(s, s.Faction, blueFleet, "Blue 1");

            CombatEngagement.RequireWeaponsReleaseToEngage = true;
            try
            {
                // Both hold fire → tense standoff, no battle, even though they're hostile and in range.
                FleetDoctrine.SetEngagementPosture(redFleet, EngagementPosture.WeaponsHold);
                FleetDoctrine.SetEngagementPosture(blueFleet, EngagementPosture.WeaponsHold);
                CombatEngagement.Tick(s.StartingSystem, 5);
                Log($"both weapons-hold: red engaged={redFleet.HasDataBlob<FleetCombatStateDB>()} blue engaged={blueFleet.HasDataBlob<FleetCombatStateDB>()}");
                Assert.That(redFleet.HasDataBlob<FleetCombatStateDB>(), Is.False, "two weapons-hold fleets in range hold a tense standoff — no battle");
                Assert.That(blueFleet.HasDataBlob<FleetCombatStateDB>(), Is.False, "neither side fires first → no engagement forms");

                // Blue goes weapons-free → it releases the first shot and the battle forms (both sides enter).
                FleetDoctrine.SetEngagementPosture(blueFleet, EngagementPosture.WeaponsFree);
                CombatEngagement.Tick(s.StartingSystem, 5);
                Log($"blue weapons-free: red engaged={redFleet.HasDataBlob<FleetCombatStateDB>()} blue engaged={blueFleet.HasDataBlob<FleetCombatStateDB>()}");
                Assert.That(blueFleet.HasDataBlob<FleetCombatStateDB>(), Is.True, "a weapons-free fleet opens the battle");
                Assert.That(redFleet.HasDataBlob<FleetCombatStateDB>(), Is.True, "both sides enter once the first shot is released");
            }
            finally
            {
                CombatEngagement.RequireWeaponsReleaseToEngage = false;
            }
        }

        [Test]
        [Description("Flag OFF: proximity still engages (default posture WeaponsFree) — the pre-P3 behaviour is unchanged.")]
        public void FirstShot_Off_ProximityStillEngages()
        {
            var s = TestScenario.CreateWithColony();
            ClearExistingFleets(s);
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var redFleet = MakeFleet(s, reds, "Red");
            AddShip(s, reds, redFleet, "Red 1");
            var blueFleet = MakeFleet(s, s.Faction, "Blue");
            AddShip(s, s.Faction, blueFleet, "Blue 1");

            // RequireWeaponsReleaseToEngage stays FALSE — hostile + in range = fight, as before.
            CombatEngagement.Tick(s.StartingSystem, 5);
            Assert.That(blueFleet.HasDataBlob<FleetCombatStateDB>(), Is.True, "with the flag off, proximity engages as before");
            Assert.That(redFleet.HasDataBlob<FleetCombatStateDB>(), Is.True);
        }
    }
}
