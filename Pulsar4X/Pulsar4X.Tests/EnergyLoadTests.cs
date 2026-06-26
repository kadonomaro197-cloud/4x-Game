using NUnit.Framework;
using Pulsar4X.Energy;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Reactor load gauge — guards the 2026-06-26 fix to <see cref="EnergyGenProcessor.CalcLoad"/>. "Load" is how
    /// hard the reactor is working as a fraction of its max output (0 = idle, 1 = maxed). It feeds TWO things: the
    /// power-UI readout (shown as a percent) and reactor fuel use (fuel burned = maxFuelUse × load). The old
    /// formula was inverted and unbounded — `TotalOutputMax ÷ spareCapacity` — so it read 100% at idle, 200% at
    /// half demand, and shot toward infinity near full. That made the power gauge nonsense AND made an idle reactor
    /// burn near-max fuel. This fixture pins the corrected behaviour: load = demand ÷ max, clamped to 0..1.
    /// </summary>
    [TestFixture]
    public class EnergyLoadTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[energy-load] " + m);

        [Test]
        [Description("Reactor load = demand ÷ max output, clamped 0..1: idle 0 (old bug read 1.0=max), half 0.5 " +
                     "(old read 2.0), full 1.0 (old read →∞), over-demand clamps to 1.0 (reactor can't exceed max; " +
                     "the battery covers the rest), and a no-reactor 0 with no divide-by-zero. Because reactor fuel " +
                     "use is maxFuelUse × load, fixing idle from 1.0→0 stops idle reactors burning near-max fuel.")]
        public void CalcLoad_IsDemandOverMax_Clamped()
        {
            Assert.That(EnergyGenProcessor.CalcLoad(0, 100), Is.EqualTo(0.0).Within(1e-12),
                "idle reactor draws 0% of max (the old inverted formula read 100%)");
            Assert.That(EnergyGenProcessor.CalcLoad(50, 100), Is.EqualTo(0.5).Within(1e-12),
                "half demand is 50% load (the old formula read 200%)");
            Assert.That(EnergyGenProcessor.CalcLoad(100, 100), Is.EqualTo(1.0).Within(1e-12),
                "full demand is 100% load (the old formula shot toward infinity)");
            Assert.That(EnergyGenProcessor.CalcLoad(150, 100), Is.EqualTo(1.0).Within(1e-12),
                "over-demand clamps to 100% — a reactor can't exceed max output (the battery covers the shortfall)");
            Assert.That(EnergyGenProcessor.CalcLoad(50, 0), Is.EqualTo(0.0).Within(1e-12),
                "no reactor capacity reads 0 — no divide-by-zero");

            Log("CalcLoad: 0/100=0  50/100=0.5  100/100=1  150/100=1(clamped)  50/0=0(guarded)");
        }
    }
}
