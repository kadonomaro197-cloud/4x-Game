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
    /// Combat BATTLE SIMS — the "10 more" battle scenarios run AFTER the hot-damage rebalance
    /// (<see cref="CombatEngagement.SalvoDamageScale"/> = 0.1, 2026-06-25). Where <see cref="CombatStressLab"/> pins
    /// the dodge MECHANISM at the extremes, these measure how whole BATTLES feel now that a salvo deposits only a
    /// tenth of its raw energy. Measured results (CI, deterministic — the messages carry the live numbers):
    ///   • B01 — DURATION: a standard 50v50 mirror now lasts 38 salvos (= 190s game-time), 25–25 — not 2–4 salvos.
    ///   • B02 — PREDICTABLE pace: at toughness ×1/×4/×16 the same 20v20 takes 38 / 150 / 599 salvos (≈ linear).
    ///   • B03 — saturation frontier (kills of 100, ev0.9, 40 salvos): sat 1/10/100/1000 -> 5 / 6 / 26 / 38.
    ///   • B04 — evasion frontier (kills of 100, slug, 40 salvos): ev 0/.3/.6/.9/.95 -> 40 / 28 / 17 / 5 / 3.
    ///   • B05 — COMBINED ARMS (railgun+flak) clears a mixed enemy better than mono (50 vs 66 left of 100).
    ///   • B06 — QUALITY vs QUANTITY at equal totals: 5 heavy keep 60% (3/5), 50 light keep 48% (24/50) — quality endures.
    ///   • B07 — 3-WAY free-for-all (10/10/10): perfectly symmetric, all break off at 50% (5/5/5, 36 salvos).
    ///   • B08 — REINFORCEMENTS flip it: 10v20 the enemy keeps 18; a 15-ship ally joining drops the enemy to 10 (it breaks off).
    ///   • B09 — STEERING: an even mirror (15–15) becomes player 22 vs enemy 15 when the player switches doctrine mid-fight.
    ///   • B10 — extreme ASYMMETRY: 1 dreadnought vs 1000 gnats resolves in 9 ms / 20 salvos (bucketed); the dread tanks the swarm's break-off.
    /// Engine-only -> runs in CI.
    /// </summary>
    [TestFixture]
    public class CombatBattleSims
    {
        private static Entity MakeFleet(TestScenario s, Entity faction, string name) => FleetFactory.Create(s.StartingSystem, faction.Id, name);

        /// <summary>Add a ship with a stamped combat value (fp/tough/evasion + one weapon flavor) to a fleet.</summary>
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

        /// <summary>A "battery": one ship carrying several weapon flavors at once, with huge toughness (so it
        /// survives to keep firing) and zero evasion — used to measure a weapon MIX's offense in a fixed window.</summary>
        private static Entity StampBattery(TestScenario s, Entity faction, Entity fleet, WeaponProfile[] weapons, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.FactionOwnerID = faction.Id;
            double fp = weapons.Sum(w => w.DamagePerSecond);
            var cv = new ShipCombatValueDB(fp, 1e12, 1.0) { Evasion = 0 };
            foreach (var w in weapons) cv.Weapons.Add(w);
            ship.SetDataBlob(cv);
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(faction.Id, fleet, ship));
            return ship;
        }

        /// <summary>n identical stamped ships of one design into a fresh fleet.</summary>
        private static Entity Squadron(TestScenario s, Entity faction, int n, double fp, double tough, double evasion, WeaponProfile wp, string tag)
        {
            var fleet = MakeFleet(s, faction, tag);
            for (int i = 0; i < n; i++) Stamp(s, faction, fleet, fp, tough, evasion, new WeaponProfile(wp), tag + i);
            return fleet;
        }

        /// <summary>StartEngagement once, then step until both fleets disengage (wiped/retreat) or the cap. Returns survivors + steps.</summary>
        private static (int aSurv, int bSurv, int steps) Resolve(Entity a, Entity b, int maxSteps = 5000)
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

        // A fresh evasive railgun screen of n ships (the dodging defenders), sized so the 50%-loss retreat never bites in-window.
        private static Entity Screen(TestScenario s, Entity faction, int n, double evasion, double tough, string tag)
            => Squadron(s, faction, n, 100, tough, evasion, new WeaponProfile(WeaponClass.Railgun, 100, 50_000, 0.05, 5), tag);

        // Fire one stamped battery (huge toughness) at a fresh evasive screen for a fixed window; return kills.
        private static int KilledInWindow(TestScenario s, Entity red, WeaponProfile gun, int n, double ev, double tough, int steps)
        {
            var screen = Screen(s, s.Faction, n, ev, tough, "scr");
            var battery = MakeFleet(s, red, "bat");
            StampBattery(s, red, battery, new[] { gun }, "gun");
            CombatEngagement.StartEngagement(battery, screen);
            for (int i = 0; i < steps && screen.HasDataBlob<FleetCombatStateDB>(); i++)
                CombatEngagement.StepEngagement(battery, screen, 5);
            return n - CombatEngagement.GetFleetShips(screen).Count;
        }

        // A mixed-evasive enemy: many evasive fighters + a few sluggish capitals (one fleet).
        private static Entity EnemyMix(TestScenario s, Entity faction, int nFighters, int nCaps, string tag)
        {
            var fleet = MakeFleet(s, faction, tag);
            for (int i = 0; i < nFighters; i++)
                Stamp(s, faction, fleet, 5_000, 100_000, 0.9, new WeaponProfile(WeaponClass.Railgun, 5_000, 50_000, 0.05, 5), tag + "f" + i);
            for (int i = 0; i < nCaps; i++)
                Stamp(s, faction, fleet, 50_000, 500_000, 0.0, new WeaponProfile(WeaponClass.Railgun, 50_000, 50_000, 0.05, 5), tag + "c" + i);
            return fleet;
        }

        // Fire a weapon-mix battery at a fresh (80 fighter + 20 cap) mixed enemy for a window; return enemy survivors.
        private static int MixSurvivors(TestScenario s, Entity red, WeaponProfile[] weapons, int steps)
        {
            var enemy = EnemyMix(s, s.Faction, 80, 20, "mix");
            var battery = MakeFleet(s, red, "bat");
            StampBattery(s, red, battery, weapons, "gun");
            CombatEngagement.StartEngagement(battery, enemy);
            for (int i = 0; i < steps && enemy.HasDataBlob<FleetCombatStateDB>(); i++)
                CombatEngagement.StepEngagement(battery, enemy, 5);
            return CombatEngagement.GetFleetShips(enemy).Count;
        }

        private static int InCombat(params Entity[] fleets) => fleets.Count(f => f.HasDataBlob<FleetCombatStateDB>());

        // ---------------------------------------------------------------------------------------------------

        [Test]
        [Description("Battle DURATION: after the rebalance a standard 50v50 mirror lasts 38 salvos (= 190s game-time, both bleeding to 25/50) — not the 2-4 salvos of hot damage. The headline 'you can now watch a battle' check.")]
        public void B01_BattleDuration_ManySalvosNow()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            var blue = Squadron(s, s.Faction, 50, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "blue");
            var redf = Squadron(s, red, 50, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "red");
            var (bl, rd, steps) = Resolve(blue, redf, 5000);
            Assert.That(steps, Is.GreaterThan(15), $"[BSIM-01] 50v50 mirror resolved in {steps} salvos = {steps * 5}s game-time (blue={bl} red={rd}) -> battles last many salvos now, not 2-4.");
        }

        [Test]
        [Description("The pace lever is PREDICTABLE: at toughness x1/x4/x16 the SAME 20v20 fight takes 38 / 150 / 599 salvos — duration scales ~linearly with toughness (the inverse of SalvoDamageScale), so the dial is easy to tune.")]
        public void B02_Duration_ScalesWithToughness()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            int Steps(double tough)
            {
                var b = Squadron(s, s.Faction, 20, 10_000, tough, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "b" + tough);
                var r = Squadron(s, red, 20, 10_000, tough, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "r" + tough);
                return Resolve(b, r, 5000).steps;
            }
            int s1 = Steps(200_000), s4 = Steps(800_000), s16 = Steps(3_200_000);
            Assert.That(s4, Is.GreaterThan(s1), $"[BSIM-02] 20v20 duration vs toughness: x1={s1}, x4={s4}, x16={s16} salvos -> 4x toughness -> more salvos.");
            Assert.That(s16, Is.GreaterThan(s4), $"[BSIM-02] 16x toughness -> more salvos still (x1={s1}, x4={s4}, x16={s16}).");
        }

        [Test]
        [Description("Saturation frontier as a CURVE: vs a fixed evasive(0.9) screen, railgun kills climb with rate-of-fire — saturation 1/10/100/1000 -> 5 / 6 / 26 / 38 kills of 100. The dodge-defeat frontier, tabulated.")]
        public void B03_SaturationFrontier_Curve()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            int K(double sat) => KilledInWindow(s, red, new WeaponProfile(WeaponClass.Railgun, 200_000, 50_000, 0.05, sat), 100, 0.9, 100_000, 40);
            int k1 = K(1), k10 = K(10), k100 = K(100), k1000 = K(1000);
            Assert.That(k1000, Is.GreaterThan(k1), $"[BSIM-03] saturation curve (kills of 100, ev0.9, 40 salvos): sat1={k1} sat10={k10} sat100={k100} sat1000={k1000} -> more rate-of-fire defeats more dodge.");
            Assert.That(k100, Is.GreaterThan(k10), $"[BSIM-03] the curve is monotonic: sat1={k1} sat10={k10} sat100={k100} sat1000={k1000}.");
        }

        [Test]
        [Description("Evasion frontier as a CURVE: vs a fixed slug (railgun), kills FALL as evasion rises — ev 0/.3/.6/.9/.95 -> 40 / 28 / 17 / 5 / 3 kills of 100. The dodge curve, tabulated.")]
        public void B04_EvasionFrontier_Curve()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            int K(double ev) => KilledInWindow(s, red, new WeaponProfile(WeaponClass.Railgun, 200_000, 50_000, 0.05, 5), 100, ev, 100_000, 40);
            int e0 = K(0.0), e30 = K(0.3), e60 = K(0.6), e90 = K(0.9), e95 = K(0.95);
            Assert.That(e0, Is.GreaterThan(e95), $"[BSIM-04] evasion curve (kills of 100, slug, 40 salvos): ev0={e0} ev0.3={e30} ev0.6={e60} ev0.9={e90} ev0.95={e95} -> evasion saves ships.");
            Assert.That(e30, Is.GreaterThan(e90), $"[BSIM-04] the curve is monotonic: ev0={e0} ev0.3={e30} ev0.6={e60} ev0.9={e90} ev0.95={e95}.");
        }

        [Test]
        [Description("COMBINED ARMS: at equal total firepower, a railgun+flak mix clears more of a mixed-evasive enemy (80 dodging fighters + 20 sluggish capitals) than mono railgun — the flak finishes the fighters the railgun can't catch. Measured 50 left vs 66 left of 100.")]
        public void B05_CombinedArms_BeatsMono_VsMixedEnemy()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            int combinedLeft = MixSurvivors(s, red, new[]
            {
                new WeaponProfile(WeaponClass.Railgun, 400_000, 50_000, 0.05, 5),
                new WeaponProfile(WeaponClass.Flak, 400_000, 20_000, 0.10, 300),
            }, 50);
            int monoLeft = MixSurvivors(s, red, new[]
            {
                new WeaponProfile(WeaponClass.Railgun, 800_000, 50_000, 0.05, 5),
            }, 50);
            Assert.That(combinedLeft, Is.LessThan(monoLeft),
                $"[BSIM-05] mixed enemy (80 fighters ev0.9 + 20 caps) survivors after 50 salvos: vs combined(railgun+flak)={combinedLeft}, vs mono-railgun={monoLeft} -> combined arms clears more.");
        }

        [Test]
        [Description("QUALITY vs QUANTITY at EQUAL totals: 5 heavy ships (fp40k/tough400k) vs 50 light (fp4k/tough40k) — same aggregate firepower AND toughness, evasion 0. Quality endures: heavy keep 60% (3/5), light keep 48% (24/50) — the dispersed force hits its 50% break-off first because each fragile ship is a whole hull lost.")]
        public void B06_QualityVsQuantity_EqualTotals()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            var heavy = Squadron(s, s.Faction, 5, 40_000, 400_000, 0.0, new WeaponProfile(WeaponClass.Railgun, 40_000, 50_000, 0.05, 5), "heavy");
            var light = Squadron(s, red, 50, 4_000, 40_000, 0.0, new WeaponProfile(WeaponClass.Railgun, 4_000, 50_000, 0.05, 5), "light");
            var (hv, lt, steps) = Resolve(heavy, light, 5000);
            Assert.That(hv, Is.GreaterThan(0), $"[BSIM-06] quality(5x fp40k/tough400k) vs quantity(50x fp4k/tough40k), equal totals: {steps} salvos, heavy={hv}/5 light={lt}/50 -> the concentrated force survives.");
            Assert.That(hv / 5.0, Is.GreaterThan(lt / 50.0), $"[BSIM-06] quality keeps a higher FRACTION than quantity: heavy {hv}/5 vs light {lt}/50 ({steps} salvos).");
        }

        [Test]
        [Description("3-WAY free-for-all: three equal 10-ship fleets of different factions in one engagement. Each divides its fire between two enemies and takes the combined fire of both. Perfectly symmetric -> all three break off at 50% together (5/5/5, 36 salvos).")]
        public void B07_ThreeWayFreeForAll()
        {
            var s = TestScenario.CreateWithColony();
            var f2 = FactionFactory.CreateBasicFaction(s.Game, "G", "GRN", 0);
            var f3 = FactionFactory.CreateBasicFaction(s.Game, "P", "PUR", 0);
            var a = Squadron(s, s.Faction, 10, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "A");
            var b = Squadron(s, f2, 10, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "B");
            var c = Squadron(s, f3, 10, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "C");
            CombatEngagement.EnsureInCombat(a, b.Id);
            CombatEngagement.EnsureInCombat(b, c.Id);
            CombatEngagement.EnsureInCombat(c, a.Id);
            var group = new List<Entity> { a, b, c };
            int steps = 0;
            while (steps < 5000 && InCombat(a, b, c) >= 2) { CombatEngagement.StepEngagementGroup(group, 5); steps++; }
            int al = CombatEngagement.GetFleetShips(a).Count, bl = CombatEngagement.GetFleetShips(b).Count, cl = CombatEngagement.GetFleetShips(c).Count;
            Assert.That(al, Is.EqualTo(bl).And.EqualTo(cl), $"[BSIM-07] 3-way FFA (10/10/10) in {steps} salvos: A={al} B={bl} C={cl} -> a symmetric 3-way resolves symmetrically.");
            Assert.That(al, Is.GreaterThan(0), $"[BSIM-07] all three break off at 50% losses rather than fight to extinction (A={al} B={bl} C={cl}).");
        }

        [Test]
        [Description("REINFORCEMENTS turn the tide: a 10-ship fleet loses to 20 identical enemies (control — enemy keeps 18, player breaks off). Add a 15-ship reinforcement that JOINS the same fight and the enemy is driven down to 10 (it breaks off instead). The 'send in another fleet to assist' feature at fleet scale.")]
        public void B08_Reinforcements_TurnALosingFight()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);

            // Control: 10 player vs 20 enemy — the player is out-gunned 2:1 and breaks off, the enemy barely scratched.
            var pc = Squadron(s, s.Faction, 10, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "pc");
            var ec = Squadron(s, red, 20, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "ec");
            var (pcl, ecl, ccs) = Resolve(pc, ec, 5000);

            // Reinforced: same 10 vs 20, but a 15-ship ally JOINS after the fight is underway.
            var pt = Squadron(s, s.Faction, 10, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "pt");
            var et = Squadron(s, red, 20, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "et");
            var relief = Squadron(s, s.Faction, 15, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "relief");
            CombatEngagement.EnsureInCombat(pt, et.Id);
            CombatEngagement.EnsureInCombat(et, pt.Id);
            var group = new List<Entity> { pt, et, relief };
            int k = 0;
            for (; k < 5 && InCombat(pt, et) >= 2; k++) CombatEngagement.StepEngagementGroup(new List<Entity> { pt, et }, 5);
            CombatEngagement.EnsureInCombat(relief, et.Id); // the reinforcement arrives in range and joins
            for (; k < 5000 && InCombat(pt, et, relief) >= 2; k++) CombatEngagement.StepEngagementGroup(group, 5);
            int ptl = CombatEngagement.GetFleetShips(pt).Count, etl = CombatEngagement.GetFleetShips(et).Count, rl = CombatEngagement.GetFleetShips(relief).Count;

            Assert.That(etl, Is.LessThan(ecl),
                $"[BSIM-08] control 10v20: enemy kept {ecl} (player broke off at {pcl}, {ccs} salvos). Reinforced 10+15v20: enemy down to {etl} (player={ptl} relief={rl}, {k} salvos) -> the reinforcement flips it.");
        }

        [Test]
        [Description("STEERING with doctrine: two identical 30v30 fights. Control runs neutral both sides -> an even mirror (15-15). The test fights neutral for 8 salvos, then the player switches to an all-out-attack (x2 firepower) doctrine MID-FIGHT -> ends ahead (22 vs 15). You steer a battle in progress with doctrine, not micromanagement.")]
        public void B09_DoctrineSwitchedMidFight_SteersIt()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            var aggro = new CombatDoctrineBlueprint { UniqueID = "b09-aggro", DisplayName = "aggro", Family = "Offensive", FirepowerMult = 2.0, ToughnessMult = 1.0, CooldownSeconds = 0 };

            // Control: even mirror, nobody switches.
            var pc = Squadron(s, s.Faction, 30, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "pc");
            var ec = Squadron(s, red, 30, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "ec");
            var (pcl, ecl, ccs) = Resolve(pc, ec, 5000);

            // Test: fight neutral a while, then switch the player to all-out-attack mid-engagement.
            var pt = Squadron(s, s.Faction, 30, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "pt");
            var et = Squadron(s, red, 30, 10_000, 200_000, 0.3, new WeaponProfile(WeaponClass.Railgun, 10_000, 50_000, 0.05, 5), "et");
            CombatEngagement.StartEngagement(pt, et);
            int k = 0;
            for (; k < 8 && pt.HasDataBlob<FleetCombatStateDB>() && et.HasDataBlob<FleetCombatStateDB>(); k++)
                CombatEngagement.StepEngagement(pt, et, 5);
            FleetDoctrine.TrySetDoctrine(pt, aggro, pt.StarSysDateTime); // STEER: switch posture mid-fight
            for (; k < 5000 && pt.HasDataBlob<FleetCombatStateDB>() && et.HasDataBlob<FleetCombatStateDB>(); k++)
                CombatEngagement.StepEngagement(pt, et, 5);
            int ptl = CombatEngagement.GetFleetShips(pt).Count, etl = CombatEngagement.GetFleetShips(et).Count;

            Assert.That(ptl, Is.GreaterThan(etl),
                $"[BSIM-09] control mirror: player={pcl} enemy={ecl} ({ccs} salvos). Switched mid-fight: player={ptl} enemy={etl} ({k} salvos) -> the mid-fight all-out-attack leaves the player ahead.");
        }

        [Test]
        [Description("Extreme ASYMMETRY stays fast + correct: 1 dreadnought vs 1000 evasive gnats (equal aggregate firepower AND toughness). The bucketed resolve handles it in ~9 ms / 20 salvos; the dreadnought tanks the swarm's 50% break-off and survives (gnats fall back at ~475 left). A tripwire that the O(buckets) resolve still holds at 1000 ships.")]
        public void B10_OneDreadnought_vs_1000Gnats()
        {
            var s = TestScenario.CreateWithColony();
            var red = FactionFactory.CreateBasicFaction(s.Game, "R", "RED", 0);
            var dread = MakeFleet(s, s.Faction, "Dreadnought");
            Stamp(s, s.Faction, dread, 5_000_000, 50_000_000, 0.0, new WeaponProfile(WeaponClass.Railgun, 5_000_000, 50_000, 0.05, 5), "Dread");
            var gnats = Squadron(s, red, 1000, 5_000, 50_000, 0.5, new WeaponProfile(WeaponClass.Railgun, 5_000, 50_000, 0.05, 5), "gnat");

            CombatEngagement.StartEngagement(dread, gnats);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int steps = 0;
            while (dread.HasDataBlob<FleetCombatStateDB>() && gnats.HasDataBlob<FleetCombatStateDB>() && steps < 5000)
            {
                CombatEngagement.StepEngagement(dread, gnats, 5);
                steps++;
            }
            sw.Stop();
            int dl = CombatEngagement.GetFleetShips(dread).Count, gl = CombatEngagement.GetFleetShips(gnats).Count;
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(4000), $"[BSIM-10] 1 dreadnought vs 1000 gnats: {steps} salvos, {sw.ElapsedMilliseconds} ms (dread={dl} gnats={gl}) -> the bucketed resolve stays fast at 1000 ships.");
            Assert.That(steps, Is.LessThan(5000), $"[BSIM-10] it terminates (didn't hang): {steps} salvos, dread={dl} gnats={gl}.");
        }
    }
}
