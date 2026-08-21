using NUnit.Framework;
using Pulsar4X.Galaxy;
using Pulsar4X.Industry;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// R1b per-hex mining (`docs/ground/UNITS-ON-THE-MAP-DESIGN.md` §2 weld #1, developer ruling 2026-08-10). This pins
    /// the PURE per-hex depletion step `MineResourcesProcessor.MineHexDeposit` — a mine placed on a hex works THAT hex's
    /// located deposit into the hex's own stockpile, conserved. The component→hex routing + colony-level fallback around
    /// it (`MineResourcesPerComponent`) is flag-gated OFF by default → the aggregate body-wide-pool mining is
    /// byte-identical (the existing `EconomyReadoutTests` mining path is the net for the OFF path), and its live behaviour
    /// is CI-blind (needs a real game); the full component-routing integration gauge is a fast-follow.
    /// </summary>
    [TestFixture]
    public class PerHexMiningTests
    {
        private const int Iron = 7;
        private const int Copper = 12;

        [Test]
        [Description("A hex-placed mine depletes its hex's located deposit into the hex's local stockpile, CONSERVED (deposit falls by exactly what the bucket gains).")]
        public void MineHexDeposit_DepletesHex_FillsBucket_Conserved()
        {
            var hex = new GroundHex(0, 0, RegionFeatureType.Mountains);
            hex.DepositMineralId = Iron;
            hex.DepositAmount = 100_000;

            long mined = MineResourcesProcessor.MineHexDeposit(hex, Iron, 500);

            Assert.That(mined, Is.EqualTo(500), "mines the per-tick rate when the deposit can cover it");
            Assert.That(hex.DepositAmount, Is.EqualTo(99_500), "the located deposit fell by exactly what was mined");
            Assert.That(hex.StockpileOf(Iron), Is.EqualTo(500), "and that ore is now in the hex's local bucket");

            // A second tick accumulates and keeps conserving.
            MineResourcesProcessor.MineHexDeposit(hex, Iron, 500);
            Assert.That(hex.DepositAmount, Is.EqualTo(99_000));
            Assert.That(hex.StockpileOf(Iron), Is.EqualTo(1_000), "the bucket accumulates over ticks");
        }

        [Test]
        [Description("Mining is capped at the deposit on hand — a rate larger than the remaining deposit empties it and no more.")]
        public void MineHexDeposit_IsCappedByTheDeposit()
        {
            var hex = new GroundHex(1, 1, RegionFeatureType.Highlands);
            hex.DepositMineralId = Iron;
            hex.DepositAmount = 300;

            long mined = MineResourcesProcessor.MineHexDeposit(hex, Iron, 100_000);

            Assert.That(mined, Is.EqualTo(300), "can't mine more than the deposit holds");
            Assert.That(hex.DepositAmount, Is.EqualTo(0), "the deposit is exhausted");
            Assert.That(hex.StockpileOf(Iron), Is.EqualTo(300), "exactly the deposit ended up in the bucket");

            Assert.That(MineResourcesProcessor.MineHexDeposit(hex, Iron, 500), Is.EqualTo(0), "an exhausted deposit yields nothing more");
        }

        [Test]
        [Description("No-op guards: a mineral that isn't this hex's deposit, and a non-positive rate, mine nothing and touch nothing.")]
        public void MineHexDeposit_NoOpOnMismatchOrZeroRate()
        {
            var hex = new GroundHex(2, 2, RegionFeatureType.Barren);
            hex.DepositMineralId = Iron;
            hex.DepositAmount = 100_000;

            // Asking for copper on an iron hex mines nothing (a mine only works the mineral located here).
            Assert.That(MineResourcesProcessor.MineHexDeposit(hex, Copper, 500), Is.EqualTo(0), "wrong mineral is a no-op");
            Assert.That(hex.DepositAmount, Is.EqualTo(100_000), "the iron deposit is untouched");
            Assert.That(hex.StockpileOf(Copper), Is.EqualTo(0), "no copper appears from nowhere");

            // A zero / negative rate is a no-op.
            Assert.That(MineResourcesProcessor.MineHexDeposit(hex, Iron, 0), Is.EqualTo(0), "zero rate mines nothing");
            Assert.That(MineResourcesProcessor.MineHexDeposit(hex, Iron, -50), Is.EqualTo(0), "negative rate mines nothing");
            Assert.That(hex.DepositAmount, Is.EqualTo(100_000), "still untouched");

            // A hex with no deposit is a no-op (and never throws).
            var barren = new GroundHex(3, 3, RegionFeatureType.Plains);
            Assert.That(MineResourcesProcessor.MineHexDeposit(barren, Iron, 500), Is.EqualTo(0), "a hex with no deposit mines nothing");
            Assert.That(MineResourcesProcessor.MineHexDeposit(null, Iron, 500), Is.EqualTo(0), "a null hex is a safe no-op");
        }
    }
}
