using System;

namespace Pulsar4X.Sites
{
    /// <summary>
    /// Site Engine SE-1a — the PURE state machine (docs/SITE-ENGINE-DESIGN.md §4). The agency-preserving core: work
    /// accrues Progress + Understanding; enough Understanding UNLOCKS the resolve branches (never a timer that closes
    /// them); committing a branch RESOLVES the site down to a terminal state by its Shape. Pure/static and deterministic
    /// (no clock, no RNG, no game state) so it's exactly testable and the SE-1b processor is a thin driver over it.
    /// </summary>
    public static class SiteMachine
    {
        /// <summary>
        /// Apply one work step from the on-site worker: the first work begins the study (DISCOVERED → WORKED), then each
        /// step banks <paramref name="work"/> into Progress (the yield magnitude) and <paramref name="understanding"/>
        /// into Understanding (the branch gate). A site that is already resolved (Depleted/Persistent/Ruptured) does NOT
        /// accrue toward a fresh resolve — a persistent site's ongoing stream is the processor's job, not this gate.
        /// Negative inputs are ignored (defensive).
        /// </summary>
        public static void Accrue(FieldSiteDB site, double work, double understanding)
        {
            if (site == null) return;

            if (site.Status == SiteStatus.Discovered)
                site.Status = SiteStatus.Worked;          // first worker on-site begins the study

            if (site.Status != SiteStatus.Worked) return; // only an actively-worked site accrues toward resolution

            if (work > 0) site.Progress += work;
            if (understanding > 0) site.Understanding += understanding;
        }

        /// <summary>The resolve branches are UNLOCKED once accrued Understanding reaches the site's threshold — the §4
        /// "knowledge unlocks branches" rule. Pure read.</summary>
        public static bool BranchUnlocked(FieldSiteDB site)
            => site != null && site.Understanding >= site.UnderstandingToResolve;

        /// <summary>
        /// Commit the resolution: a WORKED site with its branch unlocked transitions to the terminal state its Shape
        /// dictates — OneShot → DEPLETED (spent), Persistent → PERSISTENT (a standing stream). Returns true if it
        /// resolved. No-op (false) if the site isn't being worked or the branch isn't unlocked yet. (The RUPTURED edge —
        /// a persistent site turning into a crisis — is SE-5; the yield DELIVERY on resolve is SE-1c.)
        /// </summary>
        public static bool Resolve(FieldSiteDB site)
        {
            if (site == null || site.Status != SiteStatus.Worked) return false;
            if (!BranchUnlocked(site)) return false;

            site.Status = site.Shape == SiteShape.Persistent ? SiteStatus.Persistent : SiteStatus.Depleted;
            return true;
        }

        // ---- SE-4a: the INCIDENT reads (a Shape.Incident site bleeds you until contained) ----

        /// <summary>True while a <see cref="SiteShape.Incident"/> site is LIVE — it exists and is not yet contained, so
        /// it bleeds pressure and can grow its menace (§4: the pressure IS the clock). A non-Incident site, or one that
        /// has resolved (contained → Depleted), is not live. Pure read.</summary>
        public static bool IsIncidentLive(FieldSiteDB site)
            => site != null
               && site.Shape == SiteShape.Incident
               && (site.Status == SiteStatus.Discovered || site.Status == SiteStatus.Worked);

        /// <summary>The steady pressure (per day) a LIVE incident bleeds into its region — its
        /// <see cref="FieldSiteDB.PressurePerDay"/> while live, else 0. The read SE-4c's pressure application uses. Pure.</summary>
        public static double CurrentPressure(FieldSiteDB site)
            => IsIncidentLive(site) ? Math.Max(0.0, site.PressurePerDay) : 0.0;
    }
}
