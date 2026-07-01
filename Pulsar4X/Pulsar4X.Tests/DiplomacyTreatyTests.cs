using System;
using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the TREATY LEVERS (docs/DIPLOMACY-DESIGN.md "Treaties — the levers", task #33): a treaty is
    /// proposed → considered → accepted/refused by the target's own view of the proposer (relation score vs. a
    /// per-treaty trust threshold), and on acceptance the flag is set on BOTH sides + both scores warm. Proves:
    /// the trust ladder (deeper treaty needs a higher score), mutual application, the score warm-up, and the
    /// war/peace gating (no ordinary treaty mid-war; Peace only ends an actual war). Pure on two DiplomacyDBs —
    /// no harness needed.
    /// </summary>
    [TestFixture]
    public class DiplomacyTreatyTests
    {
        private const int P = 1;   // proposer faction id
        private const int T = 2;   // target faction id
        private static readonly DateTime When = new DateTime(2200, 1, 1);

        [Test]
        [Description("A trade agreement at Neutral is accepted, applied to BOTH sides, and warms both scores by the signing bonus.")]
        public void Trade_AtNeutral_AcceptedMutualAndWarms()
        {
            var pDip = new DiplomacyDB();
            var tDip = new DiplomacyDB();
            tDip.GetOrCreateRelationship(P);   // target knows proposer at Neutral (score 0)

            bool signed = Treaties.Propose(pDip, P, tDip, T, TreatyType.TradeAgreement, When);

            Assert.That(signed, Is.True);
            Assert.That(tDip.GetRelationship(P).TradeAgreement, Is.True, "target side signed");
            Assert.That(pDip.GetRelationship(T).TradeAgreement, Is.True, "proposer side signed (mutual)");
            Assert.That(tDip.GetRelationship(P).RelationScore, Is.EqualTo(Treaties.SigningBonus(TreatyType.TradeAgreement)));
            Assert.That(pDip.GetRelationship(T).RelationScore, Is.EqualTo(Treaties.SigningBonus(TreatyType.TradeAgreement)));
            Assert.That(tDip.GetRelationship(P).LastContact, Is.EqualTo(When));
        }

        [Test]
        [Description("The trust ladder: a defensive pact is refused below Allied and accepted at Allied.")]
        public void DefensivePact_NeedsAllied()
        {
            var pDip = new DiplomacyDB();
            var tDip = new DiplomacyDB();
            tDip.GetOrCreateRelationship(P).AdjustScore(RelationshipState.FriendlyThreshold); // +25, below Allied

            Assert.That(Treaties.Propose(pDip, P, tDip, T, TreatyType.DefensivePact, When), Is.False, "Friendly is not enough");
            Assert.That(tDip.GetRelationship(P).DefensivePact, Is.False);

            tDip.GetRelationship(P).AdjustScore(RelationshipState.AlliedThreshold - RelationshipState.FriendlyThreshold); // → +75
            Assert.That(Treaties.Propose(pDip, P, tDip, T, TreatyType.DefensivePact, When), Is.True, "Allied signs the pact");
            Assert.That(tDip.GetRelationship(P).DefensivePact, Is.True);
            Assert.That(pDip.GetRelationship(T).DefensivePact, Is.True, "mutual entanglement");
        }

        [Test]
        [Description("No ordinary treaty may be signed mid-war; Peace ends the war on both sides. Peace with no war is a no-op.")]
        public void War_BlocksTreaties_PeaceEndsIt()
        {
            var pDip = new DiplomacyDB();
            var tDip = new DiplomacyDB();
            tDip.GetOrCreateRelationship(P).DeclareWar();   // both sides latched at war
            pDip.GetOrCreateRelationship(T).DeclareWar();

            Assert.That(Treaties.Propose(pDip, P, tDip, T, TreatyType.TradeAgreement, When), Is.False, "no trade mid-war");

            Assert.That(Treaties.Propose(pDip, P, tDip, T, TreatyType.Peace, When), Is.True, "peace ends the war");
            Assert.That(tDip.GetRelationship(P).AtWar, Is.False);
            Assert.That(pDip.GetRelationship(T).AtWar, Is.False, "peace is mutual");

            // Peace when no war exists is refused (nothing to end).
            var pDip2 = new DiplomacyDB();
            var tDip2 = new DiplomacyDB();
            Assert.That(Treaties.Propose(pDip2, P, tDip2, T, TreatyType.Peace, When), Is.False, "no war → no peace treaty");
        }
    }
}
