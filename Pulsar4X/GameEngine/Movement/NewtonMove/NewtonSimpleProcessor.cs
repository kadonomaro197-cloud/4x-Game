using System;
using Pulsar4X.Orbital;
using Pulsar4X.Interfaces;
using Pulsar4X.Datablobs;
using Pulsar4X.Factions;
using Pulsar4X.Orbits;
using Pulsar4X.Storage;
using Pulsar4X.Galaxy;
using Pulsar4X.Engine;

namespace Pulsar4X.Movement;

public class NewtonSimpleProcessor : IHotloopProcessor
{
    public TimeSpan RunFrequency => TimeSpan.FromSeconds(30);
    public TimeSpan FirstRunOffset => TimeSpan.FromSeconds(0);
    public Type GetParameterType => typeof(NewtonSimpleMoveDB);

    public void Init(Game game)
    {
    }

    public void ProcessEntity(Entity entity, int deltaSeconds)
    {
        var nmdb = entity.GetDataBlob<NewtonSimpleMoveDB>();
        DateTime todateTime = entity.StarSysDateTime + TimeSpan.FromSeconds(deltaSeconds);
        NewtonMove(nmdb, todateTime);
        MoveStateProcessor.ProcessForType(nmdb, todateTime);
    }

    public static void ProcessEntity(Entity entity, DateTime toDateTime)
    {
        var db = entity.GetDataBlob<NewtonSimpleMoveDB>();
        NewtonMove(db, toDateTime);
        MoveStateProcessor.ProcessForType(db, toDateTime);
    }

    public int ProcessManager(EntityManager manager, int deltaSeconds)
    {
        var nmdb = manager.GetAllDataBlobsOfType<NewtonSimpleMoveDB>();
        DateTime toDate = manager.ManagerSubpulses.StarSysDateTime + TimeSpan.FromSeconds(deltaSeconds);
        foreach (var db in nmdb)
        {
            NewtonMove(db, toDate);
        }
        MoveStateProcessor.ProcessForType(nmdb, toDate);
        return nmdb.Count;
    }


    public static void NewtonMove(NewtonSimpleMoveDB newtonSimplelMoveDB, DateTime toDateTime)
    {
        Entity entity = newtonSimplelMoveDB.OwningEntity;
        var thrustdb = entity.GetDataBlob<NewtonThrustAbilityDB>();
        var posdb = entity.GetDataBlob<PositionDB>();
        var massdb = entity.GetDataBlob<MassVolumeDB>();


        //update deltav. A ship with no cargo/fuel-tank bay (e.g. the Wasp fighter — a thruster but no storage) has
        //no fuel: guard the hard CargoStorageDB index so completing a warp doesn't THROW on a background sim thread.
        //That throw (KeyNotFoundException: CargoStorageDB) was killing warp-completion mid-flight — the ship vanished /
        //teleported and the corrupted scheduling cascaded into Temporal Anomaly exceptions that froze the clock.
        //cargoLib/fuelType don't need the storage bay (they're faction-library lookups), so they stay hoisted for the
        //fuel-burn below; only the stored-mass read is guarded. A tankless ship has 0 DeltaV, so the burn never runs.
        CargoDefinitionsLibrary cargoLib = entity.GetFactionOwner.GetDataBlob<FactionInfoDB>().Data.CargoGoods;
        var fuelType = cargoLib.GetAny(thrustdb.FuelType);
        double fuelMass = 0;
        if (entity.TryGetDataBlob<CargoStorageDB>(out var storage))
        {
            fuelMass = storage.GetMassStored(fuelType, false);
        }

        var currentOrbit = newtonSimplelMoveDB.CurrentTrajectory;
        var targetOrbit = newtonSimplelMoveDB.TargetTrajectory;

        var thrust = thrustdb.ThrustInNewtons;
        var fuelRate = thrustdb.FuelBurnRate;

        var currentState = OrbitalMath.GetStateVectors(currentOrbit, toDateTime);
        var targetState = OrbitalMath.GetStateVectors(targetOrbit, toDateTime);

        var moveVector = targetState.velocity - currentState.velocity;
        var moveDeltaV = moveVector.Length();

        //if ship has enough fuel to make the manuver:
        if (thrustdb.DeltaV > moveDeltaV)
        {
            //TODO: handle longer "burns" over several turns.

            //set entity to new orbit.

            OrbitDB newOrbit = OrbitDB.FromKeplerElements(newtonSimplelMoveDB.SOIParent, massdb.MassTotal, targetOrbit, toDateTime);
            entity.SetDataBlob(newOrbit);

            //remove fuel (only if the ship actually has a tank — a tankless thruster still completes the orbit move)
            if (storage != null)
            {
                double fuelBurned = OrbitMath.TsiolkovskyFuelUse(massdb.MassTotal, thrustdb.ExhaustVelocity, moveDeltaV);
                CargoTransferProcessor.AddRemoveCargoMass(entity, fuelType, -fuelBurned);
            }

            //tag as complete
            newtonSimplelMoveDB.IsComplete = true;
        }
    }

    public static (Vector3 pos, Vector3 vel) GetRelativeState(Entity entity, DateTime atDateTime)
    {
        NewtonSimpleMoveDB db = entity.GetDataBlob<NewtonSimpleMoveDB>();
        var state = OrbitMath.GetStateVectors(db.CurrentTrajectory, atDateTime);
        return (state.position, (Vector3)state.velocity);
    }
    public static (Vector3 pos, Vector3 vel) GetAbsoluteState(Entity entity, DateTime atDateTime)
    {
        NewtonSimpleMoveDB db = entity.GetDataBlob<NewtonSimpleMoveDB>();
        var posdb = entity.GetDataBlob<PositionDB>();

        var state = OrbitMath.GetStateVectors(db.CurrentTrajectory, atDateTime);
        var pos = state.position;
        var vel = (Vector3)state.velocity;

        if (posdb.Parent != null)
        {
            pos += MoveMath.GetAbsoluteFuturePosition(posdb.Parent,atDateTime);
            vel += MoveMath.GetAbsoluteFutureVelocity(posdb.Parent, atDateTime);
        }
        return (pos, vel);
    }
}