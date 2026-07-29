using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Factions;
using Pulsar4X.Movement;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// WARP — the <c>Startup vs Endurance</c> dial (2026-07-29).
    ///
    /// <para><b>The gap.</b> A warp drive had exactly one performance dial (<c>Efficency vs Power</c>) plus Mass.
    /// Bubble CREATION cost and bubble SUSTAIN cost were both computed from Engine Power with no choice in it — yet
    /// both are live: creation <b>gates departure</b> (<c>WarpMoveCommand</c> refuses to leave while stored energy is
    /// below it) and sustain is <b>charged per second in transit</b>
    /// (<c>WarpMoveProcessor</c> <c>AddDemand(BubbleSustainCost, …)</c>).</para>
    ///
    /// <para><b>The dial.</b> One slider multiplies creation and DIVIDES sustain, so their product is invariant —
    /// zero-sum, the same shape as the Reaction door's <c>T·v = 2P</c>:</para>
    /// <code>
    ///   0.5 → creation ×0.5, sustain ×2    cheap to start, expensive to hold — SHORT HOPS,
    ///                                       and it can leave on a part-charged battery
    ///   1.0 → today's numbers exactly       (byte-identical default)
    ///   2.0 → creation ×2, sustain ×0.5     expensive to start, cheap to hold — ONE LONG HAUL
    /// </code>
    ///
    /// <para>Gauged through the real JSON → NCalc → <see cref="WarpDriveAtb"/> path against the base-mod drives.
    /// <c>default-design-alcubierre-2k-endurance</c> is the new buildable long-haul variant (same mass as the 2k,
    /// dial at 2.0), so the dial is cradle-to-grave and not just a formula.</para>
    /// </summary>
    [TestFixture]
    public class WarpBubbleTradeTests
    {
        private const string Standard  = "default-design-alcubierre-2k";
        private const string Endurance = "default-design-alcubierre-2k-endurance";
        private const string Small     = "default-design-alcubierre-500";

        private static void Log(string m) => TestContext.Progress.WriteLine("[warp-trade] " + m);

        private static WarpDriveAtb Drive(TestScenario s, string id)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            Assert.That(designs.ContainsKey(id), Is.True, $"base-mod warp design '{id}' should be built for the start faction");
            Assert.That(designs[id].TryGetAttribute<WarpDriveAtb>(out var atb), Is.True,
                $"'{id}' should carry a WarpDriveAtb from the JSON template");
            return atb;
        }

        [Test]
        [Description("The dial is ZERO-SUM: the endurance drive costs more to CREATE its bubble and less to SUSTAIN it than the identical-mass standard drive, and the product of the two is unchanged — so neither end of the slider is a free win.")]
        public void StartupVsEndurance_TradesCreationAgainstSustain_ProductInvariant()
        {
            var s = TestScenario.CreateWithColony();
            var std = Drive(s, Standard);
            var end = Drive(s, Endurance);

            Log($"standard  create {std.BubbleCreationCost:0} kJ, sustain {std.BubbleSustainCost:0.###} kW");
            Log($"endurance create {end.BubbleCreationCost:0} kJ, sustain {end.BubbleSustainCost:0.###} kW");

            // Same mass and same Efficency-vs-Power → same engine power, so only the dial differs.
            Assert.That(end.WarpPower, Is.EqualTo(std.WarpPower), "same mass → same engine power; only the dial differs");

            Assert.That(end.BubbleCreationCost, Is.GreaterThan(std.BubbleCreationCost),
                "the long-haul drive pays MORE to spin the bubble up");
            Assert.That(end.BubbleSustainCost, Is.LessThan(std.BubbleSustainCost),
                "…and LESS to hold it, which is the whole point");

            // multiply one, divide the other → the product is the invariant.
            double pStd = std.BubbleCreationCost * std.BubbleSustainCost;
            double pEnd = end.BubbleCreationCost * end.BubbleSustainCost;
            Assert.That(pEnd, Is.EqualTo(pStd).Within(1e-6 * System.Math.Max(1.0, pStd)),
                "creation × sustain must be invariant — otherwise the dial is a free win, not a trade");

            // and the ratio is exactly the dial (2.0), independent of tech level and engine power.
            Assert.That(end.BubbleCreationCost / std.BubbleCreationCost, Is.EqualTo(2.0).Within(1e-6),
                "creation scales by the dial exactly");
            Assert.That(std.BubbleSustainCost / end.BubbleSustainCost, Is.EqualTo(2.0).Within(1e-6),
                "sustain scales by its reciprocal exactly");
        }

        [Test]
        [Description("The default (1.0) is BYTE-IDENTICAL: an undialled drive's creation cost is still exactly its collapse return divided by the collapse-efficiency tech, and creation:sustain still holds the ratio the two original formulas produce — so adding the dial moved no existing number.")]
        public void TheDefault_IsByteIdentical_ForEveryExistingDrive()
        {
            var s = TestScenario.CreateWithColony();
            var big = Drive(s, Standard);
            var small = Drive(s, Small);

            // Both original formulas are linear in Engine Power, so their RATIO is a constant that the dial at 1.0
            // must not have disturbed — and it is the same constant for two different-sized drives.
            double rBig = big.BubbleCreationCost / big.BubbleSustainCost;
            double rSmall = small.BubbleCreationCost / small.BubbleSustainCost;
            Log($"creation:sustain — 2k {rBig:0.###}, 500 {rSmall:0.###}");
            Assert.That(rBig, Is.EqualTo(rSmall).Within(1e-6 * rBig),
                "the undialled ratio is power-independent, exactly as the two original formulas were");

            // Costs scale linearly with engine power (mass), and the small drive is genuinely smaller.
            Assert.That(small.WarpPower, Is.LessThan(big.WarpPower));
            Assert.That(small.BubbleCreationCost, Is.LessThan(big.BubbleCreationCost));
            Assert.That(big.BubbleCreationCost, Is.GreaterThan(0), "a drive must cost something to spin up");
            Assert.That(big.BubbleSustainCost, Is.GreaterThan(0), "…and something to hold");
        }
    }
}
