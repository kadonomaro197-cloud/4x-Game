using NUnit.Framework;
using Pulsar4X.Colonies;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// M3 sub-slice 1 gauge: people as a finite, hard-drawn resource (docs/MORALE-AND-POPULATION-DESIGN.md).
    /// Proves the pure pool math — workforce/talent derived from population, available = pool − committed, and
    /// the commit/release accounting that the construction/officer gates (sub-slice 2) will enforce.
    /// </summary>
    [TestFixture]
    public class ManpowerTests
    {
        [Test]
        [Description("Workforce and talent are fixed fractions of population.")]
        public void Pools_AreFractionsOfPopulation()
        {
            const long pop = 1_000_000;
            Assert.That(ColonyManpowerDB.Workforce(pop), Is.EqualTo((long)(pop * ColonyManpowerDB.WorkforceFraction)));
            Assert.That(ColonyManpowerDB.TalentPool(pop), Is.EqualTo((long)(pop * ColonyManpowerDB.TalentFraction)));
            // Talent is the scarce tier — far smaller than bulk workforce.
            Assert.That(ColonyManpowerDB.TalentPool(pop), Is.LessThan(ColonyManpowerDB.Workforce(pop)));
            // Zero / negative population yields no pool.
            Assert.That(ColonyManpowerDB.Workforce(0), Is.EqualTo(0));
            Assert.That(ColonyManpowerDB.Workforce(-5), Is.EqualTo(0));
        }

        [Test]
        [Description("Available = pool − committed; committing reduces it; over-commit is refused.")]
        public void Available_TracksCommitment_AndRefusesOvercommit()
        {
            const long pop = 1000; // workforce 500, talent 5
            var mp = new ColonyManpowerDB();

            Assert.That(mp.AvailableBulk(pop), Is.EqualTo(ColonyManpowerDB.Workforce(pop)), "starts fully available");
            Assert.That(mp.CanCommitBulk(pop, 500), Is.True);
            Assert.That(mp.CanCommitBulk(pop, 501), Is.False, "can't commit more than the workforce");

            mp.CommitBulk(300);
            Assert.That(mp.AvailableBulk(pop), Is.EqualTo(200));
            Assert.That(mp.CanCommitBulk(pop, 200), Is.True);
            Assert.That(mp.CanCommitBulk(pop, 201), Is.False, "only the remainder is committable");

            // Talent is the scarce pool.
            Assert.That(mp.AvailableTalent(pop), Is.EqualTo(ColonyManpowerDB.TalentPool(pop)));
            Assert.That(mp.CanCommitTalent(pop, 5), Is.True);
            Assert.That(mp.CanCommitTalent(pop, 6), Is.False);
        }

        [Test]
        [Description("Releasing frees commitment and never goes negative (disband returns people).")]
        public void Release_FreesCommitment_FlooredAtZero()
        {
            const long pop = 1000;
            var mp = new ColonyManpowerDB();
            mp.CommitBulk(400);
            mp.ReleaseBulk(150);
            Assert.That(mp.CommittedBulk, Is.EqualTo(250));
            Assert.That(mp.AvailableBulk(pop), Is.EqualTo(250));

            mp.ReleaseBulk(9999); // over-release floors at zero, not negative
            Assert.That(mp.CommittedBulk, Is.EqualTo(0));
            Assert.That(mp.AvailableBulk(pop), Is.EqualTo(ColonyManpowerDB.Workforce(pop)));
        }

        [Test]
        [Description("ManpowerDB clones deeply (survives entity transfer / save-load).")]
        public void ColonyManpowerDB_ClonesDeeply()
        {
            var original = new ColonyManpowerDB();
            original.CommitBulk(123);
            original.CommitTalent(4);

            var clone = (ColonyManpowerDB)original.Clone();
            Assert.That(clone.CommittedBulk, Is.EqualTo(123));
            Assert.That(clone.CommittedTalent, Is.EqualTo(4));

            clone.CommitBulk(1);
            Assert.That(original.CommittedBulk, Is.EqualTo(123), "clone shares no state with the original");
        }
    }
}
