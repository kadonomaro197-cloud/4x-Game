using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Blueprints;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Combat STRESS LAB — push the ends of weapon design and fleet scale and read what the dodge resolver
    /// actually does. EXPLORATION phase: each test reports its real numbers via Assert.Fail (the only readout
    /// channel CI surfaces for a passing-or-failing run is the assertion message). Once the numbers are read these
    /// get converted into green regression assertions. Stamped combat values let us dial weapon flavor + hull to
    /// the extremes directly. Engine-only -> runs in CI.
    /// </summary>
    [TestFixture]
    public class CombatStressLab
    {
        private static Entity MakeFleet(TestScenario s, Entity faction, string name) => FleetFactory.Create(s.StartingSystem, faction.Id, name);

        /// <summary>Build a ship with a fully stamped combat value (firepower/toughness/evasion + one weapon flavor).</summary>
        private static Entity Stamp(TestScenario s, Entity faction, Entity fleet, double fp, double tough, double evasion, WeaponProfile wp, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.FactionOwnerID = faction.Id;
            var cv = new ShipCombatValueDB(fp, tough, 1.0) { Evasion = evasion };
            if (wp != null) cv.Weapons.Add(wp);
            ship.SetDataBlob(cv);
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return ship;
        }

        private static Entity Screen(TestScenario s, Entity faction, int n, double evasion, double tough, string tag)
        {
            var fleet = MakeFleet(s, faction, tag);
            for (int i = 0; i < n; i++)
                Stamp(s, faction, fleet, 100, tough, evasion, new WeaponProfile(WeaponClass.Railgun, 100, 50_000, 0.05, 5), tag + i);
            return fleet;
        }

        private static (int aSurv, int bSurv, int steps) Resolve(Entity a, Entity b, int maxSteps)
        {
            CombatEngagement.StartEngagement(a, b);
            int steps = 0;
            while (a.HasDataBlob<FleetCombatStateDB>() && b.HasDataBlob<FleetCombatStateDB>() && steps < maxSteps)
            {
                CombatEngagement.StepEngagement(a, b, 5);
                steps++;
            }
            return (CombatEngagement.GetFleetShips(a).Count, CombatEngagement.GetFleetShips(b).Count, steps);
        }

        // Fire a single stamped gun (huge toughness, so it survives) at a fresh evasive screen for a fixed window;
        // return how many of the screen were killed.
        private static int Killed(TestScenario s, Entity red, WeaponProfile gun, int n, double ev, double tough, int steps)
        {
            var screen = Screen(s, s.Faction, n, ev, tough, "scr");
            var battery = MakeFleet(s, red, "bat");
            Stamp(s, red, battery, gun.DamagePerSecond, 1e12, 0, gun, "gun");
            CombatEngagement.StartEngagement(battery, screen);
            for (int i = 0; i < steps && screen.HasDataBlob<FleetCombatStateDB>(); i++)
                CombatEngagement.StepEngagement(battery, screen, 5);
            return n - CombatEngagement.GetFleetShips(screen).Count;
        }

        [Test]
        public void S01_RateOfFire_DefeatsEvasion()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            int hi = Killed(s, red, new WeaponProfile(WeaponClass.Railgun, 200_000, 50_000, 0.05, 1000), 20, 0.9, 100_000, 4);
            int lo = Killed(s, red, new WeaponProfile(WeaponClass.Railgun, 200_000, 50_000, 0.05, 5), 20, 0.9, 100_000, 4);
            Assert.Fail($"[STRESS-01] vs 20 evasive(0.9) fighters in 4 steps: a SPINAL slug (saturation 1000) killed {hi}; a normal railgun (saturation 5) killed {lo} -> rate-of-fire/saturation defeats the dodge.");
        }

        [Test]
        public void S02_SlowFlak_IsUseless()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            int fast = Killed(s, red, new WeaponProfile(WeaponClass.Flak, 200_000, 20_000, 0.10, 300), 20, 0.9, 100_000, 4);
            int slow = Killed(s, red, new WeaponProfile(WeaponClass.Flak, 200_000, 20_000, 0.10, 0.02), 20, 0.9, 100_000, 4);
            Assert.Fail($"[STRESS-02] vs 20 evasive(0.9) fighters in 4 steps: fast flak (saturation 300) killed {fast}; a 1/min flak cannon (saturation 0.02) killed {slow} -> slow flak is useless, exactly as predicted.");
        }

        [Test]
        public void S03_MuzzleVelocity_DefeatsEvasion()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            int light = Killed(s, red, new WeaponProfile(WeaponClass.Railgun, 200_000, 3e8, 0.05, 5), 20, 0.9, 100_000, 4);
            int slow = Killed(s, red, new WeaponProfile(WeaponClass.Railgun, 200_000, 50_000, 0.05, 5), 20, 0.9, 100_000, 4);
            Assert.Fail($"[STRESS-03] vs 20 evasive(0.9) fighters in 4 steps: a near-light railgun (velocity 3e8) killed {light}; a normal railgun (velocity 5e4) killed {slow} -> muzzle velocity defeats the dodge.");
        }

        [Test]
        public void S04_SaturationFloor_NothingIsUntouchable()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            int extreme = Killed(s, red, new WeaponProfile(WeaponClass.Flak, 500_000, 20_000, 0.10, 100_000), 20, 0.95, 100_000, 20);
            int normalSlug = Killed(s, red, new WeaponProfile(WeaponClass.Railgun, 500_000, 50_000, 0.05, 5), 20, 0.95, 100_000, 20);
            Assert.Fail($"[STRESS-04] vs 20 MAX-evasion(0.95) fighters in 20 steps: extreme-saturation flak (100000) killed {extreme}; a normal slug killed {normalSlug} -> the saturation floor means nothing is truly untouchable.");
        }

        [Test]
        public void S05_GlassCannon_vs_Brick()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            var glass = MakeFleet(s, s.Faction, "glass");
            var glassShip = Stamp(s, s.Faction, glass, 1e7, 50_000, 0, new WeaponProfile(WeaponClass.Beam, 1e7, 3e8, 0.95, 0.5), "Glass");
            var brick = MakeFleet(s, red, "brick");
            var brickShip = Stamp(s, red, brick, 1_000, 1e8, 0, new WeaponProfile(WeaponClass.Beam, 1_000, 3e8, 0.95, 0.5), "Brick");
            var (g, b, steps) = Resolve(glass, brick, 5000);
            Assert.Fail($"[STRESS-05] glass cannon (dps 1e7, tough 5e4) vs brick (dps 1e3, tough 1e8) in {steps} steps: glass alive={glassShip.IsValid}, brick alive={brickShip.IsValid} (survivors glass={g} brick={b}) -> alpha-strike vs attrition.");
        }

        [Test]
        public void S06_MirrorFleet_100v100()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            var blue = MakeFleet(s, s.Faction, "blue");
            var redf = MakeFleet(s, red, "red");
            for (int i = 0; i < 100; i++)
            {
                Stamp(s, s.Faction, blue, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "B" + i);
                Stamp(s, red, redf, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "R" + i);
            }
            var (bl, rd, steps) = Resolve(blue, redf, 5000);
            Assert.Fail($"[STRESS-06] 100v100 mirror fleet in {steps} steps: blue survivors={bl}, red survivors={rd} -> identical fleets, how even is it?");
        }

        [Test]
        public void S07_Swarm_vs_SuperCapital()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            var swarm = MakeFleet(s, s.Faction, "swarm");
            for (int i = 0; i < 150; i++)
                Stamp(s, s.Faction, swarm, 50_000, 100_000, 0.85, new WeaponProfile(WeaponClass.Railgun, 50_000, 50_000, 0.05, 5), "F" + i);
            var cap = MakeFleet(s, red, "cap");
            Stamp(s, red, cap, 5e6, 5e7, 0, new WeaponProfile(WeaponClass.Railgun, 5e6, 50_000, 0.05, 5), "SuperCap");
            var (sw, cp, steps) = Resolve(swarm, cap, 5000);
            Assert.Fail($"[STRESS-07] 150 evasive(0.85) fighters vs 1 super-capital (dps 5e6, tough 5e7) in {steps} steps: fighters survived={sw}/150, capital survived={cp} -> can a swarm overwhelm an uber-ship?");
        }

        [Test]
        public void S08_Doctrine_SwingsAFleetFight()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            var off = MakeFleet(s, s.Faction, "offensive");
            var def = MakeFleet(s, red, "plain");
            for (int i = 0; i < 50; i++)
            {
                Stamp(s, s.Faction, off, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "O" + i);
                Stamp(s, red, def, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "D" + i);
            }
            FleetDoctrine.TrySetDoctrine(off, new CombatDoctrineBlueprint { UniqueID = "aggro", DisplayName = "aggro", Family = "Offensive", FirepowerMult = 2.0, ToughnessMult = 1.0, CooldownSeconds = 0 }, off.StarSysDateTime);
            var (o, d, steps) = Resolve(off, def, 5000);
            Assert.Fail($"[STRESS-08] 50 offensive(x2 firepower doctrine) vs 50 identical plain ships in {steps} steps: offensive survivors={o}, plain survivors={d} -> how far does doctrine swing a fair fleet fight?");
        }

        [Test]
        public void S09_BeamVsRailgun_Fleet_vs_EvasiveScreen()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            // 30 beam ships vs a 60-fighter evasive screen.
            var beamFleet = MakeFleet(s, red, "beams");
            for (int i = 0; i < 30; i++) Stamp(s, red, beamFleet, 20_000, 1e9, 0, new WeaponProfile(WeaponClass.Beam, 20_000, 3e8, 0.95, 0.5), "Bm" + i);
            var screenA = Screen(s, s.Faction, 60, 0.85, 150_000, "sA");
            CombatEngagement.StartEngagement(beamFleet, screenA);
            for (int i = 0; i < 8 && screenA.HasDataBlob<FleetCombatStateDB>(); i++) CombatEngagement.StepEngagement(beamFleet, screenA, 5);
            int beamScreenLeft = CombatEngagement.GetFleetShips(screenA).Count;
            // 30 railgun ships of EQUAL dps vs an identical screen.
            var slugFleet = MakeFleet(s, red, "slugs");
            for (int i = 0; i < 30; i++) Stamp(s, red, slugFleet, 20_000, 1e9, 0, new WeaponProfile(WeaponClass.Railgun, 20_000, 50_000, 0.05, 5), "Sg" + i);
            var screenB = Screen(s, s.Faction, 60, 0.85, 150_000, "sB");
            CombatEngagement.StartEngagement(slugFleet, screenB);
            for (int i = 0; i < 8 && screenB.HasDataBlob<FleetCombatStateDB>(); i++) CombatEngagement.StepEngagement(slugFleet, screenB, 5);
            int slugScreenLeft = CombatEngagement.GetFleetShips(screenB).Count;
            Assert.Fail($"[STRESS-09] equal-firepower 30-ship fleets vs identical 60-fighter screens (8 steps): beams left {beamScreenLeft}/60 alive, railguns left {slugScreenLeft}/60 alive -> dodge at fleet scale.");
        }

        [Test]
        public void S10_SwarmVsCapital_BreakEvenSweep()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            var results = new List<string>();
            int breakeven = -1;
            foreach (int n in new[] { 5, 10, 25, 50, 100 })
            {
                var swarm = MakeFleet(s, s.Faction, "sw" + n);
                for (int i = 0; i < n; i++) Stamp(s, s.Faction, swarm, 50_000, 100_000, 0.85, new WeaponProfile(WeaponClass.Railgun, 50_000, 50_000, 0.05, 5), "f" + n + "_" + i);
                var cap = MakeFleet(s, red, "cap" + n);
                Stamp(s, red, cap, 4e6, 2.5e6, 0.1, new WeaponProfile(WeaponClass.Railgun, 4e6, 50_000, 0.05, 5), "Cap" + n);
                var (sw, cp, _) = Resolve(swarm, cap, 5000);
                results.Add($"N={n}->fighters {sw}, capital {cp}");
                if (breakeven < 0 && cp == 0 && sw > 0) breakeven = n;
            }
            Assert.Fail($"[STRESS-10] fighter-swarm vs 1 capital break-even sweep: {string.Join("; ", results)}. Smallest swarm that wins WITH survivors: N={breakeven}.");
        }
    }
}
