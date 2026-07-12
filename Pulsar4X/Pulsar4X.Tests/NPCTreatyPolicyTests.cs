using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Sensors;

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
        private static Entity MakeShip(EntityManager mgr, int factionId)
        {
            var e = Entity.Create();
            e.FactionOwnerID = factionId;
            mgr.AddEntity(e);
            return e;
        }

        // Fabricate a live sensor contact so the observer "sees" the given entity at a set signal strength.
        private static void SeeContact(FactionInfoDB observerInfo, Entity actual, double strength)
        {
            var info = new SensorInfoDB { LatestDetectionQuality = new SensorReturnValues { SignalStrength_kW = strength } };
            observerInfo.SensorContacts[actual.Id] = new SensorContact { ActualEntity = actual, ActualEntityId = actual.Id, SensorInfo = info };
        }

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

        [Test]
        [Description("Phase-3.4 seed: sharing a feared threat, an NPC forges a DefensivePact with a TRUSTED (Allied) neighbour — not with the threat.")]
        public void RunTreatyPolicy_WithASharedThreat_ProposesADefensivePactToATrustedAlly()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);     // the proposer
            var blues = FactionFactory.CreateBasicFaction(s.Game, "Blues", "BLU", 0);   // the trusted ally
            var menace = FactionFactory.CreateBasicFaction(s.Game, "Menace", "MEN", 0); // the shared threat

            var redDip = reds.GetDataBlob<DiplomacyDB>();
            var blueDip = blues.GetDataBlob<DiplomacyDB>();

            // Reds & Blues are close allies (Allied 75 both directions) — clears the DefensivePact trust bar.
            redDip.GetOrCreateRelationship(blues.Id).AdjustScore(RelationshipState.AlliedThreshold);
            blueDip.GetOrCreateRelationship(reds.Id).AdjustScore(RelationshipState.AlliedThreshold);

            // Reds SEE the Menace at overwhelming strength → GreatestThreatTo(reds) names it as the shared threat.
            SeeContact(reds.GetDataBlob<FactionInfoDB>(), MakeShip(s.Game.GlobalManager, menace.Id), strength: 1e9);

            NPCDecisionProcessor.RunTreatyPolicy(reds);

            Assert.That(redDip.GetOrCreateRelationship(blues.Id).DefensivePact, Is.True,
                "sharing a feared threat, Reds forge a defensive pact with their trusted ally");
            Assert.That(blueDip.GetOrCreateRelationship(reds.Id).DefensivePact, Is.True, "signed on both ledgers (two-sided)");
            Assert.That(redDip.GetOrCreateRelationship(menace.Id).DefensivePact, Is.False, "we don't ally WITH the one we fear");
        }

        [Test]
        [Description("Phase-3.4b: a DefensivePact ally at war with a third faction DRAGS us in — we declare war on our ally's enemy (the coalition forms).")]
        public void RunTreatyPolicy_JoinsAPactAllysWar_AgainstTheirEnemy()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);     // us — the pact partner dragged in
            var blues = FactionFactory.CreateBasicFaction(s.Game, "Blues", "BLU", 0);   // our defensive-pact ally
            var menace = FactionFactory.CreateBasicFaction(s.Game, "Menace", "MEN", 0); // the ally's enemy

            var redDip = reds.GetDataBlob<DiplomacyDB>();
            var blueDip = blues.GetDataBlob<DiplomacyDB>();

            // Reds & Blues hold a mutual defensive pact.
            redDip.GetOrCreateRelationship(blues.Id).DefensivePact = true;
            blueDip.GetOrCreateRelationship(reds.Id).DefensivePact = true;

            // Blues are AT WAR with the Menace (Blues were attacked / declared — either way, war is latched).
            blueDip.GetOrCreateRelationship(menace.Id).DeclareWar();

            Assert.That(redDip.GetOrCreateRelationship(menace.Id).AtWar, Is.False, "we start neutral toward our ally's enemy");

            NPCDecisionProcessor.RunTreatyPolicy(reds);   // honour the pact

            Assert.That(redDip.GetOrCreateRelationship(menace.Id).AtWar, Is.True,
                "the pact drags us into our ally's war — the coalition forms");
            // War is symmetric (Diplomacy.DeclareWar latches both ledgers), so the Menace now sees us at war too.
            Assert.That(menace.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(reds.Id).AtWar, Is.True,
                "war is two-sided — the Menace now counts us among its enemies");
        }

        [Test]
        [Description("Phase-3.4c: once the shared threat is gone, a LOW-Honour ally BREAKS the defensive pact (the coalition cracks); a HIGH-Honour one keeps faith.")]
        public void RunTreatyPolicy_WhenThreatIsGone_LowHonourCracksThePact_HighHonourKeepsFaith()
        {
            // --- Low-Honour faction: it defects once there's no common enemy left. ---
            var s = TestScenario.CreateWithColony();
            var faithless = FactionFactory.CreateBasicFaction(s.Game, "Faithless", "FTH", 0);
            var ally = FactionFactory.CreateBasicFaction(s.Game, "Ally", "ALY", 0);

            var faithlessDip = faithless.GetDataBlob<DiplomacyDB>();
            var allyDip = ally.GetDataBlob<DiplomacyDB>();
            faithlessDip.GetOrCreateRelationship(ally.Id).DefensivePact = true;
            allyDip.GetOrCreateRelationship(faithless.Id).DefensivePact = true;

            // No sensor contacts → GreatestThreatTo names nobody (the shared threat is defeated/gone).
            var lowHonour = new PersonalityDB();
            lowHonour.SetTrait(PersonalityTrait.Honor, 0.1);   // below the abandon temptation (0.5) → won't keep faith
            faithless.SetDataBlob(lowHonour);

            NPCDecisionProcessor.RunTreatyPolicy(faithless);

            Assert.That(faithlessDip.GetOrCreateRelationship(ally.Id).DefensivePact, Is.False,
                "with the threat gone, a low-Honour faction sheds the pact — the coalition cracks");
            Assert.That(allyDip.GetOrCreateRelationship(faithless.Id).DefensivePact, Is.False,
                "BreakTreaty clears the flag on BOTH ledgers — the pact no longer binds either side");

            // --- High-Honour faction: same situation, but it holds the line. ---
            var s2 = TestScenario.CreateWithColony();
            var loyal = FactionFactory.CreateBasicFaction(s2.Game, "Loyal", "LYL", 0);
            var friend = FactionFactory.CreateBasicFaction(s2.Game, "Friend", "FRD", 0);

            var loyalDip = loyal.GetDataBlob<DiplomacyDB>();
            loyalDip.GetOrCreateRelationship(friend.Id).DefensivePact = true;
            friend.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(loyal.Id).DefensivePact = true;

            var highHonour = new PersonalityDB();
            highHonour.SetTrait(PersonalityTrait.Honor, 0.9);   // above the temptation → keeps faith
            loyal.SetDataBlob(highHonour);

            NPCDecisionProcessor.RunTreatyPolicy(loyal);

            Assert.That(loyalDip.GetOrCreateRelationship(friend.Id).DefensivePact, Is.True,
                "a high-Honour faction keeps the pact even with no enemy left to justify it");
        }
    }
}
