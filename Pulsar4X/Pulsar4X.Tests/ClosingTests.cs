using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase 1 of the closing-fight model (docs/FLEET-COMBAT-CLOSING-DESIGN.md): combat becomes a CLOSING fight —
    /// a weapon only fires if it REACHES the current gap, and the gap closes toward the faster side's preferred
    /// range. The decision this proves real: STANDOFF vs BRAWL — a faster long-range fleet kites a slower short-range
    /// one and takes nothing back; a faster brawler forces the merge. All behind <c>EnableClosingRange</c> (default
    /// off), so every existing combat fixture is byte-identical; these fixtures opt in and reset it in finally.
    /// </summary>
    [TestFixture]
    public class ClosingTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[closing] " + m);

        private static Entity MakeFleet(TestScenario s, Entity faction, string name)
            => FleetFactory.Create(s.StartingSystem, faction.Id, name);

        /// <summary>Build a corvette under the player, flip its owner, assign it to the fleet, and stamp a CONTROLLED
        /// combat value (one beam of the given range; known firepower/toughness/evasion) so the gauge is deterministic.</summary>
        private static Entity AddShip(TestScenario s, Entity owner, Entity fleet, double range_m, double evasion,
            double firepower = 1e6, double toughness = 1e7)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns["default-ship-design-test-corvette"];
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, "ship");
            ship.FactionOwnerID = owner.Id;
            s.Game.OrderHandler.HandleOrder(FleetOrder.AssignShip(owner.Id, fleet, ship));

            var cv = new ShipCombatValueDB(firepower, toughness, 1.0);
            cv.Evasion = evasion;
            cv.Weapons = new List<WeaponProfile> { new WeaponProfile(WeaponClass.Beam, firepower, 3e8, 1.0, 1.0, range_m) };
            ship.SetDataBlob(cv);
            return ship;
        }

        private static double Pool(Entity fleet)
            => fleet.TryGetDataBlob<FleetCombatStateDB>(out var st) ? st.DamageTakenPool : -1;

        /// <summary>Override the engagement gap + maneuver budget directly (combat values stamped, so the gauge is
        /// deterministic and independent of seeding/fuel).</summary>
        private static void Set(Entity fleet, double gap, double budget)
        {
            var st = fleet.GetDataBlob<FleetCombatStateDB>();
            st.Separation_m = gap;
            st.ManeuverBudget = budget;
        }

        // ─── The heart: range gate ─────────────────────────────────────────────────────────────────────────────

        [Test]
        [Description("At a gap of 50 km, a 100-km-range fleet hits the enemy while a 1-km-range fleet cannot reach " +
                     "back — the kited side deals ZERO. The core of the closing model: a weapon only fires if it " +
                     "reaches the gap. (Closing frozen here to isolate the range gate from the closing dynamics.)")]
        public void RangeGate_OnlyWeaponsThatReachTheGapFire()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var longFleet = MakeFleet(s, s.Faction, "Long");
            var shortFleet = MakeFleet(s, reds, "Short");
            AddShip(s, s.Faction, longFleet, range_m: 100_000, evasion: 0);
            AddShip(s, reds, shortFleet, range_m: 1_000, evasion: 0);

            CombatEngagement.EnableClosingRange = true;
            CombatEngagement.ClosingSpeedScale_mps = 0;   // FREEZE the gap — isolate the range gate
            try
            {
                CombatEngagement.StartEngagement(longFleet, shortFleet);
                longFleet.GetDataBlob<FleetCombatStateDB>().Separation_m = 50_000;
                shortFleet.GetDataBlob<FleetCombatStateDB>().Separation_m = 50_000;

                CombatEngagement.StepEngagement(longFleet, shortFleet, 5.0);

                Log($"after one salvo at 50 km gap: short-fleet pool={Pool(shortFleet):E2}  long-fleet pool={Pool(longFleet):E2}");
                Assert.That(Pool(shortFleet), Is.GreaterThan(0), "the 100-km fleet reaches across the 50-km gap and hits");
                Assert.That(Pool(longFleet), Is.EqualTo(0), "the 1-km fleet can't reach 50 km — it deals nothing (kited)");
            }
            finally
            {
                CombatEngagement.EnableClosingRange = false;
                CombatEngagement.ClosingSpeedScale_mps = 100_000.0;
            }
        }

        [Test]
        [Description("Determinism: the same closing matchup resolves identically twice — fast-forward must equal " +
                     "watch (the engine has no wall-clock or RNG in the pool math).")]
        public void RangeGate_IsDeterministic()
        {
            double Run()
            {
                var s = TestScenario.CreateWithColony();
                var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
                var lf = MakeFleet(s, s.Faction, "Long");
                var sf = MakeFleet(s, reds, "Short");
                AddShip(s, s.Faction, lf, 100_000, 0);
                AddShip(s, reds, sf, 1_000, 0);
                CombatEngagement.EnableClosingRange = true;
                CombatEngagement.ClosingSpeedScale_mps = 0;
                try
                {
                    CombatEngagement.StartEngagement(lf, sf);
                    lf.GetDataBlob<FleetCombatStateDB>().Separation_m = 50_000;
                    sf.GetDataBlob<FleetCombatStateDB>().Separation_m = 50_000;
                    for (int i = 0; i < 3; i++) CombatEngagement.StepEngagement(lf, sf, 5.0);
                    return Pool(sf);
                }
                finally { CombatEngagement.EnableClosingRange = false; CombatEngagement.ClosingSpeedScale_mps = 100_000.0; }
            }

            double a = Run(), b = Run();
            Log($"determinism: run A short-pool={a:E3}  run B={b:E3}");
            Assert.That(b, Is.EqualTo(a).Within(1e-9).Percent, "the same matchup must resolve to the same pool every time");
        }

        [Test]
        [Description("Flag OFF: the range gate is a no-op — the short-range fleet's fire is NOT gated and it deals " +
                     "damage despite the 50-km gap. Proves closing is opt-in and the legacy resolve is untouched.")]
        public void RangeGate_Off_ShortRangeFleetStillFires()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var longFleet = MakeFleet(s, s.Faction, "Long");
            var shortFleet = MakeFleet(s, reds, "Short");
            AddShip(s, s.Faction, longFleet, 100_000, 0);
            AddShip(s, reds, shortFleet, 1_000, 0);

            // EnableClosingRange stays FALSE.
            CombatEngagement.StartEngagement(longFleet, shortFleet);
            // Even if a stale separation were present, flag-off means SeparationOf() returns 0 → no gating.
            CombatEngagement.StepEngagement(longFleet, shortFleet, 5.0);

            Log($"flag off: long-fleet pool={Pool(longFleet):E2} (short fleet's 1-km gun is NOT range-gated)");
            Assert.That(Pool(longFleet), Is.GreaterThan(0), "with closing off, the short-range fleet fires as before — range ignored");
        }

        // ─── The closing dynamics: who dictates the range ──────────────────────────────────────────────────────

        [Test]
        [Description("The FASTER (more maneuverable) side dictates the range. A fast long-range fleet OPENS the gap " +
                     "toward its own range (kites); a fast short-range fleet CLOSES it (forces the merge). Tests the " +
                     "closing advance + that maneuverability picks the controller.")]
        public void Closing_FasterSideDictatesTheRange()
        {
            // Case A — fast LONG-range fleet kites: gap should GROW toward its 100-km range.
            {
                var s = TestScenario.CreateWithColony();
                var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
                var fastLong = MakeFleet(s, s.Faction, "FastLong");
                var slowShort = MakeFleet(s, reds, "SlowShort");
                AddShip(s, s.Faction, fastLong, range_m: 100_000, evasion: 0.9);   // fast (high evasion) + long
                AddShip(s, reds, slowShort, range_m: 1_000, evasion: 0.1);         // slow + short
                CombatEngagement.EnableClosingRange = true;
                CombatEngagement.ClosingSpeedScale_mps = 100_000.0;
                try
                {
                    CombatEngagement.StartEngagement(fastLong, slowShort);
                    Set(fastLong, gap: 50_000, budget: 1e9);   // budget so P2's gate doesn't freeze the maneuver
                    Set(slowShort, gap: 50_000, budget: 1e9);
                    CombatEngagement.StepEngagement(fastLong, slowShort, 5.0);
                    double gap = fastLong.GetDataBlob<FleetCombatStateDB>().Separation_m;
                    Log($"fast long-range fleet: gap 50,000 -> {gap:N0} (expect GROW toward its 100 km range)");
                    Assert.That(gap, Is.GreaterThan(50_000), "the faster long-range fleet opens the range — it kites");
                }
                finally { CombatEngagement.EnableClosingRange = false; CombatEngagement.ClosingSpeedScale_mps = 100_000.0; }
            }

            // Case B — fast SHORT-range fleet brawls: gap should SHRINK toward its 1-km range.
            {
                var s = TestScenario.CreateWithColony();
                var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
                var fastShort = MakeFleet(s, s.Faction, "FastShort");
                var slowLong = MakeFleet(s, reds, "SlowLong");
                AddShip(s, s.Faction, fastShort, range_m: 1_000, evasion: 0.9);    // fast + short
                AddShip(s, reds, slowLong, range_m: 100_000, evasion: 0.1);        // slow + long
                CombatEngagement.EnableClosingRange = true;
                CombatEngagement.ClosingSpeedScale_mps = 100_000.0;
                try
                {
                    CombatEngagement.StartEngagement(fastShort, slowLong);
                    Set(fastShort, gap: 50_000, budget: 1e9);
                    Set(slowLong, gap: 50_000, budget: 1e9);
                    CombatEngagement.StepEngagement(fastShort, slowLong, 5.0);
                    double gap = fastShort.GetDataBlob<FleetCombatStateDB>().Separation_m;
                    Log($"fast short-range fleet: gap 50,000 -> {gap:N0} (expect SHRINK toward its 1 km range)");
                    Assert.That(gap, Is.LessThan(50_000), "the faster short-range fleet closes the range — it forces the merge");
                }
                finally { CombatEngagement.EnableClosingRange = false; CombatEngagement.ClosingSpeedScale_mps = 100_000.0; }
            }
        }

        // ─── Phase 2 — kiting has a clock ──────────────────────────────────────────────────────────────────────

        [Test]
        [Description("Phase 2: a fast long-range kiter with a LIMITED maneuver budget can't hold the enemy off " +
                     "forever. When its budget runs dry it stops dictating the range, and the slower short-range " +
                     "enemy closes the gap into its OWN range — the kiter gets caught. The counter to P1's kite.")]
        public void Kiting_RunsOutOfBudget_TheEnemyCloses()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var kiter = MakeFleet(s, s.Faction, "Kiter");
            var brawler = MakeFleet(s, reds, "Brawler");
            AddShip(s, s.Faction, kiter, range_m: 100_000, evasion: 0.9);   // fast + long
            AddShip(s, reds, brawler, range_m: 5_000, evasion: 0.5);        // slower + short, but persistent

            CombatEngagement.EnableClosingRange = true;
            CombatEngagement.ClosingSpeedScale_mps = 100_000.0;
            CombatEngagement.ManeuverBurnRate = 5.0;
            try
            {
                CombatEngagement.StartEngagement(kiter, brawler);
                Set(kiter, gap: 50_000, budget: 100);     // a SHORT kiting clock — runs dry in a few steps
                Set(brawler, gap: 50_000, budget: 1e9);   // the brawler can maneuver all day

                double gapStart = kiter.GetDataBlob<FleetCombatStateDB>().Separation_m;
                for (int i = 0; i < 12; i++) CombatEngagement.StepEngagement(kiter, brawler, 5.0);
                double gapEnd = kiter.GetDataBlob<FleetCombatStateDB>().Separation_m;

                Log($"kiter gap: start {gapStart:N0} -> end {gapEnd:N0} (brawler range 5,000 — the gap should collapse to it)");
                Assert.That(gapEnd, Is.LessThanOrEqualTo(5_000 + 1).And.GreaterThanOrEqualTo(0),
                    "once the kiter's budget runs dry, the brawler closes the gap to its 5 km range — you can't kite forever");
            }
            finally
            {
                CombatEngagement.EnableClosingRange = false;
                CombatEngagement.ClosingSpeedScale_mps = 100_000.0;
                CombatEngagement.ManeuverBurnRate = 5.0;
            }
        }
    }
}
