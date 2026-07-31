using System;
using System.Linq;
using Pulsar4X.Colonies;
using Pulsar4X.Engine;
using Pulsar4X.Extensions;
using Pulsar4X.Factions;
using Pulsar4X.Fleets;
using Pulsar4X.Interfaces;
using Pulsar4X.Orbital;
using Pulsar4X.Storage;

namespace Pulsar4X.Ships
{
    public class LaunchComplexProcessor : IHotloopProcessor
    {
        public TimeSpan RunFrequency => TimeSpan.FromDays(1);
        public TimeSpan FirstRunOffset => TimeSpan.FromHours(4);
        public Type GetParameterType => typeof(LaunchComplexDB);

        public void Init(Game game) { }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            if (!entity.TryGetDataBlob<LaunchComplexDB>(out var launchDB))
                return;

            AssignQueueToPads(launchDB, entity);
            ProcessPadLaunches(launchDB, entity);
        }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var entities = manager.GetAllEntitiesWithDataBlob<LaunchComplexDB>();
            foreach (var entity in entities)
            {
                ProcessEntity(entity, deltaSeconds);
            }
            return entities.Count;
        }

        private static void AssignQueueToPads(LaunchComplexDB launchDB, Entity colonyEntity)
        {
            foreach (var kvp in launchDB.Pads)
            {
                var pad = kvp.Value;
                if (pad.ShipDesignId != null)
                    continue;

                for (int i = 0; i < launchDB.LaunchQueue.Count; i++)
                {
                    var entry = launchDB.LaunchQueue[i];

                    var factionInfo = colonyEntity.GetFactionOwner.GetDataBlob<FactionInfoDB>();
                    if (!factionInfo.IndustryDesigns.TryGetValue(entry.DesignId, out var designInfo))
                        continue;

                    var shipDesign = (ShipDesign)designInfo;
                    if (shipDesign.MassPerUnit > pad.MaxTonnage)
                        continue;

                    pad.ShipDesignId = entry.DesignId;
                    pad.ShipName = entry.ShipName;
                    pad.ShipMass = shipDesign.MassPerUnit;
                    pad.TargetOrbitRadius = 0;
                    pad.ReadyToLaunch = true;
                    launchDB.LaunchQueue.RemoveAt(i);
                    break;
                }
            }
        }

        private static void ProcessPadLaunches(LaunchComplexDB launchDB, Entity colonyEntity)
        {
            foreach (var kvp in launchDB.Pads.ToArray())
            {
                var pad = kvp.Value;
                if (pad.ShipDesignId == null || !pad.ReadyToLaunch)
                    continue;

                TryLaunchShip(colonyEntity, kvp.Key);
            }
        }

        public static bool TryLaunchShip(Entity colonyEntity, string padId)
        {
            if (!colonyEntity.TryGetDataBlob<LaunchComplexDB>(out var launchDB))
                return false;

            if (!launchDB.Pads.TryGetValue(padId, out var pad))
                return false;

            if (pad.ShipDesignId == null)
                return false;

            if (!colonyEntity.TryGetDataBlob<ColonyInfoDB>(out var colonyInfo))
                return false;

            var planet = colonyInfo.PlanetEntity;
            var faction = colonyEntity.GetFactionOwner;
            var factionInfo = faction.GetDataBlob<FactionInfoDB>();

            if (!factionInfo.IndustryDesigns.TryGetValue(pad.ShipDesignId, out var designInfo))
                return false;

            var shipDesign = (ShipDesign)designInfo;

            var targetRadius = pad.TargetOrbitRadius > 0
                ? pad.TargetOrbitRadius
                : OrbitMath.LowOrbitRadius(planet);

            double fuelCost = OrbitMath.FuelCostToOrbit(planet, shipDesign.MassPerUnit, targetRadius);

            if (!TryDeductFuel(colonyEntity, fuelCost))
                return false;

            var position = new Vector3(targetRadius, 0, 0);
            var ship = ShipFactory.CreateShip(shipDesign, faction, position, planet, pad.ShipName);

            // M3-2b: stamp the crew provenance on a launch-complex-built ship. Its crew was already committed
            // from this colony's pool at build-complete (ShipDesign.OnConstructionComplete); the launching
            // colony IS the source colony, so record it here so destroy/disband releases the right pool.
            if (shipDesign.CrewReq > 0 && ship.TryGetDataBlob<ShipInfoDB>(out var launchedInfo))
                launchedInfo.CrewSourceColonyId = colonyEntity.Id;

            // P4.2 (Operation Earthfall) — a launch-complex-built hull is provisioned by the SAME built-ship
            // charge/fuel policy as the direct-build path (ShipDesign.OnConstructionComplete): NPC-owned boots
            // ready to fly so the AI sealift can warp the instant the hull leaves the pad; player-owned ALSO boots
            // charged as of developer decision 2026-07-21 (ChargeBuiltPlayerShips true — one flag flips it back).
            // The colony already paid the lift-to-orbit fuel above (TryDeductFuel); this tops the ship's own tanks +
            // reactor so it can maneuver/fight, not just reach orbit. findings/A4-sealift.md cause 3.
            ShipDesign.ProvisionBuiltShip(ship, faction);

            if (faction.TryGetDataBlob<FleetDB>(out var fleetDB))
            {
                fleetDB.AddChild(ship);
            }

            pad.ShipDesignId = null;
            pad.ShipName = null;
            pad.ShipMass = 0;
            pad.TargetOrbitRadius = 0;
            pad.ReadyToLaunch = false;

            return true;
        }

        private static bool TryDeductFuel(Entity colonyEntity, double fuelMassNeeded)
        {
            if (!colonyEntity.TryGetDataBlob<CargoStorageDB>(out var storage))
                return false;

            if (!storage.TypeStores.ContainsKey("fuel-storage"))
                return false;

            var fuelStore = storage.TypeStores["fuel-storage"];
            var cargoLib = colonyEntity.GetFactionCargoDefinitions();
            if (cargoLib == null)
                return false;

            double remaining = fuelMassNeeded;

            foreach (var kvp in fuelStore.Cargoables.ToArray())
            {
                if (remaining <= 0)
                    break;

                var fuelItem = kvp.Value;
                double storedMass = storage.GetMassStored(fuelItem, false);
                if (storedMass <= 0)
                    continue;

                double toDeduct = Math.Min(remaining, storedMass);
                CargoTransferProcessor.AddRemoveCargoMass(colonyEntity, fuelItem, -toDeduct);
                remaining -= toDeduct;
            }

            return remaining <= 0;
        }
    }
}
