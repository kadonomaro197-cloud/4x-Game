using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar4X.Engine;
using Pulsar4X.DataStructures;
using Pulsar4X.Colonies;
using Pulsar4X.Stations;

namespace Pulsar4X.Industry
{
    public static class MiningHelper
    {
        /// <summary>
        /// Resolves the body whose <see cref="MineralsDB"/> a mining host extracts from — a colony's planet
        /// (<see cref="ColonyInfoDB.PlanetEntity"/>) or a station's hosting body
        /// (<see cref="StationInfoDB.HostingBodyEntity"/>). The economy is host-agnostic: a station that
        /// carries mining components mines exactly like a colony, just off a different body. Returns false
        /// for a host with neither, or whose body is unset/invalid (no Manager).
        /// </summary>
        public static bool TryGetMiningBody(Entity miningEntity, out Entity bodyEntity)
        {
            if (miningEntity.TryGetDataBlob<ColonyInfoDB>(out var colonyInfoDB))
                bodyEntity = colonyInfoDB.PlanetEntity;
            else if (miningEntity.TryGetDataBlob<StationInfoDB>(out var stationInfoDB))
                bodyEntity = stationInfoDB.HostingBodyEntity;
            else
            {
                bodyEntity = Entity.InvalidEntity;
                return false;
            }

            // Guard against an unset/invalid body (StationInfoDB defaults to InvalidEntity, whose Manager is null):
            // calling TryGetDataBlob on it would NPE.
            return bodyEntity != null && bodyEntity.Manager != null;
        }

        public static Dictionary<int, long> CalculateActualMiningRates(Entity miningEntity)
        {
            if (!miningEntity.TryGetDataBlob<MiningDB>(out var miningDB))
                throw new Exception("Entity does not have MiningDB");
            if (!TryGetMiningBody(miningEntity, out var bodyEntity))
                throw new Exception("Mining entity has no resource body (colony planet or station hosting body)");
            if (!bodyEntity.TryGetDataBlob<MineralsDB>(out var mineralsDB))
                throw new Exception("Resource body does not have MineralsDB");

            // A colony carries mining bonuses on ColonyBonusesDB; a station has none (defaults to 1.0).
            float miningBonuses = 1.0f;
            if (miningEntity.TryGetDataBlob<ColonyBonusesDB>(out var colonyBonusesDB))
            {
                miningBonuses = colonyBonusesDB.GetBonus(AbilityType.Mine);
            }

            var mineRates = miningDB.BaseMiningRate.ToDictionary(k => k.Key, v => v.Value);
            var planetMinerals = mineralsDB.Minerals;

            foreach (var (key, value) in mineRates)
            {
                long baseRateFromMiningInstallations = mineRates[key];
                double accessibility = planetMinerals.ContainsKey(key) ? planetMinerals[key].Accessibility : 0;
                double actualRate = baseRateFromMiningInstallations * miningBonuses * accessibility;
                mineRates[key] = Convert.ToInt64(actualRate);
            }

            return mineRates;
        }
    }
}
