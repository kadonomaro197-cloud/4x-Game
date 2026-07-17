using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.Galaxy;
using Pulsar4X.Sites;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Ground-fog slice 4 (`docs/ground/SURFACE-FOG-AND-RECON-DESIGN.md`): hidden special sites. A surface field site is
    /// invisible to a faction until it has scouted the region the site sits in — the exploration payoff. Gauges the pure
    /// derived read <see cref="SiteVisibility.IsDiscoveredBy"/> off the per-faction ground fog (a scout's radar reveals
    /// the region -> the site in it is discovered, for that faction only). A space anomaly is not ground-fog-gated.
    /// </summary>
    [TestFixture]
    public class SiteVisibilityTests
    {
        private const int Scout = 1, Blind = 2;   // two factions; one scouts the site's region, one never does

        private static PlanetRegionsDB FourRegions()
        {
            var regions = new List<Region>();
            for (int i = 0; i < 4; i++) regions.Add(new Region { Index = i });
            return new PlanetRegionsDB(regions);
        }

        [Test]
        [Description("A surface site is hidden until a faction reveals its region (per-faction); a space anomaly is not ground-fog-gated.")]
        public void SurfaceSite_IsHidden_UntilTheRegionIsScouted_PerFaction()
        {
            var regions = FourRegions();
            var site = new FieldSiteDB { SurfaceBodyEntityId = 42, SurfaceRegionIndex = 2 };   // a ruin in region 2

            // Full fog: nobody has scouted region 2, so nobody sees the site.
            Assert.That(SiteVisibility.IsDiscoveredBy(site, Scout, regions), Is.False, "un-scouted surface site is hidden");
            Assert.That(SiteVisibility.IsDiscoveredBy(site, Blind, regions), Is.False);

            // The scout reveals region 2 (as a ground radar does in slice 3) — for the Scout faction only.
            regions.RevealRegionFor(Scout, 2);
            Assert.That(SiteVisibility.IsDiscoveredBy(site, Scout, regions), Is.True, "the scout uncovered the site in its region");
            Assert.That(SiteVisibility.IsDiscoveredBy(site, Blind, regions), Is.False, "a faction that never scouted it stays blind");

            // Revealing a DIFFERENT region does not discover the site.
            regions.RevealRegionFor(Blind, 0);
            Assert.That(SiteVisibility.IsDiscoveredBy(site, Blind, regions), Is.False, "scouting the wrong region reveals nothing");

            // A SPACE anomaly (not a surface site) is not gated by the ground fog — ship sensors govern it.
            var anomaly = new FieldSiteDB();   // SurfaceBodyEntityId defaults to -1 → not a surface site
            Assert.That(anomaly.IsSurfaceSite, Is.False);
            Assert.That(SiteVisibility.IsDiscoveredBy(anomaly, Blind, regions), Is.True, "a space anomaly isn't ground-fog-gated");

            // Defensive: a null site / a surface site with no region layer.
            Assert.That(SiteVisibility.IsDiscoveredBy(null, Scout, regions), Is.False);
            Assert.That(SiteVisibility.IsDiscoveredBy(site, Scout, null), Is.False, "no region layer -> can't have been scouted");
        }
    }
}
