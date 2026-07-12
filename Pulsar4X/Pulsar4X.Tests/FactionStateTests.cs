using System.Linq;
using NUnit.Framework;
using Pulsar4X.DataStructures;
using Pulsar4X.Factions;
using Pulsar4X.Industry;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.8 P0-a gauge (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md — the means-ends planner): the "what I have"
    /// snapshot. Proves it gathers the faction-wide gauges off the built `FactionRollup` readers and one slice per
    /// colony (industry / body minerals / free-line), and is null-safe on a null faction. Read-only → byte-identical.
    /// </summary>
    [TestFixture]
    public class FactionStateTests
    {
        [Test]
        [Description("Snapshot mirrors FactionRollup for the faction gauges and captures a slice per colony.")]
        public void Snapshot_GathersFactionAndColonyGauges()
        {
            var s = TestScenario.CreateWithColony();
            var state = FactionState.Snapshot(s.Faction);

            Assert.That(state, Is.Not.Null);
            Assert.That(state.Balance, Is.EqualTo(FactionRollup.Balance(s.Faction)), "balance off FactionRollup");
            Assert.That(state.MilitaryStrength, Is.EqualTo(FactionRollup.MilitaryStrength(s.Faction)).Within(1e-9));
            Assert.That(state.MeanMorale, Is.EqualTo(FactionRollup.MeanMorale(s.Faction)).Within(1e-9));

            var info = s.Faction.GetDataBlob<FactionInfoDB>();
            Assert.That(state.Colonies.Count, Is.EqualTo(info.Colonies.Count), "one slice per colony");

            var colony = state.Colonies.First(c => c.Colony == s.Colony);
            Assert.That(colony.Industry, Is.Not.Null, "the start colony's IndustryAbilityDB is captured");
            Assert.That(colony.PlanetMinerals, Is.Not.Null, "the start colony's body deposits are captured");
        }

        [Test]
        [Description("A null faction snapshots to null (the caller no-ops).")]
        public void Snapshot_Null_ForANullFaction()
        {
            Assert.That(FactionState.Snapshot(null), Is.Null);
        }

        [Test]
        [Description("P1-a: a MissingResources job surfaces via StalledJobs(); its raw-mineral inputs surface via MineralShortfalls() (below-floor only).")]
        public void MineralShortfalls_SurfaceBelowFloorInputsOfStalledJobs()
        {
            var s = TestScenario.CreateWithColony();
            var info = s.Faction.GetDataBlob<FactionInfoDB>();

            // A refining job (a ProcessedMaterial) whose ResourceCosts are raw MINERALS — which are NOT buildable, so
            // they sit below the mineral floor. Force it to the stalled state and park it on the refining line.
            var job = new IndustryJob(info, "space-crete");
            job.Status = IndustryJobStatus.MissingResources;
            var industry = s.Colony.GetDataBlob<IndustryAbilityDB>();
            string designType = info.IndustryDesigns["space-crete"].IndustryTypeID;
            string lineId = industry.ProductionLines.First(l => l.Value.IndustryTypeRates.ContainsKey(designType)).Key;
            IndustryTools.AddJob(s.Colony, lineId, job);

            var state = FactionState.Snapshot(s.Faction);

            Assert.That(state.StalledJobs().Any(t => t.job == job), Is.True, "the MissingResources job surfaces as stalled");

            var shortfalls = state.MineralShortfalls().ToList();
            Assert.That(shortfalls, Is.Not.Empty, "a stalled refining job has below-floor mineral shortfalls");
            Assert.That(shortfalls.All(sf => !info.IndustryDesigns.ContainsKey(sf.MaterialId)), Is.True,
                "every surfaced shortfall is below the mineral floor (a raw mineral, not a buildable)");
            Assert.That(shortfalls.All(sf => sf.Missing > 0), Is.True, "each carries a positive owed amount");
        }
    }
}
