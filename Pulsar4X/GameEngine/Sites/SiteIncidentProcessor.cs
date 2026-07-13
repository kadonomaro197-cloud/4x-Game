using System;
using Pulsar4X.Engine;
using Pulsar4X.Events;
using Pulsar4X.GroundCombat;
using Pulsar4X.Interfaces;

namespace Pulsar4X.Sites
{
    /// <summary>
    /// Site Engine SE-4d — the incident's SPAWN/SPREAD engine (docs/SITE-ENGINE-DESIGN.md §4). While a Shape.Incident
    /// site is live, its menace GROWS if you don't contain it: on each interval it raises a fresh menace unit at the
    /// site's region and pushes one into an adjacent region (the outbreak creeps outward), announcing it. An
    /// <see cref="IInstanceProcessor"/> that reschedules itself (the <see cref="Pulsar4X.People.NavalAcademyProcessor"/>
    /// pattern) — and STOPS (doesn't reschedule) once the incident is contained (the grave rung).
    ///
    /// Inert until a live Incident site exists and is scheduled (via <see cref="Schedule"/>, called by the SE-4e
    /// discovery slice), so the engine is byte-identical.
    /// </summary>
    public class SiteIncidentProcessor : IInstanceProcessor
    {
        internal override void ProcessEntity(Entity entity, DateTime atDateTime)
        {
            if (entity?.Manager == null) return;
            if (!entity.TryGetDataBlob<FieldSiteDB>(out var site)) return;

            // Contained/resolved, or never spreads, or no menace → stop (do NOT reschedule; the incident is over).
            if (!SiteMachine.IsIncidentLive(site)) return;
            if (site.SpawnIntervalDays <= 0 || site.MenaceFactionId < 0) return;

            var manager = entity.Manager;
            if (!manager.TryGetEntityById(site.SurfaceBodyEntityId, out var body)) return;

            // SPAWN — the menace reinforces at its region.
            MenaceFactory.RaiseMenaceUnit(body, site.MenaceFactionId, site.SurfaceRegionIndex);

            // SPREAD — push one menace unit into an adjacent region so an ignored incident creeps outward (best-effort).
            TrySpreadToNeighbor(body, site);

            EventManager.Instance.Publish(Event.Create(
                EventType.NewHostileContact, atDateTime,
                "The incident is spreading — hostile forces are growing",
                site.MenaceFactionId, manager.ManagerID, entity.Id));

            // Still live → schedule the next spread.
            var next = atDateTime + TimeSpan.FromDays(site.SpawnIntervalDays);
            manager.ManagerSubpulses.AddEntityInterupt(next, nameof(SiteIncidentProcessor), entity);
        }

        /// <summary>Schedule the FIRST spread of a live incident site, one <see cref="FieldSiteDB.SpawnIntervalDays"/>
        /// out (the SE-4e discovery slice calls this when it authors an incident). No-op if the site never spreads.</summary>
        public static void Schedule(Entity siteEntity)
        {
            if (siteEntity?.Manager == null) return;
            if (!siteEntity.TryGetDataBlob<FieldSiteDB>(out var site) || site.SpawnIntervalDays <= 0) return;

            var when = siteEntity.StarSysDateTime + TimeSpan.FromDays(site.SpawnIntervalDays);
            siteEntity.Manager.ManagerSubpulses.AddEntityInterupt(when, nameof(SiteIncidentProcessor), siteEntity);
        }

        /// <summary>Order one of the menace's units in the site's region to move to the first adjacent region — the
        /// creep. Defensive: no region layer / no valid neighbour / no unit → no spread.</summary>
        private static void TrySpreadToNeighbor(Entity body, FieldSiteDB site)
        {
            if (!body.TryGetDataBlob<Pulsar4X.Galaxy.PlanetRegionsDB>(out var regionsDB)) return;
            int region = site.SurfaceRegionIndex;
            if (region < 0 || region >= regionsDB.Regions.Count) return;

            var neighbors = regionsDB.Regions[region].Neighbors;
            if (neighbors == null || neighbors.Count == 0) return;
            int target = neighbors[0];

            if (!body.TryGetDataBlob<GroundForcesDB>(out var forces)) return;
            foreach (var unit in forces.Units)
            {
                if (unit.Health <= 0 || unit.FactionOwnerID != site.MenaceFactionId || unit.RegionIndex != region) continue;
                GroundForces.OrderMove(body, unit, target);
                break;
            }
        }
    }
}
