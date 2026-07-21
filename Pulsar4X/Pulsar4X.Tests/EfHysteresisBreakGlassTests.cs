using System;
using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Operation Earthfall P3.3 gauge (findings/A3-objective-flip.md): the CRISIS HYSTERESIS BREAK-GLASS. The A3 log
    /// found a one-month phantom rebellion locked the UMF into Defend for 180 days at the Survive floor, because the
    /// commit was stamped at Tier=Survive (enum 0) — making the only existing break-glass (proposedTier &lt; currentTier)
    /// mathematically UNREACHABLE (nothing is more urgent than the floor). These tests pin the three P3.3 releases that
    /// fix it WITHOUT sharing that flaw: (a) a crisis objective holds the shorter <see cref="ObjectiveTransition.CrisisCommitFor"/>
    /// dwell, (b) it releases the instant the specific condition that forced it clears, and (c) it releases after
    /// <see cref="ObjectiveTransition.ContradictionReleaseCycles"/> consecutive cycles the ladder proposes a HIGHER tier.
    /// Pure — drives <see cref="ObjectiveTransition"/> directly (deterministic, no sim harness), the same style as
    /// <c>ObjectiveTransitionTests</c>.
    /// </summary>
    [TestFixture]
    public class EfHysteresisBreakGlassTests
    {
        private static readonly DateTime T0 = new DateTime(2050, 1, 1);

        [Test]
        [Description("(a) A crisis objective (Defend/Consolidate) holds the SHORTER crisis dwell; expansion + peaceful aims are unchanged.")]
        public void CrisisObjective_HoldsShorterDwell_ExpansionAndPeacefulUnchanged()
        {
            Assert.That(ObjectiveTransition.CrisisCommitFor, Is.LessThan(ObjectiveTransition.DefaultCommitFor),
                "a crisis plan must re-plan sooner than a normal one");

            // Crisis-response objectives → the shorter crisis dwell.
            Assert.That(ObjectiveTransition.CommitFor(StrategicObjective.Defend, null),
                Is.EqualTo(ObjectiveTransition.CrisisCommitFor), "Defend holds the crisis dwell");
            Assert.That(ObjectiveTransition.CommitFor(StrategicObjective.Consolidate, null),
                Is.EqualTo(ObjectiveTransition.CrisisCommitFor), "Consolidate holds the crisis dwell");

            // Expansion push (incl. a war-footing Conquer) → the Ambition-scaled default (byte-identical to pre-P3.3).
            Assert.That(ObjectiveTransition.CommitFor(StrategicObjective.Conquer, null),
                Is.EqualTo(ObjectiveTransition.DefaultCommitFor), "Conquer keeps the (neutral) default dwell");
            Assert.That(ObjectiveTransition.CommitFor(StrategicObjective.Expand, null),
                Is.EqualTo(ObjectiveTransition.DefaultCommitFor), "Expand keeps the (neutral) default dwell");

            // Every peaceful growth aim keeps the fixed default.
            Assert.That(ObjectiveTransition.CommitFor(StrategicObjective.GrowEconomy, null),
                Is.EqualTo(ObjectiveTransition.DefaultCommitFor), "GrowEconomy keeps the fixed dwell");
            Assert.That(ObjectiveTransition.CommitFor(StrategicObjective.AdvanceTech, null),
                Is.EqualTo(ObjectiveTransition.DefaultCommitFor), "AdvanceTech keeps the fixed dwell");
            Assert.That(ObjectiveTransition.CommitFor(StrategicObjective.None, null),
                Is.EqualTo(ObjectiveTransition.DefaultCommitFor), "None keeps the fixed dwell");
        }

        [Test]
        [Description("The CRITICAL BUG: the downward proposedTier<currentTier break-glass is unreachable at the Survive floor; "
            + "the P3.3 (b)/(c) releases ARE reachable from it.")]
        public void SurviveFloorCommit_DownwardBreakGlassIsUnreachable_ButTheP33ReleasesAreNot()
        {
            var committedUntil = T0 + ObjectiveTransition.DefaultCommitFor;
            var mid = T0.AddDays(30); // well inside the commitment, so only a break-glass could release it

            // Committed at the Survive floor: NO proposed tier is "more urgent" (Survive=0 is the minimum), so the
            // original downward break-glass can never fire before expiry — the exact A3 lock.
            foreach (NeedTier proposed in new[] { NeedTier.Survive, NeedTier.Stabilize, NeedTier.Thrive, NeedTier.Ambition })
                Assert.That(ObjectiveTransition.ShouldReplan(NeedTier.Survive, committedUntil, proposed, mid), Is.False,
                    $"nothing preempts a Survive-floor commit before expiry (proposed {proposed})");

            // But the two P3.3 crisis releases DO fire from the floor — they don't rely on the dead tier compare.
            Assert.That(ObjectiveTransition.ShouldReplan(NeedTier.Survive, committedUntil, NeedTier.Ambition, mid, triggerCleared: true),
                Is.True, "(b) the trigger clearing releases a Survive-floor commit");
            Assert.That(ObjectiveTransition.ShouldReplan(NeedTier.Survive, committedUntil, NeedTier.Ambition, mid, contradictionReleased: true),
                Is.True, "(c) a persistent higher-tier contradiction releases a Survive-floor commit");
        }

        [Test]
        [Description("(b) A crisis commit records what forced it, and releases early the instant those conditions clear.")]
        public void TriggerCleared_ReleasesTheDefendCommit_Early()
        {
            var obj = new StrategicObjectiveDB();

            // Commit Defend at Survive, forced by a rebellion + collapsed morale/legitimacy.
            var crisis = ObjectiveTransition.CrisisTriggersFrom(atWar: false, ownStrength: 0, enemyStrength: 0,
                meanMorale: 15, meanLegitimacy: 15, balance: 1000m, inRebellion: true);
            Assert.That(ObjectiveTransition.Advance(obj, NeedTier.Survive, StrategicObjective.Defend, -1, T0,
                ObjectiveTransition.CrisisCommitFor, crisis), Is.True);
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.Defend));
            // The commit recorded the Survive-rung conditions (masked to Defend's tier).
            Assert.That(obj.CommitTrigger, Is.EqualTo(CrisisTrigger.Rebellion | CrisisTrigger.MoraleCollapse | CrisisTrigger.LegitimacyCollapse),
                "the Defend commit recorded its Survive-rung triggers");

            // A cycle later the rebellion is quelled and morale/legitimacy have recovered (only the ongoing war remains,
            // an AtWar flag that is NOT in Defend's Survive mask), and the ladder proposes Conquer at Stabilize.
            var recovered = ObjectiveTransition.CrisisTriggersFrom(atWar: true, ownStrength: 100, enemyStrength: 50,
                meanMorale: 55, meanLegitimacy: 50, balance: 1000m, inRebellion: false);
            // Without (b) this would HOLD (proposedTier Stabilize is not more urgent than Survive, and the dwell hasn't
            // expired). (b) releases it because none of the recorded Survive triggers still holds.
            Assert.That(ObjectiveTransition.Advance(obj, NeedTier.Stabilize, StrategicObjective.Conquer, 7, T0.AddDays(1),
                ObjectiveTransition.CommitFor(StrategicObjective.Conquer, null), recovered), Is.True,
                "the crisis trigger cleared → early release");
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.Conquer), "returned to Conquer once the crisis passed");

            // Complement: while a recorded trigger PERSISTS, (b) does not fire — a re-commit of Defend is held.
            var obj2 = new StrategicObjectiveDB();
            ObjectiveTransition.Advance(obj2, NeedTier.Survive, StrategicObjective.Defend, -1, T0, ObjectiveTransition.CrisisCommitFor, crisis);
            Assert.That(ObjectiveTransition.Advance(obj2, NeedTier.Survive, StrategicObjective.Defend, -1, T0.AddDays(1),
                ObjectiveTransition.CrisisCommitFor, crisis), Is.False, "the trigger still holds → no early release");
            Assert.That(obj2.Objective, Is.EqualTo(StrategicObjective.Defend));
        }

        [Test]
        [Description("(c) N consecutive higher-tier proposals release a crisis commit even when (b) can't fire (no recorded trigger).")]
        public void PersistentContradiction_ReleasesAfterN_Cycles()
        {
            var obj = new StrategicObjectiveDB();

            // Commit Defend at Survive with NO recorded trigger (currentTriggers None) so (b) is structurally disabled —
            // this ISOLATES the contradiction debounce (c). Use the DEFAULT dwell so expiry can't fire within N days.
            Assert.That(ObjectiveTransition.Advance(obj, NeedTier.Survive, StrategicObjective.Defend, -1, T0,
                ObjectiveTransition.DefaultCommitFor, CrisisTrigger.None), Is.True);
            Assert.That(obj.CommitTrigger, Is.EqualTo(CrisisTrigger.None), "no trigger recorded → (b) can't fire, only (c)");

            // The ladder proposes a strictly HIGHER tier every day. Held for the first N-1 cycles, the counter climbing.
            for (int cycle = 1; cycle < ObjectiveTransition.ContradictionReleaseCycles; cycle++)
            {
                bool replanned = ObjectiveTransition.Advance(obj, NeedTier.Ambition, StrategicObjective.Conquer, -1,
                    T0.AddDays(cycle), ObjectiveTransition.DefaultCommitFor, CrisisTrigger.None);
                Assert.That(replanned, Is.False, $"held at contradiction cycle {cycle} (< {ObjectiveTransition.ContradictionReleaseCycles})");
                Assert.That(obj.ContradictionCycles, Is.EqualTo(cycle), "the contradiction counter climbs each consecutive cycle");
                Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.Defend), "still Defend while the counter climbs");
            }

            // The N-th consecutive contradiction releases the commit for a re-plan.
            Assert.That(ObjectiveTransition.Advance(obj, NeedTier.Ambition, StrategicObjective.Conquer, -1,
                T0.AddDays(ObjectiveTransition.ContradictionReleaseCycles), ObjectiveTransition.DefaultCommitFor, CrisisTrigger.None),
                Is.True, "N consecutive higher-tier proposals → release");
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.Conquer), "released and re-planned to the proposed objective");
            Assert.That(obj.ContradictionCycles, Is.EqualTo(0), "a fresh commit resets the debounce");
        }

        [Test]
        [Description("A non-consecutive contradiction RESETS the streak — a flickering gauge must not unwind a real crisis plan.")]
        public void Contradiction_ResetsOnANonConsecutiveRead()
        {
            var obj = new StrategicObjectiveDB();
            ObjectiveTransition.Advance(obj, NeedTier.Survive, StrategicObjective.Defend, -1, T0,
                ObjectiveTransition.DefaultCommitFor, CrisisTrigger.None);

            // Two higher-tier reads climb the counter to 2 ...
            ObjectiveTransition.Advance(obj, NeedTier.Ambition, StrategicObjective.Conquer, -1, T0.AddDays(1), ObjectiveTransition.DefaultCommitFor, CrisisTrigger.None);
            ObjectiveTransition.Advance(obj, NeedTier.Ambition, StrategicObjective.Conquer, -1, T0.AddDays(2), ObjectiveTransition.DefaultCommitFor, CrisisTrigger.None);
            Assert.That(obj.ContradictionCycles, Is.EqualTo(2));

            // ... then one cycle BACK at the Survive floor (still in crisis) breaks the streak.
            ObjectiveTransition.Advance(obj, NeedTier.Survive, StrategicObjective.Defend, -1, T0.AddDays(3), ObjectiveTransition.DefaultCommitFor, CrisisTrigger.None);
            Assert.That(obj.ContradictionCycles, Is.EqualTo(0), "a non-consecutive read restarts the debounce");
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.Defend), "still Defend — the streak reset, no release");
        }

        [Test]
        [Description("The A3 log's exact scenario: 1-month rebellion → quell → daily Conquer reads returns to Conquer within DAYS.")]
        public void LogScenario_OneMonthRebellionThenQuell_ReturnsToConquerWithinDays()
        {
            var obj = new StrategicObjectiveDB();

            // The UMF was pursuing its war (Conquer, war-footing at Stabilize).
            var start = T0;
            Assert.That(ObjectiveTransition.Advance(obj, NeedTier.Stabilize, StrategicObjective.Conquer, 7, start,
                ObjectiveTransition.CommitFor(StrategicObjective.Conquer, null), CrisisTrigger.None), Is.True);
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.Conquer));

            // A one-month phantom rebellion hits → Survive → Defend preempts (the downward break-glass DOES fire here,
            // because the current commit is Conquer at Stabilize=1 and Survive=0 < 1 — this is the legitimate path).
            var rebellionDay = start.AddDays(40);
            var crisis = ObjectiveTransition.CrisisTriggersFrom(atWar: false, ownStrength: 100, enemyStrength: 50,
                meanMorale: 15, meanLegitimacy: 15, balance: 1000m, inRebellion: true);
            Assert.That(ObjectiveTransition.Advance(obj, NeedTier.Survive, StrategicObjective.Defend, -1, rebellionDay,
                ObjectiveTransition.CrisisCommitFor, crisis), Is.True);
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.Defend), "the rebellion forced Defend");
            Assert.That(obj.Tier, Is.EqualTo(NeedTier.Survive));

            // The rebellion is quelled a month later; from here the ladder reads Conquer every DAY (winning war, no
            // revolt, morale/legitimacy recovered). Pre-P3.3 this held Defend until rebellionDay + 180d (the 6-month lock).
            var quellDay = rebellionDay.AddDays(30);
            var recovered = ObjectiveTransition.CrisisTriggersFrom(atWar: true, ownStrength: 100, enemyStrength: 50,
                meanMorale: 55, meanLegitimacy: 50, balance: 1000m, inRebellion: false);
            DateTime returnedOn = DateTime.MinValue;
            for (int day = 0; day < 10; day++)
            {
                var t = quellDay.AddDays(day);
                ObjectiveTransition.Advance(obj, NeedTier.Stabilize, StrategicObjective.Conquer, 7, t,
                    ObjectiveTransition.CommitFor(StrategicObjective.Conquer, null), recovered);
                if (obj.Objective == StrategicObjective.Conquer) { returnedOn = t; break; }
            }
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.Conquer), "the brain returns to Conquer once the rebellion clears");
            Assert.That((returnedOn - quellDay).TotalDays, Is.LessThanOrEqualTo(2), "within DAYS of the quell, not the 6-month lock");
        }

        [Test]
        [Description("Complement: a GENUINE sustained crisis still holds Defend (the shorter dwell re-commits Defend while the crisis persists).")]
        public void SustainedCrisis_HoldsDefend_ThroughoutTheCrisis()
        {
            var obj = new StrategicObjectiveDB();
            var start = T0;
            var crisis = ObjectiveTransition.CrisisTriggersFrom(atWar: false, ownStrength: 100, enemyStrength: 50,
                meanMorale: 15, meanLegitimacy: 15, balance: 1000m, inRebellion: true);
            Assert.That(ObjectiveTransition.Advance(obj, NeedTier.Survive, StrategicObjective.Defend, -1, start,
                ObjectiveTransition.CrisisCommitFor, crisis), Is.True);

            // The crisis PERSISTS every day (still at Survive, still proposing Defend): (b) can't fire (the trigger
            // holds), (c) can't accumulate (the tier never rises above the commit), and when the shorter dwell expires
            // it simply re-commits Defend. Over 120 days (past the 60-day CrisisCommitFor, so we cross a re-commit),
            // the objective must never abandon Defend.
            for (int day = 1; day <= 120; day++)
            {
                ObjectiveTransition.Advance(obj, NeedTier.Survive, StrategicObjective.Defend, -1, start.AddDays(day),
                    ObjectiveTransition.CrisisCommitFor, crisis);
                Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.Defend),
                    $"still holding Defend on day {day} (a genuine sustained crisis)");
            }
            Assert.That(obj.ContradictionCycles, Is.EqualTo(0), "a sustained Survive crisis never accumulates a contradiction");
        }

        [Test]
        [Description("A NON-crisis commit is untouched by the crisis break-glass — the counter never ticks, the release paths never fire.")]
        public void NonCrisisCommit_IgnoresTheCrisisBreakGlass()
        {
            var obj = new StrategicObjectiveDB();

            // Commit GrowEconomy (Thrive) — not a crisis objective.
            Assert.That(ObjectiveTransition.Advance(obj, NeedTier.Thrive, StrategicObjective.GrowEconomy, -1, T0,
                ObjectiveTransition.DefaultCommitFor, CrisisTrigger.None), Is.True);
            Assert.That(obj.CommitTrigger, Is.EqualTo(CrisisTrigger.None), "a non-crisis commit records no trigger");

            // Even a strictly HIGHER-tier proposal every day for well past N cycles never releases it and never
            // advances the contradiction counter — the crisis break-glass is crisis-only. It holds by ordinary
            // hysteresis until expiry.
            for (int day = 1; day <= ObjectiveTransition.ContradictionReleaseCycles + 5; day++)
            {
                bool replanned = ObjectiveTransition.Advance(obj, NeedTier.Ambition, StrategicObjective.Conquer, -1,
                    T0.AddDays(day), ObjectiveTransition.DefaultCommitFor, CrisisTrigger.None);
                Assert.That(replanned, Is.False, $"a non-crisis plan holds through day {day} (contradiction release is crisis-only)");
            }
            Assert.That(obj.Objective, Is.EqualTo(StrategicObjective.GrowEconomy), "held the peaceful plan");
            Assert.That(obj.ContradictionCycles, Is.EqualTo(0), "the contradiction counter never advanced for a non-crisis commit");
        }

        [Test]
        [Description("CrisisTriggersFrom mirrors the NeedsLadder tier predicates (reusing its thresholds, so they can't drift).")]
        public void CrisisTriggersFrom_MirrorsTheNeedsLadderPredicates()
        {
            // Rebellion + collapsed morale/legitimacy → the Survive-rung flags.
            var survive = ObjectiveTransition.CrisisTriggersFrom(atWar: false, ownStrength: 0, enemyStrength: 0,
                meanMorale: NeedsLadder.MoraleCrisis, meanLegitimacy: NeedsLadder.LegitimacyCrisis, balance: 1000m, inRebellion: true);
            Assert.That(survive.HasFlag(CrisisTrigger.Rebellion));
            Assert.That(survive.HasFlag(CrisisTrigger.MoraleCollapse));
            Assert.That(survive.HasFlag(CrisisTrigger.LegitimacyCollapse));

            // A healthy faction → no crisis flags at all.
            var healthy = ObjectiveTransition.CrisisTriggersFrom(atWar: false, ownStrength: 100, enemyStrength: 0,
                meanMorale: 60, meanLegitimacy: 60, balance: 1000m, inRebellion: false);
            Assert.That(healthy, Is.EqualTo(CrisisTrigger.None), "a healthy faction has no crisis triggers");

            // At war (not losing) with mild unrest → the Stabilize-rung flags, no Survive-rung ones.
            var stabilize = ObjectiveTransition.CrisisTriggersFrom(atWar: true, ownStrength: 100, enemyStrength: 100,
                meanMorale: 40, meanLegitimacy: 40, balance: 1000m, inRebellion: false);
            Assert.That(stabilize.HasFlag(CrisisTrigger.AtWar));
            Assert.That(stabilize.HasFlag(CrisisTrigger.MoraleUnhealthy));
            Assert.That(stabilize.HasFlag(CrisisTrigger.LegitimacyUnhealthy));
            Assert.That(stabilize.HasFlag(CrisisTrigger.LosingWar), Is.False, "equal strength is not 'losing'");
            Assert.That(stabilize.HasFlag(CrisisTrigger.Rebellion), Is.False, "no rebellion → no Rebellion flag");
        }
    }
}
