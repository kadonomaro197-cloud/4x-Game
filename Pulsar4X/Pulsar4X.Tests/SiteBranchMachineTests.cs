using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-5b — the composable MULTI-BRANCH state machine (docs/SITE-ENGINE-DESIGN.md §3/§4). Adds the
    /// branch-aware reads + resolve to <see cref="SiteMachine"/> WITHOUT touching the single-path resolve: a branchless
    /// site still resolves by Shape exactly as SE-1 (byte-identical — <see cref="FieldSiteTests"/> pins that), while a
    /// branched site unlocks each branch independently by its own understanding cost and resolves to the CHOSEN branch's
    /// result state. Pure machine, no colony harness.
    /// </summary>
    [TestFixture]
    public class SiteBranchMachineTests
    {
        private static FieldSiteDB WorkedBranchedSite()
        {
            // A worked site offering two branches: a cheap "Seal" (unlocks at 50, spends the site) and an expensive
            // "Ally" (unlocks at 150, leaves a persistent stream).
            var site = new FieldSiteDB
            {
                Role = SiteRole.Science,
                Shape = SiteShape.OneShot,
                Branches = new List<SiteBranch>
                {
                    new SiteBranch { Name = "Seal", UnderstandingRequired = 50,  Yield = SiteYield.Nothing,       ResultStatus = SiteStatus.Depleted },
                    new SiteBranch { Name = "Ally", UnderstandingRequired = 150, Yield = SiteYield.StrategicAsset, ResultStatus = SiteStatus.Persistent },
                }
            };
            SiteMachine.Accrue(site, work: 100, understanding: 0); // begin the study (Discovered→Worked), bank progress
            return site;
        }

        [Test]
        [Description("SE-5b byte-identity: a branchless site exposes NO branches and still resolves by Shape (the SE-1 single path is untouched).")]
        public void BranchlessSite_ResolvesBySinglePath()
        {
            var site = new FieldSiteDB { Shape = SiteShape.OneShot, UnderstandingToResolve = 100 };
            SiteMachine.Accrue(site, work: 50, understanding: 100);

            Assert.That(site.HasBranches, Is.False);
            Assert.That(SiteMachine.UnlockedBranchIndices(site), Is.Empty, "no branches to unlock");
            Assert.That(SiteMachine.AnyBranchUnlocked(site), Is.False);
            Assert.That(SiteMachine.ResolveBranch(site, 0), Is.False, "a branchless site can't resolve a branch");
            Assert.That(site.Status, Is.EqualTo(SiteStatus.Worked), "and ResolveBranch didn't touch it");

            // The old single-path resolve still works exactly as SE-1.
            Assert.That(SiteMachine.Resolve(site), Is.True);
            Assert.That(site.Status, Is.EqualTo(SiteStatus.Depleted));
        }

        [Test]
        [Description("SE-5b: branches unlock INDEPENDENTLY as Understanding reaches each branch's own cost (knowledge unlocks, branches compose).")]
        public void Branches_UnlockIndependentlyByUnderstanding()
        {
            var site = WorkedBranchedSite();

            // Understanding 0 → nothing unlocked.
            Assert.That(SiteMachine.AnyBranchUnlocked(site), Is.False, "nothing unlocked at 0 understanding");

            // Understanding 50 → the cheap branch unlocks, the expensive one does not.
            SiteMachine.Accrue(site, work: 0, understanding: 50);
            Assert.That(SiteMachine.IsBranchUnlocked(site, 0), Is.True, "Seal (req 50) unlocked at 50");
            Assert.That(SiteMachine.IsBranchUnlocked(site, 1), Is.False, "Ally (req 150) still locked");
            Assert.That(SiteMachine.UnlockedBranchIndices(site), Is.EqualTo(new List<int> { 0 }));

            // Understanding 150 → both unlocked (composable — the cheap one is NOT consumed).
            SiteMachine.Accrue(site, work: 0, understanding: 100);
            Assert.That(SiteMachine.UnlockedBranchIndices(site), Is.EqualTo(new List<int> { 0, 1 }),
                "a patient player who accrues full understanding still gets BOTH choices");
        }

        [Test]
        [Description("SE-5b: committing a chosen unlocked branch resolves the site to THAT branch's result state and records the choice.")]
        public void ResolveBranch_CommitsChosenBranch()
        {
            var site = WorkedBranchedSite();
            SiteMachine.Accrue(site, work: 0, understanding: 150); // both branches unlocked

            // Commit "Ally" (index 1, persistent stream).
            Assert.That(SiteMachine.ResolveBranch(site, 1), Is.True, "an unlocked branch commits");
            Assert.That(site.CommittedBranchIndex, Is.EqualTo(1), "the choice is recorded");
            Assert.That(site.Status, Is.EqualTo(SiteStatus.Persistent), "resolved to the chosen branch's result state, not the Shape default");
        }

        [Test]
        [Description("SE-5b guards: a LOCKED branch, an out-of-range index, and an un-worked site all refuse to commit.")]
        public void ResolveBranch_RefusesLockedInvalidOrUnworked()
        {
            var site = WorkedBranchedSite();
            SiteMachine.Accrue(site, work: 0, understanding: 50); // only branch 0 unlocked

            Assert.That(SiteMachine.ResolveBranch(site, 1), Is.False, "branch 1 (req 150) is still locked");
            Assert.That(SiteMachine.ResolveBranch(site, 5), Is.False, "out-of-range index");
            Assert.That(SiteMachine.ResolveBranch(site, -1), Is.False, "negative index");
            Assert.That(site.Status, Is.EqualTo(SiteStatus.Worked), "none of those touched the site");

            // A resolved site won't re-commit.
            Assert.That(SiteMachine.ResolveBranch(site, 0), Is.True, "branch 0 is unlocked → commits");
            Assert.That(SiteMachine.ResolveBranch(site, 0), Is.False, "already resolved (not Worked) → refuses a second commit");
        }
    }
}
