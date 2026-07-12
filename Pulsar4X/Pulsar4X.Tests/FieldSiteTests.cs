using NUnit.Framework;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Site Engine SE-1a — the site record + pure state machine (docs/SITE-ENGINE-DESIGN.md §4). Proves the
    /// agency-preserving core end to end at the data level: a discovered site is begun by the first work
    /// (DISCOVERED→WORKED), progress + understanding accrue, understanding UNLOCKS the resolve branch (never a timer),
    /// resolving transitions to the terminal state its Shape dictates, and a resolved site stops accruing. The whole
    /// SE-1 spine drives this machine; getting it exactly right first is the keystone.
    /// </summary>
    [TestFixture]
    public class FieldSiteTests
    {
        private static FieldSiteDB NewAnomaly() => new FieldSiteDB
        {
            Role = SiteRole.Science,
            Shape = SiteShape.OneShot,
            Hook = SiteHook.Benign,
            Yield = SiteYield.Research,
            UnderstandingToResolve = 100.0
        };

        [Test]
        [Description("SE-1a: a fresh site is Discovered with the SE-1 science/research anomaly defaults.")]
        public void NewSite_IsDiscovered_WithDefaults()
        {
            var site = new FieldSiteDB();
            Assert.That(site.Status, Is.EqualTo(SiteStatus.Discovered));
            Assert.That(site.Role, Is.EqualTo(SiteRole.Science));
            Assert.That(site.Yield, Is.EqualTo(SiteYield.Research));
            Assert.That(site.Progress, Is.EqualTo(0.0));
            Assert.That(site.Understanding, Is.EqualTo(0.0));
        }

        [Test]
        [Description("SE-1a: the first work begins the study (Discovered→Worked) and banks progress + understanding.")]
        public void Accrue_FirstWork_BeginsStudy_AndBanks()
        {
            var site = NewAnomaly();
            SiteMachine.Accrue(site, work: 10, understanding: 5);

            Assert.That(site.Status, Is.EqualTo(SiteStatus.Worked), "the first worker begins the study");
            Assert.That(site.Progress, Is.EqualTo(10.0), "work banks into Progress (the yield magnitude)");
            Assert.That(site.Understanding, Is.EqualTo(5.0), "understanding banks toward the branch gate");

            SiteMachine.Accrue(site, work: 10, understanding: 5);
            Assert.That(site.Progress, Is.EqualTo(20.0), "accrual accumulates");
            Assert.That(site.Understanding, Is.EqualTo(10.0));
        }

        [Test]
        [Description("SE-1a: the resolve branch is locked below the understanding threshold and unlocks at it.")]
        public void BranchUnlocks_AtThreshold()
        {
            var site = NewAnomaly();
            SiteMachine.Accrue(site, work: 0, understanding: 99);
            Assert.That(SiteMachine.BranchUnlocked(site), Is.False, "99 < 100 → still locked");

            SiteMachine.Accrue(site, work: 0, understanding: 1);
            Assert.That(SiteMachine.BranchUnlocked(site), Is.True, "100 ≥ 100 → the branch unlocks (knowledge, not a timer)");
        }

        [Test]
        [Description("SE-1a: resolve fails until the branch is unlocked, then transitions to the terminal state by Shape.")]
        public void Resolve_RequiresUnlock_ThenTransitionsByShape()
        {
            var oneShot = NewAnomaly();                       // OneShot
            SiteMachine.Accrue(oneShot, work: 50, understanding: 50);
            Assert.That(SiteMachine.Resolve(oneShot), Is.False, "can't resolve before understanding is enough");
            Assert.That(oneShot.Status, Is.EqualTo(SiteStatus.Worked));

            SiteMachine.Accrue(oneShot, work: 0, understanding: 50); // reach the threshold
            Assert.That(SiteMachine.Resolve(oneShot), Is.True, "unlocked → resolves");
            Assert.That(oneShot.Status, Is.EqualTo(SiteStatus.Depleted), "a one-shot resolves to Depleted (spent)");

            var faucet = NewAnomaly();
            faucet.Shape = SiteShape.Persistent;
            SiteMachine.Accrue(faucet, work: 100, understanding: 100);
            Assert.That(SiteMachine.Resolve(faucet), Is.True);
            Assert.That(faucet.Status, Is.EqualTo(SiteStatus.Persistent), "a persistent site resolves to a standing stream");
        }

        [Test]
        [Description("SE-1a: a resolved (Depleted) site no longer accrues toward a fresh resolve.")]
        public void ResolvedSite_DoesNotAccrue()
        {
            var site = NewAnomaly();
            SiteMachine.Accrue(site, work: 100, understanding: 100);
            SiteMachine.Resolve(site);
            Assert.That(site.Status, Is.EqualTo(SiteStatus.Depleted));

            double progressBefore = site.Progress;
            SiteMachine.Accrue(site, work: 999, understanding: 999);
            Assert.That(site.Progress, Is.EqualTo(progressBefore), "a spent site ignores further work");
            Assert.That(site.Status, Is.EqualTo(SiteStatus.Depleted), "and stays Depleted");
        }

        [Test]
        [Description("SE-1a: Clone deep-copies the site record (save/load + move-between-managers safety).")]
        public void Clone_DeepCopies()
        {
            var site = NewAnomaly();
            SiteMachine.Accrue(site, work: 42, understanding: 60);
            var copy = (FieldSiteDB)site.Clone();

            Assert.That(copy.Progress, Is.EqualTo(42.0));
            Assert.That(copy.Understanding, Is.EqualTo(60.0));
            Assert.That(copy.Status, Is.EqualTo(SiteStatus.Worked));

            // Mutating the copy must not touch the original (a real deep copy).
            SiteMachine.Accrue(copy, work: 100, understanding: 100);
            Assert.That(site.Progress, Is.EqualTo(42.0), "the original is untouched by the copy's accrual");
        }
    }
}
