using System.Linq;
using NUnit.Framework;
using Pulsar4X.Colonies;
using Pulsar4X.Factions;

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
        [Description("Crew-shortage policy: full crew always builds; shortage blocks under Block, conscripts under BuildUnderstaffed. (The rule a government type flips.)")]
        public void ResolveConstructionCrew_PolicyDecidesShortage()
        {
            // Fully crewed → builds, commits exactly the requirement, not understaffed (policy irrelevant).
            var full = ColonyManpowerDB.ResolveConstructionCrew(availableBulk: 500, crewRequired: 100, CrewShortagePolicy.Block);
            Assert.That(full.CanBuild, Is.True);
            Assert.That(full.CrewToCommit, Is.EqualTo(100));
            Assert.That(full.Understaffed, Is.False);
            Assert.That(full.ShortBy, Is.EqualTo(0));

            // Short + Block → no build, commits nothing.
            var blocked = ColonyManpowerDB.ResolveConstructionCrew(availableBulk: 40, crewRequired: 100, CrewShortagePolicy.Block);
            Assert.That(blocked.CanBuild, Is.False);
            Assert.That(blocked.CrewToCommit, Is.EqualTo(0));
            Assert.That(blocked.ShortBy, Is.EqualTo(60));

            // Short + BuildUnderstaffed → builds anyway, conscripts what's available, flagged understaffed.
            var conscript = ColonyManpowerDB.ResolveConstructionCrew(availableBulk: 40, crewRequired: 100, CrewShortagePolicy.BuildUnderstaffed);
            Assert.That(conscript.CanBuild, Is.True);
            Assert.That(conscript.CrewToCommit, Is.EqualTo(40));
            Assert.That(conscript.Understaffed, Is.True);
            Assert.That(conscript.ShortBy, Is.EqualTo(60));

            // A design needing no crew always builds regardless of policy/pool.
            var crewless = ColonyManpowerDB.ResolveConstructionCrew(availableBulk: 0, crewRequired: 0, CrewShortagePolicy.Block);
            Assert.That(crewless.CanBuild, Is.True);
            Assert.That(crewless.CrewToCommit, Is.EqualTo(0));
        }

        [Test]
        [Description("M3-2b build gate through the REAL colony host: a crew shortage blocks the build under the default (Mid) government; a high-authority regime conscripts (BuildUnderstaffed) — the CrewPolicy rule-override end-to-end.")]
        public void ManpowerTools_ResolveBuild_HonoursHostPoolAndGovernment()
        {
            var s = TestScenario.CreateWithColony();

            // Force a near-total crew shortage on the real start colony (billions of pop → huge workforce):
            // commit all but 5 of the workforce, so a 100-crew hull can't be crewed.
            long pop = s.Colony.GetDataBlob<ColonyInfoDB>().Population.Values.Sum();
            long workforce = ColonyManpowerDB.Workforce(pop);
            var mp = s.Colony.GetDataBlob<ColonyManpowerDB>();
            mp.CommitBulk(workforce - 5);
            Assert.That(mp.AvailableBulk(pop), Is.EqualTo(5), "left only 5 crew free");

            // Default government is all-Mid → CrewPolicy = Block → the build is refused.
            var blocked = ManpowerTools.ResolveBuild(s.Colony, 100);
            Assert.That(blocked.CanBuild, Is.False, "Mid-authority (Block) refuses a build it can't crew");

            // Flip the regime to high authority → CrewPolicy = BuildUnderstaffed → it conscripts the 5 it has.
            s.Faction.GetDataBlob<GovernmentDB>().Authority = GovNotch.High;
            var conscript = ManpowerTools.ResolveBuild(s.Colony, 100);
            Assert.That(conscript.CanBuild, Is.True, "high-authority conscripts instead of blocking");
            Assert.That(conscript.Understaffed, Is.True);
            Assert.That(conscript.CrewToCommit, Is.EqualTo(5), "conscripts exactly what's available");
        }

        [Test]
        [Description("M3-2b: the gate is INERT on a host with no manpower pool (e.g. a station) — the build is always allowed.")]
        public void ManpowerTools_ResolveBuild_InertWithoutPool()
        {
            var s = TestScenario.CreateWithColony();
            // The hosting body has no ColonyManpowerDB → unenforced → always allowed, commits nothing.
            var decision = ManpowerTools.ResolveBuild(s.StartingBody, 100000);
            Assert.That(decision.CanBuild, Is.True, "a pool-less host is unenforced");
            Assert.That(decision.CrewToCommit, Is.EqualTo(0));
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
