using System;
using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for FIRST CONTACT (TESTING-TRACKER C6, the diplomacy "front door"): when a faction's sensors first
    /// detect an entity owned by another non-neutral faction, both factions get a Neutral relationship row (they
    /// now KNOW of each other) stamped with the contact time, and a first-contact event fires. The scan wires this
    /// with a one-line call to <see cref="FirstContact.OnDetection"/>; these test the logic that call runs. Proves:
    /// contact is MUTUAL, it fires exactly ONCE per pair (idempotent), and neutral/own-faction targets are skipped.
    /// </summary>
    [TestFixture]
    public class DiplomacyFirstContactTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[first-contact] " + m);

        private static Entity MakeShip(TestScenario s, int ownerFactionId, string name)
        {
            var design = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns.Values.First();
            var ship = ShipFactory.CreateShip(design, s.Faction, s.StartingBody, name);
            ship.FactionOwnerID = ownerFactionId;
            return ship;
        }

        [Test]
        [Description("A real detection of a foreign entity records a MUTUAL Neutral relationship (both sides now know each other) stamped with the contact time.")]
        public void Detection_RecordsMutualContact()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var theirScout = MakeShip(s, reds.Id, "Red Scout");
            var when = s.Game.TimePulse.GameGlobalDateTime;

            var mine = s.Faction.GetDataBlob<DiplomacyDB>();
            var theirs = reds.GetDataBlob<DiplomacyDB>();
            Assume.That(mine.HasMet(reds.Id), Is.False, "precondition: strangers");

            bool met = FirstContact.OnDetection(s.Faction, theirScout, when);

            Assert.That(met, Is.True, "first detection registers a meeting");
            Assert.That(mine.HasMet(reds.Id), Is.True, "detector now knows the other faction");
            Assert.That(theirs.HasMet(s.Faction.Id), Is.True, "contact is MUTUAL — the other side knows us too");
            Assert.That(mine.GetRelationship(reds.Id).CurrentStance(), Is.EqualTo(DiplomaticStance.Neutral),
                "first contact opens at Neutral (no auto-hostility)");
            Assert.That(mine.GetRelationship(reds.Id).LastContact, Is.EqualTo(when), "LastContact stamped");
            Assert.That(theirs.GetRelationship(s.Faction.Id).LastContact, Is.EqualTo(when), "mirror stamped too");
            Log("mutual Neutral contact recorded ✓");
        }

        [Test]
        [Description("First contact fires exactly ONCE per faction pair — a second detection of the same faction is a no-op.")]
        public void Detection_IsIdempotentPerPair()
        {
            var s = TestScenario.CreateWithColony();
            var reds = FactionFactory.CreateBasicFaction(s.Game, "Reds", "RED", 0);
            var scoutA = MakeShip(s, reds.Id, "Red Scout A");
            var scoutB = MakeShip(s, reds.Id, "Red Scout B");
            var when = s.Game.TimePulse.GameGlobalDateTime;

            long before = FirstContact.ContactCount;
            Assert.That(FirstContact.OnDetection(s.Faction, scoutA, when), Is.True, "first meeting");
            // A DIFFERENT ship of the SAME faction — the pair has already met, so no new meeting.
            Assert.That(FirstContact.OnDetection(s.Faction, scoutB, when), Is.False, "same faction, already met");
            Assert.That(FirstContact.ContactCount - before, Is.EqualTo(1), "the counter climbed exactly once");
            Log("idempotent per pair ✓");
        }

        [Test]
        [Description("Neutral targets (planets/asteroids) and our own entities never count as first contact.")]
        public void Detection_SkipsNeutralAndOwnFaction()
        {
            var s = TestScenario.CreateWithColony();

            var neutralRock = MakeShip(s, Game.NeutralFactionId, "Neutral Object");
            Assert.That(FirstContact.OnDetection(s.Faction, neutralRock, s.Game.TimePulse.GameGlobalDateTime),
                Is.False, "a neutral object is not a faction to meet");

            var ourOwnShip = MakeShip(s, s.Faction.Id, "Our Ship");
            Assert.That(FirstContact.OnDetection(s.Faction, ourOwnShip, s.Game.TimePulse.GameGlobalDateTime),
                Is.False, "our own ship is not first contact");

            Assert.That(s.Faction.GetDataBlob<DiplomacyDB>().Relationships.Count, Is.EqualTo(0),
                "no relationship rows created for neutral/own targets");
            Log("neutral + own-faction skipped ✓");
        }
    }
}
