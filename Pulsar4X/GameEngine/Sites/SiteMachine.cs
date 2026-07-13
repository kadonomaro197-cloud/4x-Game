using System;
using System.Collections.Generic;

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

        // ---- SE-5b: the composable MULTI-BRANCH resolve (docs/SITE-ENGINE-DESIGN.md §3/§4) ----
        // A branchless site resolves the single-path way above (Resolve, Shape-driven). A site with authored
        // FieldSiteDB.Branches instead offers a SET of choices, each UNLOCKED independently by its own understanding
        // cost — the player commits one via ResolveBranch. All of these are NEW reads/transitions; the single-path
        // path above is untouched, so a branchless site is byte-identical. (SE-5c's order calls ResolveBranch.)

        /// <summary>
        /// SE-5b — the indices (into <see cref="FieldSiteDB.Branches"/>) of the branches UNLOCKED right now: a branch is
        /// unlocked once accrued Understanding reaches its own <see cref="SiteBranch.UnderstandingRequired"/> (§4:
        /// knowledge unlocks a branch, never a timer; branches COMPOSE — each unlocks independently, none is consumed by
        /// choosing another). Empty for a branchless site (which resolves the single-path way). Pure read.
        /// </summary>
        public static List<int> UnlockedBranchIndices(FieldSiteDB site)
        {
            var result = new List<int>();
            if (site == null || !site.HasBranches) return result;
            for (int i = 0; i < site.Branches.Count; i++)
            {
                var b = site.Branches[i];
                if (b != null && site.Understanding >= b.UnderstandingRequired)
                    result.Add(i);
            }
            return result;
        }

        /// <summary>SE-5b — is the specific branch at <paramref name="branchIndex"/> unlocked (valid index + Understanding
        /// ≥ that branch's cost)? Pure read.</summary>
        public static bool IsBranchUnlocked(FieldSiteDB site, int branchIndex)
        {
            if (site == null || !site.HasBranches) return false;
            if (branchIndex < 0 || branchIndex >= site.Branches.Count) return false;
            var b = site.Branches[branchIndex];
            return b != null && site.Understanding >= b.UnderstandingRequired;
        }

        /// <summary>SE-5b — true once AT LEAST ONE branch is unlocked (the branched-site analogue of
        /// <see cref="BranchUnlocked"/> — "there is a choice available to commit"). Pure read.</summary>
        public static bool AnyBranchUnlocked(FieldSiteDB site)
            => site != null && site.HasBranches && UnlockedBranchIndices(site).Count > 0;

        /// <summary>
        /// SE-5b — commit a CHOSEN branch: a WORKED, branched site whose branch <paramref name="branchIndex"/> is unlocked
        /// transitions to THAT branch's <see cref="SiteBranch.ResultStatus"/> (Depleted = spent / Persistent = a standing
        /// stream) and records the commitment in <see cref="FieldSiteDB.CommittedBranchIndex"/>. Returns true if it
        /// resolved. No-op (false) if the site isn't being worked, isn't branched, the index is invalid, or that branch
        /// isn't unlocked yet. The multi-path twin of <see cref="Resolve"/> — SE-5c's commit-branch order calls it and
        /// then delivers the chosen branch's yield; a branch's Ruptures flag is read in SE-5d.
        /// </summary>
        public static bool ResolveBranch(FieldSiteDB site, int branchIndex)
        {
            if (site == null || site.Status != SiteStatus.Worked) return false;
            if (!site.HasBranches) return false;
            if (!IsBranchUnlocked(site, branchIndex)) return false;

            site.CommittedBranchIndex = branchIndex;
            site.Status = site.Branches[branchIndex].ResultStatus;
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
