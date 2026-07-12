using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Components;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Weapons ⚙1 (GROUND) — the ground-weapon ATTACK dial now costs, resolving the flagged "Attack is a free dial"
    /// question (S15). Every ground weapon (rifle / autocannon / cannon / energy-weapon / claw) let a designer dial
    /// firepower to the ceiling for zero weight: the ground resolver reads <c>Attack</c> as damage, but the component
    /// mass and the carry-GATE both read the independent <c>CarryMass</c> dial. The ground analog of the space
    /// free-dial gaps (S8–S14) — but with a twist: the carry gate reads the atb's <c>Mass</c> (= CarryMass), not the
    /// component <c>MassPerUnit</c>, so a JSON-only cost isn't enough.
    ///
    /// Fixed on BOTH axes (the sensible-default decision — Option A + the build-cost coupling):
    ///   (1) BUILD COST — each ground-weapon Mass formula adds <c>Max(0, Attack - baseline)*1</c>, so a harder-hitting
    ///       weapon costs more materials to build (component <c>MassPerUnit</c> rises).
    ///   (2) CARRY-WEIGHT (un-bypassable) — <c>GroundUnitAssembly.Compute</c> floors a weapon's effective carry-mass at
    ///       <c>max(CarryMass, Attack × AttackCarryFactor)</c>, so firepower ALWAYS eats frame carry-capacity even if
    ///       the designer dials CarryMass low. The factor (0.1) is the tightest stock CarryMass/Attack ratio, so every
    ///       base-mod weapon is byte-identical; only a heavier-hitting-than-stock design pays.
    ///
    /// Both anchored so stock designs (and the infantry/armour/etc. that field them) are byte-identical. This resolves
    /// the ground-Attack flag (`GroundCombat/CLAUDE.md`) — done byte-identically (Option A with the min-ratio factor).
    /// Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class GroundWeaponAttackCostTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[ground-attack-cost] " + m);

        [Test]
        [Description("BUILD-COST axis: the Heavy Battle Rifle (Attack 200) costs the exact attack-cost term more to build than the stock Service Rifle (Max(0, 200-40)*1 = 160 more component mass → more materials), and the stock rifle (anchored at its 40 baseline) is byte-identical.")]
        public void TheAttackDial_CostsBuildMass_StockUntouched()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns;
            long stockMass = designs["default-design-ground-rifle"].MassPerUnit;
            long heavyMass = designs["default-design-heavy-rifle"].MassPerUnit;
            Log($"Service Rifle build-mass = {stockMass}, Heavy Battle Rifle = {heavyMass}, delta = {heavyMass - stockMass}");
            Assert.That(heavyMass, Is.GreaterThan(stockMass),
                "a harder-hitting ground weapon now costs more to build — Attack is earned, not free");
            Assert.That(heavyMass - stockMass, Is.EqualTo(160),
                "the extra build-mass is exactly the attack-cost term (200 - 40 baseline) * 1 — the stock rifle pays nothing (byte-identical)");
        }

        [Test]
        [Description("CARRY-WEIGHT axis (Option A, un-bypassable): on the same frame the Heavy Battle Rifle eats more carry-capacity than the stock rifle — its effective carry-mass is floored at Attack×0.1 = 20 (vs the stock rifle's 10), so the delta is exactly 10. The stock rifle's floor (4) is below its CarryMass (10), so it's byte-identical.")]
        public void TheAttackDial_CostsCarryWeight_ViaTheFloor_StockUntouched()
        {
            var s = TestScenario.CreateWithColony();
            ComponentDesign Part(string id) => (ComponentDesign)s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns[id];
            var frame = Part("default-design-human-frame");

            var stock = GroundUnitAssembly.Compute(frame, new List<(ComponentDesign, int)> { (Part("default-design-ground-rifle"), 1) });
            var heavy = GroundUnitAssembly.Compute(frame, new List<(ComponentDesign, int)> { (Part("default-design-heavy-rifle"), 1) });
            Log($"carry used: stock rifle {stock.UsedCapacity:0}, heavy rifle {heavy.UsedCapacity:0}, delta {heavy.UsedCapacity - stock.UsedCapacity:0}");

            Assert.That(stock.UsedCapacity, Is.EqualTo(10).Within(1e-9),
                "the stock rifle's carry cost is its CarryMass (10) — its Attack floor (40*0.1=4) is below it → byte-identical");
            Assert.That(heavy.UsedCapacity - stock.UsedCapacity, Is.EqualTo(10).Within(1e-9),
                "the heavy rifle is floored to Attack*0.1 = 20 carry-weight (vs 10) — firepower now un-bypassably costs carry-capacity");
        }
    }
}
