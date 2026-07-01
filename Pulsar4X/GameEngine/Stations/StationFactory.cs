using System.Collections.Generic;
using Pulsar4X.Orbital;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Colonies;
using Pulsar4X.Factions;
using Pulsar4X.Industry;
using Pulsar4X.Names;
using Pulsar4X.People;
using Pulsar4X.Storage;
using Pulsar4X.Galaxy;
using Pulsar4X.Movement;

namespace Pulsar4X.Stations
{
    /// <summary>
    /// Creates a space station entity and attaches the shared infrastructure DataBlobs — the deliberate
    /// PARALLEL to <see cref="ColonyFactory"/>. A station carries the SAME equipment chassis a colony does
    /// (so the mining / industry / research processors, which discover work by component ability and not by
    /// host type, process a station for free), but it is registered as its own host on the faction so it can
    /// later own its own cost curve, durability, and invasion math. See docs/SPACE-STATIONS-DESIGN.md.
    /// </summary>
    public static class StationFactory
    {
        public const string DEFAULT_SUFFIX = "Station";

        /// <summary>
        /// Creates a new space station orbiting <paramref name="hostingBody"/> (a planet, asteroid/belt body,
        /// or anomaly). Population defaults to zero (an automated platform); pass a species + count to man it.
        /// </summary>
        public static Entity CreateStation(Entity factionEntity, Entity hostingBody, long initialPopulation = 0, Entity speciesEntity = null)
        {
            var blobs = new List<BaseDataBlob>();

            string bodyName = hostingBody.GetDataBlob<NameDB>().GetName(factionEntity.Id);
            NameDB name = new NameDB($"{bodyName} {DEFAULT_SUFFIX}"); // TODO: Review default name.
            name.SetName(factionEntity.Id, name.DefaultName);

            // Station sits in orbit at the hosting body's surface radius, same positioning pattern a colony uses
            // relative to its planet. (A free-orbiting station can be given an OrbitDB later; this matches the
            // proven colony placement and keeps the foundation slice minimal.)
            var pos = new Vector3(hostingBody.GetDataBlob<MassVolumeDB>().RadiusInM, 0, 0);

            StationInfoDB stationInfo = (speciesEntity != null && initialPopulation > 0)
                ? new StationInfoDB(speciesEntity, initialPopulation, hostingBody)
                : new StationInfoDB(hostingBody);

            blobs.Add(name);
            blobs.Add(stationInfo);
            // NOTE: ColonyBonusesDB is deliberately NOT attached here — its GetDependencies() hard-requires
            // ColonyInfoDB (which a station does not have), so attaching it would fail AddEntity's dependency
            // validation. How a station carries production/research/mining bonuses is a decision for the
            // economy-wiring slice (relax that dependency, or a station-compatible bonuses blob).
            blobs.Add(new MiningDB());
            blobs.Add(new OrderableDB());
            blobs.Add(new MassVolumeDB());
            blobs.Add(new CargoStorageDB());
            blobs.Add(new PositionDB(pos, hostingBody));
            blobs.Add(new TeamsHousedDB());
            blobs.Add(new ComponentInstancesDB()); // installed station modules live here
            blobs.Add(new InfrastructureDB());      // capacity summed from installed modules
            blobs.Add(new ColonyMoraleDB());        // shared, host-agnostic morale valve — StationPopulationProcessor reads it for a manned station
            blobs.Add(new LegitimacyDB());          // a station holds its OWN legitimacy (LegitimacyProcessor recomputes it from morale) — the fragile frontier node
            blobs.Add(new RebellionDB());           // and can break away on its own (rebellion state driven off legitimacy collapse)

            Entity stationEntity = Entity.Create();
            stationEntity.FactionOwnerID = factionEntity.Id;
            hostingBody.Manager.AddEntity(stationEntity, blobs);

            var factionInfo = factionEntity.GetDataBlob<FactionInfoDB>();
            factionInfo.Stations.Add(stationEntity);
            factionEntity.GetDataBlob<FactionOwnerDB>().SetOwned(stationEntity);

            // Grant faction access to mineral data on the hosting body so a mining station can read it.
            if (hostingBody.TryGetDataBlob<MineralsDB>(out var mineralsDB))
            {
                mineralsDB.GrantFactionAccess(factionInfo.FactionMask);
            }

            return stationEntity;
        }
    }
}
