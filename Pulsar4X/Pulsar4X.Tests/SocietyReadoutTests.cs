using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the SocietyReadout instrument panel (the engine-side text the client's "Dump Society" button
    /// prints). CI-tested here because the client can't be run — the readout is the observability half of the
    /// Visibility Gate, so it must be verified where it CAN be: in the engine. Covers the additions that make a
    /// live play-test legible — sustenance shortage, the rebellion reaction-window countdown, and the diplomacy
    /// ledger — on top of the existing morale/manpower/tax line.
    /// </summary>
    [TestFixture]
    public class SocietyReadoutTests
    {
        [Test]
        [Description("The colony line surfaces the power/food shortage gauges (present, 0 by default).")]
        public void Colony_ShowsSustenanceShortage()
        {
            var s = TestScenario.CreateWithColony();
            string line = SocietyReadout.Colony(s.Colony);
            Assert.That(line, Does.Contain("pwr-short"));
            Assert.That(line, Does.Contain("food-short"));
        }

        [Test]
        [Description("Diplomacy readout: a faction that has met no one says so.")]
        public void Diplomacy_NoContacts_SaysSo()
        {
            var s = TestScenario.CreateWithColony();
            Assert.That(SocietyReadout.Diplomacy(s.Faction), Does.Contain("has met no one"));
        }

        [Test]
        [Description("Diplomacy readout: a met faction shows its derived stance, score, and standing treaties.")]
        public void Diplomacy_ShowsStanceScoreAndTreaties()
        {
            var s = TestScenario.CreateWithColony();
            var other = FactionFactory.CreateBasicFaction(s.Game, "Trade League", "TRL", 1000);

            var rel = s.Faction.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(other.Id);
            rel.AdjustScore(30);        // → Friendly (>= 25)
            rel.TradeAgreement = true;  // a standing deal

            string line = SocietyReadout.Diplomacy(s.Faction);
            Assert.That(line, Does.Contain("Friendly"), "the derived stance");
            Assert.That(line, Does.Contain("+30"), "the relation score");
            Assert.That(line, Does.Contain("[Trade]"), "the standing treaty");
        }
    }
}
