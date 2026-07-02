using NUnit.Framework;
using Pulsar4X.Colonies;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the M5b power/food sustenance wiring (docs/MORALE-AND-POPULATION-DESIGN.md, task #29): the
    /// shortage math, the starvation death rate, and the crucial NEUTRAL-WHEN-ABSENT property — a colony built by
    /// the factory carries a `ColonySustenanceDB` but computes ZERO shortage until per-capita demand is set (the
    /// guard against the "default deficit tanks every colony" trap). Once demand is set, the processor computes a
    /// real shortage. Feel calibration (the actual demand rates + a food-supply good) is a PC-test.
    /// </summary>
    [TestFixture]
    public class SustenanceTests
    {
        [Test]
        [Description("Shortage math: 0 when there's no demand (neutral-safe); demand-vs-supply otherwise; clamped 0..1.")]
        public void Shortage_Math()
        {
            Assert.That(ColonySustenanceDB.Shortage(0, 0), Is.EqualTo(0.0), "no demand → never a shortage");
            Assert.That(ColonySustenanceDB.Shortage(0, 50), Is.EqualTo(0.0), "no demand even with supply");
            Assert.That(ColonySustenanceDB.Shortage(100, 40), Is.EqualTo(0.6).Within(0.0001));
            Assert.That(ColonySustenanceDB.Shortage(100, 100), Is.EqualTo(0.0), "met demand");
            Assert.That(ColonySustenanceDB.Shortage(100, 250), Is.EqualTo(0.0), "surplus clamps to 0");
            Assert.That(ColonySustenanceDB.Shortage(100, 0), Is.EqualTo(1.0), "no supply = total shortage");
        }

        [Test]
        [Description("Starvation death rate scales with food shortage up to the cap, and is 0 with no shortage.")]
        public void Starvation_Math()
        {
            Assert.That(ColonySustenanceDB.StarvationDeathRate(0.0), Is.EqualTo(0.0));
            Assert.That(ColonySustenanceDB.StarvationDeathRate(0.5), Is.EqualTo(0.5 * ColonySustenanceDB.MaxStarvationDeathRate).Within(0.0001));
            Assert.That(ColonySustenanceDB.StarvationDeathRate(2.0), Is.EqualTo(ColonySustenanceDB.MaxStarvationDeathRate), "clamps at total famine");
        }

        [Test]
        [Description("The factory attaches a ColonySustenanceDB, and it computes ZERO shortage by default (demand coefficients are 0) — no colony is starved/browned-out on New Game.")]
        public void Colony_HasSustenance_AndIsInertByDefault()
        {
            var s = TestScenario.CreateWithColony();
            Assert.That(s.Colony.HasDataBlob<ColonySustenanceDB>(), Is.True, "the factory attaches it");

            var sust = s.Colony.GetDataBlob<ColonySustenanceDB>();
            SustenanceProcessor.Recalc(s.Colony);
            Assert.That(sust.PowerShortage, Is.EqualTo(0.0), "no power demand configured → inert");
            Assert.That(sust.FoodShortage, Is.EqualTo(0.0), "no food demand configured → inert");
        }

        [Test]
        [Description("Once a per-capita food demand is set with no supply, the processor computes a real shortage (the wiring works; the numbers are the local calibration).")]
        public void Recalc_ComputesShortage_WhenDemandSet()
        {
            var s = TestScenario.CreateWithColony();
            var sust = s.Colony.GetDataBlob<ColonySustenanceDB>();

            sust.PerCapitaFoodDemand = 0.001;   // now food demand = pop × 0.001 > 0, with no food good → no supply
            SustenanceProcessor.Recalc(s.Colony);
            Assert.That(sust.FoodShortage, Is.EqualTo(1.0), "demand with zero supply = total shortage");
        }
    }
}
