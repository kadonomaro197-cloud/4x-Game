using Pulsar4X.Galaxy;

namespace Pulsar4X.Sites
{
    /// <summary>
    /// Ground-fog slice 4 (`docs/ground/SURFACE-FOG-AND-RECON-DESIGN.md`): hidden special sites — a SURFACE field site
    /// (a ruin / anomaly on a planet's ground) is INVISIBLE to a faction until that faction has scouted the region it
    /// sits in. The "exploration payoff": you send a scout across the planet and it uncovers what orbit couldn't see.
    ///
    /// This is a DERIVED read off the per-faction ground fog (<see cref="PlanetRegionsDB.PerFactionRevealed"/>, populated
    /// by a scout's ground radar in slice 3): a surface site is discovered iff its region has been revealed to the
    /// faction. No stored per-site "discovered-by" set — so it can't drift, needs no populate hook, and is trivially
    /// byte-identical (nothing consumes it until the client `SiteWindow` filters by it — slice 4b). A SPACE anomaly is
    /// NOT gated here (its visibility is governed by ship-sensor detection, a separate system), so this returns visible
    /// for a non-surface site.
    ///
    /// v1 granularity: region-level (a scout that maps the region uncovers the ruin in it), consistent with how the same
    /// radar reveals the region's terrain + deposit LOCATIONS at reach. Hex-EXACT hidden sites (invisible even within a
    /// scouted region until a unit walks the exact hex) are a flagged refinement.
    /// </summary>
    public static class SiteVisibility
    {
        /// <summary>Is <paramref name="site"/> discovered by <paramref name="factionId"/>? A surface site is discovered
        /// once the faction has revealed the site's region in <paramref name="regionsDB"/> (the per-faction ground fog);
        /// a space anomaly (not a surface site) is not gated here and reads visible. Defensive: a null site reads not
        /// visible; a surface site with no region layer reads not visible (can't have been scouted).</summary>
        public static bool IsDiscoveredBy(FieldSiteDB site, int factionId, PlanetRegionsDB regionsDB)
        {
            if (site == null) return false;
            if (!site.IsSurfaceSite) return true;   // a space anomaly — ship sensors govern it, not the ground fog
            if (regionsDB == null) return false;
            return regionsDB.IsRegionRevealedFor(factionId, site.SurfaceRegionIndex);
        }
    }
}
