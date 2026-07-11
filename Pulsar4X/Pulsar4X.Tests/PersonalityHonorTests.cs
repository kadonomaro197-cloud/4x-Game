using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// M2-1c gauge (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II): the third personality→behaviour wire — Honor →
    /// keep-faith / renege. Two halves: the pure DECISION (`Treaties.WouldKeepFaith` — a high-Honor faction honours a
    /// pact even when betrayal would pay, a low-Honor one caves) and the mechanical ACT (`Diplomacy.BreakTreaty` —
    /// clears the pact on both books and craters the injured party's trust). Nothing calls either autonomously yet,
    /// so live diplomacy is unchanged; this proves the lever works when the brain pulls it.
    /// </summary>
    [TestFixture]
    public class PersonalityHonorTests
    {
        [Test]
        [Description("High Honor keeps faith under a rich payoff; low Honor reneges; null reads neutral Honor.")]
        public void WouldKeepFaith_HonorResistsTemptation()
        {
            var honorable = new PersonalityDB();
            honorable.SetTrait(PersonalityTrait.Honor, 1.0);
            Assert.That(Treaties.WouldKeepFaith(honorable, 0.9), Is.True,
                "a faction of the highest honour keeps its pact even when reneging would pay well");

            var faithless = new PersonalityDB();
            faithless.SetTrait(PersonalityTrait.Honor, 0.0);
            Assert.That(Treaties.WouldKeepFaith(faithless, 0.9), Is.False,
                "a faithless faction reneges for the same payoff");
            Assert.That(Treaties.WouldKeepFaith(faithless, 0.0), Is.True,
                "…but with nothing to gain, even a faithless faction has no reason to break the pact");

            // null → neutral Honor (0.5): keeps faith below the midpoint, reneges above it.
            Assert.That(Treaties.WouldKeepFaith(null, 0.4), Is.True);
            Assert.That(Treaties.WouldKeepFaith(null, 0.6), Is.False);
        }

        [Test]
        [Description("BreakTreaty voids the pact on BOTH books, craters the victim's trust, and is a no-op the second time / on an unmet faction.")]
        public void BreakTreaty_VoidsThePactAndPunishesTheBetrayal()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var when = s.Game.TimePulse.GameGlobalDateTime;

            // Sign a non-aggression pact (a fresh Neutral score 0 clears the −25 trust bar), then read the trust.
            Assert.That(Treaties.Propose(s.Faction, reds, TreatyType.NonAggression, when), Is.True);
            var mine = s.Faction.GetDataBlob<DiplomacyDB>();
            var theirs = reds.GetDataBlob<DiplomacyDB>();
            Assert.That(mine.GetRelationship(reds.Id).NonAggressionPact, Is.True);
            Assert.That(theirs.GetRelationship(s.Faction.Id).NonAggressionPact, Is.True);
            int victimScoreBefore = theirs.GetRelationship(s.Faction.Id).RelationScore;

            // I renege. The pact voids on both books and the victim's trust in me craters.
            Assert.That(Diplomacy.BreakTreaty(s.Faction, reds, TreatyType.NonAggression, when), Is.True);
            Assert.That(mine.GetRelationship(reds.Id).NonAggressionPact, Is.False, "the pact no longer binds the breaker");
            Assert.That(theirs.GetRelationship(s.Faction.Id).NonAggressionPact, Is.False, "nor the victim");
            Assert.That(theirs.GetRelationship(s.Faction.Id).RelationScore,
                Is.EqualTo(victimScoreBefore - Treaties.BetrayalScorePenalty), "the betrayal craters the victim's trust");

            // Breaking a pact that's already gone, or one that was never signed, is a safe no-op.
            Assert.That(Diplomacy.BreakTreaty(s.Faction, reds, TreatyType.NonAggression, when), Is.False,
                "nothing left to break");
            var greens = FactionFactory.CreateBasicFaction(s.Game, "Greens", "GRN", 0);
            Assert.That(Diplomacy.BreakTreaty(s.Faction, greens, TreatyType.NonAggression, when), Is.False,
                "no pact with a never-met faction");
        }
    }
}
