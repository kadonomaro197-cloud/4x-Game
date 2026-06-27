using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;
using System.Linq;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Combat depth — the DODGE model in the auto-resolver (docs/WEAPONS-AND-DODGE-DESIGN.md). A weapon's flavor
    /// (velocity/tracking/saturation) decides what fraction of its fire LANDS on a target, given the target's
    /// evasion: you can't dodge a beam, you dodge ballistic slugs, flak floors it. The payoff is the developer's
    /// acceptance test — under slug fire the battleship dies while the nimble fighter holds. Engine-only -> CI.
    /// </summary>
    [TestFixture]
    public class DodgeResolveTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[dodge] " + m);

        // --- the dodge curve, tested directly --------------------------------------------------------------
        [Test]
        [Description("Hit fraction: beams ignore evasion; ballistic slugs are dodged by the evasive but land on the sluggish; flak's saturation floors it above a slug.")]
        public void HitFraction_BeamsIgnoreEvasion_SlugsAreDodged_FlakFloors()
        {
            var beam = new WeaponProfile(WeaponClass.Beam, 1000, 3e8, 0.95, 0.5);
            var slug = new WeaponProfile(WeaponClass.Railgun, 1000, 50_000, 0.05, 5);
            var flak = new WeaponProfile(WeaponClass.Flak, 1000, 20_000, 0.10, 2000);

            double beamVsFighter = CombatEngagement.HitFraction(beam, 0.9);
            double slugVsFighter = CombatEngagement.HitFraction(slug, 0.9);
            double slugVsBattleship = CombatEngagement.HitFraction(slug, 0.0);
            double flakVsFighter = CombatEngagement.HitFraction(flak, 0.9);
            Log($"beam→fighter={beamVsFighter:0.###} slug→fighter={slugVsFighter:0.###} slug→battleship={slugVsBattleship:0.###} flak→fighter={flakVsFighter:0.###}");

            Assert.That(beamVsFighter, Is.GreaterThan(0.9), "a fighter can't dodge a light-speed beam");
            Assert.That(slugVsFighter, Is.LessThan(0.5), "a fighter dodges most ballistic slug fire");
            Assert.That(slugVsBattleship, Is.GreaterThan(0.9), "a sluggish battleship can't dodge a slug");
            Assert.That(slugVsFighter, Is.LessThan(slugVsBattleship), "evasion cuts how much slug fire lands");
            Assert.That(flakVsFighter, Is.GreaterThan(slugVsFighter), "flak fills the sky — harder to dodge than a single slug");
        }

        [Test]
        [Description("RANGE degrades ballistic accuracy (the 'pay to play' closing model): at a long separation a " +
                     "dumb slug is largely juked by an evasive target, while a GUIDED weapon (high tracking) still " +
                     "hits and a light-speed BEAM is unaffected by distance entirely. separation 0 reproduces the " +
                     "legacy 2-arg curve exactly, so the pre-closing resolve is byte-identical.")]
        public void HitFraction_RangeDegradesBallistics_NotBeamsOrGuided()
        {
            var beam    = new WeaponProfile(WeaponClass.Beam, 1000, 3e8, 0.95, 0.5);     // light-speed
            var slug    = new WeaponProfile(WeaponClass.Railgun, 1000, 5_000, 0.05, 5);  // slow, DUMB (low tracking)
            var missile = new WeaponProfile(WeaponClass.Missile, 1000, 5_000, 0.9, 5);   // slow but GUIDED (high tracking)

            const double ev = 0.9;            // a nimble target
            const double far = 1_000_000.0;   // a 1000 km gap

            double slugNear = CombatEngagement.HitFraction(slug, ev, 0);
            double slugFar  = CombatEngagement.HitFraction(slug, ev, far);
            double missFar  = CombatEngagement.HitFraction(missile, ev, far);
            double beamNear = CombatEngagement.HitFraction(beam, ev, 0);
            double beamFar  = CombatEngagement.HitFraction(beam, ev, far);
            Log($"slug near={slugNear:0.###} far={slugFar:0.###} | missile far={missFar:0.###} | beam near={beamNear:0.###} far={beamFar:0.###}");

            Assert.That(slugFar, Is.LessThan(slugNear), "a dumb slug loses accuracy at long range (flight-time dodge)");
            Assert.That(slugFar, Is.LessThan(0.2), "...and is largely juked by a nimble target at 1000 km");
            Assert.That(missFar, Is.GreaterThan(0.5), "a GUIDED missile still hits well at the same range (tracking resists distance)");
            Assert.That(beamFar, Is.EqualTo(beamNear).Within(1e-6), "a light-speed beam's accuracy doesn't fall with distance");
            Assert.That(CombatEngagement.HitFraction(slug, ev, 0), Is.EqualTo(CombatEngagement.HitFraction(slug, ev)).Within(1e-12),
                "separation 0 == the legacy 2-arg curve (pre-closing resolve byte-identical)");
        }

        // --- the dodge flowing through the resolve ---------------------------------------------------------
        private static Entity MakeFleet(TestScenario s, Entity faction, string name)
            => FleetFactory.Create(s.StartingSystem, faction.Id, name);

        private static Entity AddShip(TestScenario s, Entity faction, Entity fleet, ShipCombatValueDB cv, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name); // build under player faction
            ship.FactionOwnerID = faction.Id;                                            // then assign true owner
            ship.SetDataBlob(cv);                                                        // stamp the combat value we want
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return ship;
        }

        private static ShipCombatValueDB Railgunner(double dps)
            => new ShipCombatValueDB(dps, 100_000, 1.0) { Weapons = { new WeaponProfile(WeaponClass.Railgun, dps, 50_000, 0.05, 5) } };

        // An unarmed defender hull — same toughness, differing only in evasion (so evasion is the only variable).
        private static ShipCombatValueDB Hull(double evasion, double toughness)
            => new ShipCombatValueDB(0, toughness, 1.0) { Evasion = evasion };

        [Test]
        [Description("Under slug fire, the un-evasive battleship dies while the nimble fighter (same toughness, only evasion differs) dodges and survives.")]
        public void RailgunFire_KillsTheBattleship_SparesTheFighter()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var attacker = MakeFleet(s, enemyFaction, "Slug Battery");
            AddShip(s, enemyFaction, attacker, Railgunner(20_000), "Slugger");

            var defender = MakeFleet(s, s.Faction, "Defenders");
            var battleship = AddShip(s, s.Faction, defender, Hull(0.0, 200_000), "Battleship"); // can't dodge
            var fighter = AddShip(s, s.Faction, defender, Hull(0.9, 200_000), "Fighter");       // dodges

            CombatEngagement.StartEngagement(attacker, defender);
            int steps = 0;
            while (battleship.IsValid && defender.HasDataBlob<FleetCombatStateDB>() && steps < 2000)
            {
                CombatEngagement.StepEngagement(attacker, defender, 5);
                steps++;
            }

            Log($"after {steps} steps: battleship={battleship.IsValid} fighter={fighter.IsValid}");
            Assert.That(battleship.IsValid, Is.False, "the slugs hit the un-evasive battleship");
            Assert.That(fighter.IsValid, Is.True, "the fighter dodged the slugs and held — same toughness, only evasion differs");
        }

        [Test]
        [Description("Class buckets, by count: under slug fire one battleship dies while a whole fighter screen (same toughness, high evasion) dodges and holds — casualties are applied per class bucket, not per individual.")]
        public void RailgunFire_AgainstAClassMix_KillsTheBattleship_HoldsTheFighterScreen()
        {
            var s = TestScenario.CreateWithColony();
            var enemyFaction = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var attacker = MakeFleet(s, enemyFaction, "Slug Battery");
            AddShip(s, enemyFaction, attacker, Railgunner(20_000), "Slugger");

            var defender = MakeFleet(s, s.Faction, "Task Force");
            var battleship = AddShip(s, s.Faction, defender, Hull(0.0, 200_000), "Battleship");
            var fighters = new System.Collections.Generic.List<Entity>();
            for (int i = 0; i < 5; i++)
                fighters.Add(AddShip(s, s.Faction, defender, Hull(0.9, 200_000), "Fighter" + i)); // same toughness, evasive

            CombatEngagement.StartEngagement(attacker, defender);
            int steps = 0;
            while (battleship.IsValid && defender.HasDataBlob<FleetCombatStateDB>() && steps < 2000)
            {
                CombatEngagement.StepEngagement(attacker, defender, 5);
                steps++;
            }

            int aliveFighters = fighters.Count(f => f.IsValid);
            Log($"after {steps} steps: battleship={battleship.IsValid} fightersAlive={aliveFighters}/5");
            Assert.That(battleship.IsValid, Is.False, "the slugs hit the un-evasive battleship first");
            Assert.That(aliveFighters, Is.EqualTo(5), "the whole fighter screen (one class bucket) dodged the slugs and held");
        }
    }
}
