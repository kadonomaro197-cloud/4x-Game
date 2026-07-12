using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-3.3 gauge (docs/AI-BRAIN-BUILD-TRACKER.md — the Ecosystem): the NPC treaty POLICY — the first step of
    /// the living galaxy (NPCs act on each other, not just drift). Proves the proposal gate defaults OFF
    /// (byte-identical), that `RunTreatyPolicy` proposes a NonAggression pact to a qualifying met rival and it signs
    /// on BOTH ledgers (two-sided), and that an already-signed pair is left alone (the skip-if-already guard — no
    /// re-warm churn). Deterministic, engine-only (2-faction harness), no live client.
    /// </summary>
    [TestFixture]
    public class NPCTreatyPolicyTests
    {
        [Test]
        [Description("The diplomacy-proposal gate defaults OFF so every existing fixture is byte-identical.")]
        public void Gate_DefaultsOff()
        {
            Assert.That(NPCDecisionProcessor.EnableDiplomaticProposals, Is.False,
                "an NPC decides but does not sign treaties until a client/test opts in — keeps existing tests byte-identical");
        }

        [Test]
        [Description("RunTreatyPolicy proposes a NonAggression pact to a met, at-neutral rival; it signs on both ledgers; a second cycle doesn't re-warm.")]
        public void RunTreatyPolicy_ProposesANonAggressionPact_TwoSided_AndIdempotent()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var blues = FactionFactory.CreateBasicFaction(s.Game, "Blues", "BLU", 0);

            // A mutual, met, at-neutral (score 0) relationship — 0 clears the NonAggression bar (Hostile −25), not at war.
            var redDip = reds.GetDataBlob<DiplomacyDB>();
            var blueDip = blues.GetDataBlob<DiplomacyDB>();
            redDip.GetOrCreateRelationship(blues.Id);
            blueDip.GetOrCreateRelationship(reds.Id);

            Assert.That(redDip.GetOrCreateRelationship(blues.Id).NonAggressionPact, Is.False, "no pact before the policy runs");

            NPCDecisionProcessor.RunTreatyPolicy(reds);   // Reds seek détente

            Assert.That(redDip.GetOrCreateRelationship(blues.Id).NonAggressionPact, Is.True, "the proposer's ledger is signed");
            Assert.That(blueDip.GetOrCreateRelationship(reds.Id).NonAggressionPact, Is.True, "the target's ledger is signed too (two-sided)");

            // Idempotency: a second cycle finds it already signed and does NOT re-warm the score (the skip-if-already guard).
            int scoreAfter = redDip.GetOrCreateRelationship(blues.Id).RelationScore;
            NPCDecisionProcessor.RunTreatyPolicy(reds);
            Assert.That(redDip.GetOrCreateRelationship(blues.Id).RelationScore, Is.EqualTo(scoreAfter),
                "already signed → the skip-if-already guard prevents re-warm churn");
        }
    }
}
