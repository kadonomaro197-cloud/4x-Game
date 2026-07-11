using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// F-A3 gauge (docs/AI-BRAIN-BUILD-TRACKER.md, Movement I): the faction-tier roll-up reads that the NPC
    /// needs-ladder will consume. Proves (a) the roll-up equals a hand-sum of the same per-colony sources, (b) the
    /// morale/legitimacy means are genuinely POPULATION-WEIGHTED (a big unhappy world outweighs a small content one),
    /// and (c) a colony-less faction reads sane neutral/zero defaults ("no data" is not "in crisis"). Read-only and
    /// additive — no existing behaviour changes, so this is a new gauge, not a rebalance.
    /// </summary>
    [TestFixture]
    public class FactionRollupTests
    {
        private static ColonyMoraleDB MoraleAt(double v) { var m = new ColonyMoraleDB(); m.Morale = v; return m; }
        private static LegitimacyDB LegitAt(double v) { var l = new LegitimacyDB(); l.Legitimacy = v; return l; }

        // Build entities empty then SetDataBlob each blob (the EntityManagerTests pattern) — this stores the blobs
        // directly and avoids AddEntity's dependency validation, which a bare colony/faction blob set wouldn't pass.
        private static Entity MakeColony(EntityManager mgr, long pop, double morale, double legitimacy)
        {
            var e = Entity.Create();
            mgr.AddEntity(e);
            e.SetDataBlob(new ColonyInfoDB(new Dictionary<int, long> { { 1, pop } }, Entity.InvalidEntity));
            e.SetDataBlob(MoraleAt(morale));
            e.SetDataBlob(LegitAt(legitimacy));
            return e;
        }

        private static Entity MakeFaction(EntityManager mgr, params Entity[] colonies)
        {
            var f = Entity.Create();
            mgr.AddEntity(f);
            f.SetDataBlob(new FactionInfoDB());
            var info = f.GetDataBlob<FactionInfoDB>();
            foreach (var c in colonies) info.Colonies.Add(c);
            return f;
        }

        [Test]
        [Description("The roll-up equals a hand-sum of the SAME per-colony sources on the real start faction.")]
        public void Rollup_EqualsHandSum_OnTheRealStartFaction()
        {
            var s = TestScenario.CreateWithColony();
            var info = s.Faction.GetDataBlob<FactionInfoDB>();

            // Balance reads exactly the faction's own ledger (rollup == the source, not a re-derivation).
            Assert.That(FactionRollup.Balance(s.Faction), Is.EqualTo(info.Money.GetCurrentFunds()));

            // Population == the hand-sum over the faction's colonies.
            long handPop = info.Colonies.Sum(c => c.GetDataBlob<ColonyInfoDB>().Population.Values.Sum());
            Assert.That(FactionRollup.TotalPopulation(s.Faction), Is.EqualTo(handPop));
            Assert.That(FactionRollup.ColonyCount(s.Faction), Is.EqualTo(info.Colonies.Count));

            // Set every colony's morale/legitimacy to a known value → the (weighted) mean is that value.
            foreach (var c in info.Colonies)
            {
                c.GetDataBlob<ColonyMoraleDB>().Morale = 42;
                c.GetDataBlob<LegitimacyDB>().Legitimacy = 63;
            }
            Assert.That(FactionRollup.MeanMorale(s.Faction), Is.EqualTo(42.0).Within(1e-9));
            Assert.That(FactionRollup.MeanLegitimacy(s.Faction), Is.EqualTo(63.0).Within(1e-9));
        }

        [Test]
        [Description("Morale/legitimacy means are POPULATION-WEIGHTED, not a plain average — the big world dominates.")]
        public void MeanMorale_AndLegitimacy_ArePopulationWeighted()
        {
            var s = TestScenario.CreateWithColony();
            var mgr = s.Game.GlobalManager;

            var big = MakeColony(mgr, pop: 900, morale: 80, legitimacy: 90);
            var small = MakeColony(mgr, pop: 100, morale: 30, legitimacy: 10);
            var faction = MakeFaction(mgr, big, small);

            Assert.That(FactionRollup.TotalPopulation(faction), Is.EqualTo(1000));
            Assert.That(FactionRollup.ColonyCount(faction), Is.EqualTo(2));

            // Weighted: (80*900 + 30*100)/1000 = 75.0  (a plain average would be 55 — this is what proves weighting).
            Assert.That(FactionRollup.MeanMorale(faction), Is.EqualTo(75.0).Within(1e-9));
            // Weighted: (90*900 + 10*100)/1000 = 82.0.
            Assert.That(FactionRollup.MeanLegitimacy(faction), Is.EqualTo(82.0).Within(1e-9));
        }

        [Test]
        [Description("A colony-less faction reads sane defaults: 0 population/balance, Neutral(50) morale/legitimacy.")]
        public void Rollup_OnAColonylessFaction_ReadsNeutralDefaults()
        {
            var s = TestScenario.CreateWithColony();
            var mgr = s.Game.GlobalManager;
            var faction = MakeFaction(mgr); // no colonies

            Assert.That(FactionRollup.TotalPopulation(faction), Is.EqualTo(0));
            Assert.That(FactionRollup.ColonyCount(faction), Is.EqualTo(0));
            Assert.That(FactionRollup.Balance(faction), Is.EqualTo(0m), "a fresh ledger holds nothing");
            Assert.That(FactionRollup.MeanMorale(faction), Is.EqualTo(ColonyMoraleDB.Neutral).Within(1e-9),
                "no colonies is 'no data', which reads neutral — not a crisis reading of 0");
            Assert.That(FactionRollup.MeanLegitimacy(faction), Is.EqualTo(LegitimacyDB.Neutral).Within(1e-9));
        }
    }
}
