using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Engine;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// P-3 military-reach gauge, slice 1 (docs/AI-BRAIN-BUILD-TRACKER.md — the deferred "sail the fleet at the enemy"
    /// muscle). Proves <see cref="MilitaryTarget.NearestEnemyColonyBody"/> — the "which enemy world do I aim at?"
    /// perception — (a) names the body of a colony owned by a faction we're AT WAR with (the latch a 3.4b coalition
    /// sets), (b) returns InvalidEntity when we're at peace (no war → no target), and (c) returns InvalidEntity when
    /// the at-war rival owns no colony (nothing to strike). Pure read, no warp/order surface — byte-identical.
    /// </summary>
    [TestFixture]
    public class MilitaryTargetTests
    {
        /// <summary>Give a rival faction a colony sitting on a fresh body; return that body (the strike target).</summary>
        private static Entity GiveRivalAColony(TestScenario s, Entity rival)
            => GiveRivalAColonyInManager(s, rival, s.Game.GlobalManager, 0);

        /// <summary>Give a rival a colony of a set population on a fresh body added to the given manager (so the test
        /// controls the target's VALUE and which star system it sits in — i.e. its reach).</summary>
        private static Entity GiveRivalAColonyInManager(TestScenario s, Entity rival, EntityManager mgr, long pop)
        {
            var body = Entity.Create();
            mgr.AddEntity(body);

            var colony = Entity.Create();
            colony.FactionOwnerID = rival.Id;
            mgr.AddEntity(colony);
            colony.SetDataBlob(new ColonyInfoDB(new Dictionary<int, long> { { 1, pop } }, body));

            rival.GetDataBlob<FactionInfoDB>().Colonies.Add(colony);
            return body;
        }

        [Test]
        [Description("At war with a colony-owning rival: MilitaryTarget names that colony's body. At peace: it names nothing.")]
        public void NearestEnemyColonyBody_NamesAnAtWarRivalsColonyBody_NothingAtPeace()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var enemyBody = GiveRivalAColony(s, reds);

            // At peace (no war on record) → nothing to strike, even though the rival owns a colony.
            Assert.That(MilitaryTarget.NearestEnemyColonyBody(s.Faction), Is.EqualTo(Entity.InvalidEntity),
                "at peace we name no target — a colony we're not at war with is not a strike target");

            // Declare war (the latch a coalition sets) → the rival's colony body becomes the target.
            Diplomacy.DeclareWar(s.Faction, reds, CasusBelli.ConfrontRival, s.Game.TimePulse.GameGlobalDateTime);
            Assert.That(MilitaryTarget.NearestEnemyColonyBody(s.Faction), Is.EqualTo(enemyBody),
                "at war with a colony-owning rival → we aim at that colony's body");
        }

        [Test]
        [Description("At war but the rival owns no colony → InvalidEntity (there's a war, but nothing on the ground to hit).")]
        public void NearestEnemyColonyBody_AtWarButNoRivalColony_NamesNothing()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);   // a fleet-only rival, no colony

            Diplomacy.DeclareWar(s.Faction, reds, CasusBelli.ConfrontRival, s.Game.TimePulse.GameGlobalDateTime);

            Assert.That(MilitaryTarget.NearestEnemyColonyBody(s.Faction), Is.EqualTo(Entity.InvalidEntity),
                "a war with no enemy colony yields no ground target (massing/patrol is the fallback, not a strike)");
        }

        [Test]
        [Description("BestEnemyTarget scores value x reach: a SMALLER but in-system prize beats a BIGGER but distant one (best given circumstances).")]
        public void BestEnemyTarget_PicksReachableValueOverRawSize()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            Diplomacy.DeclareWar(s.Faction, reds, CasusBelli.ConfrontRival, s.Game.TimePulse.GameGlobalDateTime);

            // A BIG prize in a DISTANT system (reach 0.35): 1,000,000 pop -> score 350,000.
            var distantBig = GiveRivalAColonyInManager(s, reds, s.Game.GlobalManager, 1_000_000);
            // A SMALLER prize in the SAME system as our own colony (reach 1.0): 500,000 pop -> score 500,000.
            var nearSmall = GiveRivalAColonyInManager(s, reds, s.StartingSystem, 500_000);

            var best = MilitaryTarget.BestEnemyTarget(s.Faction);

            Assert.That(best.IsValid, Is.True, "we're at war with a colony-owning rival, so there's a real target");
            Assert.That(best.ColonyBody, Is.EqualTo(nearSmall),
                "the reachable 500k world (score 500k) beats the distant 1M world (score 350k) — reach discounts distance");

            // No war -> no target (the invalid, byte-identical case).
            Diplomacy.MakePeace(s.Faction, reds, s.Game.TimePulse.GameGlobalDateTime);
            Assert.That(MilitaryTarget.BestEnemyTarget(s.Faction).IsValid, Is.False, "at peace there is no strike target");
        }

        [Test]
        [Description("Audit M3 — the fog-honest easiest-landing discount: an un-scouted/undefended world is undiscounted; a scouted garrison is discounted monotonically but never to zero.")]
        public void LandingEase_DiscountsAScoutedGarrison_ButNeverZeroes()
        {
            Assert.That(MilitaryTarget.LandingEaseFactor(0.0), Is.EqualTo(1.0),
                "an un-scouted (0 detected) world is undiscounted — byte-identical to the pre-M3 score");
            Assert.That(MilitaryTarget.LandingEaseFactor(500.0), Is.LessThan(1.0),
                "a scouted garrison makes the world a less attractive landing");
            Assert.That(MilitaryTarget.LandingEaseFactor(500.0), Is.GreaterThan(0.0),
                "the discount never zeroes a target's score (it stays valid)");
            Assert.That(MilitaryTarget.LandingEaseFactor(1000.0), Is.LessThan(MilitaryTarget.LandingEaseFactor(500.0)),
                "more detected defence => a stronger discount (the AI prefers the softer landing)");
        }
    }
}
