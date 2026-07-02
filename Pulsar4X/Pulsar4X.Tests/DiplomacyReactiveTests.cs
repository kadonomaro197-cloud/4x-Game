using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the reactive "Are we good?" engine (docs/DIPLOMACY-DESIGN.md "Reactive diplomacy", task #35): a
    /// faction acts on what it OBSERVES, generating an overture gated by its current stance toward you, and some
    /// observations nudge the relationship needle directly. This is the developer's marquee depth example — the
    /// AI sees your fleet near its space and asks "are we good?".
    /// </summary>
    [TestFixture]
    public class DiplomacyReactiveTests
    {
        private static RelationshipState View(int score, bool atWar = false)
        {
            var r = new RelationshipState(otherFactionId: 1);
            if (atWar) r.DeclareWar(); else r.AdjustScore(score);
            return r;
        }

        [Test]
        [Description("Your fleet near their border reads differently by stance: a neutral probes 'are we good?', a hostile warns you off, an ally shrugs.")]
        public void FleetNearBorder_DependsOnStance()
        {
            Assert.That(ReactiveDiplomacy.Overture(ExternalStimulus.FleetNearBorder, View(0)),
                Is.EqualTo(DiplomaticOverture.AreWeGoodProbe), "neutral → the intent probe");
            Assert.That(ReactiveDiplomacy.Overture(ExternalStimulus.FleetNearBorder, View(RelationshipState.HostileThreshold - 5)),
                Is.EqualTo(DiplomaticOverture.WarningToStop), "hostile → a warning");
            Assert.That(ReactiveDiplomacy.Overture(ExternalStimulus.FleetNearBorder, View(RelationshipState.AlliedThreshold)),
                Is.EqualTo(DiplomaticOverture.None), "an ally is unbothered");
        }

        [Test]
        [Description("A null view defaults to Neutral — the fleet probe still fires.")]
        public void NullView_DefaultsNeutral()
        {
            Assert.That(ReactiveDiplomacy.Overture(ExternalStimulus.FleetNearBorder, null),
                Is.EqualTo(DiplomaticOverture.AreWeGoodProbe));
        }

        [Test]
        [Description("Shared-enemy → alliance offer; a crisis on their border → a defense-fleet request (the commitment hook).")]
        public void OpportunityStimuli_GenerateTheRightOverture()
        {
            var neutral = View(0);
            Assert.That(ReactiveDiplomacy.Overture(ExternalStimulus.AtWarWithTheirEnemy, neutral), Is.EqualTo(DiplomaticOverture.AllianceOffer));
            Assert.That(ReactiveDiplomacy.Overture(ExternalStimulus.CrisisOnTheirBorder, neutral), Is.EqualTo(DiplomaticOverture.RequestDefenseFleet));
            Assert.That(ReactiveDiplomacy.Overture(ExternalStimulus.YouAppearWeak, neutral), Is.EqualTo(DiplomaticOverture.PressTheAdvantage));
            Assert.That(ReactiveDiplomacy.Overture(ExternalStimulus.TheyLackAResourceYouHold, neutral), Is.EqualTo(DiplomaticOverture.TradeProposal));
        }

        [Test]
        [Description("Track-record stimuli move the needle directly: breaking faith bites hardest; honoring deals builds trust.")]
        public void TrustAndDistrust_NudgeTheNeedle()
        {
            Assert.That(ReactiveDiplomacy.Overture(ExternalStimulus.YouBrokeATreaty, View(50)), Is.EqualTo(DiplomaticOverture.DistrustGuardRises));
            Assert.That(ReactiveDiplomacy.RelationDelta(ExternalStimulus.YouBrokeATreaty), Is.LessThan(0));

            Assert.That(ReactiveDiplomacy.Overture(ExternalStimulus.YouHonoredTreaties, View(0)), Is.EqualTo(DiplomaticOverture.DeeperDealOffer));
            Assert.That(ReactiveDiplomacy.RelationDelta(ExternalStimulus.YouHonoredTreaties), Is.GreaterThan(0));

            // A pure overture (a probe) carries no automatic score change.
            Assert.That(ReactiveDiplomacy.RelationDelta(ExternalStimulus.FleetNearBorder), Is.EqualTo(0));
        }
    }
}
