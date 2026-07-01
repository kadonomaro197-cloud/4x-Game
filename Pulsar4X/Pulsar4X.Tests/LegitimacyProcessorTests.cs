using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Engine;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the LIVE wiring of legitimacy (task #31): the colony factory attaches a <see cref="LegitimacyDB"/>
    /// to every province, and <see cref="LegitimacyProcessor"/> recomputes it from the sibling
    /// <see cref="ColonyMoraleDB"/> each cycle. Proves the blob is present on a real colony and that the recompute
    /// tracks morale (the v1 driver) — so a province's loyalty follows its people's contentment, and a miserable
    /// one trips the collapse/rebellion threshold.
    /// </summary>
    [TestFixture]
    public class LegitimacyProcessorTests
    {
        [Test]
        [Description("Every colony is built with a LegitimacyDB (the factory wiring).")]
        public void Colony_IsBuiltWithLegitimacy()
        {
            var s = TestScenario.CreateWithColony();
            Assert.That(s.Colony.HasDataBlob<LegitimacyDB>(), Is.True, "the factory attaches LegitimacyDB");
        }

        [Test]
        [Description("The processor recomputes legitimacy from the colony's current morale — loyalty tracks contentment.")]
        public void Recalc_TracksColonyMorale()
        {
            var s = TestScenario.CreateWithColony();
            var morale = s.Colony.GetDataBlob<ColonyMoraleDB>();
            var legitimacy = s.Colony.GetDataBlob<LegitimacyDB>();

            // A content province → high legitimacy.
            morale.Morale = 85.0;
            LegitimacyProcessor.RecalcLegitimacy(s.Colony);
            Assert.That(legitimacy.Legitimacy, Is.EqualTo(85.0).Within(0.001), "loyal because content");
            Assert.That(LegitimacyDB.IsCollapsing(legitimacy.Legitimacy), Is.False);

            // A miserable province → legitimacy collapses into the rebellion band.
            morale.Morale = 10.0;
            LegitimacyProcessor.RecalcLegitimacy(s.Colony);
            Assert.That(legitimacy.Legitimacy, Is.EqualTo(10.0).Within(0.001));
            Assert.That(LegitimacyDB.IsCollapsing(legitimacy.Legitimacy), Is.True, "a miserable province is rebelling");

            // The factors gauge is populated (why, not just the number).
            Assert.That(legitimacy.Factors.ContainsKey("morale"), Is.True);
        }
    }
}
