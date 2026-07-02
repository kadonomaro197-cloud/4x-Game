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
        [Test]
        [Description("Early adds a second (frontier) colony — the economy becomes multi-world.")]
        public void Early_AddsAFrontierColony()
        {
            var s = TestScenario.CreateWithColony();
            int before = s.Faction.GetDataBlob<FactionInfoDB>().Colonies.Count;

            GameStageFactory.AgeTo(s.Game, s.Faction, GameStage.Early);

            Assert.That(s.Faction.GetDataBlob<FactionInfoDB>().Colonies.Count,
                Is.GreaterThanOrEqualTo(2), "a frontier colony should be added");
            Assert.That(s.Faction.GetDataBlob<FactionInfoDB>().Colonies.Count, Is.GreaterThan(before));
        }

        [Test]
        [Description("Mid establishes met rivals — one friendly (with a treaty), one hostile — so the diplomacy readout has both ends.")]
        public void Mid_EstablishesRivalsWithVariedRelations()
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
        }

        [Test]
        [Description("Late brings an active war and a colony in open rebellion — the full drama is visible.")]
        public void Late_BringsWarAndRebellion()
        {
            var s = TestScenario.CreateWithColony();
            GameStageFactory.AgeTo(s.Game, s.Faction, GameStage.Late);

            Assert.That(s.Faction.GetDataBlob<DiplomacyDB>().IsAtWarWithAnyone(), Is.True, "at war with a rival");

            var colonies = s.Faction.GetDataBlob<FactionInfoDB>().Colonies;
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
