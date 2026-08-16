using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Datablobs;   // ComponentInstancesDB
using Pulsar4X.Factions;    // FactionInfoDB, TransactionCategory

namespace Pulsar4X.Tests
{
    /// <summary>
    /// TIER 2.5 gauge — colony INSTALLATION UPKEEP (the colony echo of StationUpkeep / GroundForceUpkeep). Proves a
    /// colony's installations cost a fraction of their build price to KEEP each month, billed as a
    /// <see cref="TransactionCategory.ColonyInstallationUpkeep"/> expense on the owning faction's ledger — and only when
    /// the flag is on (so the engine suite is byte-identical off). Measured on a REAL start colony, and
    /// calibration-independent: it asserts the RELATIONSHIP (a positive bill exactly equal to the derived upkeep,
    /// negative on the ledger, nothing when off) against the colony's own live installations.
    /// </summary>
    [TestFixture]
    public class ColonyUpkeepTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[colony-upkeep] " + m);

        // The flag is a process-global static — always restore it so it can't leak into the rest of the shard.
        [TearDown]
        public void ResetFlag() => ColonyEconomyProcessor.EnableInstallationUpkeep = false;

        [Test]
        [Description("A colony's installations cost a fraction of their build price to keep each month, billed as a ColonyInstallationUpkeep expense on the owning faction's ledger — and only when the flag is on (byte-identical off).")]
        public void InstallationUpkeep_BillsAFractionOfBuildPrice_FlagGated()
        {
            var s = TestScenario.CreateWithColony();
            var colony = s.Colony;
            var comps = colony.GetDataBlob<ComponentInstancesDB>();

            decimal upkeep = ColonyEconomyProcessor.InstallationUpkeep(comps);
            Log($"installation upkeep = {upkeep:N2} credits/month (rate {ColonyEconomyProcessor.UpkeepRatePerMonth:P0})");
            Assert.That(upkeep, Is.GreaterThan(0m), "the start colony's installations have a build price → upkeep > 0");

            int factionId = colony.FactionOwnerID;
            Assert.That(s.Game.Factions.ContainsKey(factionId), Is.True, "the colony's owner is a real faction");
            var factionInfo = s.Game.Factions[factionId].GetDataBlob<FactionInfoDB>();

            // Flag OFF → billing books nothing (the byte-identical default).
            ColonyEconomyProcessor.EnableInstallationUpkeep = false;
            ColonyEconomyProcessor.BillInstallationUpkeep(colony);
            Assert.That(factionInfo.Money.GetTransactionsByCategory(TransactionCategory.ColonyInstallationUpkeep), Is.Empty,
                "flag off → no upkeep transaction booked");

            // Flag ON → books exactly one expense equal to the derived upkeep.
            ColonyEconomyProcessor.EnableInstallationUpkeep = true;
            ColonyEconomyProcessor.BillInstallationUpkeep(colony);
            var billed = factionInfo.Money.GetTransactionsByCategory(TransactionCategory.ColonyInstallationUpkeep);
            Assert.That(billed.Count, Is.EqualTo(1), "flag on → one upkeep transaction booked");
            Assert.That(billed[0].Amount, Is.EqualTo(-upkeep),
                "booked as a NEGATIVE expense equal to the derived upkeep");
            Assert.That(billed[0].IsExpense, Is.True);
        }

        [Test]
        [Description("The upkeep helper is defensive — null components read 0 without throwing.")]
        public void InstallationUpkeep_NullComponents_IsZero()
        {
            Assert.That(ColonyEconomyProcessor.InstallationUpkeep(null), Is.EqualTo(0m));
        }
    }
}
