using System.Linq;
using NUnit.Framework;
using Pulsar4X.Factions;

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
    }
}
