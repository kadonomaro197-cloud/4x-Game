using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-5a — the composable BRANCH data structure (docs/SITE-ENGINE-DESIGN.md §3/§4). The first slice of
    /// SE-5 (branches + the ruptured edge): it only ADDS the <see cref="SiteBranch"/> record + the
    /// <see cref="FieldSiteDB.Branches"/> list — no site machine or processor reads them yet (SE-5b/5c do). Proves the
    /// record carries its dials, a fresh/existing site is byte-identical (empty branch list, no branch committed), and
    /// the list deep-clones so a moved/saved site can't share branch records. Pure data, no colony harness.
    /// </summary>
    [TestFixture]
    public class SiteBranchDataTests
    {
        [Test]
        [Description("SE-5a byte-identity: a site with no authored branches has an EMPTY list and nothing committed (the SE-1 single-path site is untouched).")]
        public void FreshSite_HasNoBranches_ByteIdentical()
        {
            var site = new FieldSiteDB();
            Assert.That(site.Branches, Is.Not.Null, "the list exists (never null → safe to read)");
            Assert.That(site.Branches, Is.Empty, "no branches on a plain SE-1 site");
            Assert.That(site.HasBranches, Is.False, "a single-path site presents no choice");
            Assert.That(site.CommittedBranchIndex, Is.EqualTo(-1), "nothing committed yet");
        }

        [Test]
        [Description("SE-5a: a SiteBranch carries its own name, unlock cost, yield, magnitude scale, result state, and rupture flag.")]
        public void SiteBranch_CarriesItsDials()
        {
            var branch = new SiteBranch
            {
                Name = "Ally with the guardian",
                UnderstandingRequired = 150.0,
                Yield = SiteYield.StrategicAsset,
                YieldScale = 0.75,
                ResultStatus = SiteStatus.Persistent,
                Ruptures = false
            };

            Assert.That(branch.Name, Is.EqualTo("Ally with the guardian"));
            Assert.That(branch.UnderstandingRequired, Is.EqualTo(150.0));
            Assert.That(branch.Yield, Is.EqualTo(SiteYield.StrategicAsset));
            Assert.That(branch.YieldScale, Is.EqualTo(0.75));
            Assert.That(branch.ResultStatus, Is.EqualTo(SiteStatus.Persistent));
            Assert.That(branch.Ruptures, Is.False);
        }

        [Test]
        [Description("SE-5a defaults: a bare SiteBranch is a Nothing-yield, full-magnitude, Depleted, non-rupturing choice.")]
        public void SiteBranch_Defaults_AreSafe()
        {
            var branch = new SiteBranch();
            Assert.That(branch.Yield, Is.EqualTo(SiteYield.Nothing), "a bare branch pays nothing until authored");
            Assert.That(branch.YieldScale, Is.EqualTo(1.0), "full banked magnitude by default");
            Assert.That(branch.ResultStatus, Is.EqualTo(SiteStatus.Depleted), "spends the site by default");
            Assert.That(branch.Ruptures, Is.False, "no rupture unless authored");
        }

        [Test]
        [Description("SE-5a: a site's authored branches are readable and HasBranches flips true.")]
        public void SiteWithBranches_ReadsThem()
        {
            var site = new FieldSiteDB
            {
                Branches = new List<SiteBranch>
                {
                    new SiteBranch { Name = "Seal it",  UnderstandingRequired = 50,  Yield = SiteYield.Nothing },
                    new SiteBranch { Name = "Study on", UnderstandingRequired = 100, Yield = SiteYield.Research, YieldScale = 1.0 },
                }
            };

            Assert.That(site.HasBranches, Is.True);
            Assert.That(site.Branches, Has.Count.EqualTo(2));
            Assert.That(site.Branches[0].Name, Is.EqualTo("Seal it"));
            Assert.That(site.Branches[1].Yield, Is.EqualTo(SiteYield.Research));
        }

        [Test]
        [Description("SE-5a: Clone deep-copies the branch list — a moved/saved site can't share branch records with the original.")]
        public void Clone_DeepCopiesBranches()
        {
            var site = new FieldSiteDB
            {
                CommittedBranchIndex = 1,
                Branches = new List<SiteBranch>
                {
                    new SiteBranch { Name = "Fight",    Yield = SiteYield.Nothing },
                    new SiteBranch { Name = "Negotiate", Yield = SiteYield.StrategicAsset },
                }
            };

            var copy = (FieldSiteDB)site.Clone();
            Assert.That(copy.Branches, Has.Count.EqualTo(2), "branches copied");
            Assert.That(copy.CommittedBranchIndex, Is.EqualTo(1), "committed index copied");
            Assert.That(copy.Branches[1].Name, Is.EqualTo("Negotiate"));

            // Mutating the copy's branch must not touch the original (a real deep copy, not a shared reference).
            copy.Branches[0].Name = "Surrender";
            Assert.That(site.Branches[0].Name, Is.EqualTo("Fight"), "the original branch record is untouched");
            Assert.That(ReferenceEquals(site.Branches[0], copy.Branches[0]), Is.False, "distinct branch instances");
        }
    }
}
