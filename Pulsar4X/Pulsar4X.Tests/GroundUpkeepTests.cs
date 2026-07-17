using System;
using NUnit.Framework;
using Pulsar4X.Factions;
using Pulsar4X.GroundCombat;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Ground-force UPKEEP (the litmus follow-up "an army costs money as it exists"). Gauges
    /// <see cref="GroundUpkeep.BillIfDue"/>: a standing ground unit bills its owning faction a monthly credit upkeep
    /// (the ground echo of a station's operating cost), billing is MONTHLY (not per-tick), PER-FACTION (a contested
    /// body charges each side its own force), and BYTE-IDENTICAL for a free (0-upkeep) unit. Drives the biller directly
    /// with a controlled clock (deterministic) rather than advancing the whole sim.
    /// </summary>
    [TestFixture]
    public class GroundUpkeepTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[upkeep] " + m);

        private static GroundUnitDesign Design(string id, double upkeep) =>
            new GroundUnitDesign { UniqueID = id, Name = id, UnitType = GroundUnitType.Infantry, Attack = 10, Defense = 1, HitPoints = 100, UpkeepCredits = upkeep };

        [Test]
        [Description("Two paid units bill their faction monthly; a free unit adds nothing; the first sight only stamps the clock; not re-billed within a month.")]
        public void GroundUnit_BillsMonthlyUpkeep_ToItsFaction()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            int factionId = s.Faction.Id;
            var money = s.Faction.GetDataBlob<FactionInfoDB>().Money;

            GroundForces.RaiseUnit(body, Design("upkeep-paid", 100), factionId, 0);
            GroundForces.RaiseUnit(body, Design("upkeep-paid", 100), factionId, 0);
            GroundForces.RaiseUnit(body, Design("upkeep-free", 0), factionId, 0);   // free unit — bills nothing

            var t0 = new DateTime(2050, 1, 1);
            decimal start = money.GetCurrentFunds();

            // First sight: initialise the clock, DON'T back-bill.
            GroundUpkeep.BillIfDue(body, t0);
            Assert.That(money.GetCurrentFunds(), Is.EqualTo(start), "first sight only stamps the clock — no back-bill");

            // Within a month: still no bill.
            GroundUpkeep.BillIfDue(body, t0.AddDays(20));
            Assert.That(money.GetCurrentFunds(), Is.EqualTo(start), "no bill before a month elapses");

            // A month later: bill 2 x 100 = 200 (the free unit adds nothing).
            GroundUpkeep.BillIfDue(body, t0.AddDays(31));
            decimal afterFirst = money.GetCurrentFunds();
            Assert.That(start - afterFirst, Is.EqualTo(200m), "two 100-credit units bill 200; the free unit adds nothing");
            Log($"month 1 billed {start - afterFirst:0} credits (2 paid + 1 free)");

            // Immediately after: not due again.
            GroundUpkeep.BillIfDue(body, t0.AddDays(32));
            Assert.That(money.GetCurrentFunds(), Is.EqualTo(afterFirst), "not billed again within the same month");

            // Next month: bills another 200.
            GroundUpkeep.BillIfDue(body, t0.AddDays(62));
            Assert.That(afterFirst - money.GetCurrentFunds(), Is.EqualTo(200m), "the next month bills another 200");
        }

        [Test]
        [Description("A body of only free (0-upkeep) units bills nobody — byte-identical, the default for every existing unit.")]
        public void FreeUnits_BillNothing()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var money = s.Faction.GetDataBlob<FactionInfoDB>().Money;

            GroundForces.RaiseUnit(body, Design("upkeep-free-2", 0), s.Faction.Id, 0);

            var t0 = new DateTime(2050, 1, 1);
            decimal start = money.GetCurrentFunds();
            GroundUpkeep.BillIfDue(body, t0);              // init
            GroundUpkeep.BillIfDue(body, t0.AddDays(31));  // due, but nothing to bill
            Assert.That(money.GetCurrentFunds(), Is.EqualTo(start), "0-upkeep units never bill — byte-identical");
        }
    }
}
