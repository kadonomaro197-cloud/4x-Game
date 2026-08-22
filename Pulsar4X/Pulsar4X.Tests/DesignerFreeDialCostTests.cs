using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Factions;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The dial audit's Bucket 2 — the "free" designer dials now COST mass. A capability bonus (caliber, crew
    /// automation, the survivability augments) used to add real combat/crew value while adding ZERO weight, so it
    /// was a free lunch. Each is now priced into the component's Mass formula with the baseline-anchored idiom the
    /// campaign already uses (`+ Max(0, dial - baseline) * factor`), anchored so every STOCK design sits at the
    /// baseline and is byte-identical — only an above-baseline (upgraded) design pays. Because CrewReq / ResearchCost
    /// / CreditCost / ResourceCost all derive from [Mass], the price cascades into credits/research/build-time/materials
    /// automatically.
    ///
    /// Test A pins byte-identity (the six shipped designs sit at baseline → unchanged). Test B pins the BITE (an
    /// above-baseline demo design costs the exact expected delta). Reads MassPerUnit off the real start faction, the
    /// same way <see cref="GroundWeaponAttackCostTests"/> / <see cref="ShipLongRangeLaserTests"/> do. Engine-only → CI.
    ///
    /// CARRY AXIS (now DONE — the flagged follow-up landed): for the GROUND augments the assembler's CARRY gate read
    /// the atb's Mass (= the CarryMass ctor arg), not the component MassPerUnit — so the Mass-formula pricing above cost
    /// the BUILD but not the frame carry-weight. <see cref="GroundUnitAssembly.Compute"/> now floors an augment's
    /// effective carry-mass at <c>Math.Max(g.Mass, d.MassPerUnit)</c> — exactly what <see cref="GroundWeaponAttackCostTests"/>
    /// did for the weapon Attack dial, and reusing this bucket's own priced MassPerUnit. At baseline the priced terms are
    /// zero so MassPerUnit == CarryMass → <c>Math.Max</c> is a no-op → byte-identical; only an above-baseline (upgraded)
    /// augment eats extra frame carry-capacity, un-bypassably. <see cref="AboveBaselineAugment_CostsCarryWeight_ViaTheFloor"/>
    /// pins it. (The ship components — unit-caliber / crew-automation — were fully priced from the start since Mass IS ship mass.)
    /// </summary>
    [TestFixture]
    public class DesignerFreeDialCostTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[free-dial-cost] " + m);

        [Test]
        [Description("Byte-identity: every shipped free-dial design sits at its template baseline, so its mass is unchanged by the new pricing terms.")]
        public void StockDesigns_AreByteIdentical_AtBaseline()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;

            void AssertStock(string id, long expected)
            {
                long m = designs[id].MassPerUnit;
                Log($"{id} mass = {m} (expect {expected})");
                Assert.That(m, Is.EqualTo(expected), $"{id} sits at baseline → its priced terms are all zero → byte-identical");
            }

            AssertStock("default-design-unit-caliber", 3000);
            AssertStock("default-design-crew-automation", 5000);
            AssertStock("default-design-power-armor", 30);
            AssertStock("default-design-reflex-booster", 15);
            AssertStock("default-design-shield-generator", 20);
            AssertStock("default-design-ward-projector", 20);
        }

        [Test]
        [Description("The bite: an above-baseline SHIP caliber (Firepower Caliber 2.0) and GROUND augment (StrengthBonus 800) each cost the exact expected extra mass — the free dial is no longer free.")]
        public void AboveBaselineDesigns_CostTheExpectedExtraMass()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;

            // Elite Veteran Cadre: 3000 + (2.0 - 1.3)*2000 = 4400 (+1400 over the stock cadre's 3000).
            long elite = designs["default-design-elite-cadre"].MassPerUnit;
            long stockCadre = designs["default-design-unit-caliber"].MassPerUnit;
            Log($"elite cadre = {elite}, stock cadre = {stockCadre}, delta = {elite - stockCadre}");
            Assert.That(elite, Is.EqualTo(4400), "Firepower Caliber 2.0 adds exactly (2.0-1.3)*2000 = 1400 mass");
            Assert.That(elite, Is.GreaterThan(stockCadre), "a higher-caliber cadre now weighs more — the multiplier is earned");

            // Heavy Power Armour: 30 + (800 - 300)*0.1 = 80 (+50 over the stock armour's 30).
            long heavyArmour = designs["default-design-heavy-power-armor"].MassPerUnit;
            long stockArmour = designs["default-design-power-armor"].MassPerUnit;
            Log($"heavy power armour = {heavyArmour}, stock = {stockArmour}, delta = {heavyArmour - stockArmour}");
            Assert.That(heavyArmour, Is.EqualTo(80), "StrengthBonus 800 adds exactly (800-300)*0.1 = 50 mass");
            Assert.That(heavyArmour, Is.GreaterThan(stockArmour), "a stronger power armour now weighs more — the bonus is earned");
        }

        [Test]
        [Description("CARRY-WEIGHT axis (the augment analog of GroundWeaponAttackCostTests): on the same frame the Heavy Power Armour eats more carry-capacity than the stock armour — its effective carry-mass is floored at its priced MassPerUnit (80) vs the stock armour's CarryMass (30), so the delta is exactly 50. The stock armour's MassPerUnit (30) equals its CarryMass (30), so it's byte-identical.")]
        public void AboveBaselineAugment_CostsCarryWeight_ViaTheFloor()
        {
            var s = TestScenario.CreateWithColony();
            ComponentDesign Part(string id) => (ComponentDesign)s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns[id];
            var frame = Part("default-design-human-frame");

            var stock = GroundUnitAssembly.Compute(frame, new List<(ComponentDesign, int)> { (Part("default-design-power-armor"), 1) });
            var heavy = GroundUnitAssembly.Compute(frame, new List<(ComponentDesign, int)> { (Part("default-design-heavy-power-armor"), 1) });
            Log($"carry used: stock armour {stock.UsedCapacity:0}, heavy armour {heavy.UsedCapacity:0}, delta {heavy.UsedCapacity - stock.UsedCapacity:0}");

            Assert.That(stock.UsedCapacity, Is.EqualTo(30).Within(1e-9),
                "the stock power armour's carry cost is its CarryMass (30) — its priced MassPerUnit (30) equals it → byte-identical");
            Assert.That(heavy.UsedCapacity - stock.UsedCapacity, Is.EqualTo(50).Within(1e-9),
                "the heavy power armour is floored to its priced MassPerUnit (80) carry-weight (vs 30) — the survivability bonus now un-bypassably costs carry-capacity");
        }
    }
}
