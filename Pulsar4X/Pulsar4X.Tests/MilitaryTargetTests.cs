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
        {
            var mgr = s.Game.GlobalManager;

            var body = Entity.Create();
            mgr.AddEntity(body);

            var colony = Entity.Create();
            colony.FactionOwnerID = rival.Id;
            mgr.AddEntity(colony);
            colony.SetDataBlob(new ColonyInfoDB(new Dictionary<int, long>(), body));

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
    }
}
