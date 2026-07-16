using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// B6-a gauge — the OPPORTUNISTIC declare-war trigger (`NPCDecisionProcessor.RunWarPolicy`, the developer's Q2:
    /// "Aggression/Ambition × detected enemy weakness × low relation"). The existing AI only ever went to war out of
    /// OBLIGATION (joining a defensive-pact ally's war); this proves the war of CHOICE — a warlike faction that
    /// out-muscles a rival it already dislikes DECLARES — and that each of the three gates BITES (a neutral personality,
    /// or a warm relation, does NOT start a war). Drives the policy method DIRECTLY (no clock advance — the combat
    /// fine-step hang lesson); the default game is byte-identical because the CALL SITE is gated on
    /// EnableDiplomaticProposals (see <see cref="NPCTreatyPolicyTests"/> Gate_DefaultsOff).
    /// </summary>
    [TestFixture]
    public class NPCWarPolicyTests
    {
        private static void MakeWarlike(Entity faction)
        {
            var p = new PersonalityDB();
            p.SetTrait(PersonalityTrait.Aggression, 0.9);
            p.SetTrait(PersonalityTrait.Ambition, 0.9);
            faction.SetDataBlob(p);
        }

        [Test]
        [Description("A warlike faction that clearly out-muscles a HATED (Hostile) rival declares an opportunistic war on it.")]
        public void RunWarPolicy_WarlikeFaction_DeclaresOnAWeakHatedRival()
        {
            var s = TestScenario.CreateWithColony();   // the attacker keeps its start fleet → real MilitaryStrength
            MakeWarlike(s.Faction);
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);   // owns no ships → strength 0 (weak prey)

            var dip = s.Faction.GetDataBlob<DiplomacyDB>();
            dip.GetOrCreateRelationship(reds.Id).AdjustScore(-100);   // MET + deeply HOSTILE (below −25)

            Assert.That(FactionRollup.MilitaryStrength(s.Faction), Is.GreaterThan(0),
                "precondition: the attacker has a real military (its start fleet) to out-muscle the rival with");
            Assert.That(dip.GetRelationship(reds.Id).AtWar, Is.False, "precondition: not yet at war");

            NPCDecisionProcessor.RunWarPolicy(s.Faction);

            Assert.That(dip.GetRelationship(reds.Id).AtWar, Is.True,
                "aggression × out-muscle × hostility all clear → the AI seizes the moment and declares war");
        }

        [Test]
        [Description("A NEUTRAL (no-personality) faction never opens a war of choice, even against a weak hated rival — the appetite gate bites.")]
        public void RunWarPolicy_NeutralFaction_DoesNotOpenAWarOfChoice()
        {
            var s = TestScenario.CreateWithColony();   // no PersonalityDB → neutral appetite (0.5)
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var dip = s.Faction.GetDataBlob<DiplomacyDB>();
            dip.GetOrCreateRelationship(reds.Id).AdjustScore(-100);   // same weak, hated rival

            NPCDecisionProcessor.RunWarPolicy(s.Faction);

            Assert.That(dip.GetRelationship(reds.Id).AtWar, Is.False,
                "a peaceable (neutral) faction stays out of a war of choice — the personality appetite gate stops it");
        }

        [Test]
        [Description("A warlike faction does NOT declare on a rival it doesn't dislike (relation not Hostile) — the low-relation gate bites.")]
        public void RunWarPolicy_WarmRelation_NoWar()
        {
            var s = TestScenario.CreateWithColony();
            MakeWarlike(s.Faction);
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var dip = s.Faction.GetDataBlob<DiplomacyDB>();
            dip.GetOrCreateRelationship(reds.Id);   // MET but at NEUTRAL score (0, above the −25 Hostile bar)

            NPCDecisionProcessor.RunWarPolicy(s.Faction);

            Assert.That(dip.GetRelationship(reds.Id).AtWar, Is.False,
                "even a warlike faction won't attack a rival it isn't hostile toward — a war of choice needs a grievance");
        }
    }
}
