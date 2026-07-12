using System;
using NUnit.Framework;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// F-C3a gauge (docs/ESPIONAGE-AND-INTELLIGENCE-DESIGN.md — the Information Ledger): per-rival, per-facet intel
    /// with the Inferred → Confirmed → Stale bands. Proves an unknown rival/facet reads Inferred, Confirm raises it,
    /// intel decays to Stale only after the refresh window, and the blob clones deeply (save/load, entity transfer).
    /// </summary>
    [TestFixture]
    public class InformationLedgerTests
    {
        private static readonly DateTime T0 = new DateTime(2050, 1, 1);

        [Test]
        [Description("Unknown = Inferred; Confirm raises one facet only; intel decays to Stale after the window, not before.")]
        public void Ledger_Inferred_Confirm_Decay()
        {
            var ledger = new InformationLedgerDB();
            const int rival = 42;

            Assert.That(ledger.LevelOf(rival, IntelFacet.Military), Is.EqualTo(IntelLevel.Inferred),
                "a rival you hold nothing on reads Inferred — the poker default");

            ledger.Confirm(rival, IntelFacet.Military, T0);
            Assert.That(ledger.LevelOf(rival, IntelFacet.Military), Is.EqualTo(IntelLevel.Confirmed));
            Assert.That(ledger.LevelOf(rival, IntelFacet.Economy), Is.EqualTo(IntelLevel.Inferred),
                "confirming one facet leaves the others Inferred — intel is a specialisation choice");

            // Still within the refresh window → stays Confirmed.
            ledger.DecayStale(T0.AddDays(10), TimeSpan.FromDays(30));
            Assert.That(ledger.LevelOf(rival, IntelFacet.Military), Is.EqualTo(IntelLevel.Confirmed),
                "fresh intel doesn't decay");

            // Past the window → decays to Stale (you must refresh).
            ledger.DecayStale(T0.AddDays(40), TimeSpan.FromDays(30));
            Assert.That(ledger.LevelOf(rival, IntelFacet.Military), Is.EqualTo(IntelLevel.Stale),
                "unrefreshed intel goes Stale — you can't know a rival forever");
        }

        [Test]
        [Description("The ledger clones deeply — a clone shares no state with the original (save/load, entity transfer).")]
        public void Ledger_ClonesDeeply()
        {
            var ledger = new InformationLedgerDB();
            ledger.Confirm(7, IntelFacet.Secrets, T0);

            var clone = (InformationLedgerDB)ledger.Clone();
            Assert.That(clone.LevelOf(7, IntelFacet.Secrets), Is.EqualTo(IntelLevel.Confirmed), "the clone carries the intel");

            // Mutating the clone must not touch the original.
            clone.Confirm(7, IntelFacet.Military, T0);
            Assert.That(ledger.LevelOf(7, IntelFacet.Military), Is.EqualTo(IntelLevel.Inferred),
                "the clone shares no state with the original");
        }
    }
}
