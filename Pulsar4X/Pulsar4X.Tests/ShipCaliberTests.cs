using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Enhancers ⚙6.2 — UNIT CALIBER, the first wired dial in the (previously all-dark) Enhancers category. A
    /// per-hull ELITE stamp: a <c>UnitCaliberAtb</c> "Veteran Cadre" component multiplies the hull's Firepower AND
    /// Toughness at build, so two ships of identical chassis + weapons + armour fight differently — the axis neither
    /// doctrine (switchable, fleet-wide) nor the admiral's bonus (flagship, fleet-wide) can express (see
    /// <see cref="ShipCombatValueDB.UnitCaliberFirepowerMult"/>). It STACKS on top of both.
    ///
    /// Cradle-to-grave: JSON <c>unit-caliber</c> template → NCalc <c>unitCaliberAtbArgs</c> → <c>UnitCaliberAtb</c> →
    /// read (health-scaled) in <see cref="ShipCombatValueDB.Calculate"/>. The base-mod Praetorian Veteran Cruiser is an
    /// EXACT copy of the Aegis test warship PLUS one Veteran Cadre, so the caliber shows as a clean firepower ratio.
    /// Byte-identical when absent (no module → mult 1.0): every existing ship is unchanged. Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipCaliberTests
    {
        private const string Praetorian = "default-ship-design-test-praetorian";
        private const string Aegis = "default-ship-design-test-warship";
        private static void Log(string m) => TestContext.Progress.WriteLine("[unit-caliber] " + m);

        private static Entity Build(TestScenario s, string designId, string name)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            return ShipFactory.CreateShip(designs[designId], s.Faction, s.StartingBody, name);
        }

        [Test]
        [Description("The Veteran Cadre binds from JSON and reads its design mults (firepower 1.30, toughness 1.20) health-scaled through the real UnitCaliberAtb -> ShipCombatValueDB path; an un-calibered Aegis reads exactly 1.0 (byte-identical).")]
        public void TheVeteranCadre_ReadsItsCaliber_AndAnUncalibredShipReadsOne()
        {
            var s = TestScenario.CreateWithColony();
            var praetorian = Build(s, Praetorian, "Praetorian");
            var aegis = Build(s, Aegis, "Aegis");

            double pFire = ShipCombatValueDB.UnitCaliberFirepowerMult(praetorian);
            double pTough = ShipCombatValueDB.UnitCaliberToughnessMult(praetorian);
            Log($"Praetorian caliber: firepower ×{pFire:0.00}, toughness ×{pTough:0.00}");
            Assert.That(pFire, Is.EqualTo(1.30).Within(1e-6), "firepower caliber read from the JSON design (full-health → full mult)");
            Assert.That(pTough, Is.EqualTo(1.20).Within(1e-6), "toughness caliber read from the JSON design");

            Assert.That(ShipCombatValueDB.UnitCaliberFirepowerMult(aegis), Is.EqualTo(1.0), "an un-calibered ship is byte-identical (mult 1.0)");
            Assert.That(ShipCombatValueDB.UnitCaliberToughnessMult(aegis), Is.EqualTo(1.0), "an un-calibered ship is byte-identical (mult 1.0)");
        }

        [Test]
        [Description("End-to-end: the Praetorian is an exact Aegis + a Veteran Cadre, so it out-guns the identical stock hull by exactly the caliber (firepower ×1.30) and is tougher — the per-hull elite stamp lands in the spec sheet the auto-resolver reads.")]
        public void ThePraetorian_OutFightsAnIdenticalStockHull_ByItsCaliber()
        {
            var s = TestScenario.CreateWithColony();
            var pv = Build(s, Praetorian, "Praetorian").GetDataBlob<ShipCombatValueDB>();
            var av = Build(s, Aegis, "Aegis").GetDataBlob<ShipCombatValueDB>();
            Log($"firepower: Praetorian {pv.Firepower:N0} vs Aegis {av.Firepower:N0} (×{pv.Firepower / av.Firepower:0.00}); toughness {pv.Toughness:N0} vs {av.Toughness:N0}");

            Assert.That(av.Firepower, Is.GreaterThan(0), "the stock Aegis has firepower to scale");
            Assert.That(pv.Firepower, Is.EqualTo(av.Firepower * 1.30).Within(av.Firepower * 1e-6),
                "same four lasers, but the veteran crew hits 30% harder — caliber applied to the built firepower");
            Assert.That(pv.Toughness, Is.GreaterThan(av.Toughness),
                "the veteran crew (×1.20 toughness) plus the module's own hull-points make the Praetorian tougher");
        }
    }
}
