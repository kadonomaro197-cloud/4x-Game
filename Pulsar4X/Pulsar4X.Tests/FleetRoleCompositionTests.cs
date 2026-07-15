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

        // --- B-2b: FormRoleSubFleets actually forms the tree ------------------------------------------------

        // The real start fleet (first owned fleet that has ships).
        private static Entity StartFleet(TestScenario s)
            => s.StartingSystem.GetAllEntitiesWithDataBlob<FleetDB>()
                .First(f => f.FactionOwnerID == s.Faction.Id && FleetCombat.Ships(f).Count > 0);

        // The parent fleet's DIRECT ship children (the ones FormRoleSubFleets sorts), same filter the method uses.
        private static System.Collections.Generic.List<Entity> DirectShips(Entity fleet)
            => fleet.GetDataBlob<FleetDB>().GetChildren()
                .Where(c => c.IsValid && !c.HasDataBlob<FleetDB>() && c.HasDataBlob<Pulsar4X.Ships.ShipInfoDB>()).ToList();

        [Test]
        [Description("Forming the real start fleet: one sub-fleet per role its ships fill, each tagged + flagshipped + parented, and NO ship lost or duplicated.")]
        public void FormRoleSubFleets_MakesOneSubFleetPerRole_AndConservesShips()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = StartFleet(s);
            var before = FleetCombat.Ships(fleet);
            Assert.That(before, Is.Not.Empty);

            var expectedRoles = DirectShips(fleet).Select(FleetRoleComposer.ClassifyRole).Distinct().ToHashSet();

            var formed = FleetRoleComposer.FormRoleSubFleets(fleet);

            Assert.That(formed.Keys.ToHashSet(), Is.EquivalentTo(expectedRoles), "one sub-fleet per non-empty role");

            foreach (var kv in formed)
            {
                var sub = kv.Value;
                Assert.That(sub.HasDataBlob<FleetDB>(), Is.True, "the sub-fleet is a real fleet");
                Assert.That(sub.HasDataBlob<FleetRoleDB>(), Is.True, "the sub-fleet is tagged with its role");
                Assert.That(sub.GetDataBlob<FleetRoleDB>().Role, Is.EqualTo(kv.Key), "the tag matches the map key");
                Assert.That(sub.FactionOwnerID, Is.EqualTo(s.Faction.Id), "the sub-fleet is owned by the same faction");

                var subDB = sub.GetDataBlob<FleetDB>();
                Assert.That(subDB.Parent, Is.Not.Null, "the sub-fleet has a parent");
                Assert.That(subDB.Parent.Id, Is.EqualTo(fleet.Id), "parented to the original fleet");
                Assert.That(subDB.FlagShipID, Is.Not.EqualTo(-1), "the sub-fleet has a flagship");

                var subShips = subDB.GetChildren().ToList();
                Assert.That(subShips.Select(x => x.Id), Contains.Item(subDB.FlagShipID), "the flagship is one of its ships");
                foreach (var ship in subShips)
                    Assert.That(FleetRoleComposer.ClassifyRole(ship), Is.EqualTo(kv.Key), "every ship matches the sub-fleet's role");
            }

            // Ship conservation — the load-bearing tripwire: no ship lost, none duplicated across the move.
            CollectionAssert.AreEquivalent(before, FleetCombat.Ships(fleet), "the fleet's ships are unchanged in total");
            var placed = formed.Values.SelectMany(sub => sub.GetDataBlob<FleetDB>().GetChildren()).ToList();
            Assert.That(placed.Distinct().Count(), Is.EqualTo(before.Count), "every ship sits in exactly one sub-fleet");

            // The flat start fleet is fully decomposed — the parent holds sub-fleets now, no loose ships.
            Assert.That(DirectShips(fleet), Is.Empty, "the parent has no direct ships left");
        }

        [Test]
        [Description("A formed sub-fleet is discoverable by its FleetRoleDB tag; the player root fleet never carries it — the B-2c find-and-classify handle.")]
        public void FormedSubFleets_AreDiscoverableAndDistinctFromPlayerFleet()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = StartFleet(s);

            var formed = FleetRoleComposer.FormRoleSubFleets(fleet);

            Assert.That(fleet.HasDataBlob<FleetRoleDB>(), Is.False, "the root is a player fleet, not a role sub-fleet");
            foreach (var sub in formed.Values)
                Assert.That(sub.HasDataBlob<FleetRoleDB>(), Is.True, "each formed sub-fleet is found by its marker");
        }

        [Test]
        [Description("Re-running on an already-formed fleet is a no-op (no direct ships left), and null / ship-less inputs return empty — no duplication or loss.")]
        public void FormRoleSubFleets_IsSafeToReRun_AndOnEmptyInput()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = StartFleet(s);
            var before = FleetCombat.Ships(fleet);

            var first = FleetRoleComposer.FormRoleSubFleets(fleet);
            Assert.That(first, Is.Not.Empty);

            var second = FleetRoleComposer.FormRoleSubFleets(fleet);
            Assert.That(second, Is.Empty, "no direct ships remain, so nothing new to form");

            CollectionAssert.AreEquivalent(before, FleetCombat.Ships(fleet), "re-run neither loses nor duplicates a ship");
            var subCount = fleet.GetDataBlob<FleetDB>().GetChildren().Count(c => c.HasDataBlob<FleetRoleDB>());
            Assert.That(subCount, Is.EqualTo(first.Count), "re-run created no extra sub-fleets");

            Assert.That(FleetRoleComposer.FormRoleSubFleets(null), Is.Empty, "null fleet → empty");
            var emptyFleet = FleetFactory.Create(s.StartingSystem, s.Faction.Id, "Empty");
            Assert.That(FleetRoleComposer.FormRoleSubFleets(emptyFleet), Is.Empty, "ship-less fleet → empty");
        }

        [Test]
        [Description("After forming, the recursive combat read still collects each ship exactly once, at 1.0 firepower (no doctrine leaked) — pins the combat seam.")]
        public void GetCombatShips_StillCountsEachShipOnce_AfterForming()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = StartFleet(s);
            var before = FleetCombat.Ships(fleet);

            FleetRoleComposer.FormRoleSubFleets(fleet);

            var combatShips = CombatEngagement.GetCombatShips(fleet);
            Assert.That(combatShips.Count, Is.EqualTo(before.Count), "each ship counted exactly once through the nesting");
            foreach (var cs in combatShips)
                Assert.That(cs.FirepowerMult, Is.EqualTo(1.0), "no doctrine leaked — B-2b sub-fleets carry no doctrine");
        }

        [Test]
        [Description("The formed sub-fleet tree (parent link, flagship, role tags, ships) survives Game.Save -> Game.Load — the nested-FleetDB round-trip gauge.")]
        public void FormedSubFleetTree_SurvivesSaveLoad()
        {
            var s = TestScenario.CreateWithColony();
            var fleet = StartFleet(s);

            var formed = FleetRoleComposer.FormRoleSubFleets(fleet);
            Assert.That(formed, Is.Not.Empty);
            int parentId = fleet.Id;
            int expectedSubCount = formed.Count;
            var expectedRoles = formed.Keys.ToHashSet();

            string json = null;
            Assert.DoesNotThrow(() => json = Game.Save(s.Game), "Save threw on a fleet with sub-fleets");
            Assert.That(json, Is.Not.Null.And.Not.Empty);

            Game reloaded = null;
            Assert.DoesNotThrow(() => reloaded = Game.Load(json), "Load threw on the sub-fleet-tree JSON");
            Assert.That(reloaded, Is.Not.Null);

            var reFleet = reloaded.Systems.SelectMany(sys => sys.GetAllEntitiesWithDataBlob<FleetDB>())
                .FirstOrDefault(f => f.Id == parentId);
            Assert.That(reFleet, Is.Not.Null, "the parent fleet survived reload");

            var reSubs = reFleet.GetDataBlob<FleetDB>().GetChildren().Where(c => c.HasDataBlob<FleetRoleDB>()).ToList();
            Assert.That(reSubs.Count, Is.EqualTo(expectedSubCount), "all sub-fleets survived reload");
            Assert.That(reSubs.Select(x => x.GetDataBlob<FleetRoleDB>().Role).ToHashSet(),
                Is.EquivalentTo(expectedRoles), "each sub-fleet's role tag survived reload");

            foreach (var sub in reSubs)
            {
                var sdb = sub.GetDataBlob<FleetDB>();
                Assert.That(sdb.Parent, Is.Not.Null, "sub-fleet keeps a parent after reload");
                Assert.That(sdb.Parent.Id, Is.EqualTo(parentId), "still parented to the fleet after reload");
                Assert.That(sdb.FlagShipID, Is.Not.EqualTo(-1), "flagship id survived reload");
                Assert.That(sdb.GetChildren().Any(), Is.True, "sub-fleet kept its ships after reload");
            }
        }
    }
}
