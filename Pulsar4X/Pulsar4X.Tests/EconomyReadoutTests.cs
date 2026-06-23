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
    /// Deliberately a READOUT, not a tight assertion: we have never measured this economy, so this prints the
    /// numbers and asserts only a non-negotiable invariant (cargo mass can't go negative). Once we've read a
    /// baseline off the CI log — e.g. "the starting colony has no Mine, so cargo never grows" — tighten the
    /// assertions here (mining adds minerals, refining converts them, etc.). Instrument before you theorize.
    ///
    /// 2026-06-23 — chasing "the mine does zero work over a game-year." Added a full mining-chain dump
    /// (NumberOfMines → per-mine ResourcesPerEconTick → BaseMiningRate → ActualMiningRate) plus the
    /// infrastructure efficiency throttle and the body's gravity/pressure, so one CI run shows exactly which
    /// node in the chain is empty. This is the "build the gauge before you theorize" move from the Visibility Gate.
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
            ReportDeposits(s);

            s.AdvanceTime(TimeSpan.FromDays(365));

            Log("================ AFTER 1 GAME-YEAR =============");
            ReportMining(s, "end");
            double massEnd = ReportCargo(s, "end");
            ReportDeposits(s);

            Log($">>> Colony cargo total mass: {massStart:N1} -> {massEnd:N1}   (delta {massEnd - massStart:N1})");

            // We're establishing 'normal', not asserting it yet — so only the non-negotiable invariant.
            Assert.That(massEnd, Is.GreaterThanOrEqualTo(0),
                "Colony cargo mass went negative across a game-year — storage accounting is broken.");
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

        private static void ReportDeposits(TestScenario s)
        {
            if (!s.StartingBody.TryGetDataBlob<MineralsDB>(out var minerals))
            {
                Log("  deposits: (no MineralsDB on starting body)");
                return;
            }
            Log($"  planet mineral deposits: {minerals.Minerals.Count} type(s)");
            foreach (var kv in minerals.Minerals)
                Log($"    deposit id {kv.Key,-8} accessibility {kv.Value.Accessibility:P0}  amount {kv.Value.Amount.Actual:N0}");
        }

        private static long Sum(Dictionary<int, long> d)
        {
            long total = 0;
            foreach (var v in d.Values) total += v;
            return total;
        }
    }
}
