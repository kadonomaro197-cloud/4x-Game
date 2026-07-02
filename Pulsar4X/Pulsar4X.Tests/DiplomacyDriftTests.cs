using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the reactive-diplomacy DRIFT (docs/DIPLOMACY-DESIGN.md "Are we good?"): the previously-dead
    /// <see cref="ReactiveDiplomacy"/> table is now a LIVE monthly loop in <see cref="NPCDecisionProcessor"/> — a
    /// faction's feelings move on what it can observe of its neighbours. Proves the DIRECTION (a militarist
    /// neighbour cools relations; a standing treaty warms them; a plain neutral neither), and the crucial
    /// start-safe property: a faction with no relationship rows (an un-met single-faction New Game) drifts
    /// nothing. The magnitudes are the locked RelationDelta values; the cadence is a PC-calibration knob.
    /// </summary>
    [TestFixture]
    public class DiplomacyDriftTests
    {
        [Test]
        [Description("A militarist neighbour sours the mood: the relationship score drops by the locked TheirMilitaristsRose delta.")]
        public void Drift_MilitaristNeighbour_CoolsRelations()
        {
            var s = TestScenario.CreateWithColony();
            var hawk = FactionFactory.CreateBasicFaction(s.Game, "Iron Legion", "IRL", 1000);
            hawk.GetDataBlob<GovernmentDB>().Militarism = GovNotch.High;

            var view = s.Faction.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(hawk.Id); // met, Neutral (0)
            Assert.That(view.RelationScore, Is.EqualTo(0));

            NPCDecisionProcessor.RunDiplomaticDrift(s.Faction);

            Assert.That(view.RelationScore,
                Is.EqualTo(ReactiveDiplomacy.RelationDelta(ExternalStimulus.TheirMilitaristsRose)),
                "a militarist neighbour should cool relations by the locked delta");
            Assert.That(view.RelationScore, Is.LessThan(0));
        }

        [Test]
        [Description("A standing treaty warms relations each cycle (kept faith): the score rises by the YouHonoredTreaties delta.")]
        public void Drift_StandingTreaty_WarmsRelations()
        {
            var s = TestScenario.CreateWithColony();
            var partner = FactionFactory.CreateBasicFaction(s.Game, "Trade League", "TRL", 1000); // default Mid gov

            var view = s.Faction.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(partner.Id);
            view.TradeAgreement = true; // a standing deal

            NPCDecisionProcessor.RunDiplomaticDrift(s.Faction);

            Assert.That(view.RelationScore,
                Is.EqualTo(ReactiveDiplomacy.RelationDelta(ExternalStimulus.YouHonoredTreaties)),
                "a standing treaty should warm relations by the locked delta");
            Assert.That(view.RelationScore, Is.GreaterThan(0));
        }

        [Test]
        [Description("A plain neutral (no militarism, no treaty) neither warms nor cools — drift is only what the world justifies.")]
        public void Drift_PlainNeutral_NoChange()
        {
            var s = TestScenario.CreateWithColony();
            var stranger = FactionFactory.CreateBasicFaction(s.Game, "Quiet Reach", "QRE", 1000);

            var view = s.Faction.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(stranger.Id);
            NPCDecisionProcessor.RunDiplomaticDrift(s.Faction);

            Assert.That(view.RelationScore, Is.EqualTo(0), "nothing observable → no drift");
        }

        [Test]
        [Description("Start-safe: a faction that has met no one (no relationship rows) drifts nothing and does not throw.")]
        public void Drift_NoRelationships_IsInert()
        {
            var s = TestScenario.CreateWithColony();
            Assert.That(s.Faction.GetDataBlob<DiplomacyDB>().Relationships, Is.Empty, "fresh faction has met no one");
            Assert.DoesNotThrow(() => NPCDecisionProcessor.RunDiplomaticDrift(s.Faction));
            Assert.That(s.Faction.GetDataBlob<DiplomacyDB>().Relationships, Is.Empty, "drift creates no rows");
        }
    }
}
