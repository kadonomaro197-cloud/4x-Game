using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Logistics;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the COMMERCE half of the diplomacy wiring (TESTING-TRACKER C6): cross-faction logistics is no
    /// longer hardcoded off — a ship may service a foreign trade base exactly when that base's owner has granted
    /// the ship's faction <see cref="RelationshipState.LogisticsAccess"/> by treaty. The rule under test
    /// (`LogisticsCycle.LogisticsAccessAllowed`): same faction always; foreign only on a STORED grant; and the grant
    /// is directional — the BASE OWNER opens their own supply network. Nothing sets the flag yet, so the default
    /// (false) reproduces the old same-faction-only behaviour — every existing logistics fixture is unchanged.
    /// </summary>
    [TestFixture]
    public class DiplomacyLogisticsAccessTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[logi-access] " + m);

        [Test]
        [Description("Same faction always has access (unchanged); a foreign faction needs a stored LogisticsAccess grant.")]
        public void Access_SameFactionAlways_ForeignNeedsGrant()
        {
            var s = TestScenario.CreateWithColony();
            int shipFac = s.Faction.Id;
            var baseFaction = FactionFactory.CreateBasicFaction(s.Game, "Traders", "TRD", 0);
            int baseFac = baseFaction.Id;

            // Same faction — always allowed.
            Assert.That(LogisticsCycle.LogisticsAccessAllowed(shipFac, shipFac, s.Game), Is.True, "own network");

            // Foreign, never met — closed.
            Assert.That(LogisticsCycle.LogisticsAccessAllowed(baseFac, shipFac, s.Game), Is.False, "unmet foreign");

            // Foreign, a relationship exists but no access flag — still closed (default false = old behaviour).
            var dip = baseFaction.GetDataBlob<DiplomacyDB>();
            var rel = dip.GetOrCreateRelationship(shipFac);
            Assert.That(LogisticsCycle.LogisticsAccessAllowed(baseFac, shipFac, s.Game), Is.False, "met but no grant");

            // The base owner grants access — now open.
            rel.LogisticsAccess = true;
            Assert.That(LogisticsCycle.LogisticsAccessAllowed(baseFac, shipFac, s.Game), Is.True, "granted by treaty");
            Log("same=open, foreign gated on grant ✓");
        }

        [Test]
        [Description("Access is granted by the BASE owner: the ship faction granting the base access does NOT let the base's network serve the ship — the grant must be on the base owner's ledger.")]
        public void Access_IsDirectional_BaseOwnerGrants()
        {
            var s = TestScenario.CreateWithColony();
            int shipFac = s.Faction.Id;
            var baseFaction = FactionFactory.CreateBasicFaction(s.Game, "Traders", "TRD", 0);
            int baseFac = baseFaction.Id;

            // The SHIP's faction grants the base faction access on ITS OWN ledger (wrong direction for this base).
            s.Faction.GetDataBlob<DiplomacyDB>().GetOrCreateRelationship(baseFac).LogisticsAccess = true;

            // Servicing the BASE still requires the BASE OWNER's grant, which doesn't exist → closed.
            Assert.That(LogisticsCycle.LogisticsAccessAllowed(baseFac, shipFac, s.Game), Is.False,
                "only the base owner can open its own network");
            Log("directional — base owner grants ✓");
        }
    }
}
