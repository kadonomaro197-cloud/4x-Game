using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;    // ShipCombatValueDB, WeaponProfile, FleetCombat
using Pulsar4X.Engine;
using Pulsar4X.Factions;  // FactionInfoDB
using Pulsar4X.Fleets;    // FleetRoleComposer, FleetRole
using Pulsar4X.Ships;     // ShipFactory

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase B-2a gauge — the AI can now look at a fleet and tell each ship's fighting JOB (Q7: sub-fleets that each
    /// play a part). <see cref="FleetRoleComposer"/> reads the combat numbers a ship already carries and sorts it into
    /// Screen (fast movers out front) / Line (the gunline) / Artillery (long-reach stand-off) / Support (tenders kept
    /// out of the shooting). This proves the sorter reads those numbers correctly. It mutates NOTHING — no fleet tree,
    /// no orders — so a running game is byte-identical; a later slice takes this bucketing and forms real sub-fleets.
    /// The classification tests hand-stamp the combat value so the answer is deterministic (I can't run the sim here);
    /// the smoke test proves it also runs clean over the real start fleet.
    /// </summary>
    [TestFixture]
    public class FleetRoleCompositionTests
    {
        // A ship whose combat value we control exactly — build a real hull, then stamp the numbers we want to test.
        private static Entity ShipWith(TestScenario s, ShipCombatValueDB cv)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "role-test");
            ship.SetDataBlob(cv);   // overwrite with the exact combat value under test
            return ship;
        }

        private static ShipCombatValueDB Cv(double roleWeight, double evasion, params double[] weaponRanges)
        {
            var cv = new ShipCombatValueDB(firepower: 100, toughness: 1000, roleWeight: roleWeight) { Evasion = evasion };
            foreach (var r in weaponRanges)
                cv.Weapons.Add(new WeaponProfile(10, 50_000, 0.5, 5, range_m: r));
            return cv;
        }

        // --- ClassifyRole: one ship, one job -----------------------------------------------------------------

        [Test]
        [Description("A utility hull (RoleWeight < 1) is Support even if it's nimble and long-ranged — utility beats everything.")]
        public void UtilityHull_IsSupport()
        {
            var s = TestScenario.CreateWithColony();
            var ship = ShipWith(s, Cv(roleWeight: 0.25, evasion: 0.9, 500_000)); // evasive AND long-range, but utility
            Assert.That(FleetRoleComposer.ClassifyRole(ship), Is.EqualTo(FleetRole.Support));
        }

        [Test]
        [Description("A nimble warship (evasion >= threshold) leads as the Screen — and that beats a long gun.")]
        public void NimbleWarship_IsScreen()
        {
            var s = TestScenario.CreateWithColony();
            var ship = ShipWith(s, Cv(roleWeight: 1.0, evasion: 0.7, 500_000)); // fast AND long-range → Screen wins
            Assert.That(FleetRoleComposer.ClassifyRole(ship), Is.EqualTo(FleetRole.Screen));
        }

        [Test]
        [Description("A slow, long-reach warship stands off as Artillery (a railgun/missile-class 500 km gun).")]
        public void LongReachWarship_IsArtillery()
        {
            var s = TestScenario.CreateWithColony();
            var ship = ShipWith(s, Cv(roleWeight: 1.0, evasion: 0.1, 500_000));
            Assert.That(FleetRoleComposer.ClassifyRole(ship), Is.EqualTo(FleetRole.Artillery));
        }

        [Test]
        [Description("A slow, short-reach warship (a 50 km flak knife-fighter) is the gunline — Line.")]
        public void ShortReachWarship_IsLine()
        {
            var s = TestScenario.CreateWithColony();
            var ship = ShipWith(s, Cv(roleWeight: 1.0, evasion: 0.1, 50_000)); // short of the artillery threshold
            Assert.That(FleetRoleComposer.ClassifyRole(ship), Is.EqualTo(FleetRole.Line));
        }

        [Test]
        [Description("A ship with no combat value at all is Support — it's not a fighting ship.")]
        public void UnratedHull_IsSupport()
        {
            var s = TestScenario.CreateWithColony();
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "unrated");
            ship.RemoveDataBlob<ShipCombatValueDB>();
            Assert.That(FleetRoleComposer.ClassifyRole(ship), Is.EqualTo(FleetRole.Support));
        }

        // --- MaxWeaponRange: the number the Artillery sort reads ----------------------------------------------

        [Test]
        [Description("MaxWeaponRange is the longest gun's reach — a short beam alongside a long railgun reads the railgun.")]
        public void MaxWeaponRange_IsTheLongestGun()
        {
            var cv = Cv(roleWeight: 1.0, evasion: 0.0, 5_000, 500_000, 50_000); // beam + railgun + flak
            Assert.That(cv.MaxWeaponRange, Is.EqualTo(500_000));
        }

        [Test]
        [Description("An unarmed hull reads 0 reach (no weapons).")]
        public void MaxWeaponRange_UnarmedIsZero()
        {
            var cv = Cv(roleWeight: 1.0, evasion: 0.0); // no weapons
            Assert.That(cv.MaxWeaponRange, Is.EqualTo(0));
        }

        // --- PlanRoleSubFleets over the REAL start fleet ------------------------------------------------------

        [Test]
        [Description("Bucketing the real start fleet: all four roles keyed, every ship placed exactly once, count matches FleetCombat.Ships.")]
        public void PlanRoleSubFleets_BucketsEveryRealShipOnce()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>()
                .First(f => f.FactionOwnerID == s.Faction.Id && FleetCombat.Ships(f).Count > 0);

            var buckets = FleetRoleComposer.PlanRoleSubFleets(fleet);

            // all four jobs are present as keys (empty lists allowed), so a caller iterates without null checks
            foreach (FleetRole role in System.Enum.GetValues(typeof(FleetRole)))
                Assert.That(buckets.ContainsKey(role), $"role {role} is a key");

            var ships = FleetCombat.Ships(fleet);
            var placed = buckets.Values.SelectMany(x => x).ToList();
            Assert.That(placed.Count, Is.EqualTo(ships.Count), "every ship is placed exactly once");
            Assert.That(placed.Distinct().Count(), Is.EqualTo(placed.Count), "no ship is in two buckets");
            CollectionAssert.AreEquivalent(ships, placed, "the buckets partition the fleet's ships");
        }
    }
}
