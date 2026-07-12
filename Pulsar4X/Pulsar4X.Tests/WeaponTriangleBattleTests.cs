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
    /// Combat depth P6 — the weapon triangle as ACTUAL BATTLES (not just the hit-fraction mechanism). These run
    /// real fleets through the dodge resolver (<see cref="CombatEngagement.StepEngagement"/>) and assert the
    /// rock-paper-scissors OUTCOMES, using the real base-mod Wasp fighter / Leviathan capital designs.
    ///
    /// All three tests are calibration-ROBUST by ISOLATING evasion: identical forces that differ ONLY in whether
    /// the fighters can dodge, so whatever the absolute damage numbers, the dodge is the only variable and the side
    /// that can dodge ends better. (The screen tests vary the weapon flavor on identical fighter screens; the
    /// swarm test zeroes the fighters' evasion on one of two identical swarms.) Pace note: the hot-damage rebalance
    /// (2026-06-25) added <see cref="CombatEngagement.SalvoDamageScale"/> (0.1) so a salvo deposits a tenth of its
    /// raw energy — a capital no longer one-shots a wing of fighters; the same fight just plays out over ~10× more
    /// salvos. Because the scale is uniform it doesn't change these evasion-isolated OUTCOMES (the dodging side
    /// still ends better) — only how many steps it takes, so the loop caps below were raised ~10× to match the
    /// slower pace. Engine-only -> runs in CI.
    /// </summary>
    [TestFixture]
    public class WeaponTriangleBattleTests
    {
        private const string Fighter = "default-ship-design-test-fighter";
        private static void Log(string m) => TestContext.Progress.WriteLine("[triangle-battle] " + m);

        private static Entity MakeFleet(TestScenario s, Entity faction, string name)
            => FleetFactory.Create(s.StartingSystem, faction.Id, name);

        /// <summary>Build a REAL base-mod design (keeps its real evasion + weapons) and assign it to the fleet.</summary>
        private static void AddReal(TestScenario s, Entity faction, Entity fleet, string designId, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns[designId];
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.FactionOwnerID = faction.Id;
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
        }

        /// <summary>A controlled attacker: one ship that fires exactly the given weapon flavor at the given dps,
        /// with huge toughness (so it survives to keep firing) and zero evasion (it isn't the thing being tested).</summary>
        private static void AddGun(TestScenario s, Entity faction, Entity fleet, WeaponProfile wp, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.FactionOwnerID = faction.Id;
            ship.SetDataBlob(new ShipCombatValueDB(wp.DamagePerSecond, 1e12, 1.0) { Evasion = 0, Weapons = { wp } });
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
        }

        /// <summary>A "sitting-duck" fighter: a REAL Wasp (same hull, same weapon, same toughness) with its evasion
        /// zeroed — so a swarm of these is identical to a real Wasp swarm in every way EXCEPT it can't dodge. That
        /// isolates evasion as the only variable in the swarm-vs-capital comparison.</summary>
        private static void AddDuck(TestScenario s, Entity faction, Entity fleet, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns[Fighter];
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.FactionOwnerID = faction.Id;
            ship.GetDataBlob<ShipCombatValueDB>().Evasion = 0; // same fighter, but can't dodge
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
        }

        private static int RunBattle(Entity attacker, Entity defender, int maxSteps)
        {
            CombatEngagement.StartEngagement(attacker, defender);
            int steps = 0;
            while (defender.HasDataBlob<FleetCombatStateDB>() && steps < maxSteps)
            {
                CombatEngagement.StepEngagement(attacker, defender, 5);
                steps++;
            }
            return CombatEngagement.GetFleetShips(defender).Count;
        }

        [Test]
        [Description("BEAM ▸ FIGHTER: two identical real fighter screens take EQUAL damage/sec — one from a beam, one from a railgun. The fighters dodge the railgun but not the light-speed beam, so more of them survive the railgun.")]
        public void FighterScreen_SurvivesRailgunFire_ButFallsToBeams()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            const int screen = 10;
            const double dps = 40_000; // equal for both attackers — only the flavor differs

            var beamGun = MakeFleet(s, red, "Beam Battery");
            AddGun(s, red, beamGun, new WeaponProfile(dps, 3e8, 0.95, 0.5), "Beamer");
            var beamScreen = MakeFleet(s, s.Faction, "Beam-side Screen");
            for (int i = 0; i < screen; i++) AddReal(s, s.Faction, beamScreen, Fighter, "BW" + i);
            int survivesBeam = RunBattle(beamGun, beamScreen, 400);

            var slugGun = MakeFleet(s, red, "Slug Battery");
            AddGun(s, red, slugGun, new WeaponProfile(dps, 50_000, 0.05, 5), "Slugger");
            var slugScreen = MakeFleet(s, s.Faction, "Slug-side Screen");
            for (int i = 0; i < screen; i++) AddReal(s, s.Faction, slugScreen, Fighter, "SW" + i);
            int survivesRailgun = RunBattle(slugGun, slugScreen, 400);

            Log($"equal {dps:0} dps on identical fighter screens -> survivors: beam={survivesBeam}/{screen}  railgun={survivesRailgun}/{screen}");
            Assert.That(survivesRailgun, Is.GreaterThan(survivesBeam),
                "BEAM ▸ FIGHTER: same firepower, but the fighters dodge the railgun and can't dodge the beam — more survive the railgun");
        }

        [Test]
        [Description("FLAK ▸ FIGHTER: identical real fighter screens take EQUAL damage/sec from a railgun vs from flak. Flak's saturation floors the dodge, so it kills more fighters than the railgun the fighters can juke.")]
        public void FighterScreen_FallsFasterToFlak_ThanToRailguns()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            const int screen = 10;
            const double dps = 40_000;

            var slugGun = MakeFleet(s, red, "Slug Battery");
            AddGun(s, red, slugGun, new WeaponProfile(dps, 50_000, 0.05, 5), "Slugger");
            var slugScreen = MakeFleet(s, s.Faction, "Slug-side Screen");
            for (int i = 0; i < screen; i++) AddReal(s, s.Faction, slugScreen, Fighter, "SW" + i);
            int survivesRailgun = RunBattle(slugGun, slugScreen, 400);

            var flakGun = MakeFleet(s, red, "Flak Battery");
            AddGun(s, red, flakGun, new WeaponProfile(dps, 20_000, 0.10, 300), "Flakker");
            var flakScreen = MakeFleet(s, s.Faction, "Flak-side Screen");
            for (int i = 0; i < screen; i++) AddReal(s, s.Faction, flakScreen, Fighter, "FW" + i);
            int survivesFlak = RunBattle(flakGun, flakScreen, 400);

            Log($"equal {dps:0} dps on identical fighter screens -> survivors: railgun={survivesRailgun}/{screen}  flak={survivesFlak}/{screen}");
            Assert.That(survivesFlak, Is.LessThan(survivesRailgun),
                "FLAK ▸ FIGHTER: same firepower, but flak's saturation floors the dodge — it kills more fighters than the dodgeable railgun");
        }

        [Test]
        [Description("FIGHTER ▸ dodge (evasion isolated): two identical screens of REAL Wasp fighters take the SAME sustained railgun fire — one dodging, one with its evasion zeroed. The dodging screen loses meaningfully fewer, so the fighters' evasion (juking the slugs) is the difference. Uses the AddGun sustained-battery + dps calibration of the sibling screen tests (which prove railgun-dodgers out-survive non-dodgers), so the dodge shows across many salvos — calibration-robust where the earlier one-off capital was knife-edge (a single Leviathan died in a few salvos landing ~1 hit, far too little for evasion to separate the swarms; that fragility surfaced when ships gained hull mass 2026-07-10).")]
        public void EvasiveScreen_OutLastsSittingDucks_UnderTheSameRailgunFire()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            const int N = 10;
            const double dps = 40_000; // same calibration the screen tests use to show dodgers out-survive non-dodgers

            // Real, evasive Wasp screen under a sustained railgun battery.
            var evasive = MakeFleet(s, s.Faction, "Evasive Screen");
            for (int i = 0; i < N; i++) AddReal(s, s.Faction, evasive, Fighter, "E" + i);
            double waspEvasion = CombatEngagement.GetFleetShips(evasive)[0].GetDataBlob<ShipCombatValueDB>().Evasion;
            var gunA = MakeFleet(s, red, "Railgun Battery A");
            AddGun(s, red, gunA, new WeaponProfile(dps, 50_000, 0.05, 5), "Slugger A");
            int evasiveLeft = RunBattle(gunA, evasive, 400);

            // The SAME Wasp screen with evasion zeroed (sitting ducks) under an identical battery.
            var ducks = MakeFleet(s, s.Faction, "Sitting-Duck Screen");
            for (int i = 0; i < N; i++) AddDuck(s, s.Faction, ducks, "D" + i);
            var gunB = MakeFleet(s, red, "Railgun Battery B");
            AddGun(s, red, gunB, new WeaponProfile(dps, 50_000, 0.05, 5), "Slugger B");
            int ducksLeft = RunBattle(gunB, ducks, 400);

            Log($"{N} fighters under equal railgun fire (real wasp evasion={waspEvasion:0.###}): " +
                $"evasive survivors={evasiveLeft}, sitting-duck survivors={ducksLeft}");

            Assert.That(ducksLeft, Is.LessThan(N), "the non-dodging sitting ducks take real losses — the fire is lethal enough to measure evasion against");
            Assert.That(evasiveLeft, Is.GreaterThan(ducksLeft),
                $"evasion is the difference: {evasiveLeft} evasive fighters survive vs {ducksLeft} sitting ducks under the SAME railgun fire (real wasp evasion={waspEvasion:0.###})");
        }
    }
}
