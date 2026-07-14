using System;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Industry;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Regression for the DevTest TIME-STALL (2026-07-14): the mining tick threw a KeyNotFoundException every cycle on
    /// the parallel sim thread, so the exception went unobserved and the clock stopped advancing. Root cause:
    /// <see cref="MineResourcesProcessor"/> hard-indexed the PLANET's deposits (<c>planetMinerals[key]</c>) using a key
    /// from the colony's MINING-RATE table — but that table can carry a mineral the planet has NO deposit of
    /// (<see cref="MiningHelper.CalculateActualMiningRates"/> keeps the key with rate 0 when accessibility is 0). A mine
    /// whose mineable set is broader than the body's actual deposits (e.g. a DevTest game on a random seed whose Earth
    /// lacks one of the default mine's minerals) then crashed. The fix skips such a mineral (nothing there to mine).
    /// This reproduces the exact mismatch and asserts the tick no longer throws.
    /// </summary>
    [TestFixture]
    public class MiningRobustnessTests
    {
        [Test]
        [Description("The mining tick skips a rate-table mineral the planet has no deposit of, instead of throwing "
                     + "(the crash that stalled the DevTest clock on a random seed).")]
        public void MiningTick_SkipsAMineralThePlanetLacks_DoesNotThrow()
        {
            var s = TestScenario.CreateWithColony();

            var proc = new MineResourcesProcessor();
            proc.Init(s.Game);

            Assert.That(s.Colony.TryGetDataBlob<MiningDB>(out var miningDB), Is.True, "the start colony has a mine");
            // Recompute the rate table off the installed mine, then confirm it named some minerals.
            miningDB.ActualMiningRate = MiningHelper.CalculateActualMiningRates(s.Colony);
            Assert.That(miningDB.ActualMiningRate.Count, Is.GreaterThan(0), "the colony's mine produced a rate table");

            Assert.That(s.StartingBody.TryGetDataBlob<MineralsDB>(out var mineralsDB), Is.True, "the planet has deposits");
            var planetMinerals = mineralsDB.Minerals;

            // Reproduce the mismatch: take a real mineral the rate table carries AND the planet has, then remove it
            // from the planet's deposits — now the rate table names a mineral with no deposit (the crash condition).
            int victim = -1;
            foreach (var key in miningDB.ActualMiningRate.Keys)
                if (planetMinerals.ContainsKey(key)) { victim = key; break; }
            Assert.That(victim, Is.GreaterThanOrEqualTo(0), "the rate table and planet share at least one mineral to remove");
            planetMinerals.Remove(victim);

            // BEFORE the fix this threw KeyNotFoundException(victim) at planetMinerals[victim]; now it skips it.
            Assert.DoesNotThrow(() => proc.ProcessEntity(s.Colony, 86400),
                "the mining tick must skip a rate-table mineral the planet has no deposit of, not throw");
        }
    }
}
