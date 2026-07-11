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
