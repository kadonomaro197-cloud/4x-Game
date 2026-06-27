using System;
using System.Collections.Generic;
using Pulsar4X.Components;
using Pulsar4X.Datablobs;
using Pulsar4X.Engine;
using Pulsar4X.Extensions;
using Pulsar4X.Movement;
using Pulsar4X.Orbital;

namespace Pulsar4X.Weapons;

public class WeaponUtils
{
    /// <summary>
    /// The farthest a ship can land a beam hit: the largest <see cref="GenericBeamWeaponAtb.MaxRange"/> among its
    /// installed, working beam weapons. This is the number the player needs to answer "how close do I have to get
    /// to shoot?" — the same MaxRange the firing processor already enforces in <see cref="GenericBeamWeaponAtb.IsInRange"/>,
    /// just surfaced so the UI can draw a range ring and a readout instead of the gun silently refusing to fire.
    ///
    /// Returns 0 if the ship has no working beam weapon (or only legacy MaxRange==0 "unlimited" designs) — the UI
    /// treats 0 as "no finite beam range to draw". A weapon counts only if at least one of its instances is
    /// enabled and not fully destroyed (HealthPercent > 0) — a shot-off gun no longer extends your reach (the
    /// cradle-to-grave loss rung, the same as a destroyed sensor blinding you).
    /// </summary>
    public static double GetMaxBeamRange_m(Entity ship)
    {
        double maxRange = 0;
        foreach (var (_, max, _) in GetBeamWeaponRanges(ship))
            if (max > maxRange) maxRange = max;
        return maxRange;
    }

    /// <summary>
    /// Per-weapon range breakdown for the combat readout: one row per installed, working beam design, carrying its
    /// player name, its hard MaxRange (the no-hit-beyond cutoff), and its OptimalRange (inside which it hits at full
    /// energy; beyond it damage falls off inverse-square out to MaxRange). Reads the design attributes the firing
    /// path uses, so the readout and the gun agree. Legacy "unlimited" designs (MaxRange == 0) are skipped — there
    /// is no finite range to report for them.
    /// </summary>
    public static List<(string name, double maxRange, double optimalRange)> GetBeamWeaponRanges(Entity ship)
    {
        var result = new List<(string, double, double)>();
        if (!ship.TryGetDataBlob<ComponentInstancesDB>(out var components))
            return result;

        foreach (ComponentDesign design in components.GetDesignsByType(typeof(GenericBeamWeaponAtb)))
        {
            if (!design.TryGetAttribute<GenericBeamWeaponAtb>(out var beam))
                continue;
            if (beam.MaxRange <= 0)            // legacy "unlimited" design — no finite range to draw/report
                continue;

            // Count the weapon only if a real instance of it is installed, enabled, and not destroyed.
            bool hasLiveInstance = false;
            foreach (var instance in components.GetComponentsBySpecificDesign(design.UniqueID))
            {
                if (instance.IsEnabled && instance.HealthPercent > 0)
                {
                    hasLiveInstance = true;
                    break;
                }
            }
            if (!hasLiveInstance)
                continue;

            result.Add((design.Name, beam.MaxRange, beam.OptimalRange_m));
        }
        return result;
    }

    /// <summary>
    /// Returns seconds to target
    /// </summary>
    /// <param name="distanceToTarget">Distance to target (in meters)</param>
    /// <param name="ourVelocity">Source velocity</param>
    /// <param name="targetVelocity">Target velocity</param>
    /// <returns></returns>
    public static double TimeToTarget(double distanceToTarget, Vector3 ourVelocity, Vector3 targetVelocity)
    {
        return distanceToTarget / (targetVelocity - ourVelocity).Length();
    }

    public static double TimeToTarget(Vector3 vectorToTarget, double weaponVelocity)
    {
        return vectorToTarget.Length() / weaponVelocity;
    }

    public static (Vector3 pos, double seconds) PredictTargetPositionAndTime(Vector3 ourPos, DateTime atTime, Entity targetEntity, double weaponVelocity)
    {
        var tgtPos = targetEntity.GetDataBlob<PositionDB>().AbsolutePosition;
        var vectorToTarget = ourPos - tgtPos;
        var timeToTarget = TimeToTarget(vectorToTarget, weaponVelocity);
        var futureDate = atTime + TimeSpan.FromSeconds(timeToTarget);
        var futurePosition = (Vector3)MoveMath.GetAbsoluteFuturePosition(targetEntity, futureDate);
        return (futurePosition, timeToTarget);
    }

    public static (Vector3 pos, double seconds) PredictTargetPositionAndTime(double timeToTarget, DateTime atTime, Entity targetEntity)
    {
        var futureDate = atTime + TimeSpan.FromSeconds(timeToTarget);
        var futurePosition = (Vector3)MoveMath.GetAbsoluteFuturePosition(targetEntity, futureDate);
        return (futurePosition, timeToTarget);
    }

    public static double ToHitChance(Vector3 launchPosition, Vector3 targetPosition, double projectileSpeed, double baseHitChance)
    {
        double range = launchPosition.GetDistanceTo_m(targetPosition);

        //var ttt = BeamWeapnProcessor.TimeToTarget(range, launchingEntity.))
        //tempory timetotarget
        double ttt = range / projectileSpeed; //this should be the closing speed (ie the velocity of the two, the beam speed and the range)
        double missChance = ttt * ( 1 - baseHitChance);
        return Math.Max(0, 1 - missChance); // avoid negative hit chances
    }

    Vector3 LeadVector(
        double dvToUse,
        double burnTime,
        Entity targetEntity,
        (Vector3 pos, Vector3 Velocity) ourState,
        (Vector3 pos, Vector3 Velocity) tgtState,
        DateTime atDateTime )
    {
        var distanceToTgt = (ourState.pos - tgtState.pos).Length();
        var tgtBearing = tgtState.pos - ourState.pos;

        Vector3 leadToTgt = tgtState.Velocity - ourState.Velocity;
        var closingSpeed = leadToTgt.Length() ;
        double newttt = distanceToTgt / closingSpeed;
        double oldttt = 0;
        int itterations = 0;

        while (Math.Abs(newttt - oldttt) > 1) //itterate till we get a solution that's less than a second difference from last.
        {
            oldttt = newttt;

            TimeSpan timespanToIntercept = TimeSpan.MaxValue;
            if (newttt * 10000000 <= long.MaxValue)
            {
                timespanToIntercept = TimeSpan.FromSeconds(newttt);
            }
            DateTime futureDate = atDateTime + timespanToIntercept;
            var futurePosition = (Vector3)MoveMath.GetRelativeFuturePosition(targetEntity, futureDate);

            tgtBearing = futurePosition - ourState.pos;
            distanceToTgt = (tgtBearing).Length();

            leadToTgt = tgtState.Velocity - ourState.Velocity;
            closingSpeed = leadToTgt.Length() ;
            newttt = distanceToTgt / closingSpeed;

            itterations++;

        }

        var vectorToTgt = Vector3.Normalise(tgtBearing);
        var deltaVVector = vectorToTgt * dvToUse;

        return vectorToTgt * dvToUse;
    }
}