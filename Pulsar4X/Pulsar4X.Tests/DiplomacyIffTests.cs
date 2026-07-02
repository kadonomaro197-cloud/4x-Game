using System.Linq;
using NUnit.Framework;
using Pulsar4X.Combat;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the FIRST behavior wiring off the diplomacy substrate (TESTING-TRACKER C6): combat hostility
    /// (<see cref="CombatEngagement.AreHostile"/>) now reads each faction's <see cref="DiplomacyDB"/>. The rule
    /// under test — diplomacy can only SUPPRESS the v1 "different faction = hostile" default, and only when BOTH
    /// sides hold a mutual peace (Friendly/Allied). This proves the lever has teeth (allies stop shooting) WITHOUT
    /// changing the default (an unmet stranger still fights), which is why every existing combat fixture is green.
    /// </summary>
    [TestFixture]
    public class DiplomacyIffTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[diplomacy-iff] " + m);

        /// <summary>A real ship owned by the given faction (so it carries a Manager → Game to reach the ledger).</summary>
        private static Entity MakeShip(TestScenario s, Entity faction, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.FactionOwnerID = faction.Id;
            return ship;
        }

        /// <summary>Set one faction's stance toward another to a target score (drives CurrentStance()).</summary>
        private static void SetStance(Entity faction, int towardFactionId, int score)
        {
            var dip = faction.GetDataBlob<DiplomacyDB>();
            var rel = dip.GetOrCreateRelationship(towardFactionId);
            rel.AdjustScore(score - rel.RelationScore);   // land exactly on `score`
        }

        [Test]
        [Description("Default (no relationship on record): two different non-neutral factions are HOSTILE — the v1 rule is preserved, so existing combat is unchanged.")]
        public void UnmetFactions_AreHostileByDefault()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var mine = MakeShip(s, s.Faction, "Ours");
            var theirs = MakeShip(s, reds, "Theirs");

            Assert.That(CombatEngagement.AreHostile(mine, theirs), Is.True, "unmet foreign factions default to hostile");
            Log("unmet → hostile ✓");
        }

        [Test]
        [Description("A MUTUAL Friendly/Allied stance suppresses hostility — allies don't shoot. A one-sided friendly declaration does NOT disarm you.")]
        public void MutualPeace_SuppressesHostility_ButOneSidedDoesNot()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var mine = MakeShip(s, s.Faction, "Ours");
            var theirs = MakeShip(s, reds, "Theirs");

            // One-sided friendly: only WE like THEM. They haven't reciprocated → still a fight.
            SetStance(s.Faction, reds.Id, RelationshipState.AlliedThreshold);
            Assert.That(CombatEngagement.AreHostile(mine, theirs), Is.True, "one-sided peace must not disarm us");
            Log("one-sided allied → still hostile ✓");

            // Now THEY reciprocate → mutual alliance → no longer hostile.
            SetStance(reds, s.Faction.Id, RelationshipState.AlliedThreshold);
            Assert.That(CombatEngagement.AreHostile(mine, theirs), Is.False, "a mutual alliance suppresses combat");
            Log("mutual allied → not hostile ✓");
        }

        [Test]
        [Description("A Friendly (not just Allied) mutual stance also suppresses; a Hostile/War stance keeps the fight (diplomacy never REMOVES a default it didn't create).")]
        public void FriendlySuppresses_WhileHostileStanceStillFights()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);

            var mine = MakeShip(s, s.Faction, "Ours");
            var theirs = MakeShip(s, reds, "Theirs");

            // Mutual Friendly (score in the Friendly band, below Allied) → suppressed.
            SetStance(s.Faction, reds.Id, RelationshipState.FriendlyThreshold);
            SetStance(reds, s.Faction.Id, RelationshipState.FriendlyThreshold);
            Assert.That(CombatEngagement.AreHostile(mine, theirs), Is.False, "mutual Friendly suppresses");
            Log("mutual friendly → not hostile ✓");

            // Drop one side to Hostile → the fight is back on.
            SetStance(reds, s.Faction.Id, RelationshipState.HostileThreshold);
            Assert.That(CombatEngagement.AreHostile(mine, theirs), Is.True, "a hostile side re-arms the pair");
            Log("one side hostile → hostile ✓");
        }

        [Test]
        [Description("The treaty→combat loop, CLOSED: signing a mutual non-aggression pact suppresses hostility even at a neutral score — a treaty actually stops the fight.")]
        public void SignedPact_StopsTheFight()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var mine = MakeShip(s, s.Faction, "Ours");
            var theirs = MakeShip(s, reds, "Theirs");

            // Default strangers fight.
            Assert.That(CombatEngagement.AreHostile(mine, theirs), Is.True, "unmet foreign factions fight");

            // Sign a mutual non-aggression pact (accepted at the default neutral score).
            bool signed = Treaties.Propose(s.Faction, reds, TreatyType.NonAggression, s.Game.TimePulse.GameGlobalDateTime);
            Assert.That(signed, Is.True, "a non-aggression pact is accepted at neutral");

            // The pact now stays both hands — no fight, without needing a high relation score.
            Assert.That(CombatEngagement.AreHostile(mine, theirs), Is.False, "the signed pact suppresses combat");
            Log("signed non-aggression pact → not hostile ✓");
        }

        [Test]
        [Description("Same faction is never hostile (unchanged v1 rule), even with no diplomacy involved.")]
        public void SameFaction_IsNeverHostile()
        {
            var s = TestScenario.CreateWithColony();
            var a = MakeShip(s, s.Faction, "A");
            var b = MakeShip(s, s.Faction, "B");
            Assert.That(CombatEngagement.AreHostile(a, b), Is.False, "same faction = same side");
            Log("same faction → not hostile ✓");
        }
    }
}
