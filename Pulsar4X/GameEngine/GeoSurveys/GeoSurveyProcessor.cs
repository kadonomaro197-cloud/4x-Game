using System;
using Pulsar4X.Engine;
using Pulsar4X.Events;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Industry;
using Pulsar4X.Interfaces;

namespace Pulsar4X.GeoSurveys;

public class GeoSurveyProcessor : IInstanceProcessor
{
    public Entity Fleet { get; internal set; }
    public Entity Target { get; internal set; }

    public GeoSurveyProcessor() {}

    public GeoSurveyProcessor(Entity fleet, Entity target)
    {
        Fleet = fleet;
        Target = target;
    }

    internal override void ProcessEntity(Entity entity, DateTime atDateTime)
    {
        // TODO: need to only get the survey points from ships that are at the survey location
        uint totalSurveyPoints = GetSurveyPoints(Fleet);

        if(Target.TryGetDataBlob<GeoSurveyableDB>(out var geoSurveyableDB))
        {
            if(!geoSurveyableDB.GeoSurveyStatus.ContainsKey(Fleet.FactionOwnerID))
                geoSurveyableDB.GeoSurveyStatus[Fleet.FactionOwnerID] = geoSurveyableDB.PointsRequired;

            if(totalSurveyPoints >= geoSurveyableDB.GeoSurveyStatus[Fleet.FactionOwnerID])
            {
                // Survey is complete
                geoSurveyableDB.GeoSurveyStatus[Fleet.FactionOwnerID] = 0;

                // Grant partial access to mineral data
                if (Target.TryGetDataBlob<MineralsDB>(out var mineralsDB))
                {
                    var factionMask = Fleet.GetFactionOwner.GetDataBlob<FactionInfoDB>().FactionMask;
                    mineralsDB.GrantFactionPartialAccess(factionMask);
                }

                // Reveal the planet's surface regions — a completed geo survey now KNOWS the geography
                // (the exploration→ground-map link, slice 4). A procedurally-generated world's regions start
                // as fog (surveyed:false at gen); surveying it flips them to known so the planet-view map and
                // any ground decisions can read real terrain. Defensive: bodies without a region layer (asteroids,
                // comets) simply skip. See docs/GROUND-COMBAT-MAP-DESIGN.md slice 4.
                if (Target.TryGetDataBlob<Pulsar4X.Galaxy.PlanetRegionsDB>(out var regionsDB))
                {
                    regionsDB.RevealAll();
                    // ...and generate its FINE hex grid (the developer's call: hexes exist for every SURVEYED world,
                    // so the planet-view hex map is never empty on a world you've actually scanned). Lazy still holds —
                    // an unsurveyed world carries none; only the coarse regions (4/body) are galaxy-wide. Idempotent +
                    // defensive (a re-survey is a no-op; a body without a region layer is skipped inside).
                    Pulsar4X.Galaxy.PlanetHexFactory.EnsureHexesForBody(Target);
                    // ...and build its continuous cylinder grid, which ALSO seeds the located mineral DEPOSITS onto
                    // hexes (Industry.HexMinerals). This makes "there are resources HERE" authoritative engine state
                    // the instant a world is scanned — for EVERY planet and moon, not just the home world and not
                    // only when the player opens the planet view. Post-survey the map flags them and a mine gets built
                    // on the deposit. Lazy/idempotent/defensive: a world with no minerals seeds nothing, a re-survey
                    // is a no-op, a body without a region layer is skipped inside.
                    Pulsar4X.Galaxy.PlanetGridFactory.EnsureGridForBody(Target);
                }

                EventManager.Instance.Publish(
                    Event.Create(
                        EventType.GeoSurveyCompleted,
                        atDateTime,
                        $"Geo Survey of {Target.GetName(Fleet.FactionOwnerID)} complete",
                        Fleet.FactionOwnerID,
                        Target.Manager.ManagerID,
                        Target.Id));
            }
            else
            {
                geoSurveyableDB.GeoSurveyStatus[Fleet.FactionOwnerID] -= totalSurveyPoints;
            }
        }
    }

    private uint GetSurveyPoints(Entity entity)
    {
        uint totalSurveyPoints = 0;

        if(entity.TryGetDataBlob<GeoSurveyAbilityDB>(out var geoSurveyAbilityDB))
        {
            totalSurveyPoints += geoSurveyAbilityDB.Speed;
        }

        if(entity.TryGetDataBlob<FleetDB>(out var fleetDB))
        {
            foreach(var child in fleetDB.Children)
            {
                totalSurveyPoints += GetSurveyPoints(child);
            }
        }

        return totalSurveyPoints;
    }
}