using System;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// P3.3 (Operation Earthfall, findings/A3-objective-flip.md): the flags of the crisis conditions that FORCED a
    /// crisis-response commit (Defend/Consolidate). A [Flags] enum so a single Defend commit can record "rebellion AND
    /// collapsed legitimacy" at once; <see cref="ObjectiveTransition.Advance"/> releases that commit the instant NONE of
    /// the recorded flags still holds (the trigger-cleared break-glass). The bit values are explicit + only ever
    /// APPENDED, so it stays save-safe (enums serialize by int; <see cref="None"/> = 0 is the inert default an older
    /// save with no value loads to). The predicates mirror <see cref="NeedsLadder.AssessTier"/> exactly (built in
    /// <see cref="ObjectiveTransition.CrisisTriggersFrom"/> from the SAME NeedsLadder thresholds).
    /// </summary>
    [Flags]
    public enum CrisisTrigger
    {
        /// <summary>No crisis condition recorded — the inert default for a non-crisis commit (and old saves).</summary>
        None = 0,

        // --- Survive-rung conditions (the ones that force Defend) ---
        /// <summary>A colony is in open rebellion.</summary>
        Rebellion = 1 << 0,
        /// <summary>Mean morale at or below <see cref="NeedsLadder.MoraleCrisis"/> — an existential collapse.</summary>
        MoraleCollapse = 1 << 1,
        /// <summary>Mean legitimacy at or below <see cref="NeedsLadder.LegitimacyCrisis"/> — the mandate is gone.</summary>
        LegitimacyCollapse = 1 << 2,
        /// <summary>At war and own strength below enemy × <see cref="NeedsLadder.LosingWarRatio"/> — losing badly.</summary>
        LosingWar = 1 << 3,

        // --- Stabilize-rung conditions (the ones that force Consolidate when no Survive condition holds) ---
        /// <summary>At war (but not being lost) — the war itself demands attention.</summary>
        AtWar = 1 << 4,
        /// <summary>Treasury below zero — bleeding money.</summary>
        Insolvent = 1 << 5,
        /// <summary>Mean morale below <see cref="NeedsLadder.MoraleHealthy"/> — unhealthy, needs shoring up.</summary>
        MoraleUnhealthy = 1 << 6,
        /// <summary>Mean legitimacy below <see cref="NeedsLadder.LegitimacyHealthy"/> — needs shoring up.</summary>
        LegitimacyUnhealthy = 1 << 7,
    }

    /// <summary>
    /// Phase-2.3 (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II — the Organism engine): the TRANSITION ENGINE. The
    /// needs-ladder (2.2) re-reads a faction's tier every cycle, but a brain that re-planned every month would
    /// THRASH — abandon a half-built fleet the moment a gauge wobbles. This is the hysteresis that stops that: once
    /// a faction commits to an objective it HOLDS it for a while, and only re-plans when the commitment expires OR a
    /// genuinely more urgent need appears (an emergency always preempts a commitment — you drop the expansion plan
    /// when the capital rebels).
    ///
    /// P3.3 (Operation Earthfall, findings/A3-objective-flip.md) adds three CRISIS break-glasses so a crisis-response
    /// commit (Defend/Consolidate) can't LOCK the brain the way a one-month phantom rebellion pinned the UMF on Defend
    /// for 180 days at the Survive floor: (a) a crisis commit holds the shorter <see cref="CrisisCommitFor"/> dwell,
    /// (b) it releases the instant the specific condition that FORCED it clears (<see cref="CrisisTrigger"/>), and
    /// (c) it releases after <see cref="ContradictionReleaseCycles"/> consecutive cycles the ladder proposes a HIGHER
    /// tier than the one committed. The critical reason (b)/(c) are shaped the way they are: the original downward
    /// <c>proposedTier &lt; currentTier</c> preempt is MATHEMATICALLY UNREACHABLE for a Survive-floor commit — nothing
    /// is more urgent than Survive (enum 0, the floor) — so (b) is deliberately condition-based (not a tier compare)
    /// and (c) counts UPWARD (proposedTier &gt; committedTier), the reachable mirror of the dead break-glass. The
    /// non-crisis expansion aims (Expand/Conquer) are untouched — they keep the Ambition-scaled dwell + plain hysteresis.
    ///
    /// Stateless engine (no static state of its own); the only mutation is stamping the passed
    /// <see cref="StrategicObjectiveDB"/>. The Tick (2.4) drives it: assess tier → pick objective → <see cref="Advance"/>.
    /// </summary>
    public static class ObjectiveTransition
    {
        /// <summary>How long a freshly-committed objective is held before the brain is free to re-plan it (absent a
        /// more urgent need). Long enough that a multi-cycle build (a fleet, a colony) isn't abandoned mid-way.</summary>
        public static readonly TimeSpan DefaultCommitFor = TimeSpan.FromDays(180);

        /// <summary>
        /// Phase-2.5 (docs/AI-BRAIN-BUILD-TRACKER.md — the Ambition CADENCE): how far the AMBITION trait swings an
        /// expansion objective's dwell away from <see cref="DefaultCommitFor"/> at the trait extremes. At 1.0 the dwell
        /// runs from 0.5× (Ambition 1.0 — half the dwell, so the brain re-commits to the expansion push twice as often)
        /// up to 1.5× (Ambition 0.0 — a low-drive faction sits on its plan longer). Named so the swing tunes in one
        /// place; provisional/live-tunable.
        /// </summary>
        public static readonly double AmbitionCadenceSpread = 1.0;

        /// <summary>
        /// P3.3 (findings/A3): the SHORTER dwell a crisis-response objective (<see cref="StrategicObjective.Defend"/> /
        /// <see cref="StrategicObjective.Consolidate"/>) holds before the brain is free to re-plan it. A crisis is a
        /// reaction to a shock that often passes fast; a full <see cref="DefaultCommitFor"/> lock is what let a one-month
        /// rebellion pin a faction on Defend for six months. This is the OUTERMOST of the three crisis break-glasses —
        /// even if the trigger-cleared (b) and contradiction (c) releases both miss, a crisis plan re-plans within it.
        /// </summary>
        public static readonly TimeSpan CrisisCommitFor = TimeSpan.FromDays(60);   // FLAGGED balance value

        /// <summary>
        /// P3.3 (findings/A3): the contradiction-release debounce. If the needs-ladder proposes a STRICTLY HIGHER tier
        /// than the one a crisis objective is committed at for this many consecutive cycles (the brain ticks daily), the
        /// commit is released for a re-plan — the reachable-from-the-floor mirror of the (unreachable at Survive)
        /// <c>proposedTier &lt; currentTier</c> break-glass. Long enough that a flickering gauge doesn't unwind a real
        /// crisis plan, short enough that a passed crisis doesn't linger for the full dwell.
        /// </summary>
        public const int ContradictionReleaseCycles = 14;   // FLAGGED balance value

        /// <summary>
        /// Phase-2.5: how long a faction dwells on a freshly-committed objective before it's free to re-plan, as a
        /// function of its AMBITION trait. The base is <see cref="DefaultCommitFor"/>; for an EXPANSION-family aim
        /// (<see cref="StrategicObjective.Expand"/> / <see cref="StrategicObjective.Conquer"/> — the Ambition-tier grand
        /// aims Aggression flips between) a HIGH-Ambition faction dwells SHORTER — it renews the expansion push on a
        /// faster cadence — while a LOW-Ambition faction dwells LONGER. A CRISIS-response aim (Defend/Consolidate) holds
        /// the shorter <see cref="CrisisCommitFor"/> (P3.3 break-glass a). Every OTHER objective, and a NEUTRAL (0.5) or
        /// absent <paramref name="personality"/>, returns exactly <see cref="DefaultCommitFor"/> → byte-identical with
        /// the pre-2.5 fixed dwell. Pure/stateless; the expansion branch is monotonic DECREASING in Ambition.
        /// </summary>
        public static TimeSpan CommitFor(StrategicObjective objective, PersonalityDB personality)
        {
            // The expansion push (Expand/Conquer, INCLUDING a war-footing Conquer picked from a low tier): the
            // Ambition-scaled dwell — UNCHANGED by P3.3.
            if (IsExpansionAim(objective))
            {
                double ambition = personality == null
                    ? PersonalityDB.Neutral
                    : personality.TraitOf(PersonalityTrait.Ambition);

                // Dwell factor: 1 at neutral (multiply-by-1.0 is exact → byte-identical), <1 above neutral (shorter
                // dwell, push more often), >1 below neutral (longer dwell). Monotonic decreasing in Ambition.
                double factor = 1.0 + AmbitionCadenceSpread * (PersonalityDB.Neutral - ambition);
                return TimeSpan.FromTicks((long)(DefaultCommitFor.Ticks * factor));
            }

            // P3.3 break-glass (a): a crisis-response objective (Defend from Survive, Consolidate from Stabilize) holds
            // a SHORTER dwell than a normal plan, so a crisis that passes fast doesn't lock the brain for six months.
            if (IsCrisisObjective(objective))
                return CrisisCommitFor;

            return DefaultCommitFor;   // every other objective keeps the fixed default dwell
        }

        /// <summary>The Ambition-tier grand aims whose commit cadence scales with the Ambition trait: settle new worlds
        /// (<see cref="StrategicObjective.Expand"/>) or take them by force (<see cref="StrategicObjective.Conquer"/> —
        /// the aggressive form of the same expansion drive). Every other objective keeps the fixed default dwell.</summary>
        private static bool IsExpansionAim(StrategicObjective objective)
            => objective == StrategicObjective.Expand || objective == StrategicObjective.Conquer;

        /// <summary>The CRISIS-response objectives whose commit is shock-driven and gets the shorter
        /// <see cref="CrisisCommitFor"/> dwell + the (b)/(c) early-release paths: hold the line under an existential
        /// threat (<see cref="StrategicObjective.Defend"/>, from the Survive tier) or quell internal trouble
        /// (<see cref="StrategicObjective.Consolidate"/>, from Stabilize). The expansion aims (Expand/Conquer) are NOT
        /// crisis objectives even when a war-footing picks Conquer from a low tier — they keep the Ambition-scaled dwell
        /// and the ordinary hysteresis, so this stays byte-identical for them.</summary>
        public static bool IsCrisisObjective(StrategicObjective objective)
            => objective == StrategicObjective.Defend || objective == StrategicObjective.Consolidate;

        /// <summary>
        /// P3.3 (findings/A3): the crisis conditions active for a faction with these gauges, as flags — the SAME
        /// predicates <see cref="NeedsLadder.AssessTier"/> reads to settle the Survive/Stabilize rungs (thresholds
        /// reused straight from NeedsLadder so the two never drift). The Tick passes this each cycle; a fresh crisis
        /// commit records the flags relevant to its tier (<see cref="CrisisMaskFor"/>) so <see cref="Advance"/> can
        /// release it the instant those specific conditions clear (release b) — a condition read, not the tier compare
        /// that dies at the Survive floor. Pure.
        /// </summary>
        public static CrisisTrigger CrisisTriggersFrom(bool atWar, double ownStrength, double enemyStrength,
            double meanMorale, double meanLegitimacy, decimal balance, bool inRebellion)
        {
            var t = CrisisTrigger.None;

            // Survive-rung conditions (force Defend).
            if (inRebellion) t |= CrisisTrigger.Rebellion;
            if (meanMorale <= NeedsLadder.MoraleCrisis) t |= CrisisTrigger.MoraleCollapse;
            if (meanLegitimacy <= NeedsLadder.LegitimacyCrisis) t |= CrisisTrigger.LegitimacyCollapse;
            if (atWar && ownStrength < enemyStrength * NeedsLadder.LosingWarRatio) t |= CrisisTrigger.LosingWar;

            // Stabilize-rung conditions (force Consolidate when no Survive condition holds).
            if (atWar) t |= CrisisTrigger.AtWar;
            if (balance < 0m) t |= CrisisTrigger.Insolvent;
            if (meanMorale < NeedsLadder.MoraleHealthy) t |= CrisisTrigger.MoraleUnhealthy;
            if (meanLegitimacy < NeedsLadder.LegitimacyHealthy) t |= CrisisTrigger.LegitimacyUnhealthy;

            return t;
        }

        /// <summary>The subset of <see cref="CrisisTrigger"/> flags relevant to a crisis objective's own tier — a Defend
        /// commit is defined by the Survive-rung conditions, a Consolidate commit by the Stabilize-rung ones. Recording
        /// only the tier-relevant flags means a Defend commit releases (path b) once the faction climbs OUT of the
        /// existential crisis, even if it's still in Stabilize territory — a partial recovery doesn't have to become a
        /// full one to re-plan. A non-crisis objective masks to <see cref="CrisisTrigger.None"/> → records nothing.</summary>
        private static CrisisTrigger CrisisMaskFor(StrategicObjective objective)
        {
            switch (objective)
            {
                case StrategicObjective.Defend:
                    return CrisisTrigger.Rebellion | CrisisTrigger.MoraleCollapse
                         | CrisisTrigger.LegitimacyCollapse | CrisisTrigger.LosingWar;
                case StrategicObjective.Consolidate:
                    return CrisisTrigger.AtWar | CrisisTrigger.Insolvent
                         | CrisisTrigger.MoraleUnhealthy | CrisisTrigger.LegitimacyUnhealthy;
                default:
                    return CrisisTrigger.None;
            }
        }

        /// <summary>
        /// Should the brain re-plan, given it currently holds <paramref name="currentTier"/> committed until
        /// <paramref name="committedUntil"/> and the ladder now proposes <paramref name="proposedTier"/> at
        /// <paramref name="now"/>? Yes if the proposal is MORE urgent (a lower tier — an emergency preempts any
        /// commitment), OR a P3.3 crisis break-glass fires (<paramref name="triggerCleared"/> b / <paramref name="contradictionReleased"/> c),
        /// OR the commitment has expired. No otherwise (hold the plan — the anti-thrash guard). Pure. The two crisis
        /// signals default false, so every pre-P3.3 4-arg caller is byte-identical.
        /// </summary>
        public static bool ShouldReplan(NeedTier currentTier, DateTime committedUntil, NeedTier proposedTier, DateTime now,
            bool triggerCleared = false, bool contradictionReleased = false)
        {
            if (proposedTier < currentTier) return true;   // more urgent (Survive=0 < Thrive=2) → preempt now
            if (triggerCleared) return true;               // (b) the crisis condition that FORCED this commit has cleared
            if (contradictionReleased) return true;        // (c) N consecutive cycles proposed a HIGHER tier than committed
            return now >= committedUntil;                   // else only when the commitment has run out
        }

        /// <summary>
        /// Apply one transition step to <paramref name="objective"/>. For a crisis-response commit already in place
        /// (Defend/Consolidate) this also drives the two P3.3 crisis break-glasses off <paramref name="currentTriggers"/>
        /// (the crisis conditions active THIS cycle, from <see cref="CrisisTriggersFrom"/>): (b) release when NONE of the
        /// conditions the commit recorded still holds, and (c) release after <see cref="ContradictionReleaseCycles"/>
        /// consecutive cycles the ladder proposes a strictly higher tier. If <see cref="ShouldReplan"/> says so, adopt the
        /// proposed tier/objective/target, re-arm the commitment clock (<paramref name="now"/> + <paramref name="commitFor"/>),
        /// record the crisis trigger for a fresh crisis commit, reset the contradiction counter, and return true
        /// (re-planned). Otherwise leave the objective otherwise untouched (the contradiction counter still ticks for a
        /// held crisis commit) and return false (held). A null blob is a safe no-op (false). <paramref name="currentTriggers"/>
        /// defaults to <see cref="CrisisTrigger.None"/>, so a pre-P3.3 6-arg caller behaves exactly as before.
        /// </summary>
        public static bool Advance(StrategicObjectiveDB objective, NeedTier proposedTier, StrategicObjective proposedObjective,
            int proposedTargetFactionId, DateTime now, TimeSpan commitFor, CrisisTrigger currentTriggers = CrisisTrigger.None)
        {
            if (objective == null) return false;

            // The two P3.3 crisis break-glasses apply ONLY to a crisis-response commit already in place; a normal
            // commit keeps plain hysteresis. Read them against the CURRENTLY committed objective/tier, before any
            // overwrite below.
            bool committedIsCrisis = IsCrisisObjective(objective.Objective);

            // (c) Persistent-contradiction debounce: count consecutive cycles the ladder proposes a STRICTLY HIGHER
            // tier than the crisis is committed at (the shock eased but the commit hasn't expired). This counts UPWARD,
            // so — unlike the downward proposedTier<currentTier break-glass, which is DEAD when a crisis is committed at
            // the Survive floor (Tier=0) — it is reachable. A non-consecutive read (the tier falls back to/under the
            // commit) restarts the streak.
            if (committedIsCrisis)
            {
                if (proposedTier > objective.Tier) objective.ContradictionCycles++;
                else objective.ContradictionCycles = 0;
            }
            bool contradictionReleased = committedIsCrisis && objective.ContradictionCycles >= ContradictionReleaseCycles;

            // (b) Trigger-cleared release: a crisis commit recorded WHICH conditions forced it (masked to its tier);
            // release the instant NONE of those still holds. Condition-based, not tier-based, so it too sidesteps the
            // Survive-floor flaw. Inert for a non-crisis commit or one that recorded no trigger.
            bool triggerCleared = committedIsCrisis && objective.CommitTrigger != CrisisTrigger.None
                && (objective.CommitTrigger & currentTriggers) == CrisisTrigger.None;

            if (!ShouldReplan(objective.Tier, objective.CommittedUntil, proposedTier, now, triggerCleared, contradictionReleased))
                return false;

            objective.Tier = proposedTier;
            objective.Objective = proposedObjective;
            objective.TargetFactionId = proposedTargetFactionId;
            objective.CommittedUntil = now + commitFor;
            // Record the crisis trigger for a fresh crisis commit (so (b) can re-check the specific conditions that
            // forced it), masked to the flags relevant to that objective's tier; a non-crisis commit records nothing.
            objective.CommitTrigger = CrisisMaskFor(proposedObjective) & currentTriggers;
            objective.ContradictionCycles = 0;   // a fresh commit restarts the contradiction debounce
            return true;
        }
    }
}
