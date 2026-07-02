using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Colonies;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the per-province LEGITIMACY substrate (docs/GOVERNMENT-AND-POLITICS-DESIGN.md, task #31):
    /// legitimacy is DERIVED each cycle — its v1 baseline is the local hosts' morale, adjusted by the demand
    /// track-record, war outcomes, governor competence, and connectivity to the capital, each of which is neutral
    /// when unwired. Below the collapse threshold the province is rebelling (the #38 grave-rung hook). Pure math —
    /// mirrors MoraleTests.
    /// </summary>
    [TestFixture]
    public class LegitimacyTests
    {
        private static double Compute(LegitimacyInputs inp) => LegitimacyDB.ComputeLegitimacy(inp, null);

        [Test]
        [Description("v1 baseline: with no other data, legitimacy tracks the province's average morale exactly.")]
        public void Legitimacy_TracksMorale_WhenNothingElseWired()
        {
            Assert.That(Compute(LegitimacyInputs.FromMorale(50.0)), Is.EqualTo(50.0).Within(0.001), "neutral morale → neutral");
            Assert.That(Compute(LegitimacyInputs.FromMorale(80.0)), Is.EqualTo(80.0).Within(0.001), "content province is loyal");
            Assert.That(Compute(LegitimacyInputs.FromMorale(15.0)), Is.EqualTo(15.0).Within(0.001), "miserable province is restless");
        }

        [Test]
        [Description("Unmet demands erode legitimacy; a met track-record does not (it's the baseline).")]
        public void Demands_Erode_WhenUnmet()
        {
            var baseInp = LegitimacyInputs.FromMorale(60.0);

            var unmet = baseInp; unmet.DemandSatisfaction = 0.0;
            Assert.That(Compute(unmet), Is.EqualTo(60.0 - LegitimacyDB.MaxDemandPenalty).Within(0.001));

            var met = baseInp; met.DemandSatisfaction = 1.0;
            Assert.That(Compute(met), Is.EqualTo(60.0).Within(0.001), "meeting demands = no erosion");
        }

        [Test]
        [Description("A recent war win props the regime up; a loss saps it.")]
        public void War_SwingsBothWays()
        {
            var win = LegitimacyInputs.FromMorale(50.0); win.WarOutcome = 1.0;
            var loss = LegitimacyInputs.FromMorale(50.0); loss.WarOutcome = -1.0;
            Assert.That(Compute(win), Is.EqualTo(50.0 + LegitimacyDB.MaxWarSwing).Within(0.001));
            Assert.That(Compute(loss), Is.EqualTo(50.0 - LegitimacyDB.MaxWarSwing).Within(0.001));
        }

        [Test]
        [Description("A capable governor holds a restless province (bonus); poor connectivity to the capital costs.")]
        public void Governor_Helps_Connectivity_Costs()
        {
            var gov = LegitimacyInputs.FromMorale(50.0); gov.GovernorCompetence = 1.0;
            Assert.That(Compute(gov), Is.EqualTo(50.0 + LegitimacyDB.MaxGovernorBonus).Within(0.001));

            var isolated = LegitimacyInputs.FromMorale(50.0); isolated.Connectivity = 0.0;
            Assert.That(Compute(isolated), Is.EqualTo(50.0 - LegitimacyDB.MaxDistancePenalty).Within(0.001));

            var connected = LegitimacyInputs.FromMorale(50.0); connected.Connectivity = 1.0;
            Assert.That(Compute(connected), Is.EqualTo(50.0).Within(0.001), "well-connected = no penalty");
        }

        [Test]
        [Description("Legitimacy clamps to 0..100, and IsCollapsing trips below the collapse threshold (the rebellion hook).")]
        public void Clamps_And_CollapseThreshold()
        {
            // Everything bad at once — clamps at 0, never negative.
            var doomed = new LegitimacyInputs { AverageMorale = 5.0, DemandSatisfaction = 0.0, WarOutcome = -1.0, GovernorCompetence = 0.0, Connectivity = 0.0 };
            double v = Compute(doomed);
            Assert.That(v, Is.EqualTo(0.0).Within(0.001));
            Assert.That(LegitimacyDB.IsCollapsing(v), Is.True, "a floored province is rebelling");

            Assert.That(LegitimacyDB.IsCollapsing(LegitimacyDB.CollapseThreshold - 0.1), Is.True);
            Assert.That(LegitimacyDB.IsCollapsing(LegitimacyDB.CollapseThreshold + 0.1), Is.False);

            // The factors gauge is populated (why, not just the number).
            var factors = new Dictionary<string, double>();
            LegitimacyDB.ComputeLegitimacy(LegitimacyInputs.FromMorale(50.0), factors);
            Assert.That(factors.ContainsKey("morale"), Is.True);
        }

        [Test]
        [Description("Clone deep-copies the factors gauge (moved-between-managers safety).")]
        public void Clone_DeepCopiesFactors()
        {
            var db = new LegitimacyDB();
            db.Factors["morale"] = 42.0;
            var copy = (LegitimacyDB)db.Clone();
            copy.Factors["morale"] = 0.0;
            Assert.That(db.Factors["morale"], Is.EqualTo(42.0), "original untouched");
        }
    }
}
