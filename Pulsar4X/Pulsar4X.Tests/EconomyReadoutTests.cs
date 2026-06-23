using System;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Storage;
using Pulsar4X.Datablobs;
using Pulsar4X.Industry;
using Pulsar4X.Components;

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
            double massStart = ReportCargo(s, "start");
            ReportDeposits(s);

            s.AdvanceTime(TimeSpan.FromDays(365));

            Log("================ AFTER 1 GAME-YEAR =============");
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
                Log($"    deposit id {kv.Key,-6} accessibility {kv.Value.Accessibility:P0}");
        }
    }
}
