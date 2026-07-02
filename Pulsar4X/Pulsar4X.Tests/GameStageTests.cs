using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Colonies;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the staged game-state generator (task #39): `GameStageFactory.AgeTo` layers a New-Game start up
    /// to Early/Mid/Late so the late-triggering political cluster can be SEEN (and asserted) without playing for
    /// hours. Proves each stage lands its content — a frontier colony (Early), met rivals + a treaty (Mid), an
    /// active war + a rebelling colony (Late) — and that the transforms are cumulative + convergent (re-aging
    /// doesn't duplicate). This is what makes a rich state loadable in CI and, via the DevTools button, at the PC.
    /// </summary>
    [TestFixture]
    public class GameStageTests
    {
        // At least one faction OTHER than the player that owns a colony (a real rival empire).
        private static int RivalEmpireCount(TestScenario s)
            => s.Game.Factions.Values.Count(f => f.Id != s.Faction.Id
                && f.TryGetDataBlob<FactionInfoDB>(out var fi) && fi.Colonies.Count > 0);

        [Test]
        [Description("Early makes the player multi-world (several colonies) — the economy is no longer one planet.")]
        public void Early_MakesThePlayerMultiWorld()
        {
            var s = TestScenario.CreateWithColony();
            int before = s.Faction.GetDataBlob<FactionInfoDB>().Colonies.Count;

            GameStageFactory.AgeTo(s.Game, s.Faction, GameStage.Early);

            Assert.That(s.Faction.GetDataBlob<FactionInfoDB>().Colonies.Count,
                Is.GreaterThanOrEqualTo(2), "frontier colonies should be added");
            Assert.That(s.Faction.GetDataBlob<FactionInfoDB>().Colonies.Count, Is.GreaterThan(before));
        }

        [Test]
        [Description("Mid establishes rival EMPIRES — one friendly (with a treaty), one hostile — each with its OWN colonies.")]
        public void Mid_EstablishesRivalEmpiresWithColonies()
        {
            var s = TestScenario.CreateWithColony();
            GameStageFactory.AgeTo(s.Game, s.Faction, GameStage.Mid);

            var dip = s.Faction.GetDataBlob<DiplomacyDB>();
            Assert.That(dip.Relationships.Count, Is.GreaterThanOrEqualTo(2), "at least two rivals met");
            Assert.That(dip.Relationships.Values.Any(r => r.CurrentStance() == DiplomaticStance.Friendly),
                Is.True, "a cooperative neighbour");
            Assert.That(dip.Relationships.Values.Any(r =>
                    r.TradeAgreement || r.NonAggressionPact || r.LogisticsAccess || r.MilitaryAccess || r.DefensivePact),
                Is.True, "a standing treaty was signed");
            // The rivals are real empires, not just contacts — they own colonies of their own.
            Assert.That(RivalEmpireCount(s), Is.GreaterThanOrEqualTo(1),
                "at least one rival faction should own colonies");
        }

        [Test]
        [Description("Late is a mature galaxy: several player colonies, multiple rival empires, an active war, and a colony in open rebellion.")]
        public void Late_IsAMatureGalaxy()
        {
            var s = TestScenario.CreateWithColony();
            GameStageFactory.AgeTo(s.Game, s.Faction, GameStage.Late);

            var colonies = s.Faction.GetDataBlob<FactionInfoDB>().Colonies;
            Assert.That(colonies.Count, Is.GreaterThanOrEqualTo(3), "the player has grown to several colonies");
            Assert.That(s.Game.Factions.Values.Count(f => f.Id != s.Faction.Id
                && f.TryGetDataBlob<FactionInfoDB>(out var fi) && fi.Colonies.Count > 0),
                Is.GreaterThanOrEqualTo(2), "multiple rival empires with colonies");
            Assert.That(s.Faction.GetDataBlob<DiplomacyDB>().IsAtWarWithAnyone(), Is.True, "at war with a rival");
            Assert.That(colonies.Any(c => c.TryGetDataBlob<RebellionDB>(out var reb) && reb.IsRebelling),
                Is.True, "a frontier colony should be in rebellion");
        }

        [Test]
        [Description("Aging is cumulative (Late includes Early+Mid) and convergent (re-aging doesn't duplicate colonies/rivals).")]
        public void Aging_IsCumulativeAndConvergent()
        {
            var s = TestScenario.CreateWithColony();

            GameStageFactory.AgeTo(s.Game, s.Faction, GameStage.Late);
            int colonies1 = s.Faction.GetDataBlob<FactionInfoDB>().Colonies.Count;
            int rivals1 = s.Faction.GetDataBlob<DiplomacyDB>().Relationships.Count;

            // Late must have applied Early + Mid too.
            Assert.That(colonies1, Is.GreaterThanOrEqualTo(2));
            Assert.That(rivals1, Is.GreaterThanOrEqualTo(2));

            // Re-aging converges — no duplicate colonies/rivals.
            GameStageFactory.AgeTo(s.Game, s.Faction, GameStage.Late);
            Assert.That(s.Faction.GetDataBlob<FactionInfoDB>().Colonies.Count, Is.EqualTo(colonies1), "no duplicate colony");
            Assert.That(s.Faction.GetDataBlob<DiplomacyDB>().Relationships.Count, Is.EqualTo(rivals1), "no duplicate rival");
        }
    }
}
