using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the faction-level war ACTS (docs/DIPLOMACY-DESIGN.md, task #33): `Diplomacy.DeclareWar` /
    /// `MakePeace` latch and un-latch the war state on BOTH ledgers (a war is symmetric) and `IsAtWarWithAnyone`
    /// is the standing read that legitimacy/morale and NPC AI consult. This is the trigger that the casus-belli →
    /// legitimacy loop rides.
    /// </summary>
    [TestFixture]
    public class DiplomacyWarTests
    {
        [Test]
        [Description("Declaring war latches it on both sides; making peace un-latches it on both.")]
        public void DeclareWar_And_MakePeace_AreMutual()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var when = s.Game.TimePulse.GameGlobalDateTime;

            Assert.That(Diplomacy.DeclareWar(s.Faction, reds, CasusBelli.BorderDispute, when), Is.True);

            var mine = s.Faction.GetDataBlob<DiplomacyDB>();
            var theirs = reds.GetDataBlob<DiplomacyDB>();
            Assert.That(mine.GetRelationship(reds.Id).AtWar, Is.True);
            Assert.That(theirs.GetRelationship(s.Faction.Id).AtWar, Is.True, "war is mutual");
            Assert.That(mine.GetRelationship(reds.Id).CurrentStance(), Is.EqualTo(DiplomaticStance.War));
            Assert.That(mine.IsAtWarWithAnyone(), Is.True);

            Assert.That(Diplomacy.MakePeace(s.Faction, reds, when), Is.True);
            Assert.That(mine.GetRelationship(reds.Id).AtWar, Is.False);
            Assert.That(theirs.GetRelationship(s.Faction.Id).AtWar, Is.False, "peace is mutual");
            Assert.That(mine.IsAtWarWithAnyone(), Is.False);
        }

        [Test]
        [Description("Declaring war on yourself or a faction with no ledger is a safe no-op; peace with no war is a no-op.")]
        public void War_DefensiveNoOps()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var when = s.Game.TimePulse.GameGlobalDateTime;

            Assert.That(Diplomacy.DeclareWar(s.Faction, s.Faction, CasusBelli.None, when), Is.False, "no war on self");
            Assert.That(Diplomacy.MakePeace(s.Faction, reds, when), Is.False, "no peace where there's no war");
        }
    }
}
