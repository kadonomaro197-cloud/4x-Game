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
            blobs.Add(new ColonySustenanceDB());    // power/food shortage gauges (M5b) — inert until demand is calibrated locally
            blobs.Add(new StationEconomyDB());       // operating-cost side (Slice C) — StationUpkeepProcessor bills the faction monthly

            // A MANNED station draws crew from its OWN residents (crew works off-world) — attach the manpower pool
            // so its builds are crew-gated exactly like a colony's (ManpowerTools reads StationInfoDB.Population).
            // An UNMANNED automated platform gets NO pool → the crew gate stays inert (byte-identical; a crewless
            // platform has no population to man a warship anyway). Condition mirrors the manned StationInfoDB above.
            if (speciesEntity != null && initialPopulation > 0)
                blobs.Add(new ColonyManpowerDB());   // people-as-a-resource pool (crew/talent) — the crew ENFORCEMENT source

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

        /// <summary>
        /// The GRAVE RUNG (Slice B, 2026-07-03) — the inverse of <see cref="CreateStation"/>. Called when a station's
        /// structural integrity is exhausted (<see cref="Pulsar4X.Damage.DamageProcessor"/> → <c>OnStationDamage</c>).
        /// A station being "cheap to KILL" is the whole point of it being its own host, but a station the player can
        /// destroy but that leaves dangling references or keeps producing from the grave is a bug, not a decision —
        /// so this tears the station cleanly out of the game, mirroring <see cref="ShipFactory.DestroyShip"/>:
        ///  • tears down SPAWNED SUB-ENTITIES (a research lab's ResearcherDB lives on a SEPARATE entity referenced by
        ///    <see cref="Pulsar4X.Components.ComponentInstance.SpawnedEntityId"/>; RemoveComponentInstance does NOT
        ///    clean these up, so without this a destroyed research station would ORPHAN its researcher and keep
        ///    accruing research from a dead station — the cradle-to-grave leak the grave rung closes),
        ///  • loses the population with the station,
        ///  • unregisters from the faction (the inverse of the CreateStation registration — otherwise the entity
        ///    lingers in <see cref="FactionInfoDB.Stations"/> with IsValid == false, a dangling ref that survives
        ///    save/load), then
        ///  • removes the entity from the game.
        /// </summary>
        public static void DestroyStation(Entity stationToDestroy)
        {
            var manager = stationToDestroy.Manager;
            var game = manager?.Game;

            // Tear down spawned sub-entities (e.g. the research lab's ResearcherDB) so a dead station stops working.
            if (stationToDestroy.TryGetDataBlob<ComponentInstancesDB>(out var componentsDB))
            {
                foreach (var instance in componentsDB.AllComponents.Values)
                {
                    if (instance.SpawnedEntityId >= 0
                        && manager != null
                        && manager.TryGetEntityById(instance.SpawnedEntityId, out var spawned))
                    {
                        manager.TagEntityForRemoval(spawned);
                    }
                }
            }

            // The people are lost with the station.
            if (stationToDestroy.TryGetDataBlob<StationInfoDB>(out var stationInfo))
                stationInfo.Population.Clear();

            // Unregister from the faction (inverse of CreateStation). Capture the faction BEFORE RemoveEntity,
            // which zeroes FactionOwnerID.
            if (game != null && game.Factions.TryGetValue(stationToDestroy.FactionOwnerID, out var factionEntity))
            {
                if (factionEntity.TryGetDataBlob<FactionInfoDB>(out var factionInfo))
                    factionInfo.Stations.Remove(stationToDestroy);
                if (factionEntity.TryGetDataBlob<FactionOwnerDB>(out var ownerDB))
                    ownerDB.RemoveEntity(stationToDestroy);
            }

            stationToDestroy.Destroy();
        }
    }
}
