using System;
using System.Linq;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Extensions;
using Pulsar4X.Colonies;

namespace Pulsar4X.Stations
{
    /// <summary>
    /// Grows (or starves) the population of a MANNED space station — the station-side parallel to
    /// <see cref="Pulsar4X.Colonies.PopulationProcessor"/>, which is hard-keyed to ColonyInfoDB.
    ///
    /// Why this is its OWN processor and not a shared path: a station is a SEALED HABITAT in space, NOT a
    /// patch of planet surface. Its population is capped by the life support its habitat MODULES provide
    /// (<see cref="Pulsar4X.Galaxy.PopulationSupportAtbDB"/>), NOT by the habitability (ColonyCost) of the body
    /// it orbits. So a station with no life-support modules is a tomb — population dies off — while one with
    /// ample habitat grows toward that cap. (A native world like Earth grows un-capped; a station never does —
    /// that contrast IS the station's fragility.) The morale machinery (ColonyMoraleDB) is shared + host-agnostic.
    ///
    /// Keyed on <see cref="StationInfoDB"/> (its own DataBlob type) so it never collides with
    /// PopulationProcessor's ColonyInfoDB hotloop (the engine registers one hotloop per blob type) and so it
    /// cannot regress colony population.
    /// </summary>
    public class StationPopulationProcessor : IHotloopProcessor
    {
        public TimeSpan RunFrequency { get; } = TimeSpan.FromDays(30);
        public TimeSpan FirstRunOffset { get; } = TimeSpan.FromDays(30);
        public Type GetParameterType { get; } = typeof(StationInfoDB);

        public void Init(Game game) { }

        public void ProcessEntity(Entity entity, int deltaSeconds) => GrowStationPopulation(entity);

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var stations = manager.GetAllDataBlobsOfType<StationInfoDB>();
            foreach (var station in stations)
            {
                if (station.OwningEntity != null)
                    GrowStationPopulation(station.OwningEntity);
            }
            return stations.Count;
        }

        internal void GrowStationPopulation(Entity stationEntity)
        {
            var stationInfo = stationEntity.GetDataBlob<StationInfoDB>();
            var population = stationInfo.Population;
            if (population.Count == 0)
                return; // an automated (unmanned) platform — no people to grow

            if (!stationEntity.TryGetDataBlob<ComponentInstancesDB>(out var instancesDB))
                return;

            // A station is a SEALED HABITAT: its population CAP is whatever life support its habitat modules
            // provide. The hosting body is passed only for the module tolerance gate (a void-designed habitat
            // module passes at the body's micro-gravity). No habitat modules => cap 0 => the station starves.
            long popCap = instancesDB.GetPopulationSupportValue(stationInfo.HostingBodyEntity);

            long totalPop = 0;
            foreach (var kvp in population)
                totalPop += kvp.Value;

            // Shared, host-agnostic morale. A station has no planet-habitability cost (worstColonyCost = 0 — the
            // habitat is climate-controlled); crowding is pressure against the habitat cap. Jobs/comfort come
            // from the same module extensions a colony uses; there is no station tax lever yet (0).
            double migration = 0.0;
            if (stationEntity.TryGetDataBlob<ColonyMoraleDB>(out var moraleDB))
            {
                double crowdingRatio = popCap > 0 ? (double)totalPop / popCap : 2.0;
                long jobs = instancesDB.GetTotalJobs();
                long workforce = ColonyManpowerDB.Workforce(totalPop);
                double employmentRatio = (jobs > 0 && workforce > 0) ? (double)jobs / workforce : -1.0;
                double comfort = instancesDB.GetHousingComfort();
                moraleDB.Morale = ColonyMoraleDB.ComputeMorale(0.0, crowdingRatio, employmentRatio, comfort, 0.0, moraleDB.Factors);
                migration = ColonyMoraleDB.MigrationRate(moraleDB.Morale);
            }

            foreach (var (id, value) in population.ToArray())
            {
                long newPop;
                if (totalPop > popCap) // over the life-support cap (this also catches the no-support cap of 0)
                {
                    // Harsh proportional die-off toward the cap — mirrors the colony's over-cap decay.
                    newPop = (long)(value * 0.5);
                }
                else
                {
                    // The same growth curve the colony uses, plus morale-driven migration, ceilinged at life support.
                    double growthRate = 20.0 / Math.Pow(Math.Max(value, 1L), 1.0 / 3.0);
                    if (growthRate > 10.0)
                        growthRate = 10.0;
                    newPop = (long)(value * (1.0 + growthRate + migration));
                    if (newPop > popCap)
                        newPop = popCap;
                }
                if (newPop < 0)
                    newPop = 0;
                population[id] = newPop;
            }
        }
    }
}
