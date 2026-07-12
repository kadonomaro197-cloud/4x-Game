using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Components;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Enhancers ⚙6.3 Systems ▸ AUTOMATION — the one live-wireable dial in the otherwise net-new Systems door
    /// ("run with fewer crew"). A <see cref="CrewAutomationAtb"/> "Automation Suite" cuts a hull's BULK crew
    /// requirement at design time, so an automated ship draws less scarce workforce from the building colony's
    /// manpower pool — the economy×engineering trade: spend mass + high tech to spend fewer people.
    ///
    /// Cradle-to-grave: JSON <c>crew-automation</c> template → NCalc <c>crewAutomationAtbArgs</c> →
    /// <c>CrewAutomationAtb</c> → read in <see cref="ShipDesign.Recalculate"/> (subtracts from BULK crew, never the
    /// veteran cadre). The base-mod Vanguard Automated Cruiser is an EXACT copy of the Aegis test warship PLUS one
    /// Automation Suite, so the crew saving shows as a clean delta. Byte-identical when absent (no module → 0
    /// reduction): every existing ship's crew is unchanged. Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipAutomationTests
    {
        private const string Vanguard = "default-ship-design-test-vanguard";
        private const string Aegis = "default-ship-design-test-warship";
        private const string AutomationDesign = "default-design-crew-automation";
        private const double ExpectedReduction = 30; // the crew-automation template's default Crew Reduction
        private static void Log(string m) => TestContext.Progress.WriteLine("[crew-automation] " + m);

        [Test]
        [Description("The Automation Suite binds from JSON: its CrewAutomationAtb reads the design's Crew Reduction (30) through the real NCalc constructor path — the gotcha-10 registration sensor.")]
        public void TheAutomationSuite_BindsItsCrewReductionFromJson()
        {
            var s = TestScenario.CreateWithColony();
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ComponentDesigns[AutomationDesign];
            Assert.That(design.TryGetAttribute<CrewAutomationAtb>(out var atb), Is.True, "the automation design carries a CrewAutomationAtb (six-point registration intact)");
            Log($"Automation Suite: reduces {atb.CrewReduction:0} bulk crew, own crew {design.CrewReq}");
            Assert.That(atb.CrewReduction, Is.EqualTo(ExpectedReduction).Within(1e-6), "the Crew Reduction dial bound from the JSON design");
        }

        [Test]
        [Description("End-to-end: the Vanguard is an exact Aegis + one Automation Suite, so it needs FEWER crew than the identical stock hull — the suite cuts more bulk crew than it costs to run. CrewReq = Aegis + suite's own crew - the reduction; TalentReq stays 0 (no caliber → byte-identical talent path).")]
        public void TheVanguard_NeedsFewerCrew_ThanTheIdenticalStockHull()
        {
            var s = TestScenario.CreateWithColony();
            var factionInfo = s.Faction.GetDataBlob<FactionInfoDB>();
            var vanguard = factionInfo.ShipDesigns[Vanguard];
            var aegis = factionInfo.ShipDesigns[Aegis];
            var autoDesign = factionInfo.ComponentDesigns[AutomationDesign];
            Log($"crew: Vanguard {vanguard.CrewReq} vs Aegis {aegis.CrewReq} (suite own crew {autoDesign.CrewReq}, reduction {ExpectedReduction})");

            Assert.That(vanguard.TalentReq, Is.EqualTo(0), "no caliber module → the talent path is byte-identical (0)");
            Assert.That(vanguard.CrewReq, Is.LessThan(aegis.CrewReq), "the automation suite is a net crew win — it replaces more crew than it costs to run");
            // Exact: the Vanguard adds the suite's own crew to the Aegis, then the automation cuts the reduction from bulk.
            Assert.That(vanguard.CrewReq, Is.EqualTo(aegis.CrewReq + autoDesign.CrewReq - (int)ExpectedReduction),
                "crew = stock Aegis + the suite's operators - the bulk positions it automates away");
        }
    }
}
