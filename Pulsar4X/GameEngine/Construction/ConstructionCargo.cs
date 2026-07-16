using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Storage;
using Pulsar4X.Fleets;
using Pulsar4X.Ships;

namespace Pulsar4X.Construction
{
    /// <summary>
    /// Shared "what can this constructor draw on, and can it pay?" helpers for the on-site construction flow — the
    /// fleet-pooled cargo logic factored out of <see cref="Pulsar4X.Stations.DeployStationOrder"/> so the deploy path
    /// and the recipe-driven <see cref="OnSiteConstructionOrder"/> can't drift apart. Pure + defensive: never throws.
    ///
    /// The model (developer's "A"): a constructor can use the components in its OWN hold, and — if it's in a fleet —
    /// every fleet-mate's hold too ("has them itself, or is part of a fleet that has them"). All spend uses
    /// CHECK-THEN-CONSUME so a build that comes up short never half-drains a hold.
    /// </summary>
    public static class ConstructionCargo
    {
        /// <summary>The cargo holds a constructor can draw from: its OWN hold, plus every fleet-mate's hold if it's in a
        /// fleet. There is NO ship→fleet back-reference, so the fleet is found by searching the system's fleets for the
        /// one whose direct children include this ship (sub-fleet recursion is a documented refinement). A ship in no
        /// fleet just draws on its own hold. Never throws.</summary>
        public static List<CargoStorageDB> GatherPooledHolds(Entity ship)
        {
            var holds = new List<CargoStorageDB>();
            if (ship == null) return holds;
            if (ship.TryGetDataBlob<CargoStorageDB>(out var ownHold))
                holds.Add(ownHold);

            var manager = ship.Manager;
            if (manager == null) return holds;

            try
            {
                foreach (var fleetEntity in manager.GetAllEntitiesWithDataBlob<FleetDB>())
                {
                    if (!fleetEntity.TryGetDataBlob<FleetDB>(out var fleetDB)) continue;
                    if (!fleetDB.Children.Contains(ship)) continue;

                    foreach (var member in fleetDB.Children)
                    {
                        if (member.Id == ship.Id) continue; // the constructor's own hold is already in the pool
                        if (member.HasDataBlob<ShipInfoDB>() && member.TryGetDataBlob<CargoStorageDB>(out var siblingHold))
                            holds.Add(siblingHold);
                    }
                    break; // a ship belongs to at most one fleet
                }
            }
            catch { /* a bad fleet entry never blocks the build — fall back to the pool gathered so far */ }

            return holds;
        }

        /// <summary>Total units of <paramref name="item"/> across the whole pool of holds. Never throws.</summary>
        public static long CountPooled(IEnumerable<CargoStorageDB> holds, ICargoable item)
        {
            if (holds == null || item == null) return 0;
            long total = 0;
            foreach (var hold in holds)
            {
                if (hold == null) continue;
                total += hold.GetUnitsStored(item, false);
            }
            return total;
        }

        /// <summary>Remove <paramref name="units"/> of <paramref name="item"/> from the pool, draining hold-by-hold, ONLY
        /// if the whole pool can cover it — checks the total FIRST, so a short pool consumes nothing and returns false
        /// (RemoveCargoByUnit clamps silently, so a spend-then-discover-short path would leave a half-drained hold).
        /// Returns true iff the full amount was consumed.</summary>
        public static bool TryConsumePooled(IEnumerable<CargoStorageDB> holds, ICargoable item, long units)
        {
            if (units <= 0) return true;
            if (holds == null || item == null) return false;

            var list = holds as IList<CargoStorageDB> ?? new List<CargoStorageDB>(holds);
            if (CountPooled(list, item) < units) return false;

            long remaining = units;
            foreach (var hold in list)
            {
                if (remaining <= 0) break;
                if (hold == null) continue;
                long inThisHold = hold.GetUnitsStored(item, false);
                long take = Math.Min(inThisHold, remaining);
                if (take > 0)
                    remaining -= hold.RemoveCargoByUnit(item, take);
            }
            return remaining <= 0;
        }
    }
}
