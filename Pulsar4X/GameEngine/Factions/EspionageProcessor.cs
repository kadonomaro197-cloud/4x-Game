using System;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.People;

namespace Pulsar4X.Factions
{
    /// <summary>
    /// Espionage E3 — the RESOLVER: the live consumer that finally makes the covert-action catalog + risk model do
    /// something. When an agent's <see cref="CovertOpDB"/> comes due, this rolls detection (agent tradecraft vs the
    /// target's counter-intel, via <see cref="CovertRisk.Resolve"/>), lands the effect on success (GatherIntel raises
    /// the actor's <see cref="InformationLedgerDB"/> on the chosen facet — the pure hidden-info lever, "gather first"),
    /// and on the bad end applies the caught consequences: the target sours toward the actor (<see cref="RelationshipState.AdjustScore"/>),
    /// their suspicion of the actor spikes (<see cref="RelationshipState.Suspicion"/>), and a CAUGHT agent is LOST —
    /// captured/killed (<see cref="CommanderFactory.DestroyCommander"/>), the grave rung. So a covert op is a real
    /// risk/reward bet: it can sharpen your picture of a rival, or blow up in your face.
    ///
    /// An <see cref="IInstanceProcessor"/> scheduled by <see cref="Espionage.TaskAgent"/>. The resolution splits into a
    /// deterministic core (<see cref="ApplyOutcome"/>, which takes the outcome directly — tested exactly) and the roll
    /// that feeds it (from the seeded system RNG, never a wall clock / Math.Random — deterministic replay).
    /// Byte-identical on the default start: no agent carries a <see cref="CovertOpDB"/> until the player tasks one.
    /// </summary>
    public class EspionageProcessor : IInstanceProcessor
    {
        /// <summary>Relation-score hit when an op is TRACED (they noticed, can't prove who) — a small souring.</summary>
        public const int RelationHitTraced = -3;
        /// <summary>Relation-score hit when an op is CAUGHT (proof) — a hard souring toward the exposed actor.</summary>
        public const int RelationHitCaught = -15;

        internal override void ProcessEntity(Entity agent, DateTime atDateTime)
        {
            if (!agent.TryGetDataBlob<CovertOpDB>(out var op)) return;   // no op (already resolved / cancelled)

            // The detection roll comes from the SEEDED system RNG (deterministic replay; never Math.Random / a clock).
            double roll = agent.Manager.RNG.NextDouble();
            ResolveOp(agent, op, agent.Manager.Game, roll, atDateTime);
        }

        /// <summary>
        /// Resolve an op with an explicit <paramref name="roll01"/> (0..1, higher = unluckier): compute the agent's
        /// tradecraft + the target's counter-intel, band the outcome through <see cref="CovertRisk.Resolve"/>, then
        /// apply it. Returns the outcome (for readouts/tests). The roll is passed in so callers stay deterministic.
        /// </summary>
        public static CovertOutcome ResolveOp(Entity agent, CovertOpDB op, Game game, double roll01, DateTime when)
        {
            double skill = agent.TryGetDataBlob<BonusesDB>(out var bonuses)
                ? CommanderBonuses.EspionageSkill01(bonuses)
                : 0.0;
            double counterIntel = Espionage.CounterIntelOf(game, op.TargetFactionId);
            var def = CovertActionCatalog.ByAction(op.Action);

            var outcome = CovertRisk.Resolve(def.BaseDetectionRisk, skill, counterIntel, roll01);
            ApplyOutcome(agent, op, game, outcome, when);
            return outcome;
        }

        /// <summary>
        /// Apply a resolved outcome to the world — the deterministic core (no roll): on Clean/Traced the op's work
        /// LANDS (the effect), on Traced/Caught the target NOTICES (suspicion + relation hit), a CAUGHT agent is LOST
        /// (the grave rung), and the op is consumed either way. Split out so a test can drive each outcome exactly.
        /// </summary>
        public static void ApplyOutcome(Entity agent, CovertOpDB op, Game game, CovertOutcome outcome, DateTime when)
        {
            int actorFactionId = agent.FactionOwnerID;

            // The op's work lands unless the agent was caught in the act.
            if (outcome != CovertOutcome.Caught)
                ApplyEffect(agent, actorFactionId, op, game, when);

            // The target notices on anything short of a clean run — suspicion builds, the relation sours.
            if (outcome != CovertOutcome.Clean)
                ApplyDetection(game, actorFactionId, op.TargetFactionId, outcome);

            // The op is consumed (the agent, if it survives, is idle again).
            agent.RemoveDataBlob<CovertOpDB>();

            // Grave rung: a caught agent is captured/killed — you must recruit and re-run.
            if (outcome == CovertOutcome.Caught && agent.IsValid)
                CommanderFactory.DestroyCommander(agent);
        }

        /// <summary>The op's payoff on a successful run. E3 lands GatherIntel (raise the actor's intel on the target's
        /// chosen facet to Confirmed — the sharpened poker read). Other catalog actions (steal-tech/sabotage/…) get
        /// their live effects in E6; here they run clean but do nothing yet (deliberate, documented).</summary>
        private static void ApplyEffect(Entity agent, int actorFactionId, CovertOpDB op, Game game, DateTime when)
        {
            if (op.Action != CovertAction.GatherIntel) return; // steal/sabotage/… effects are E6

            if (game.Factions.TryGetValue(actorFactionId, out var actorFaction)
                && actorFaction.TryGetDataBlob<InformationLedgerDB>(out var ledger))
            {
                ledger.Confirm(op.TargetFactionId, op.TargetFacet, when);
            }
        }

        /// <summary>The caught consequences on the TARGET's view of the ACTOR: their suspicion of the actor climbs
        /// (<see cref="CovertRisk.SuspicionAfter"/>) and their relation score sours (small for Traced, hard for
        /// Caught). This is the deniability game — quiet harm builds suspicion until they're sure.</summary>
        private static void ApplyDetection(Game game, int actorFactionId, int targetFactionId, CovertOutcome outcome)
        {
            if (!game.Factions.TryGetValue(targetFactionId, out var targetFaction)) return;
            if (!targetFaction.TryGetDataBlob<DiplomacyDB>(out var targetDip)) return;

            var relation = targetDip.GetOrCreateRelationship(actorFactionId);
            relation.Suspicion = CovertRisk.SuspicionAfter(relation.Suspicion, outcome);
            relation.AdjustScore(outcome == CovertOutcome.Caught ? RelationHitCaught : RelationHitTraced);
        }
    }
}
