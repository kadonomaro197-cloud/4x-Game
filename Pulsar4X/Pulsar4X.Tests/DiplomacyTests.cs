using NUnit.Framework;
using System;
using Pulsar4X.Factions;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for the diplomacy SUBSTRATE (docs/DIPLOMACY-DESIGN.md): the per-pair relationship record
    /// (<see cref="RelationshipState"/>) and the per-faction ledger (<see cref="DiplomacyDB"/>). This proves the
    /// relationship-TRACK behavior — a single score nudged by events, with the headline stance derived from it
    /// (War a latched override) — before any processor reads it for IFF/combat. Substrate step: data + the
    /// derivation, no behavior wiring yet.
    /// </summary>
    [TestFixture]
    public class DiplomacyTests
    {
        [Test]
        [Description("The headline stance is derived from the score by fixed thresholds.")]
        public void Stance_DerivesFromScore()
        {
            var rel = new RelationshipState(otherFactionId: 7);

            rel.RelationScore = 0;
            Assert.That(rel.CurrentStance(), Is.EqualTo(DiplomaticStance.Neutral), "a stranger at 0 is Neutral");

            rel.RelationScore = RelationshipState.FriendlyThreshold;
            Assert.That(rel.CurrentStance(), Is.EqualTo(DiplomaticStance.Friendly));

            rel.RelationScore = RelationshipState.AlliedThreshold;
            Assert.That(rel.CurrentStance(), Is.EqualTo(DiplomaticStance.Allied));

            rel.RelationScore = RelationshipState.HostileThreshold;
            Assert.That(rel.CurrentStance(), Is.EqualTo(DiplomaticStance.Hostile));

            rel.RelationScore = RelationshipState.HostileThreshold - 1;
            Assert.That(rel.CurrentStance(), Is.EqualTo(DiplomaticStance.Hostile), "below the hostile band stays Hostile");
        }

        [Test]
        [Description("A declared war latches War regardless of score; peace un-latches it.")]
        public void War_IsALatchedOverride()
        {
            var rel = new RelationshipState(otherFactionId: 3);
            rel.RelationScore = 90;                 // would read Allied on score alone
            rel.DeclareWar();

            Assert.That(rel.AtWar, Is.True);
            Assert.That(rel.CurrentStance(), Is.EqualTo(DiplomaticStance.War), "war overrides a high score");
            Assert.That(rel.RelationScore, Is.EqualTo(RelationshipState.MinScore), "war floors the score");

            rel.MakePeace();
            Assert.That(rel.AtWar, Is.False);
            // Score left at the floor → peace starts fragile (Hostile), not snapped back to Neutral.
            Assert.That(rel.CurrentStance(), Is.EqualTo(DiplomaticStance.Hostile));
        }

        [Test]
        [Description("AdjustScore nudges the track and clamps to the -100..+100 limits.")]
        public void AdjustScore_NudgesAndClamps()
        {
            var rel = new RelationshipState(otherFactionId: 1);

            Assert.That(rel.AdjustScore(30), Is.EqualTo(30));
            Assert.That(rel.AdjustScore(-10), Is.EqualTo(20));

            // Over-shoot the top — clamps at MaxScore.
            rel.AdjustScore(1000);
            Assert.That(rel.RelationScore, Is.EqualTo(RelationshipState.MaxScore));

            // Over-shoot the bottom — clamps at MinScore.
            rel.AdjustScore(-1000);
            Assert.That(rel.RelationScore, Is.EqualTo(RelationshipState.MinScore));
        }

        [Test]
        [Description("Reading an unmet faction returns a fresh Neutral WITHOUT storing it; only an actual interaction persists a row.")]
        public void GetRelationship_DefaultsNeutral_WithoutStoring()
        {
            var db = new DiplomacyDB();

            var rel = db.GetRelationship(42);
            Assert.That(rel.CurrentStance(), Is.EqualTo(DiplomaticStance.Neutral));
            Assert.That(rel.RelationScore, Is.EqualTo(0));
            Assert.That(db.HasMet(42), Is.False, "merely looking must not create a relationship");

            // An actual interaction persists the row.
            var stored = db.GetOrCreateRelationship(42);
            stored.AdjustScore(40);
            Assert.That(db.HasMet(42), Is.True);
            Assert.That(db.GetRelationship(42).RelationScore, Is.EqualTo(40), "the stored row is now read back");
        }

        [Test]
        [Description("Clone deep-copies the whole ledger — mutating the copy does not touch the original.")]
        public void Clone_DeepCopiesTheLedger()
        {
            var db = new DiplomacyDB();
            var rel = db.GetOrCreateRelationship(5);
            rel.AdjustScore(50);
            rel.LastContact = new DateTime(2200, 1, 1);

            var copy = (DiplomacyDB)db.Clone();
            Assert.That(copy.GetRelationship(5).RelationScore, Is.EqualTo(50), "copy carries the value");

            // Mutate the copy — original must be untouched (deep copy, not shared reference).
            copy.GetRelationship(5).AdjustScore(-50);
            Assert.That(db.GetRelationship(5).RelationScore, Is.EqualTo(50), "original unchanged");
            Assert.That(copy.GetRelationship(5).RelationScore, Is.EqualTo(0), "copy changed independently");
        }
    }
}
