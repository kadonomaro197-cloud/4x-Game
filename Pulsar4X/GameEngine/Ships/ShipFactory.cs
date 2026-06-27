using System;
using System.Collections.Generic;
using Pulsar4X.Orbital;
using Pulsar4X.Datablobs;
using Pulsar4X.Fleets;
using Pulsar4X.Damage;
using Pulsar4X.Names;
using Pulsar4X.Orbits;
using Pulsar4X.People;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Movement;
using Pulsar4X.Combat;
using Pulsar4X.Factions;
using Pulsar4X.Storage;
using Pulsar4X.Energy;

namespace Pulsar4X.Ships
{
    public static class ShipFactory
    {
        /// <summary>
        /// new ship in a circular orbit at a distance of twice the parent bodies radius (size)
        /// </summary>
        /// <param name="shipDesign"></param>
        /// <param name="ownerFaction"></param>
        /// <param name="parent"></param>
        /// <param name="shipName"></param>
        /// <returns></returns>
        public static Entity CreateShip(ShipDesign shipDesign, Entity ownerFaction, Entity parent, string? shipName = null)
        {

            double distanceFromParent = parent.GetDataBlob<MassVolumeDB>().RadiusInM * 2;
            var pos = new Vector3(distanceFromParent, 0, 0);
            var orbit = OrbitDB.FromPosition(parent, pos, shipDesign.MassPerUnit, parent.StarSysDateTime);
            return CreateShip(shipDesign, ownerFaction, orbit, parent, shipName);
        }

        /// <summary>
        /// new ship in a circular orbit at twice the parent bodies radius (size), and a given true anomaly
        /// </summary>
        /// <param name="shipDesign"></param>
        /// <param name="ownerFaction"></param>
        /// <param name="parent"></param>
        /// <param name="angleRad">true anomaly</param>
        /// <param name="shipName"></param>
        /// <returns></returns>
        public static Entity CreateShip(ShipDesign shipDesign, Entity ownerFaction, Entity parent, double angleRad, string? shipName = null)
        {


            var distanceFromParent = parent.GetDataBlob<MassVolumeDB>().RadiusInM * 2;

            var x = distanceFromParent * Math.Cos(angleRad);
            var y = distanceFromParent * Math.Sin(angleRad);

            var pos = new Vector3( x,  y, 0);
            var orbit = OrbitDB.FromPosition(parent, pos, shipDesign.MassPerUnit, parent.StarSysDateTime);
            return CreateShip(shipDesign, ownerFaction, orbit, parent, shipName);
        }

        /// <summary>
        /// new ship in a circular orbit at a given position from the parent.
        /// </summary>
        /// <param name="shipDesign"></param>
        /// <param name="ownerFaction"></param>
        /// <param name="position"></param>
        /// <param name="parent"></param>
        /// <param name="shipName"></param>
        /// <returns></returns>
        public static Entity CreateShip(ShipDesign shipDesign, Entity ownerFaction, Vector3 position, Entity parent, string? shipName = null)
        {
            var orbit = OrbitDB.FromPosition(parent, position, shipDesign.MassPerUnit, parent.StarSysDateTime);
            return CreateShip(shipDesign, ownerFaction, orbit, parent, shipName);
        }

        /// <summary>
        /// new ship with an orbit and position defined by kepler elements.
        /// </summary>
        /// <param name="shipDesign"></param>
        /// <param name="ownerFaction"></param>
        /// <param name="ke"></param>
        /// <param name="parent"></param>
        /// <param name="shipName"></param>
        /// <returns></returns>
        public static Entity CreateShip(ShipDesign shipDesign, Entity ownerFaction,  KeplerElements ke, Entity parent, string? shipName = null)
        {
            OrbitDB orbit = OrbitDB.FromKeplerElements(parent,shipDesign.MassPerUnit, ke, parent.StarSysDateTime);
            var position =  OrbitMath.GetPosition(ke, parent.StarSysDateTime);
            return CreateShip(shipDesign, ownerFaction, orbit, parent, shipName);
        }

        public static Entity CreateShip(ShipDesign shipDesign, Entity ownerFaction, OrbitDB orbit,  Entity parent, string? shipName = null)
        {
            if (shipDesign.DesignVersion == 0) //we're using version 0 to indicate the design hasn't been built yet.
                shipDesign.DesignVersion = 1;

            var starsys = parent.Manager;
            var position = OrbitMath.GetPosition(orbit, parent.StarSysDateTime);
            List<BaseDataBlob> dataBlobs = new List<BaseDataBlob>();

            var shipinfo = new ShipInfoDB(shipDesign);
            dataBlobs.Add(shipinfo);
            var mvdb = MassVolumeDB.NewFromMassAndVolume(shipDesign.MassPerUnit, shipDesign.VolumePerUnit);
            dataBlobs.Add(mvdb);
            PositionDB posdb = new PositionDB(position, parent);
            dataBlobs.Add(posdb);
            EntityDamageProfileDB damagedb = (EntityDamageProfileDB)shipDesign.DamageProfileDB.Clone();
            dataBlobs.Add(damagedb);
            ComponentInstancesDB compInstances = new ComponentInstancesDB();
            dataBlobs.Add(compInstances);
            OrderableDB ordable = new OrderableDB();
            dataBlobs.Add(ordable);
            var ship = Entity.Create();
            ship.FactionOwnerID = ownerFaction.Id;
            starsys.AddEntity(ship, dataBlobs);


            //some DB's need tobe created after the entity.
            var namedb = new NameDB(ship.Id.ToString());
            if (string.IsNullOrEmpty(shipName))
            {
                shipName = NameFactory.GetShipName(ownerFaction.Manager.Game);
            }

            namedb.SetName(ownerFaction.Id, shipName);

            ship.SetDataBlob(namedb);
            ship.SetDataBlob(orbit);

            foreach (var item in shipDesign.Components)
            {
                ship.AddComponent(item.design, item.count);
            }

            if (ship.HasDataBlob<NewtonThrustAbilityDB>())
            {
                NewtonionMovementProcessor.UpdateNewtonThrustAbilityDB(ship);
            }

            // Rate the freshly-built ship for the auto-resolve combat engine: firepower + toughness read
            // from its real installed weapons and armour. (docs/COMBAT-DESIGN.md, combat spine step 2.)
            ship.SetDataBlob(ShipCombatValueDB.Calculate(ship));

            return ship;
        }

        /// <summary>
        /// Fill a ship's fuel tanks from a faction's material library so it can maneuver. CreateShip builds
        /// ships with EMPTY tanks on purpose — production-built ships are meant to be fuelled at a colony, so
        /// CreateShip must not hand out free fuel (that would break the fuel economy). Call this explicitly for
        /// ships that should spawn ready to fly (the DevTools spawns, the combat sandbox). Reads the ship's own
        /// thruster fuel type and pulls that material from the faction's unlocked goods, falling back to the
        /// LOCKED library (some engines burn a fuel a fresh start hasn't researched — e.g. an NTR burns 'ntp').
        /// Returns the units of fuel actually stored — 0 (never throws) if the ship has no thruster, no matching
        /// fuel-tank bay, or the fuel isn't a defined material. AddCargoByUnit caps at tank free volume, so the
        /// huge FuelFillUnits just tops the tank off.
        /// </summary>
        public static double FillFuelTanks(Entity ship, FactionInfoDB factionInfo)
        {
            if (factionInfo == null) return 0;
            if (!ship.TryGetDataBlob<NewtonThrustAbilityDB>(out var thrust) || string.IsNullOrEmpty(thrust.FuelType))
                return 0;
            // No cargo/fuel-tank bay = nowhere to put the fuel. A fighter (the Wasp) carries a thruster but no
            // storage bay, so it has no CargoStorageDB at all — guard here so the "never throws" contract holds
            // (AddCargoItems hard-indexes CargoStorageDB and would throw KeyNotFoundException otherwise).
            if (!ship.HasDataBlob<CargoStorageDB>())
                return 0;
            var fuel = factionInfo.Data.CargoGoods.GetAny(thrust.FuelType)
                     ?? factionInfo.Data.LockedCargoGoods.GetAny(thrust.FuelType);
            if (fuel == null)
                return 0;
            return CargoTransferProcessor.AddCargoItems(ship, fuel, FuelFillUnits);
        }

        private const int FuelFillUnits = 10_000_000;

        /// <summary>
        /// Top a ship's energy store (its battery bank) up to max capacity. The reactor/battery sibling of
        /// <see cref="FillFuelTanks"/>: <see cref="CreateShip"/> leaves a new ship's stored energy at ZERO
        /// (EnergyStoreAtb initialises EnergyStored = 0; a production-built ship earns its charge over time), so
        /// call this for ships that should spawn READY TO FLY — the DevTools spawns and the combat sandbox.
        ///
        /// The one that bites: WARP is paid out of STORED electricity, not fuel — `WarpMoveCommand` blocks until
        /// `EnergyStored >= BubbleCreationCost`. A 0-charge ship handed a move order therefore just sits there
        /// (the "I spawned a ship, ordered it to move, and nothing happened" symptom). Charging it removes that
        /// trap; weapons (which also draw from EnergyStored) likewise work immediately.
        ///
        /// Charges to the ship's OWN <c>EnergyStoreMax</c> (the principled version of DefaultStartFactory's
        /// hand-set 2,750,000) — correct for any design. For the base-mod ships a topped-off battery always holds
        /// 2×–4× one warp bubble at starting tech, so a charged ship can always warp. Returns total KJ added; 0
        /// (never throws) if the ship has no reactor/battery.
        /// </summary>
        public static double ChargeReactors(Entity ship)
        {
            if (!ship.TryGetDataBlob<EnergyGenAbilityDB>(out var energyDB))
                return 0;
            double added = 0;
            foreach (var kvp in energyDB.EnergyStoreMax)
            {
                double current = energyDB.EnergyStored.TryGetValue(kvp.Key, out var c) ? c : 0;
                if (kvp.Value > current)
                {
                    added += kvp.Value - current;
                    energyDB.EnergyStored[kvp.Key] = kvp.Value;
                }
            }
            return added;
        }

        public static void DestroyShip(Entity shipToDestroy)
        {
            // Steps:
            // - Remove the ship from fleet (if any)
            // - Remove the ship as the fleet flagship (if set)
            // - Kill any officers on board
            // - Create wreckage
            // - Remove the ship entity from the game

            var game = shipToDestroy.Manager.Game;
            var faction = game.Factions[shipToDestroy.FactionOwnerID];

            // Remove the ship from its fleet
            if(faction.TryGetDataBlob<FleetDB>(out var fleetDB))
            {
                // Recursively try to get the fleet the ship belongs to
                var belongsToFleet = fleetDB.TryGetChild<FleetDB>(shipToDestroy);

                // If we found it send out the order to unassign the ship
                if(belongsToFleet != null && belongsToFleet.OwningEntity != null)
                {
                    // The unassign ship command removes the ship from the fleet
                    // and checks if it is the flagship and removes that also
                    var command = FleetOrder.UnassignShip(
                        shipToDestroy.FactionOwnerID,
                        belongsToFleet.OwningEntity,
                        shipToDestroy);

                    game.OrderHandler.HandleOrder(command);
                }
            }

            // Kill any officers on board
            // (currently just the commander)
            // TODO: check for additional people on board (passengers, officers, scientists etc)
            if(shipToDestroy.TryGetDataBlob<ShipInfoDB>(out var shipInfoDB)
                && shipToDestroy.Manager.TryGetEntityById(shipInfoDB.CommanderID, out var commanderEntity))
            {
                CommanderFactory.DestroyCommander(commanderEntity);
            }


            // Remove the ship entity from the game
            shipToDestroy.Destroy();
        }
    }
}