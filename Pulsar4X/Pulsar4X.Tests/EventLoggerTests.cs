using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Events;       // Event, EventType
using Pulsar4X.Extensions;   // GetDefaultName
using Pulsar4X.Factions;     // FactionInfoDB, FactionEventLog
using Pulsar4X.Sensors;      // SensorEvents

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Event Logger mechanic (2026-07-16) — the engine half. When the player's faction detects an enemy fleet, a
    /// NewHostileContact event fires; the faction's event log HALTS the clock on it AND drops the fast-forward step
    /// back to 1-hour steps (so un-pausing while fast-forwarding months/years doesn't blast past the danger — the
    /// developer's key requirement). These gauge the two engine pieces: the halt+step-reset (driven directly, no
    /// EventManager static state), and the "nearest large body" the alert message names. The detection→publish hook
    /// in SensorScan is live-verified (the client UI reads the same log — slice 2).
    /// </summary>
    [TestFixture]
    public class EventLoggerTests
    {
        [Test]
        [Description("A halt-on event pauses AND resets the step size to 1 hour — so a fast-forward in months drops back to careful stepping.")]
        public void HaltEvent_ResetsFastForwardToOneHourStep()
        {
            var s = TestScenario.CreateWithColony();
            var tp = s.Game.TimePulse;
            tp.Ticklength = TimeSpan.FromDays(30);   // pretend the player was fast-forwarding a month at a time

            var log = (FactionEventLog)s.Faction.GetDataBlob<FactionInfoDB>().EventLog;
            log.ToggleHaltsOn(EventType.NewHostileContact);   // the player opts in to halting on enemy detection

            var ev = Event.Create(EventType.NewHostileContact, tp.GameGlobalDateTime,
                "Enemy Fleet detected at Mars.", factionId: s.Faction.Id);
            log.OnEvent(ev);

            Assert.That(tp.Ticklength, Is.EqualTo(TimeSpan.FromSeconds(3600)), "the alert reset the step size to 1 hour");
            Assert.That(log.GetEvents().Any(e => e.EventType == EventType.NewHostileContact), Is.True, "the event was logged");
        }

        [Test]
        [Description("An event type NOT in the halt set leaves the step size alone — only opted-in alerts reset the clock.")]
        public void NonHaltEvent_LeavesStepSizeAlone()
        {
            var s = TestScenario.CreateWithColony();
            var tp = s.Game.TimePulse;
            tp.Ticklength = TimeSpan.FromDays(30);

            var log = (FactionEventLog)s.Faction.GetDataBlob<FactionInfoDB>().EventLog;
            // NOT toggling HaltsOn(NewHostileContact) → this event should not reset the step.
            log.OnEvent(Event.Create(EventType.NewHostileContact, tp.GameGlobalDateTime, "x", factionId: s.Faction.Id));

            Assert.That(tp.Ticklength, Is.EqualTo(TimeSpan.FromDays(30)), "a non-halting event must not touch the step size");
        }

        [Test]
        [Description("The alert names the nearest LARGE body — for a query at a planet, that planet (distance 0).")]
        public void NearestLargeBodyName_NamesTheClosestPlanet()
        {
            var s = TestScenario.CreateWithColony();
            var name = SensorEvents.NearestLargeBodyName(s.StartingSystem, s.StartingBody);
            Assert.That(name, Is.EqualTo(s.StartingBody.GetDefaultName()),
                "the nearest large body to a planet is that planet");
        }
    }
}
