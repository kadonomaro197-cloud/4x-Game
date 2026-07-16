using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Ships;

namespace Pulsar4X.Fleets
{
    /// <summary>
    /// Read-only helpers the UI uses to draw a fleet as ONE icon instead of a scattered cluster of ship icons. The
    /// engine already treats a fleet as a single unit (moves as one, fights as one, locks orders as one); this lets
    /// the MAP match that. Pure queries — no game state is changed.
    /// </summary>
    public static class FleetTools
    {
        /// <summary>
        /// The ship ids the map should HIDE because their fleet is drawn as one icon. For every fleet owned by
        /// <paramref name="factionId"/> in this manager that holds 2+ ships, every ship EXCEPT the one that represents
        /// the fleet (its flagship, or the first member if the flagship is unset/invalid) is added to the result.
        /// A lone ship — or a one-ship fleet — is never hidden (it already IS one icon). Only DIRECT ship children of
        /// each fleet node are considered, so a sub-fleet collapses its own ships independently of its parent.
        ///
        /// Cheap (a walk of the faction's few fleets) and STATELESS, so the caller recomputes it each frame: collapsing
        /// and expanding then track fleet membership live — break a fleet up and its ships reappear the next frame.
        /// Defensive throughout (null/!IsValid skipped) because it runs on the render thread while the sim mutates.
        /// </summary>
        public static HashSet<int> CollapsedFleetMemberShipIds(EntityManager manager, int factionId)
        {
            var hidden = new HashSet<int>();
            if (manager == null) return hidden;

            foreach (var fleetEntity in manager.GetAllEntitiesWithDataBlob<FleetDB>())
            {
                if (fleetEntity == null || !fleetEntity.IsValid || fleetEntity.FactionOwnerID != factionId)
                    continue;
                if (!fleetEntity.TryGetDataBlob<FleetDB>(out var fleetDB))
                    continue;

                // Direct ship children only (a child that is itself a sub-fleet is handled on its own pass).
                List<int> shipIds = null;
                foreach (var child in fleetDB.Children)
                {
                    if (child != null && child.IsValid && child.HasDataBlob<ShipInfoDB>())
                        (shipIds ??= new List<int>()).Add(child.Id);
                }
                if (shipIds == null || shipIds.Count < 2)
                    continue; // a lone ship draws itself — nothing to collapse

                // The representative icon = the flagship if it's one of these ships, else the first member.
                int representative = fleetDB.FlagShipID;
                if (representative == -1 || !shipIds.Contains(representative))
                    representative = shipIds[0];

                foreach (var id in shipIds)
                    if (id != representative)
                        hidden.Add(id);
            }

            return hidden;
        }

        /// <summary>
        /// The number of ships in the fleet that owns this entity's icon, for the "Fleet (N)" map label. Counts the
        /// DIRECT ship children of the fleet the given representative ship belongs to. Returns 1 if the ship isn't in
        /// a multi-ship fleet (so a lone ship's label is unaffected). Best-effort — returns 1 on any miss.
        /// </summary>
        public static int FleetShipCountFor(EntityManager manager, int factionId, int representativeShipId)
        {
            if (manager == null) return 1;
            foreach (var fleetEntity in manager.GetAllEntitiesWithDataBlob<FleetDB>())
            {
                if (fleetEntity == null || !fleetEntity.IsValid || fleetEntity.FactionOwnerID != factionId)
                    continue;
                if (!fleetEntity.TryGetDataBlob<FleetDB>(out var fleetDB))
                    continue;

                int count = 0; bool hasRep = false;
                foreach (var child in fleetDB.Children)
                {
                    if (child == null || !child.IsValid || !child.HasDataBlob<ShipInfoDB>())
                        continue;
                    count++;
                    if (child.Id == representativeShipId) hasRep = true;
                }
                if (hasRep && count >= 2)
                    return count;
            }
            return 1;
        }

        private const int MaxFleetTreeDepth = 8;

        /// <summary>
        /// Every ship under a fleet, walking DOWN through any sub-fleets (fleet components) — the RECURSIVE membership,
        /// not just the direct children. This is what a fleet-wide action (a move order) must iterate so a fleet whose
        /// ships have been organised into role sub-fleets still moves as ONE: the ships nested a level down come along
        /// instead of being left behind by a direct-children-only walk. For a FLAT fleet (no sub-fleets) it returns
        /// exactly the direct ships in the same order, so it's a drop-in for the old <c>Children.Where(is-ship)</c>
        /// with no change in behaviour. Defensive; caps recursion depth so a cyclic/self-nested tree can't hang.
        /// </summary>
        public static List<Entity> AllShipsRecursive(Entity fleet)
        {
            var result = new List<Entity>();
            CollectShipsRecursive(fleet, result, 0, new HashSet<int>());
            return result;
        }

        private static void CollectShipsRecursive(Entity fleet, List<Entity> into, int depth, HashSet<int> seen)
        {
            if (fleet == null || !fleet.IsValid || depth >= MaxFleetTreeDepth) return;
            if (!fleet.TryGetDataBlob<FleetDB>(out var fleetDB)) return;
            if (!seen.Add(fleet.Id)) return;   // visit each fleet node once — a cycle/diamond can't blow this up
            foreach (var child in fleetDB.Children)
            {
                if (child == null || !child.IsValid || child.Id == fleet.Id) continue;
                if (child.HasDataBlob<ShipInfoDB>()) into.Add(child);
                else if (child.HasDataBlob<FleetDB>()) CollectShipsRecursive(child, into, depth + 1, seen);
            }
        }
    }
}
