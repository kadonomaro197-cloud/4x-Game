using System;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;
using Pulsar4X.Ships;

namespace Pulsar4X.Sites
{
    /// <summary>
    /// Site Engine SE-1b — the DRIVER that turns "a ship parked at the anomaly" into banked progress
    /// (docs/SITE-ENGINE-DESIGN.md §4). A daily hotloop keyed to <see cref="FieldSiteDB"/>: for each site, if an
    /// eligible worker (SE-1b v1 = any faction ship) is present within <see cref="PresenceRadius_m"/>, it feeds one
    /// work step into the pure <see cref="SiteMachine"/> (which begins the study on first work and banks
    /// Progress + Understanding). No worker present → the site simply doesn't advance (no timer, agency-preserving).
    ///
    /// Byte-identical in the live game: no live entity carries a <see cref="FieldSiteDB"/> yet (nothing calls
    /// <see cref="FieldSiteFactory"/> in the New-Game path), so <see cref="ProcessManager"/> finds no sites and the
    /// processor sleeps. The worker's Role/Grade sourcing the work RATE is SE-2 (the Command Berth); for now the
    /// rate is a flat constant so the spine can be proven end to end.
    /// </summary>
    public class SiteWorkProcessor : IHotloopProcessor
    {
        /// <summary>How close a worker must be (metres) to count as "on-site" and work the anomaly. 1,000 km —
        /// the same close-parked scale a jump-point/geo survey uses.</summary>
        public static double PresenceRadius_m = 1_000_000.0;

        /// <summary>Yield magnitude banked per day a worker is on-site (SE-2 replaces the flat rate with the
        /// Command Berth's Role/Grade output).</summary>
        public static double WorkPerDay = 10.0;

        /// <summary>Understanding banked per day a worker is on-site — the gate that unlocks the resolve branch.</summary>
        public static double UnderstandingPerDay = 5.0;

        public TimeSpan RunFrequency => TimeSpan.FromDays(1);
        public TimeSpan FirstRunOffset => TimeSpan.FromSeconds(1);
        public Type GetParameterType => typeof(FieldSiteDB);

        public void Init(Game game) { }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var sites = manager.GetAllEntitiesWithDataBlob<FieldSiteDB>();
            foreach (var site in sites)
                ProcessEntity(site, deltaSeconds);
            return sites.Count;
        }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            if (!entity.TryGetDataBlob<FieldSiteDB>(out var site)) return;
            if (!entity.TryGetDataBlob<PositionDB>(out var sitePos)) return;

            // A resolved site (Depleted/Persistent/Ruptured) takes no more work — SiteMachine also guards this,
            // but skip the neighbour scan when there's nothing to accrue.
            if (site.Status != SiteStatus.Discovered && site.Status != SiteStatus.Worked) return;

            if (!TryFindWorker(entity.Manager, sitePos.AbsolutePosition, out var worker)) return;

            // Scale the flat daily rate by however long this step actually covered (the scheduler catches up
            // sub-daily processors within a coarse step), so accrual is independent of Ticklength.
            double days = deltaSeconds / 86400.0;
            site.WorkedByFactionId = worker.FactionOwnerID;
            SiteMachine.Accrue(site, WorkPerDay * days, UnderstandingPerDay * days);
        }

        /// <summary>
        /// Find the nearest eligible worker to a site position. SE-1b v1: any faction (non-neutral) ship within
        /// <see cref="PresenceRadius_m"/>. SE-2 will require a Command Berth of the site's Role. Public + static so
        /// the presence rule is testable in isolation.
        /// </summary>
        public static bool TryFindWorker(EntityManager manager, Vector3 sitePosition, out Entity worker)
        {
            worker = Entity.InvalidEntity;
            double bestDistance = PresenceRadius_m;
            bool found = false;

            foreach (var candidate in manager.GetAllEntitiesWithDataBlob<ShipInfoDB>())
            {
                if (candidate.FactionOwnerID == Game.NeutralFactionId) continue;
                if (!candidate.TryGetDataBlob<PositionDB>(out var candPos)) continue;

                double distance = Vector3.Distance(sitePosition, candPos.AbsolutePosition);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    worker = candidate;
                    found = true;
                }
            }

            return found;
        }
    }
}
