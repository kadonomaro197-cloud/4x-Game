using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Storage;
using Pulsar4X.Datablobs;
using Pulsar4X.Industry;
using Pulsar4X.Components;
using Pulsar4X.Galaxy;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The first gauge on the economy board. Reads the starting colony's economy state, advances a game-year
    /// on the scenario harness, and reads it again — printing installed infrastructure, cargo, and deposits to
    /// the runner output so we can learn what "normal" actually is.
    ///
    /// Part readout, part regression sensor. It prints the full economy state (so we keep learning 'normal')
    /// AND asserts the two things we've now established must hold: cargo mass stays non-negative, and the planet's
    /// deposits deplete over a year (the mine does its job). Instrument before you theorize.
    ///
    /// 2026-06-23 — chased "the mine does zero work over a game-year" to ground with this gauge. The mining chain
    /// was correct all along (ResourcesPerEconTick=15, BaseMiningRate=15, ActualMiningRate=15, efficiency 100%);
    /// the real fault was the colony's star system sitting in Stasis, which MasterTimePulse skips entirely — so
    /// nothing processed and the readout came back byte-for-byte identical. The harness now promotes the system
    /// (TestScenario → Foreground), the mine extracts 1 unit/mineral/day (water/regolith/rare-earth go 0→365),
    /// and the deposit-depletion assertion below now guards against any re-freeze or mining break.
    /// </summary>
    [TestFixture]
    public class EconomyReadoutTests
    {
        private static void Log(string msg) => TestContext.Progress.WriteLine("[econ] " + msg);

        [Test]
        [Description("Read the starting colony economy, advance one game-year, and report what the economy did.")]
        public void Economy_BaselineReadout_OverOneYear()
        {
            var s = TestScenario.CreateWithColony();

            Log("================ STARTING COLONY ================");
            ReportInstallations(s);
            ReportInfrastructure(s);
            ReportMining(s, "start");
            double massStart = ReportCargo(s, "start");
            long depositsStart = ReportDeposits(s);

            s.AdvanceTime(TimeSpan.FromDays(365));

            Log("================ AFTER 1 GAME-YEAR =============");
            ReportMining(s, "end");
            double massEnd = ReportCargo(s, "end");
            long depositsEnd = ReportDeposits(s);

            Log($">>> Colony cargo total mass: {massStart:N1} -> {massEnd:N1}   (delta {massEnd - massStart:N1})");
            Log($">>> Planet deposits total:   {depositsStart:N0} -> {depositsEnd:N0}   (mined {depositsStart - depositsEnd:N0})");

            // Storage accounting invariant.
            Assert.That(massEnd, Is.GreaterThanOrEqualTo(0),
                "Colony cargo mass went negative across a game-year — storage accounting is broken.");

            // The mine's ONE job: pull minerals out of the planet. Over a game-year the deposits MUST shrink.
            // This is the regression sensor for the bug that started this whole chase — when the colony's system
            // was stuck in Stasis the deposits came back byte-for-byte identical. If that (or any mining break)
            // recurs, this goes red. First thing to check on failure: s.StartingSystem.ActivityState != Stasis.
            Assert.That(depositsEnd, Is.LessThan(depositsStart),
                "Planet mineral deposits did not deplete over a game-year — the mine did no work. " +
                "Check StartingSystem.ActivityState (Stasis = system not processed) and the mining-chain rates above.");
        }

        private static void ReportInstallations(TestScenario s)
        {
            if (!s.Colony.TryGetDataBlob<ComponentInstancesDB>(out var comps))
            {
                Log("  installations: (no ComponentInstancesDB on colony)");
                return;
            }
            Log($"  installed components: {comps.DesignsAndComponentCount.Count} design(s)");
            foreach (var kv in comps.DesignsAndComponentCount)
                Log($"    {kv.Key.Name,-32} x{kv.Value}");
        }

        /// <summary>
        /// Infrastructure is the multiplier on ALL colony production (MineResources scales its output by
        /// InfrastructureProcessor.GetEfficiency). If efficiency is ~0 the colony mines nothing even when the
        /// rates are correct — so read it, plus the body gravity/pressure the infra tolerance gate is checked against.
        /// </summary>
        private static void ReportInfrastructure(TestScenario s)
        {
            // If the system is in Stasis, MasterTimePulse skips it entirely — NOTHING processes, no matter how
            // far the clock advances. This is the first thing to check when "the economy did nothing."
            Log($"  system activity: {s.StartingSystem.ActivityState}  (Stasis = not processed by the time loop)");

            if (s.Colony.TryGetDataBlob<InfrastructureDB>(out var infra))
                Log($"  infrastructure: provided={infra.CapacityProvided}  required={infra.CapacityRequired}  " +
                    $"available={infra.CapacityAvailable}  efficiency={infra.Efficiency:P1}");
            else
                Log("  infrastructure: (no InfrastructureDB on colony)");

            double grav = double.NaN, press = double.NaN;
            if (s.StartingBody.TryGetDataBlob<SystemBodyInfoDB>(out var bodyInfo)) grav = bodyInfo.Gravity;
            if (s.StartingBody.TryGetDataBlob<AtmosphereDB>(out var atmo)) press = atmo.Pressure;
            Log($"  body: gravity={grav:F2} m/s^2  pressure={press:F3} atm  (infra tolerance is gravity 8.8-10.8, pressure 0.9-1.1)");
        }

        /// <summary>
        /// The mining-chain gauge. Walks the chain the way MineResourcesProcessor does:
        ///   mine component -> MineResourcesAtbDB.ResourcesPerEconTick -> MiningDB.BaseMiningRate -> ActualMiningRate.
        /// Whichever of these is empty is where mining breaks.
        /// </summary>
        private static void ReportMining(TestScenario s, string label)
        {
            if (!s.Colony.TryGetDataBlob<MiningDB>(out var mining))
            {
                Log($"  mining ({label}): (no MiningDB on colony)");
                return;
            }

            Log($"  mining ({label}): NumberOfMines={mining.NumberOfMines}  " +
                $"BaseMiningRate={mining.BaseMiningRate.Count} entries (sum {Sum(mining.BaseMiningRate)})  " +
                $"ActualMiningRate={mining.ActualMiningRate.Count} entries (sum {Sum(mining.ActualMiningRate)})");

            foreach (var kv in mining.BaseMiningRate)
                Log($"      base   mineralId {kv.Key,-8} rate {kv.Value}");
            foreach (var kv in mining.ActualMiningRate)
                Log($"      actual mineralId {kv.Key,-8} rate {kv.Value}");

            // The source of those rates: does the installed mine design actually carry a populated mining attribute?
            if (s.Colony.TryGetDataBlob<ComponentInstancesDB>(out var comps))
            {
                if (comps.TryGetComponentsByAttribute<MineResourcesAtbDB>(out var mineInstances))
                {
                    Log($"      components carrying MineResourcesAtbDB: {mineInstances.Count}");
                    foreach (var inst in mineInstances)
                    {
                        var atb = inst.Design.GetAttribute<MineResourcesAtbDB>();
                        int n = atb.ResourcesPerEconTick?.Count ?? -1;
                        Log($"        '{inst.Design.Name}'  health {inst.HealthPercent:P0}  ResourcesPerEconTick: {n} entries");
                        if (atb.ResourcesPerEconTick != null)
                            foreach (var kv in atb.ResourcesPerEconTick)
                                Log($"          {kv.Key,-26} {kv.Value}");
                    }
                }
                else
                {
                    Log("      NO installed component carries MineResourcesAtbDB (TryGetComponentsByAttribute returned false)");
                }
            }
        }

        private static double ReportCargo(TestScenario s, string label)
        {
            if (!s.Colony.TryGetDataBlob<CargoStorageDB>(out var cargo))
            {
                Log($"  cargo ({label}): (no CargoStorageDB on colony)");
                return 0;
            }
            Log($"  cargo ({label}): total stored mass = {cargo.TotalStoredMass:N1}");
            foreach (var typeKv in cargo.TypeStores)
            {
                var ts = typeKv.Value;
                var cargoables = ts.GetCargoables();
                foreach (var itemKv in ts.CurrentStoreInUnits)
                {
                    string name = cargoables.TryGetValue(itemKv.Key, out var c) ? c.Name : $"id:{itemKv.Key}";
                    Log($"    {name,-32} {itemKv.Value,14:N0} units  [{typeKv.Key}]");
                }
            }
            return cargo.TotalStoredMass;
        }

        /// <summary>Reports the planet's mineral deposits and returns their total remaining amount (the gauge the
        /// mining assertion reads — total deposits must shrink over a year if the mine is working).</summary>
        private static long ReportDeposits(TestScenario s)
        {
            if (!s.StartingBody.TryGetDataBlob<MineralsDB>(out var minerals))
            {
                Log("  deposits: (no MineralsDB on starting body)");
                return 0;
            }
            Log($"  planet mineral deposits: {minerals.Minerals.Count} type(s)");
            long total = 0;
            foreach (var kv in minerals.Minerals)
            {
                total += kv.Value.Amount.Actual;
                Log($"    deposit id {kv.Key,-8} accessibility {kv.Value.Accessibility:P0}  amount {kv.Value.Amount.Actual:N0}");
            }
            return total;
        }

        private static long Sum(Dictionary<int, long> d)
        {
            long total = 0;
            foreach (var v in d.Values) total += v;
            return total;
        }
    }
}
