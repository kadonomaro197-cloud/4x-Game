using NUnit.Framework;
using Pulsar4X.Galaxy;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// H1 hex-to-hex hauling (`docs/ground/UNITS-ON-THE-MAP-DESIGN.md` §3). Pins the PURE conserved move at the heart of
    /// <see cref="HexHaulOrder"/> (`HaulBetweenHexes`): once per-hex mining (R1b) fills a hex's local stockpile, the ore
    /// sits there until an ordered haul carries it — so hauling is a real job. The order wrapper around this (colony-issued
    /// EntityCommand, both seats, + the hex→colony-cargo direction) rides the real game/order context; its integration
    /// gauge is a fast-follow. The order is inert until a bucket holds ore, so it's byte-identical to a stock game.
    /// </summary>
    [TestFixture]
    public class HexHaulTests
    {
        private const int Iron = 7;
        private const int Copper = 12;

        [Test]
        [Description("A haul moves the requested amount hex→hex, CONSERVED (dst gains exactly what src loses); a second good is untouched.")]
        public void HaulBetweenHexes_MovesRequested_Conserved()
        {
            var src = new GroundHex(0, 0, RegionFeatureType.Mountains);
            var dst = new GroundHex(1, 0, RegionFeatureType.Plains);
            src.AddToStockpile(Iron, 500);
            src.AddToStockpile(Copper, 40);

            long moved = HexHaulOrder.HaulBetweenHexes(src, dst, Iron, 200);

            Assert.That(moved, Is.EqualTo(200), "moved exactly what was asked");
            Assert.That(src.StockpileOf(Iron), Is.EqualTo(300), "source fell by the moved amount");
            Assert.That(dst.StockpileOf(Iron), Is.EqualTo(200), "destination gained exactly that (conserved)");
            Assert.That(src.StockpileOf(Copper), Is.EqualTo(40), "a good not hauled is untouched");
            Assert.That(dst.StockpileOf(Copper), Is.EqualTo(0), "and doesn't appear at the destination");
        }

        [Test]
        [Description("Amount ≤ 0 hauls ALL of that good on hand; a bigger request is capped at what's there.")]
        public void HaulBetweenHexes_HaulAll_AndCappedByOnHand()
        {
            var src = new GroundHex(0, 0, RegionFeatureType.Highlands);
            var dst = new GroundHex(0, 1, RegionFeatureType.Plains);
            src.AddToStockpile(Iron, 750);

            // amount <= 0 => move everything.
            long movedAll = HexHaulOrder.HaulBetweenHexes(src, dst, Iron, 0);
            Assert.That(movedAll, Is.EqualTo(750), "a non-positive amount hauls all on hand");
            Assert.That(src.StockpileOf(Iron), Is.EqualTo(0), "source emptied");
            Assert.That(dst.StockpileOf(Iron), Is.EqualTo(750), "destination holds it all");

            // Over-ask on a now-partial source is capped.
            dst.AddToStockpile(Copper, 100);
            long overAsk = HexHaulOrder.HaulBetweenHexes(dst, src, Copper, 1_000_000);
            Assert.That(overAsk, Is.EqualTo(100), "an over-ask is capped at what's on hand");
            Assert.That(dst.StockpileOf(Copper), Is.EqualTo(0));
            Assert.That(src.StockpileOf(Copper), Is.EqualTo(100));
        }

        [Test]
        [Description("No-op guards: an empty/absent good, and a null hex, move nothing and never throw.")]
        public void HaulBetweenHexes_NoOpOnEmptyOrNull()
        {
            var src = new GroundHex(0, 0, RegionFeatureType.Barren);
            var dst = new GroundHex(1, 1, RegionFeatureType.Barren);

            Assert.That(HexHaulOrder.HaulBetweenHexes(src, dst, Iron, 100), Is.EqualTo(0), "an empty source hauls nothing");
            Assert.That(HexHaulOrder.HaulBetweenHexes(null, dst, Iron, 100), Is.EqualTo(0), "a null source is a safe no-op");
            Assert.That(HexHaulOrder.HaulBetweenHexes(src, null, Iron, 100), Is.EqualTo(0), "a null destination is a safe no-op");
        }
    }
}
