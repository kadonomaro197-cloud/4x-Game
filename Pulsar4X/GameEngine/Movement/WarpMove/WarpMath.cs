using System;
using System.Collections.Generic;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Extensions;
using Pulsar4X.Galaxy;
using Pulsar4X.Orbital;
using Pulsar4X.Orbits;

namespace Pulsar4X.Movement;

public static class WarpMath
{
    /// <summary>
    /// recalculates a shipsMaxSpeed.
    /// </summary>
    /// <param name="ship"></param>
    public static void CalcMaxWarpAndEnergyUsage(Entity ship)
    {
        Dictionary<string, double> totalFuelUsage = new Dictionary<string, double>();
        var instancesDB = ship.GetDataBlob<ComponentInstancesDB>();
        int totalEnginePower = instancesDB.GetTotalEnginePower(out totalFuelUsage);

        //Note: TN aurora uses the TCS for max speed calcs.
        WarpAbilityDB warpDB = ship.GetDataBlob<WarpAbilityDB>();
        warpDB.TotalWarpPower = totalEnginePower;
        //propulsionDB.FuelUsePerKM = totalFuelUsage;

        var mass = ship.GetDataBlob<MassVolumeDB>().MassTotal;
        var maxSpeed = MaxSpeedCalc(totalEnginePower, mass);
        warpDB.MaxSpeed = maxSpeed;

    }

    /// <summary>
    /// Calculates max ship speed based on engine power and ship mass
    /// </summary>
    /// <param name="power">TotalEnginePower</param>
    /// <param name="tonage">HullSize</param>
    /// <returns>Max speed in km/s</returns>
    public static int MaxSpeedCalc(double power, double tonage)
    {
        // From Aurora4x wiki:  Speed = (Total Engine Power / Total Class Size in HS) * 1000 km/s
        return (int)((power / tonage) * 1000);
    }

    struct Orbit
    {
        public Vector3 position;
        public double T;
    }

    public static (Vector3 position, DateTime etiDateTime) GetInterceptPosition(Entity mover, Entity target, DateTime atDateTime, Vector3 offsetPosition = new Vector3(), double speedOverride_m = 0)
    {
        var moverPos = (Vector3)MoveMath.GetAbsoluteFuturePosition(mover, atDateTime);
        var tgtPos = (Vector3)MoveMath.GetAbsoluteFuturePosition(target, atDateTime);
        var exitPos = tgtPos + offsetPosition;
        // speedOverride_m lets a FLEET move move every ship at the slowest unit's warp speed (so they arrive
        // together instead of scattering). 0 = use the ship's own MaxSpeed (every existing caller is unchanged).
        double spd_m = speedOverride_m > 0 ? speedOverride_m : mover.GetDataBlob<WarpAbilityDB>().MaxSpeed;

        var tgtMoveType = target.GetDataBlob<PositionDB>().MoveType;
        switch (tgtMoveType)
        {
            case PositionDB.MoveTypes.None:
            {
                var distance = (exitPos - moverPos).Length();
                var intercept = ((Vector3)exitPos, atDateTime + TimeSpan.FromSeconds(distance / spd_m));
                return intercept;
                break;
            }
            case PositionDB.MoveTypes.Orbit:
            {
                var intercept = WarpMath.GetInterceptPosition_m(moverPos, spd_m, target.GetDataBlob<OrbitDB>(), atDateTime, offsetPosition);
                return intercept;
                break;
            }
            //For the following cases, we need to know if the target is an object which is owned by the same empire and we know what it's doing,
            //or if that info is unknown and how do we try predict?
            case PositionDB.MoveTypes.NewtonSimple:
                throw new NotImplementedException("not implemented");
            case PositionDB.MoveTypes.NewtonComplex:
                throw new NotImplementedException("not implemented");
            case PositionDB.MoveTypes.Warp:
                throw new NotImplementedException("not implemented");
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Calculates a cartisian position for an intercept for a ship and an target's orbit using warp.
    /// </summary>
    /// <returns>The intercept position and DateTime</returns>
    /// <param name="mover">The entity that is trying to intercept a target.</param>
    /// <param name="targetOrbit">Target orbit.</param>
    /// <param name="atDateTime">Datetime of transit start</param>
    public static (Vector3 position, DateTime etiDateTime) GetInterceptPosition(Entity mover, OrbitDB targetOrbit, DateTime atDateTime, Vector3 offsetPosition = new Vector3())
    {
        var moverPos = (Vector3)MoveMath.GetAbsoluteFuturePosition(mover, atDateTime);
        double spd_m = mover.GetDataBlob<WarpAbilityDB>().MaxSpeed;
        return WarpMath.GetInterceptPosition_m(moverPos, spd_m, targetOrbit, atDateTime, offsetPosition);
    }
    /// <summary>
    /// Calculates a cartisian position for an intercept for a ship and an target's orbit using warp.
    /// </summary>
    /// <param name="moverAbsolutePos"></param>
    /// <param name="speed"></param>
    /// <param name="targetOrbit"></param>
    /// <param name="atDateTime"></param>
    /// <param name="offsetPosition">position relative to the target object we wish to stop warp.</param>
    /// <returns></returns>
    public static (Vector3 position, DateTime etiDateTime) GetInterceptPosition_m(Vector3 moverAbsolutePos, double speed, OrbitDB targetOrbit, DateTime atDateTime, Vector3 offsetPosition = new Vector3())
    {
        // GUARD (regression, found via a committed game_logs/ [FATAL], 2026-07-04): a non-positive or non-finite
        // warp speed makes the intercept time blow up — the loops below compute tt = distance/speed, so speed ≤ 0
        // gives tt = ∞, and `atDateTime + TimeSpan.FromSeconds(∞)` throws OverflowException. That throw happens on
        // the BACKGROUND sim thread (an unobservable [FATAL] that can kill the clock), reached when a FLEET is
        // ordered to a body and a member has a WarpAbilityDB with MaxSpeed 0 (a hull that can't actually warp). A
        // ship that can't warp has nothing to intercept → return the mover's own position/time so the caller
        // NO-OPS instead of crashing. Same bail for a degenerate (non-finite/non-positive) orbital period.
        if (!(speed > 0) || double.IsInfinity(speed))
            return (moverAbsolutePos, atDateTime);
        double targetPeriod = targetOrbit.OrbitalPeriod.TotalSeconds;
        if (!(targetPeriod > 0) || double.IsInfinity(targetPeriod))
            return (moverAbsolutePos, atDateTime);

        var pos = moverAbsolutePos;
        double tim = 0;

        var pl = new Orbit()
        {
            position = moverAbsolutePos,
            T = targetOrbit.OrbitalPeriod.TotalSeconds,
        };

        double a = targetOrbit.SemiMajorAxis * 2;

        Vector3 p;
        int i;
        double tt, t, dt, a0, a1, T;
        // find orbital position with min error (coarse)
        a1 = -1.0;
        dt = 0.01 * pl.T;


        for (t=0; t< pl.T; t+=dt)
        {
            p = OrbitMath.GetAbsolutePosition(targetOrbit, atDateTime + TimeSpan.FromSeconds(t));  //pl.position(sim_t + t);                     // try time t
            p += offsetPosition;
            tt = (p - pos).Length() / speed;  //length(p - pos) / speed;
            a0 = tt - t; if (a0 < 0.0) continue;              // ignore overshoots
            a0 /= pl.T;                                   // remove full periods from the difference
            a0 -= Math.Floor(a0);
            a0 *= pl.T;
            if ((a0 < a1) || (a1 < 0.0))
            {
                a1 = a0;
                tim = tt;
            }   // remember best option
        }
        // find orbital position with min error (fine)
        for (i = 0; i < 10; i++)                               // recursive increase of accuracy
            for (a1 = -1.0, t = tim - dt, T = tim + dt, dt *= 0.1; t < T; t += dt)
            {
                p = OrbitMath.GetAbsolutePosition(targetOrbit, atDateTime + TimeSpan.FromSeconds(t));  //p = pl.position(sim_t + t);                     // try time t
                p += offsetPosition;
                tt = (p - pos).Length() / speed;  //tt = length(p - pos) / speed;
                a0 = tt - t; if (a0 < 0.0) continue;              // ignore overshoots
                a0 /= pl.T;                                   // remove full periods from the difference
                a0 -= Math.Floor(a0);
                a0 *= pl.T;
                if ((a0 < a1) || (a1 < 0.0))
                {
                    a1 = a0;
                tim = tt;
                }   // remember best option
            }
        // direction
        p = OrbitMath.GetAbsolutePosition(targetOrbit, atDateTime + TimeSpan.FromSeconds(tim));//pl.position(sim_t + tim);
        p += offsetPosition;
        //dir = normalize(p - pos);
        return (p, atDateTime + TimeSpan.FromSeconds(tim));
    }

}