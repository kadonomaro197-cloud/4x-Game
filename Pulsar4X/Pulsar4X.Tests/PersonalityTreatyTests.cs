using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// M2-1a gauge (docs/AI-BRAIN-BUILD-TRACKER.md, Movement II): the FIRST personality→behaviour wire. A faction's
    /// Xenophobia/Zealotry raises the trust it demands to sign a treaty. Proves a neutral (or absent) personality is
    /// byte-identical to the historic threshold, a xenophobe REFUSES a deal a neutral would sign, and a xenophile
    /// signs one a neutral would refuse — personality now decides diplomacy.
    /// </summary>
    [TestFixture]
    public class PersonalityTreatyTests
    {
        [Test]
        [Description("Neutral/absent personality = the plain threshold (byte-identical); high Xenophobia demands more, low less.")]
        public void RequiredScoreWith_ShiftsByPersonality_NeutralIsByteIdentical()
        {
            int baseTrade = Treaties.RequiredScore(TreatyType.TradeAgreement); // 0

            Assert.That(Treaties.RequiredScoreWith(TreatyType.TradeAgreement, null), Is.EqualTo(baseTrade),
                "no personality → the historic threshold");
            Assert.That(Treaties.RequiredScoreWith(TreatyType.TradeAgreement, new PersonalityDB()), Is.EqualTo(baseTrade),
                "an all-neutral personality → the historic threshold (byte-identical)");

            var xeno = new PersonalityDB();
            xeno.SetTrait(PersonalityTrait.Xenophobia, 1.0);
            Assert.That(Treaties.RequiredScoreWith(TreatyType.TradeAgreement, xeno),
                Is.EqualTo(baseTrade + Treaties.XenophobiaTrustPenalty), "a maximal xenophobe demands the full penalty more");

            var xenophile = new PersonalityDB();
            xenophile.SetTrait(PersonalityTrait.Xenophobia, 0.0);
            Assert.That(Treaties.RequiredScoreWith(TreatyType.TradeAgreement, xenophile),
                Is.EqualTo(baseTrade - Treaties.XenophobiaTrustPenalty), "a xenophile signs at a lower bar");
        }

        [Test]
        [Description("At the same relation score a neutral signs a trade deal, a xenophobe refuses it, a xenophile still signs.")]
        public void WouldAccept_PersonalityDecidesTheDeal()
        {
            var dip = new DiplomacyDB();
            var rel = dip.GetOrCreateRelationship(999); // a fresh Neutral relationship: score 0

            // Trade needs score >= 0. At 0, a neutral decider signs.
            Assert.That(Treaties.WouldAccept(rel, TreatyType.TradeAgreement), Is.True);
            Assert.That(Treaties.WouldAccept(rel, TreatyType.TradeAgreement, new PersonalityDB()), Is.True,
                "neutral personality == the historic accept");

            var xeno = new PersonalityDB();
            xeno.SetTrait(PersonalityTrait.Xenophobia, 1.0);
            Assert.That(Treaties.WouldAccept(rel, TreatyType.TradeAgreement, xeno), Is.False,
                "a xenophobe refuses at a score a neutral would accept");

            var xenophile = new PersonalityDB();
            xenophile.SetTrait(PersonalityTrait.Xenophobia, 0.0);
            Assert.That(Treaties.WouldAccept(rel, TreatyType.TradeAgreement, xenophile), Is.True,
                "a xenophile still signs");
        }
    }
}
