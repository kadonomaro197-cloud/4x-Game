using System.Linq;
using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Phase-2.8 P1-e gauge (docs/AI-MEANS-ENDS-PLANNER-DESIGN.md): the FeasibilityOracle teeth. Proves the oracle
    /// still passes the start colony's real build (the teeth don't false-bite the happy path — byte-identical) but
    /// now REFUSES a build that would silently stall — here, a production line whose infra-scaled throughput is below
    /// 1 pt/tick (the same condition `ConstructStuff` skips on). It mirrors execution, never a superset.
    /// </summary>
    [TestFixture]
    public class FeasibilityOracleTests
    {
        [Test]
        [Description("CanQueue passes a real start build, but refuses one whose line turns over <1 pt/tick (would silently stall).")]
        public void CanQueue_PassesHappyPath_RefusesSubUnityCapacity()
        {
            var s = TestScenario.CreateWithColony();
            var info = s.Faction.GetDataBlob<FactionInfoDB>();
            var state = FactionState.Snapshot(s.Faction);
            var colony = state.Colonies.First(c => c.Colony == s.Colony);
            var design = info.IndustryDesigns["space-crete"];

            Assert.That(FeasibilityOracle.CanQueue(colony, design, info), Is.True,
                "the start colony can really build space-crete — the teeth don't false-bite");

            // Zero the throughput of every line that runs this design's type → infra-scaled capacity < 1.
            foreach (var line in colony.Industry.ProductionLines.Values)
                if (line.IndustryTypeRates.ContainsKey(design.IndustryTypeID))
                    line.IndustryTypeRates[design.IndustryTypeID] = 0;

            Assert.That(FeasibilityOracle.CanQueue(colony, design, info), Is.False,
                "a line that turns over <1 pt/tick would stall forever → the oracle refuses it up front");
        }
    }
}
