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
    /// The two screen tests are calibration-ROBUST by construction: identical real fighter screens are shot by
    /// attackers of EQUAL damage/sec that differ ONLY in weapon flavor, so whatever the absolute numbers, the side
    /// whose fire the fighters can dodge leaves more of them alive. The swarm-vs-capital test is robust by the
    /// triangle's own asymmetry — the fighters land full fire on the sluggish capital while dodging its railguns.
    /// Engine-only -> runs in CI.
    /// </summary>
    [TestFixture]
    public class WeaponTriangleBattleTests
    {
        private const string Fighter = "default-ship-design-test-fighter";
        private const string Capital = "default-ship-design-test-capital";
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
            AddGun(s, red, beamGun, new WeaponProfile(WeaponClass.Beam, dps, 3e8, 0.95, 0.5), "Beamer");
            var beamScreen = MakeFleet(s, s.Faction, "Beam-side Screen");
            for (int i = 0; i < screen; i++) AddReal(s, s.Faction, beamScreen, Fighter, "BW" + i);
            int survivesBeam = RunBattle(beamGun, beamScreen, 40);

            var slugGun = MakeFleet(s, red, "Slug Battery");
            AddGun(s, red, slugGun, new WeaponProfile(WeaponClass.Railgun, dps, 50_000, 0.05, 5), "Slugger");
            var slugScreen = MakeFleet(s, s.Faction, "Slug-side Screen");
            for (int i = 0; i < screen; i++) AddReal(s, s.Faction, slugScreen, Fighter, "SW" + i);
            int survivesRailgun = RunBattle(slugGun, slugScreen, 40);

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
            AddGun(s, red, slugGun, new WeaponProfile(WeaponClass.Railgun, dps, 50_000, 0.05, 5), "Slugger");
            var slugScreen = MakeFleet(s, s.Faction, "Slug-side Screen");
            for (int i = 0; i < screen; i++) AddReal(s, s.Faction, slugScreen, Fighter, "SW" + i);
            int survivesRailgun = RunBattle(slugGun, slugScreen, 40);

            var flakGun = MakeFleet(s, red, "Flak Battery");
            AddGun(s, red, flakGun, new WeaponProfile(WeaponClass.Flak, dps, 20_000, 0.10, 300), "Flakker");
            var flakScreen = MakeFleet(s, s.Faction, "Flak-side Screen");
            for (int i = 0; i < screen; i++) AddReal(s, s.Faction, flakScreen, Fighter, "FW" + i);
            int survivesFlak = RunBattle(flakGun, flakScreen, 40);

            Log($"equal {dps:0} dps on identical fighter screens -> survivors: railgun={survivesRailgun}/{screen}  flak={survivesFlak}/{screen}");
            Assert.That(survivesFlak, Is.LessThan(survivesRailgun),
                "FLAK ▸ FIGHTER: same firepower, but flak's saturation floors the dodge — it kills more fighters than the dodgeable railgun");
        }

        [Test]
        [Description("FIGHTER ▸ CAPITAL: a swarm of real Wasp fighters fights a real Leviathan railgun battleship. The fighters' fire lands full on the sluggish capital (destroyed) while they dodge its railguns (survivors remain).")]
        public void FighterSwarm_DodgesAndDestroys_TheRailgunCapital()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var swarm = MakeFleet(s, s.Faction, "Fighter Wing");
            const int wing = 6;
            for (int i = 0; i < wing; i++) AddReal(s, s.Faction, swarm, Fighter, "W" + i);
            var capitalFleet = MakeFleet(s, red, "Capital");
            AddReal(s, red, capitalFleet, Capital, "Leviathan");

            CombatEngagement.StartEngagement(swarm, capitalFleet);
            int steps = 0;
            while (capitalFleet.HasDataBlob<FleetCombatStateDB>() && steps < 200)
            {
                CombatEngagement.StepEngagement(swarm, capitalFleet, 5);
                steps++;
            }

            int capitalLeft = CombatEngagement.GetFleetShips(capitalFleet).Count;
            int wingLeft = CombatEngagement.GetFleetShips(swarm).Count;
            Log($"swarm vs capital in {steps} steps: capitalShips={capitalLeft}  fightersSurviving={wingLeft}/{wing}");
            Assert.That(capitalLeft, Is.EqualTo(0), "FIGHTER ▸ CAPITAL: the fighters' fire lands full on the sluggish capital — it is destroyed");
            Assert.That(wingLeft, Is.GreaterThan(0), "while the fighters dodge its railguns and the wing survives");
        }
    }
}
