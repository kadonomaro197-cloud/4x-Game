using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Movement;   // PositionDB (active class lives in Movement, not Datablobs)
using Pulsar4X.Orbital;    // Vector3
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The WEAPON-RANGE battle trigger (the developer's rule, 2026-07-02): a fight auto-starts only when the fleets are
    /// within actual weapon range — SEEING each other across the system is NOT enough. This is the v2 gate that the flat
    /// <c>EngagementRange_m</c> (1 Gm) proximity was always the placeholder for. Reproduces the live report: two fleets
    /// 752 km apart with 500 km guns entered a "battle" that dealt 0 damage and fizzled — that must no longer trigger.
    /// All behind <see cref="CombatEngagement.RequireWeaponRangeToEngage"/> (default off, client on), so every existing
    /// combat fixture (co-located ships / no weapon profiles) is byte-identical; these opt in and reset it in finally.
    /// </summary>
    [TestFixture]
    public class WeaponRangeTriggerTests
    {
        private static Entity MakeFleet(TestScenario s, Entity faction, string name)
            => FleetFactory.Create(s.StartingSystem, faction.Id, name);

        /// <summary>A corvette under <paramref name="owner"/> with one beam of the given range, placed at x metres on
        /// the X axis (so two fleets get a controlled real separation for <c>FleetSeparation</c>).</summary>
        private static Entity AddShip(TestScenario s, Entity owner, Entity fleet, double range_m, double x)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns["default-ship-design-test-corvette"];
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "ship");
            ship.FactionOwnerID = owner.Id;
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(owner.Id, fleet, ship));

            var cv = new ShipCombatValueDB(1e6, 1e7, 1.0);
            cv.Weapons = new List<WeaponProfile> { new WeaponProfile(1e6, 3e8, 1.0, 1.0, range_m) };
            ship.SetDataBlob(cv);

            ship.SetDataBlob(new PositionDB { AbsolutePosition = new Vector3(x, 0, 0) });
            return ship;
        }

        // ─── Pure math (no positions) — the gate logic, deterministic ────────────────────────────────────────────

        [Test]
        [Description("WithinWeaponRange: out of range = false, in range = true, the LONGER reach gates, unbounded reaches any gap, no-reach = false.")]
        public void WithinWeaponRange_PureMath()
        {
            Assert.That(CombatEngagement.WithinWeaponRange(752_377, 500_000, 500_000), Is.False, "752 km gap, 500 km guns → can't reach (the live report)");
            Assert.That(CombatEngagement.WithinWeaponRange(400_000, 500_000, 500_000), Is.True,  "400 km gap, 500 km guns → in range");
            Assert.That(CombatEngagement.WithinWeaponRange(752_377, 500_000, 800_000), Is.True,  "the LONGER-ranged side (800 km) opens the fight");
            Assert.That(CombatEngagement.WithinWeaponRange(1e12, double.PositiveInfinity, 0), Is.True, "an unbounded weapon reaches any gap");
            Assert.That(CombatEngagement.WithinWeaponRange(100, 0, 0), Is.False, "neither side has a weapon with reach");
        }

        // ─── Integration through the real Tick ───────────────────────────────────────────────────────────────────

        [Test]
        [Description("With the flag ON, two hostile 500 km-gun fleets 752 km apart DO NOT engage; close them to 400 km and they DO. With the flag OFF, the old 1 Gm proximity engages them even at 752 km.")]
        public void Tick_RequireWeaponRange_NoBattleUntilInWeaponRange()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var blue = MakeFleet(s, s.Faction, "Blue");
            var red = MakeFleet(s, reds, "Red");
            AddShip(s, s.Faction, blue, range_m: 500_000, x: 0);
            var redShip = AddShip(s, reds, red, range_m: 500_000, x: 752_377);   // the live 752 km gap

            try
            {
                CombatEngagement.RequireWeaponRangeToEngage = true;
                CombatEngagement.Tick(s.StartingSystem, 5);
                Assert.That(blue.HasDataBlob<FleetCombatStateDB>(), Is.False, "752 km apart, 500 km guns → no battle (seeing ≠ firing)");
                Assert.That(red.HasDataBlob<FleetCombatStateDB>(), Is.False);

                // Close to 400 km — now within weapon range → the fight starts.
                redShip.SetDataBlob(new PositionDB { AbsolutePosition = new Vector3(400_000, 0, 0) });
                CombatEngagement.Tick(s.StartingSystem, 5);
                Assert.That(blue.HasDataBlob<FleetCombatStateDB>(), Is.True, "400 km apart → in weapon range → engaged");
                Assert.That(red.HasDataBlob<FleetCombatStateDB>(), Is.True);
            }
            finally { CombatEngagement.RequireWeaponRangeToEngage = false; }

            // Flag OFF (a fresh pair) → the old coarse 1 Gm proximity engages even at the 752 km gap.
            var s2 = TestScenario.CreateWithColony();
            var reds2 = FactionFactory.CreateBasicFaction(s2.Game, "Reds", "RED", 0);
            var blue2 = MakeFleet(s2, s2.Faction, "Blue");
            var red2 = MakeFleet(s2, reds2, "Red");
            AddShip(s2, s2.Faction, blue2, 500_000, 0);
            AddShip(s2, reds2, red2, 500_000, 752_377);
            CombatEngagement.Tick(s2.StartingSystem, 5);
            Assert.That(blue2.HasDataBlob<FleetCombatStateDB>(), Is.True, "flag off → 752 km is inside the 1 Gm proximity → old behaviour engages");
        }

        [Test]
        [Description("The imminent gate (drives the combat-interrupt fine-step) MUST agree with Tick: out of weapon range = not imminent, in range = imminent — or the master clock crawls (the fog-gate class of bug).")]
        public void NewEngagementImminent_RequireWeaponRange_MatchesTick()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var blue = MakeFleet(s, s.Faction, "Blue");
            var red = MakeFleet(s, reds, "Red");
            AddShip(s, s.Faction, blue, range_m: 500_000, x: 0);
            var redShip = AddShip(s, reds, red, range_m: 500_000, x: 752_377);

            try
            {
                CombatEngagement.RequireWeaponRangeToEngage = true;
                Assert.That(CombatEngagement.NewEngagementImminent(s.StartingSystem), Is.False, "out of weapon range → a battle can't form → not imminent (no forced fine-step)");

                redShip.SetDataBlob(new PositionDB { AbsolutePosition = new Vector3(400_000, 0, 0) });
                Assert.That(CombatEngagement.NewEngagementImminent(s.StartingSystem), Is.True, "in weapon range → imminent");
            }
            finally { CombatEngagement.RequireWeaponRangeToEngage = false; }
        }

        [Test]
        [Description("The weapons-release gate now also guards the imminent path (it didn't before): two co-located fleets both HOLDING FIRE are not imminent; flip one Weapons Free and it is. Prevents the time-crawl once the client turns the flag on.")]
        public void NewEngagementImminent_RequireWeaponsRelease_BothHoldFire_NotImminent()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var blue = MakeFleet(s, s.Faction, "Blue");
            var red = MakeFleet(s, reds, "Red");
            AddShip(s, s.Faction, blue, range_m: 500_000, x: 0);
            AddShip(s, reds, red, range_m: 500_000, x: 0);   // co-located → weapon-range never the blocker here

            try
            {
                CombatEngagement.RequireWeaponsReleaseToEngage = true;
                FleetDoctrine.SetEngagementPosture(blue, EngagementPosture.WeaponsHold);
                FleetDoctrine.SetEngagementPosture(red, EngagementPosture.WeaponsHold);
                Assert.That(CombatEngagement.NewEngagementImminent(s.StartingSystem), Is.False, "both holding fire → standoff → not imminent");

                FleetDoctrine.SetEngagementPosture(blue, EngagementPosture.WeaponsFree);
                Assert.That(CombatEngagement.NewEngagementImminent(s.StartingSystem), Is.True, "one Weapons Free → a battle will erupt → imminent");
            }
            finally { CombatEngagement.RequireWeaponsReleaseToEngage = false; }
        }
    }
}
