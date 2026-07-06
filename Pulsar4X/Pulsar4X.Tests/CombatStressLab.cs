using System;
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
    /// Combat STRESS LAB — push the ends of weapon design and fleet scale and pin down what the dodge resolver
    /// actually does, now at the REBALANCED combat pace (<see cref="CombatEngagement.SalvoDamageScale"/> = 0.1 —
    /// fights last many salvos, not 2-4). The assertions encode the measured DIRECTION; the messages carry the real
    /// numbers so a regression shows exactly how the behaviour shifted. Findings (2026-06-25, post-rebalance):
    ///   • Three independent ways to defeat evasion: high SATURATION (S01: 38 vs 5 of 100), high VELOCITY (S03: 39
    ///     vs 5), and a beam's both. A slow flak is near-useless (S02: 34 vs 7); nothing is untouchable (S04: 39 vs
    ///     3 of MAX-evasion 0.95 — the saturation floor always lands some).
    ///   • Alpha-strike still beats attrition (S05). Identical fleets resolve EXACTLY even (S06: 50–50 of 100, 39
    ///     salvos); doctrine ×2 swings a fair fight ~1.6:1 (S08: 40 vs 25); dodge scales to fleets (S09: railguns
    ///     leave 85/100 of an evasive screen, equal-firepower beams leave 49/100).
    ///   • Exchange ratio: a capital is worth ~25–50 of these fighters (S10, break-even N=50). The rebalance made
    ///     RETREAT bite: a super-capital now tanks long enough that a 150-fighter swarm would break off at 50%
    ///     losses BEFORE killing it — it takes ~400 to overwhelm it (S07). At hot damage the swarm wiped it in one
    ///     volley, before anyone could retreat.
    /// Engine-only -> runs in CI.
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

        // An evasive railgun screen of n ships. Sized large (n=100) so the 50%-loss break-off does not cap the
        // dodge comparisons at the rebalanced pace (a smaller screen would just retreat at half losses).
        private static Entity Screen(TestScenario s, Entity faction, int n, double evasion, double tough, string tag)
        {
            var fleet = MakeFleet(s, faction, tag);
            for (int i = 0; i < n; i++)
                Stamp(s, faction, fleet, 100, tough, evasion, new WeaponProfile(100, 50_000, 0.05, 5), tag + i);
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
        [Description("Rate of fire defeats evasion: a high-saturation (spinal-slug) railgun lands on nimble fighters that dodge a normal railgun. Measured 38 vs 5 of 100 (ev0.9, 40 salvos).")]
        public void S01_RateOfFire_DefeatsEvasion()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            int hi = Killed(s, red, new WeaponProfile(200_000, 50_000, 0.05, 1000), 100, 0.9, 100_000, 40);
            int lo = Killed(s, red, new WeaponProfile(200_000, 50_000, 0.05, 5), 100, 0.9, 100_000, 40);
            Assert.That(hi, Is.GreaterThan(lo), $"[STRESS-01] vs 100 evasive(0.9) fighters/40 steps: spinal slug (saturation 1000) killed {hi}, normal railgun (saturation 5) killed {lo} -> rate-of-fire defeats the dodge.");
        }

        [Test]
        [Description("A slow flak cannon is nearly useless: fast flak (high saturation) kills far more of an evasive screen than a 1/min flak. Measured 34 vs 7 of 100 (ev0.9, 40 salvos).")]
        public void S02_SlowFlak_IsUseless()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            int fast = Killed(s, red, new WeaponProfile(200_000, 20_000, 0.10, 300), 100, 0.9, 100_000, 40);
            int slow = Killed(s, red, new WeaponProfile(200_000, 20_000, 0.10, 0.02), 100, 0.9, 100_000, 40);
            Assert.That(fast, Is.GreaterThan(slow), $"[STRESS-02] vs 100 evasive(0.9) fighters/40 steps: fast flak (saturation 300) killed {fast}, a 1/min flak (saturation 0.02) killed {slow} -> slow flak is useless.");
        }

        [Test]
        [Description("Muzzle velocity defeats evasion: a near-light railgun lands on nimble fighters that dodge a normal-velocity railgun. Measured 39 vs 5 of 100 (ev0.9, 40 salvos).")]
        public void S03_MuzzleVelocity_DefeatsEvasion()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            int light = Killed(s, red, new WeaponProfile(200_000, 3e8, 0.05, 5), 100, 0.9, 100_000, 40);
            int slow = Killed(s, red, new WeaponProfile(200_000, 50_000, 0.05, 5), 100, 0.9, 100_000, 40);
            Assert.That(light, Is.GreaterThan(slow), $"[STRESS-03] vs 100 evasive(0.9) fighters/40 steps: near-light railgun (vel 3e8) killed {light}, normal railgun (vel 5e4) killed {slow} -> muzzle velocity defeats the dodge.");
        }

        [Test]
        [Description("Nothing is truly untouchable: extreme-saturation flak wipes even max-evasion (0.95) fighters; the floor means even a normal slug grinds them down over time. Measured 39 vs 3 of 100 (ev0.95, 16 salvos).")]
        public void S04_SaturationFloor_NothingIsUntouchable()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            int extreme = Killed(s, red, new WeaponProfile(500_000, 20_000, 0.10, 100_000), 100, 0.95, 100_000, 16);
            int normalSlug = Killed(s, red, new WeaponProfile(500_000, 50_000, 0.05, 5), 100, 0.95, 100_000, 16);
            Assert.That(extreme, Is.GreaterThan(normalSlug), $"[STRESS-04] vs 100 MAX-evasion(0.95) fighters/16 steps: extreme-saturation flak killed {extreme}, a normal slug killed {normalSlug} -> the floor means nothing is untouchable.");
        }

        [Test]
        [Description("Alpha strike beats attrition: a glass cannon (huge dps, paper hull) destroys a brick (tiny dps, huge hull) before the brick's trickle matters. ~20 salvos now (was instant at hot damage).")]
        public void S05_GlassCannon_vs_Brick()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            var glass = MakeFleet(s, s.Faction, "glass");
            var glassShip = Stamp(s, s.Faction, glass, 1e7, 50_000, 0, new WeaponProfile(1e7, 3e8, 0.95, 0.5), "Glass");
            var brick = MakeFleet(s, red, "brick");
            var brickShip = Stamp(s, red, brick, 1_000, 1e8, 0, new WeaponProfile(1_000, 3e8, 0.95, 0.5), "Brick");
            var (g, b, steps) = Resolve(glass, brick, 5000);
            Assert.That(brickShip.IsValid, Is.False, $"[STRESS-05] glass cannon vs brick in {steps} steps: glass alive={glassShip.IsValid}, brick alive={brickShip.IsValid} -> alpha-strike kills the brick.");
            Assert.That(glassShip.IsValid, Is.True, "the glass cannon outruns the brick's attrition and survives");
        }

        [Test]
        [Description("Identical fleets resolve EXACTLY even (no first-mover advantage), both bleeding to exactly half before breaking off. Measured 50 vs 50 of 100 in 39 salvos (the slower pace lands them right on the 50% retreat line).")]
        public void S06_MirrorFleet_100v100()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            var blue = MakeFleet(s, s.Faction, "blue");
            var redf = MakeFleet(s, red, "red");
            for (int i = 0; i < 100; i++)
            {
                Stamp(s, s.Faction, blue, 10_000, 200_000, 0.3, new WeaponProfile(10_000, 50_000, 0.05, 5), "B" + i);
                Stamp(s, red, redf, 10_000, 200_000, 0.3, new WeaponProfile(10_000, 50_000, 0.05, 5), "R" + i);
            }
            var (bl, rd, steps) = Resolve(blue, redf, 5000);
            Assert.That(Math.Abs(bl - rd), Is.LessThanOrEqualTo(4), $"[STRESS-06] 100v100 mirror in {steps} steps: blue={bl}, red={rd} -> a fair fight is near-perfectly even.");
            Assert.That(bl, Is.LessThan(100), "both sides take real attrition (neither walks away whole)");
        }

        [Test]
        [Description("Numbers overwhelm a monster — and the rebalance RAISED the bar. At hot damage 150 fighters wiped this super-capital in one volley; now it tanks ~10x longer, so a 150-swarm would break off at 50% losses first. It takes ~400 evasive fighters to overwhelm it (losing only ~30 doing it).")]
        public void S07_Swarm_vs_SuperCapital()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            var swarm = MakeFleet(s, s.Faction, "swarm");
            for (int i = 0; i < 400; i++)
                Stamp(s, s.Faction, swarm, 50_000, 100_000, 0.85, new WeaponProfile(50_000, 50_000, 0.05, 5), "F" + i);
            var cap = MakeFleet(s, red, "cap");
            Stamp(s, red, cap, 5e6, 5e7, 0, new WeaponProfile(5e6, 50_000, 0.05, 5), "SuperCap");
            var (sw, cp, steps) = Resolve(swarm, cap, 5000);
            Assert.That(cp, Is.EqualTo(0), $"[STRESS-07] 400 evasive(0.85) fighters vs 1 super-capital in {steps} steps: fighters left={sw}/400, capital left={cp} -> a big enough swarm overwhelms it.");
            Assert.That(sw, Is.GreaterThan(0), "but the swarm wins at heavy cost (a brutal exchange ratio)");
        }

        [Test]
        [Description("Doctrine is a real lever: a x2-firepower offensive doctrine turns a 50v50 coin-flip into a ~1.6:1 win (the loser breaks off at 50% losses). Measured 40 vs 25 in 16 salvos.")]
        public void S08_Doctrine_SwingsAFleetFight()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            var off = MakeFleet(s, s.Faction, "offensive");
            var def = MakeFleet(s, red, "plain");
            for (int i = 0; i < 50; i++)
            {
                Stamp(s, s.Faction, off, 10_000, 200_000, 0.3, new WeaponProfile(10_000, 50_000, 0.05, 5), "O" + i);
                Stamp(s, red, def, 10_000, 200_000, 0.3, new WeaponProfile(10_000, 50_000, 0.05, 5), "D" + i);
            }
            FleetDoctrine.TrySetDoctrine(off, new CombatDoctrineBlueprint { UniqueID = "aggro", DisplayName = "aggro", Family = "Offensive", FirepowerMult = 2.0, ToughnessMult = 1.0, CooldownSeconds = 0 }, off.StarSysDateTime);
            var (o, d, steps) = Resolve(off, def, 5000);
            Assert.That(o, Is.GreaterThan(d), $"[STRESS-08] 50 offensive(x2 doctrine) vs 50 plain in {steps} steps: offensive left={o}, plain left={d} -> doctrine swings a fair fleet fight.");
        }

        [Test]
        [Description("Dodge scales to fleets: equal-firepower 30-ship fleets vs identical 100-fighter screens — the railgun (dodgeable) leaves far more of the screen alive than the beam. Measured railguns 85/100, beams 49/100 (40 salvos).")]
        public void S09_BeamVsRailgun_Fleet_vs_EvasiveScreen()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            var beamFleet = MakeFleet(s, red, "beams");
            for (int i = 0; i < 30; i++) Stamp(s, red, beamFleet, 20_000, 1e9, 0, new WeaponProfile(20_000, 3e8, 0.95, 0.5), "Bm" + i);
            var screenA = Screen(s, s.Faction, 100, 0.85, 150_000, "sA");
            CombatEngagement.StartEngagement(beamFleet, screenA);
            for (int i = 0; i < 40 && screenA.HasDataBlob<FleetCombatStateDB>(); i++) CombatEngagement.StepEngagement(beamFleet, screenA, 5);
            int beamScreenLeft = CombatEngagement.GetFleetShips(screenA).Count;
            var slugFleet = MakeFleet(s, red, "slugs");
            for (int i = 0; i < 30; i++) Stamp(s, red, slugFleet, 20_000, 1e9, 0, new WeaponProfile(20_000, 50_000, 0.05, 5), "Sg" + i);
            var screenB = Screen(s, s.Faction, 100, 0.85, 150_000, "sB");
            CombatEngagement.StartEngagement(slugFleet, screenB);
            for (int i = 0; i < 40 && screenB.HasDataBlob<FleetCombatStateDB>(); i++) CombatEngagement.StepEngagement(slugFleet, screenB, 5);
            int slugScreenLeft = CombatEngagement.GetFleetShips(screenB).Count;
            Assert.That(slugScreenLeft, Is.GreaterThan(beamScreenLeft), $"[STRESS-09] equal-firepower 30-ship fleets vs identical 100-fighter screens/40 steps: beams left {beamScreenLeft}/100, railguns left {slugScreenLeft}/100 -> dodge at fleet scale.");
        }

        [Test]
        [Description("Swarm-vs-capital exchange ratio: a sweep finds one capital is worth ~25-50 of these fighters. Measured break-even (win WITH survivors) at N=50; at N=25 the capital still survives.")]
        public void S10_SwarmVsCapital_BreakEvenSweep()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            var results = new List<string>();
            int breakeven = -1;
            foreach (int n in new[] { 5, 10, 25, 50, 100 })
            {
                var swarm = MakeFleet(s, s.Faction, "sw" + n);
                for (int i = 0; i < n; i++) Stamp(s, s.Faction, swarm, 50_000, 100_000, 0.85, new WeaponProfile(50_000, 50_000, 0.05, 5), "f" + n + "_" + i);
                var cap = MakeFleet(s, red, "cap" + n);
                Stamp(s, red, cap, 4e6, 2.5e6, 0.1, new WeaponProfile(4e6, 50_000, 0.05, 5), "Cap" + n);
                var (sw, cp, st) = Resolve(swarm, cap, 5000);
                results.Add($"N={n}->fighters {sw}, capital {cp} in {st} steps");
                if (breakeven < 0 && cp == 0 && sw > 0) breakeven = n;
            }
            Assert.That(breakeven, Is.GreaterThan(0).And.LessThanOrEqualTo(100),
                $"[STRESS-10] swarm-vs-capital sweep: {string.Join("; ", results)}. Smallest swarm that wins WITH survivors: N={breakeven} -> a capital is worth ~25-50 of these fighters.");
        }
    }
}
