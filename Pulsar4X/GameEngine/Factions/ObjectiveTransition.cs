using System;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Phase-2.3 (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II — the Organism engine): the TRANSITION ENGINE. The
    /// needs-ladder (2.2) re-reads a faction's tier every cycle, but a brain that re-planned every month would
    /// THRASH — abandon a half-built fleet the moment a gauge wobbles. This is the hysteresis that stops that: once
    /// a faction commits to an objective it HOLDS it for a while, and only re-plans when the commitment expires OR a
    /// genuinely more urgent need appears (an emergency always preempts a commitment — you drop the expansion plan
    /// when the capital rebels).
    ///
    /// Pure/stateless logic; the only mutation is stamping a <see cref="StrategicObjectiveDB"/> nothing attaches yet
    /// → byte-identical. The Tick (2.4) drives it: assess tier → pick objective → <see cref="Advance"/>.
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
        /// Phase-2.5: how long a faction dwells on a freshly-committed objective before it's free to re-plan, as a
        /// function of its AMBITION trait. The base is <see cref="DefaultCommitFor"/>; for an EXPANSION-family aim
        /// (<see cref="StrategicObjective.Expand"/> / <see cref="StrategicObjective.Conquer"/> — the Ambition-tier grand
        /// aims Aggression flips between) a HIGH-Ambition faction dwells SHORTER — it renews the expansion push on a
        /// faster cadence — while a LOW-Ambition faction dwells LONGER. Every other objective, and a NEUTRAL (0.5) or
        /// absent <paramref name="personality"/>, returns exactly <see cref="DefaultCommitFor"/> → byte-identical with
        /// the pre-2.5 fixed dwell. Pure/stateless; monotonic DECREASING in Ambition.
        /// </summary>
        public static TimeSpan CommitFor(StrategicObjective objective, PersonalityDB personality)
        {
            if (!IsExpansionAim(objective)) return DefaultCommitFor;   // only the expansion push scales with Ambition

            double ambition = personality == null
                ? PersonalityDB.Neutral
                : personality.TraitOf(PersonalityTrait.Ambition);

            // Dwell factor: 1 at neutral (multiply-by-1.0 is exact → byte-identical), <1 above neutral (shorter dwell,
            // push more often), >1 below neutral (longer dwell). Monotonic decreasing in Ambition.
            double factor = 1.0 + AmbitionCadenceSpread * (PersonalityDB.Neutral - ambition);
            return TimeSpan.FromTicks((long)(DefaultCommitFor.Ticks * factor));
        }

        /// <summary>The Ambition-tier grand aims whose commit cadence scales with the Ambition trait: settle new worlds
        /// (<see cref="StrategicObjective.Expand"/>) or take them by force (<see cref="StrategicObjective.Conquer"/> —
        /// the aggressive form of the same expansion drive). Every other objective keeps the fixed default dwell.</summary>
        private static bool IsExpansionAim(StrategicObjective objective)
            => objective == StrategicObjective.Expand || objective == StrategicObjective.Conquer;

        /// <summary>
        /// Should the brain re-plan, given it currently holds <paramref name="currentTier"/> committed until
        /// <paramref name="committedUntil"/> and the ladder now proposes <paramref name="proposedTier"/> at
        /// <paramref name="now"/>? Yes if the proposal is MORE urgent (a lower tier — an emergency preempts any
        /// commitment) OR the commitment has expired. No otherwise (hold the plan — this is the anti-thrash guard).
        /// </summary>
        public static bool ShouldReplan(NeedTier currentTier, DateTime committedUntil, NeedTier proposedTier, DateTime now)
        {
            if (proposedTier < currentTier) return true;   // more urgent (Survive=0 < Thrive=2) → preempt now
            return now >= committedUntil;                   // else only when the commitment has run out
        }

        /// <summary>
        /// Apply one transition step to <paramref name="objective"/>. If <see cref="ShouldReplan"/> says so, adopt the
        /// proposed tier/objective/target and re-arm the commitment clock (<paramref name="now"/> + <paramref name="commitFor"/>),
        /// returning true (re-planned). Otherwise leave it untouched and return false (held). A null blob is a safe
        /// no-op (false).
        /// </summary>
        public static bool Advance(StrategicObjectiveDB objective, NeedTier proposedTier, StrategicObjective proposedObjective,
            int proposedTargetFactionId, DateTime now, TimeSpan commitFor)
        {
            if (objective == null) return false;
            if (!ShouldReplan(objective.Tier, objective.CommittedUntil, proposedTier, now))
                return false;

            objective.Tier = proposedTier;
            objective.Objective = proposedObjective;
            objective.TargetFactionId = proposedTargetFactionId;
            objective.CommittedUntil = now + commitFor;
            return true;
        }
    }
}
