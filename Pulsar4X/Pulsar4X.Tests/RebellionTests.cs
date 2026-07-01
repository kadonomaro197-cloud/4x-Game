using System;
using NUnit.Framework;
using Pulsar4X.Colonies;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the REBELLION state machine (docs/GOVERNMENT-AND-POLITICS-DESIGN.md, locked #38): legitimacy
    /// collapse is no longer a dead-end computation — it now BEGINS a rebellion with a reaction window, which is
    /// QUELLED if legitimacy is restored in time (hysteresis so it can't flicker on the collapse line). Proves the
    /// legitimacy → rebellion loop, and that the factory attaches the state to every province. The window-expiry
    /// resolution (secession/defection) is a later slice; here we test the begin/quell + the window clock.
    /// </summary>
    [TestFixture]
    public class RebellionTests
    {
        private static readonly DateTime T0 = new DateTime(2200, 1, 1);

        [Test]
        [Description("A rebellion BEGINS when legitimacy collapses and QUELLS only once it clears the recovery bar (hysteresis).")]
        public void Rebellion_BeginsOnCollapse_QuellsOnRecovery()
        {
            var reb = new RebellionDB();

            // Healthy legitimacy → no rebellion.
            LegitimacyProcessor.UpdateRebellion(reb, 60.0, T0);
            Assert.That(reb.IsRebelling, Is.False);

            // Collapse (< 20) → rebellion begins + the reaction window opens.
            LegitimacyProcessor.UpdateRebellion(reb, 10.0, T0);
            Assert.That(reb.IsRebelling, Is.True);
            Assert.That(reb.StartDate, Is.EqualTo(T0));
            Assert.That(reb.ReactionWindowEnds, Is.EqualTo(T0 + TimeSpan.FromDays(RebellionDB.ReactionWindowDays)));

            // Between the collapse line (20) and the recovery bar (35): STILL rebelling — hysteresis.
            LegitimacyProcessor.UpdateRebellion(reb, 30.0, T0);
            Assert.That(reb.IsRebelling, Is.True, "must clear the recovery bar, not just the collapse line");

            // Restored above the recovery bar → quelled.
            LegitimacyProcessor.UpdateRebellion(reb, 40.0, T0);
            Assert.That(reb.IsRebelling, Is.False, "legitimacy restored within the window quells it");
        }

        [Test]
        [Description("The reaction window clock: WindowExpired is false inside the window, true once it lapses (and only while rebelling).")]
        public void ReactionWindow_ExpiresAfterItsSpan()
        {
            var reb = new RebellionDB();
            Assert.That(reb.WindowExpired(T0), Is.False, "not rebelling → never expired");

            LegitimacyProcessor.UpdateRebellion(reb, 5.0, T0);   // begin
            Assert.That(reb.WindowExpired(T0.AddDays(1)), Is.False, "inside the window");
            Assert.That(reb.WindowExpired(T0.AddDays(RebellionDB.ReactionWindowDays + 1)), Is.True, "past the window");
        }

        [Test]
        [Description("Live wiring: the factory attaches a RebellionDB, and tanking a colony's morale collapses its legitimacy and triggers rebellion through the processor; restoring morale quells it.")]
        public void Colony_Collapse_TriggersRebellion_ThroughProcessor()
        {
            var s = TestScenario.CreateWithColony();
            Assert.That(s.Colony.HasDataBlob<RebellionDB>(), Is.True, "the factory attaches RebellionDB");

            var morale = s.Colony.GetDataBlob<ColonyMoraleDB>();
            var reb = s.Colony.GetDataBlob<RebellionDB>();

            morale.Morale = 5.0;   // misery → legitimacy collapses
            LegitimacyProcessor.RecalcLegitimacy(s.Colony);
            Assert.That(reb.IsRebelling, Is.True, "a collapsed colony rebels");

            morale.Morale = 80.0;  // restore order
            LegitimacyProcessor.RecalcLegitimacy(s.Colony);
            Assert.That(reb.IsRebelling, Is.False, "restored legitimacy quells the rebellion");
        }
    }
}
