using NUnit.Framework;
using Newtonsoft.Json;
using Pulsar4X.Galaxy;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// R1 per-hex resource locality — slice R1a (`docs/ground/UNITS-ON-THE-MAP-DESIGN.md` §2 weld #2, developer ruling
    /// 2026-08-10 "a resource STAYS in its mini-hex"). This is the FOUNDATION field: the per-hex <see cref="GroundHex.Stockpile"/>
    /// local bucket + its pure accessors. NOTHING reads or writes it in the engine yet (the flag-gated mine/consume rewire is
    /// the next slice), so this field is byte-identical on its own — this gauge pins only the accessor contract and the
    /// DataBlob save-safety discipline (L12) the field's doc-comment claims.
    ///
    /// The accessor contract mirrors the cargo transfer: a non-positive amount is a no-op, removal is CAPPED at what is on
    /// hand and returns the amount ACTUALLY taken (conserved / take-what-fits), and an emptied key is dropped.
    /// </summary>
    [TestFixture]
    public class GroundHexStockpileTests
    {
        private const int Iron = 7;      // an ICargoable.ID (the same int DepositMineralId / CargoStorageDB use)
        private const int Copper = 12;

        [Test]
        [Description("Add accumulates, StockpileOf reads it, a non-positive add is a no-op, and an absent good reads 0 — the local-bucket accessor contract.")]
        public void Stockpile_AddsAndReads_NonPositiveIsNoOp()
        {
            var hex = new GroundHex(0, 0, RegionFeatureType.Plains);

            Assert.That(hex.StockpileOf(Iron), Is.EqualTo(0), "an untouched hex holds nothing");

            hex.AddToStockpile(Iron, 100);
            hex.AddToStockpile(Iron, 50);
            Assert.That(hex.StockpileOf(Iron), Is.EqualTo(150), "adds accumulate onto the same good");
            Assert.That(hex.StockpileOf(Copper), Is.EqualTo(0), "a good never added reads 0, not a throw");

            hex.AddToStockpile(Iron, 0);
            hex.AddToStockpile(Iron, -20);
            Assert.That(hex.StockpileOf(Iron), Is.EqualTo(150), "a zero or negative add changes nothing");
        }

        [Test]
        [Description("Remove is capped at what's on hand, returns the amount ACTUALLY taken (conserved), and drops the key at 0.")]
        public void Stockpile_RemoveIsCapped_Conserved_DropsEmptyKey()
        {
            var hex = new GroundHex(1, 1, RegionFeatureType.Mountains);
            hex.AddToStockpile(Iron, 100);

            long tookPartial = hex.RemoveFromStockpile(Iron, 30);
            Assert.That(tookPartial, Is.EqualTo(30), "a within-bounds remove takes exactly what was asked");
            Assert.That(hex.StockpileOf(Iron), Is.EqualTo(70), "and leaves the remainder");

            // Take MORE than remains: capped at the 70 on hand, and the key is dropped (no stale zero-entry).
            long tookOverdraw = hex.RemoveFromStockpile(Iron, 1000);
            Assert.That(tookOverdraw, Is.EqualTo(70), "remove is capped at what's on hand — take-what-fits");
            Assert.That(hex.StockpileOf(Iron), Is.EqualTo(0), "the bucket is now empty");
            Assert.That(hex.Stockpile.ContainsKey(Iron), Is.False, "an emptied good is dropped, not left as a zero-entry");

            // Conservation + defensiveness: removing from an empty/absent good takes nothing and never throws.
            Assert.That(hex.RemoveFromStockpile(Iron, 10), Is.EqualTo(0), "an empty good yields nothing");
            Assert.That(hex.RemoveFromStockpile(Copper, 10), Is.EqualTo(0), "an absent good yields nothing");
            Assert.That(hex.RemoveFromStockpile(Iron, -5), Is.EqualTo(0), "a negative request is a no-op");
        }

        [Test]
        [Description("The copy-ctor deep-copies the stockpile (L12 save-safety proxy): mutating the copy must not touch the original.")]
        public void Stockpile_CopyCtor_IsAnIndependentDeepCopy()
        {
            var original = new GroundHex(2, 3, RegionFeatureType.Highlands);
            original.AddToStockpile(Iron, 200);
            original.AddToStockpile(Copper, 40);

            var copy = new GroundHex(original);
            Assert.That(copy.StockpileOf(Iron), Is.EqualTo(200), "the copy carries the stockpile");
            Assert.That(copy.StockpileOf(Copper), Is.EqualTo(40));

            // Mutate the COPY — the original must be untouched (a shallow reference-copy would fail this).
            copy.AddToStockpile(Iron, 999);
            copy.RemoveFromStockpile(Copper, 40);
            Assert.That(original.StockpileOf(Iron), Is.EqualTo(200), "the original's iron is unchanged by the copy's add");
            Assert.That(original.StockpileOf(Copper), Is.EqualTo(40), "the original's copper is unchanged by the copy's remove");
            Assert.That(ReferenceEquals(original.Stockpile, copy.Stockpile), Is.False, "the two dictionaries are distinct objects");
        }

        [Test]
        [Description("The stockpile survives a JSON round-trip (the real save path uses [JsonProperty] — the field must persist).")]
        public void Stockpile_SurvivesJsonRoundTrip()
        {
            var hex = new GroundHex(4, 5, RegionFeatureType.Barren) { };
            hex.AddToStockpile(Iron, 321);
            hex.AddToStockpile(Copper, 654);

            var json = JsonConvert.SerializeObject(hex);
            var loaded = JsonConvert.DeserializeObject<GroundHex>(json);

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.StockpileOf(Iron), Is.EqualTo(321), "iron survived the round-trip");
            Assert.That(loaded.StockpileOf(Copper), Is.EqualTo(654), "copper survived the round-trip");
            // The coords/terrain still round-trip too (proves the new [JsonProperty] didn't break the existing shape).
            Assert.That(loaded.Q, Is.EqualTo(4));
            Assert.That(loaded.R, Is.EqualTo(5));
            Assert.That(loaded.Terrain, Is.EqualTo(RegionFeatureType.Barren));
        }
    }
}
