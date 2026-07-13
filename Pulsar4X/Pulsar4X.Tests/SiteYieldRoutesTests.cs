using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-5e — the Diplomat/Intelligence YIELD ROUTES (docs/SITE-ENGINE-DESIGN.md §3 Yield dial, §7). The
    /// final Site Engine slice: a resolved site can now pay into the diplomacy and espionage systems, not just research.
    /// A Diplomacy-yield site WARMS the working faction's relations with everyone it's met; an Intel-yield site CONFIRMS
    /// its picture of those rivals. Proves both deliveries land in their real engine ledgers, and that a faction that has
    /// met nobody is a safe no-op (byte-identical — no live site uses these yields).
    /// </summary>
    [TestFixture]
    public class SiteYieldRoutesTests
    {
        // A rival faction the player has "met" — a relationship row on both ledgers so the yields have somebody to act on.
        private static Entity MetRival(TestScenario s)
        {
            var rival = FactionFactory.CreateBasicFaction(s.Game, "Rival", "RV", 0);
            s.Faction.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(rival.Id);   // player knows the rival
            rival.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(s.Faction.Id);   // and vice versa
            return rival;
        }

        [Test]
        [Description("SE-5e: a Diplomacy-yield delivery warms the working faction's relationship score with a met faction.")]
        public void DeliverDiplomacy_WarmsRelations()
        {
            var s = TestScenario.CreateWithColony();
            var rival = MetRival(s);
            var diplo = s.Faction.GetDataBlob<DiplomacyDB>();

            int before = diplo.GetRelationship(rival.Id).RelationScore;
            bool delivered = SiteYields.DeliverDiplomacy(s.Game, s.Faction.Id, magnitude: 100);

            Assert.That(delivered, Is.True, "there was a met faction to warm");
            Assert.That(diplo.GetRelationship(rival.Id).RelationScore, Is.GreaterThan(before), "relations warmed");
            // Symmetric: the rival's view of the player warmed too (relations are stored per-side).
            Assert.That(rival.GetDataBlob<DiplomacyDB>().GetRelationship(s.Faction.Id).RelationScore, Is.GreaterThan(before),
                "the goodwill is mutual");
        }

        [Test]
        [Description("SE-5e: an Intel-yield delivery raises the working faction's intel on a met rival (Inferred → Confirmed).")]
        public void DeliverIntel_ConfirmsRivalIntel()
        {
            var s = TestScenario.CreateWithColony();
            var rival = MetRival(s);
            var ledger = s.Faction.GetDataBlob<InformationLedgerDB>();

            Assert.That(ledger.LevelOf(rival.Id, IntelFacet.Military), Is.EqualTo(IntelLevel.Inferred),
                "we start with only the fuzzy default picture");

            bool delivered = SiteYields.DeliverIntel(s.Game, s.Faction.Id, s.StartingSystem.StarSysDateTime, magnitude: 100);

            Assert.That(delivered, Is.True, "there was a met rival to learn about");
            Assert.That(ledger.LevelOf(rival.Id, IntelFacet.Military), Is.EqualTo(IntelLevel.Confirmed),
                "the intelligence site sharpened our picture of the rival");
        }

        [Test]
        [Description("SE-5e no-op: a faction that has met nobody has no relations to warm and no rivals to learn about (safe/byte-identical).")]
        public void NoMetFactions_IsASafeNoOp()
        {
            var s = TestScenario.CreateWithColony(); // barebones start: the player has met nobody

            Assert.That(SiteYields.DeliverDiplomacy(s.Game, s.Faction.Id, magnitude: 100), Is.False,
                "no met factions → nothing warmed");
            Assert.That(SiteYields.DeliverIntel(s.Game, s.Faction.Id, s.StartingSystem.StarSysDateTime, magnitude: 100), Is.False,
                "no met rivals → no intel raised");
        }

        [Test]
        [Description("SE-5e: the DeliverSiteYield router pays a Diplomacy-yield site's magnitude into relations end-to-end.")]
        public void SiteYieldRouter_RoutesDiplomacy()
        {
            var s = TestScenario.CreateWithColony();
            var rival = MetRival(s);
            var diplo = s.Faction.GetDataBlob<DiplomacyDB>();

            // A resolved diplomatic surface site worked by the player, with banked progress.
            var site = FieldSiteFactory.CreateSurfaceSite(s.StartingSystem, s.StartingBody, 1, 0, 0,
                "Envoy Site", role: SiteRole.Diplomatic, yield: SiteYield.Diplomacy);
            var db = site.GetDataBlob<FieldSiteDB>();
            db.WorkedByFactionId = s.Faction.Id;
            db.Progress = 100;

            int before = diplo.GetRelationship(rival.Id).RelationScore;
            SiteWorkProcessor.DeliverSiteYield(site, db, SiteYield.Diplomacy, db.Progress);

            Assert.That(diplo.GetRelationship(rival.Id).RelationScore, Is.GreaterThan(before),
                "resolving the diplomatic site warmed relations through the router");
        }
    }
}
