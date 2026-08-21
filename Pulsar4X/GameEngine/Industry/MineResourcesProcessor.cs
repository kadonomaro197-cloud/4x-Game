using System;
using System.Collections.Generic;
using Pulsar4X.Orbital;
using Pulsar4X.Datablobs;
using Pulsar4X.Interfaces;
using Pulsar4X.Extensions;
using Pulsar4X.Engine;
using Pulsar4X.Colonies;
using Pulsar4X.Events;
using Pulsar4X.Factions;
using Pulsar4X.Storage;
using Pulsar4X.Galaxy;

namespace Pulsar4X.Industry
{
    internal class MineResourcesProcessor : IHotloopProcessor, IRecalcProcessor
    {
        /// <summary>R1b PER-HEX MINING (resource locality, developer ruling 2026-08-10). Default OFF → the aggregate
        /// body-wide-pool mining is byte-identical. When ON: a mine PLACED ON A HEX works that hex's OWN located deposit
        /// into that hex's local stockpile (so the ore sits there until HAULED — H1); a colony-level mine (not placed on a
        /// hex) still mines the body-wide pool into colony cargo (the gradual-retrofit default). Client flips it ON only
        /// once the haul verb exists, so hex-mined ore is never stranded.</summary>
        public static bool EnablePerHexMining = false;

        private Dictionary<int, Mineral> _minerals;
        public TimeSpan RunFrequency => TimeSpan.FromDays(1);

        public TimeSpan FirstRunOffset => TimeSpan.FromHours(1);

        public Type GetParameterType => typeof(MiningDB);


        public void Init(Game game)
        {
            _minerals = new ();

            EventManager.Instance.Subscribe(EventType.ColonyAdministratorAssigned, OnAdminAssigned);

            foreach(var (uniqueID, mineral) in game.StartingGameData.Minerals)
            {
                _minerals.Add(mineral.ID, mineral);
            }
        }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            // Host-agnostic: a colony mines off its planet, a station off its hosting body. Both reach the
            // same MineResources path once the resource body is resolved (see MiningHelper.TryGetMiningBody).
            if(MiningHelper.TryGetMiningBody(entity, out var bodyEntity)
                && bodyEntity.TryGetDataBlob<MineralsDB>(out var mineralsDB)
                && entity.TryGetDataBlob<MiningDB>(out var miningDB)
                && entity.TryGetDataBlob<CargoStorageDB>(out var stockpile))
                MineResources(entity, mineralsDB, miningDB, stockpile);
        }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var entities = manager.GetAllEntitiesWithDataBlob<MiningDB>();
            foreach(var entity in entities)
            {
                ProcessEntity(entity, deltaSeconds);
            }
            return entities.Count;
        }

        private void MineResources(Entity miningEntity, MineralsDB mineralsDB, MiningDB miningDB, CargoStorageDB stockpile)
        {
            // Mines are buildings too: scale their output by the host's infrastructure capacity (colony or station).
            double infraEfficiency = InfrastructureProcessor.GetEfficiency(miningEntity);

            // R1b — PER-HEX MINING (flag-gated). When ON and the mined body carries a surface grid, route each mine
            // COMPONENT to its hex's own deposit (if placed on a hex) or the body-wide pool (if colony-level). The ON path
            // REPLACES the aggregate pass entirely, so a hex-placed mine can't double-count against the aggregate rate.
            if (EnablePerHexMining
                && MiningHelper.TryGetMiningBody(miningEntity, out var perHexBody)
                && perHexBody.TryGetDataBlob<PlanetRegionsDB>(out var perHexRegions)
                && perHexRegions.SurfaceGrid != null
                && miningEntity.TryGetDataBlob<ComponentInstancesDB>(out var perHexComps))
            {
                MineResourcesPerComponent(miningEntity, mineralsDB, stockpile, perHexRegions.SurfaceGrid, perHexComps, infraEfficiency);
                return;
            }

            Dictionary<int, long> actualMiningRates = miningDB.ActualMiningRate;
            Dictionary<int, MineralDeposit> planetMinerals = mineralsDB.Minerals;

            foreach (var kvp in actualMiningRates)
            {
                // The rate table can carry a mineral the planet has NO deposit of: CalculateActualMiningRates keeps
                // the key (with rate 0) when accessibility is 0 (it guards planetMinerals with ContainsKey, we didn't).
                // This happens whenever a mine's mineable set is broader than the body's actual deposits — e.g. a
                // DevTest game booting on a random seed whose Earth lacks one of the default mine's minerals. Skip it
                // (there's nothing here to mine) instead of hard-indexing planetMinerals[key] and throwing — that
                // throw faults the whole sim tick on the parallel thread, so the clock stops advancing (unobserved).
                if (!planetMinerals.ContainsKey(kvp.Key) || !_minerals.ContainsKey(kvp.Key))
                    continue;

                ICargoable mineral = _minerals[kvp.Key];
                string cargoTypeID = mineral.CargoTypeID;

                var unitsMinableThisTick = (long)Math.Min(actualMiningRates[kvp.Key] * infraEfficiency, planetMinerals[kvp.Key].Amount.Actual);

                if(!stockpile.TypeStores.ContainsKey(cargoTypeID))
                {
                    // var type = StaticRefLib.StaticData.CargoTypes[cargoTypeID];
                    // string erstr = "We didn't mine a potential " + unitsMinableThisTick + " of " + mineral.Name + " because we have no way to store " + type.Name + " cargo.";
                    // StaticRefLib.EventLog.AddPlayerEntityErrorEvent(colonyEntity, EventType.Storage, erstr);
                    continue; //can't store this mineral
                }

                var unitsMinedThisTick = stockpile.AddCargoByUnit(mineral, unitsMinableThisTick);

                if (unitsMinableThisTick > unitsMinedThisTick)
                {
                    // long dif = unitsMinableThisTick - unitsMinedThisTick;
                    // var type = StaticRefLib.StaticData.CargoTypes[cargoTypeID];
                    // string erstr = "We didn't mine a potential " + dif + " of " + mineral.Name + " because we don't have enough space to store it.";
                    // StaticRefLib.EventLog.AddPlayerEntityErrorEvent(colonyEntity,EventType.Storage, erstr);
                }

                MineralDeposit mineralDeposit = planetMinerals[kvp.Key];
                long newAmount = mineralDeposit.Amount.Actual - unitsMinedThisTick;

                var amount = mineralDeposit.Amount;
                amount.Actual = newAmount;
                mineralDeposit.Amount = amount;

                var accessability = Math.Pow((float)newAmount / mineralDeposit.HalfOriginalAmount, 3) * mineralDeposit.Accessibility;
                double newAccess = GeneralMath.Clamp(accessability, 0.1, mineralDeposit.Accessibility);
                mineralDeposit.Accessibility = newAccess;
            }
        }

        /// <summary>R1b — the flag-ON mining pass. Walks each mine COMPONENT: one placed on a hex works THAT hex's located
        /// deposit into the hex's local stockpile (locality bites); a colony-level mine (on no hex) mines the body-wide
        /// pool into colony cargo (the gradual-retrofit default). Replaces — never runs alongside — the aggregate pass,
        /// so nothing is mined twice.</summary>
        internal void MineResourcesPerComponent(Entity miningEntity, MineralsDB mineralsDB, CargoStorageDB stockpile,
            SurfaceGrid grid, ComponentInstancesDB comps, double infraEfficiency)
        {
            if (grid?.Hexes == null) return;
            if (!comps.TryGetComponentsByAttribute<MineResourcesAtbDB>(out var instances)) return;
            if (!miningEntity.GetFactionOwner.TryGetDataBlob<FactionInfoDB>(out var factionInfoDB)) return;
            var cargoLibrary = factionInfoDB.Data.CargoGoods;
            var planetMinerals = mineralsDB.Minerals;

            // Reverse index: which hex holds each footprint component instance (PlaceInstallationOnHexOrder stores the
            // ComponentInstance.ID in GroundHex.InstallationIds). Built once per pass.
            var hexByInstance = new Dictionary<int, GroundHex>();
            foreach (var hex in grid.Hexes)
                if (hex.InstallationIds != null)
                    foreach (var id in hex.InstallationIds)
                        hexByInstance[id] = hex;

            foreach (var instance in instances)
            {
                var atb = instance.Design.GetAttribute<MineResourcesAtbDB>();
                if (atb?.ResourcesPerEconTick == null) continue;
                float healthPercent = instance.HealthPercent;

                if (hexByInstance.TryGetValue(instance.ID, out var placedHex)
                    && placedHex.DepositMineralId >= 0 && placedHex.DepositAmount > 0)
                {
                    // Hex-placed mine: extract this hex's ONE located deposit into its LOCAL stockpile (not the pool / cargo).
                    int mineralInt = placedHex.DepositMineralId;
                    long baseRate = 0;
                    foreach (var kvp in atb.ResourcesPerEconTick)          // this mine's base rate for the located mineral
                    {
                        var m = cargoLibrary.GetAny(kvp.Key);
                        if (m != null && m.ID == mineralInt) { baseRate = kvp.Value; break; }
                    }
                    if (baseRate <= 0) continue;                          // this mine can't extract what's located here
                    long rate = (long)(baseRate * healthPercent * infraEfficiency);
                    MineHexDeposit(placedHex, mineralInt, rate);          // deplete the hex → fill its bucket (conserved)
                }
                else
                {
                    // Colony-level mine (not on a hex): mine the body-wide pool into colony cargo, per component. Uses the
                    // same Mineral objects + primitives the aggregate pass uses, so a colony's ordinary mining is preserved
                    // when the flag is on and nothing is placed on a hex yet.
                    foreach (var kvp in atb.ResourcesPerEconTick)
                    {
                        var lib = cargoLibrary.GetAny(kvp.Key);
                        if (lib == null || _minerals == null || !_minerals.ContainsKey(lib.ID) || !planetMinerals.ContainsKey(lib.ID)) continue;
                        ICargoable mineral = _minerals[lib.ID];
                        var deposit = planetMinerals[lib.ID];
                        long minable = (long)Math.Min(kvp.Value * healthPercent * infraEfficiency * deposit.Accessibility, deposit.Amount.Actual);
                        if (minable <= 0 || !stockpile.TypeStores.ContainsKey(mineral.CargoTypeID)) continue;
                        long mined = stockpile.AddCargoByUnit(mineral, minable);
                        if (mined <= 0) continue;
                        var amt = deposit.Amount; amt.Actual -= mined; deposit.Amount = amt;
                    }
                }
            }
        }

        /// <summary>Pure, testable per-hex depletion step: mine up to <paramref name="ratePerTick"/> of the hex's located
        /// deposit (<paramref name="mineralInt"/>) into its local stockpile, capped at what's on hand. Returns the amount
        /// mined (conserved — the deposit falls by exactly what the stockpile gains). No-op on a mismatched mineral, an
        /// exhausted deposit, or a non-positive rate.</summary>
        internal static long MineHexDeposit(GroundHex hex, int mineralInt, long ratePerTick)
        {
            if (hex == null || hex.DepositMineralId != mineralInt || hex.DepositAmount <= 0 || ratePerTick <= 0) return 0;
            long amount = Math.Min(ratePerTick, hex.DepositAmount);
            hex.DepositAmount -= amount;
            hex.AddToStockpile(mineralInt, amount);
            return amount;
        }

        /// <summary>
        /// Called by the ReCalcProcessor.
        /// </summary>
        /// <param name="colonyEntity"></param>
        internal static void CalcMaxRate(Entity colonyEntity)
        {
            if (!colonyEntity.TryGetDataBlob<ComponentInstancesDB>(out var instancesDB) ||
                !colonyEntity.GetFactionOwner.TryGetDataBlob<FactionInfoDB>(out var factionInfoDB) ||
                !colonyEntity.TryGetDataBlob<MiningDB>(out var miningDB))
                return;

            var rates = new Dictionary<int, long>();
            var cargoLibrary = factionInfoDB.Data.CargoGoods;

            if (instancesDB.TryGetComponentsByAttribute<MineResourcesAtbDB>(out var instances))
            {
                foreach (var instance in instances)
                {
                    float healthPercent = instance.HealthPercent;
                    var designInfo = instance.Design.GetAttribute<MineResourcesAtbDB>();

                    foreach (var item in designInfo.ResourcesPerEconTick)
                    {
                        // Need to convert the uniqueID (item.Key) to an int ID
                        var cargoable = cargoLibrary[item.Key];
                        rates.SafeValueAdd(cargoable.ID, Convert.ToInt64(item.Value * healthPercent));
                    }
                }
            }

            miningDB.BaseMiningRate = rates;

            // Calculate the actual mining rates if the host's resource body (colony planet or station
            // hosting body) has minerals.
            if (MiningHelper.TryGetMiningBody(colonyEntity, out var bodyEntity) && bodyEntity.HasDataBlob<MineralsDB>())
            {
                miningDB.ActualMiningRate = MiningHelper.CalculateActualMiningRates(colonyEntity);
            }
        }

        public byte ProcessPriority { get; set; } = 100;


        public void RecalcEntity(Entity entity)
        {
            CalcMaxRate(entity);
        }

        private void OnAdminAssigned(Event e)
        {


        }


    }
}