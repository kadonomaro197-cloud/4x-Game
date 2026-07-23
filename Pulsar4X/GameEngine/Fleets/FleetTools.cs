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

        /// <summary>
        /// Group DETECTED FOREIGN fleets into one marker each — the inverse of <see cref="CollapsedFleetMemberShipIds"/>
        /// (which collapses only the VIEWER's own fleets). The developer's "group ALL fleets": a rival's fleet you've
        /// detected should draw as ONE contact, not a scatter of named ship blips. Walks every fleet NOT owned by the
        /// viewer (and not neutral); for each, considers only its DIRECT ship children the viewer has actually DETECTED
        /// (their id is in <paramref name="detectedShipIds"/>); if 2+ are detected it keeps ONE representative (the
        /// flagship if it's detected, else the LOWEST detected id — deterministic across frames so the surviving blip
        /// doesn't flicker between members) and marks the rest to HIDE. <paramref name="repToDetectedCount"/> maps each
        /// kept representative to how many of its fleet's ships the viewer detects — the ONLY thing the marker exposes
        /// (never the fleet's true size, its name, or a ship's real name), so the fog leak is bounded to "these blips
        /// travel together".
        ///
        /// Fog-honest: a fleet you've only PARTLY spotted (0 or 1 of N detected) is NOT collapsed — you can't group ships
        /// you can't see. Reads live FleetDB membership, so a foreign fleet that breaks up ungroups next frame. Cheap,
        /// STATELESS (recompute each frame), defensive (render-thread safe). NEVER touches the viewer's own fleets —
        /// those stay on <see cref="CollapsedFleetMemberShipIds"/>, byte-identical.
        /// </summary>
        public static HashSet<int> CollapsedForeignFleetContacts(
            EntityManager manager, int viewerFactionId, ISet<int> detectedShipIds,
            out Dictionary<int, int> repToDetectedCount)
        {
            var hidden = new HashSet<int>();
            repToDetectedCount = new Dictionary<int, int>();
            if (manager == null || detectedShipIds == null || detectedShipIds.Count == 0)
                return hidden;

            foreach (var fleetEntity in manager.GetAllEntitiesWithDataBlob<FleetDB>())
            {
                if (fleetEntity == null || !fleetEntity.IsValid)
                    continue;
                // FOREIGN only: the viewer's own fleets are handled by CollapsedFleetMemberShipIds; neutral fleets skip.
                if (fleetEntity.FactionOwnerID == viewerFactionId || fleetEntity.FactionOwnerID == Game.NeutralFactionId)
                    continue;
                if (!fleetEntity.TryGetDataBlob<FleetDB>(out var fleetDB))
                    continue;

                // Direct ship children the viewer actually DETECTS (a sub-fleet child is handled on its own pass).
                List<int> detected = null;
                foreach (var child in fleetDB.Children)
                {
                    if (child != null && child.IsValid && child.HasDataBlob<ShipInfoDB>() && detectedShipIds.Contains(child.Id))
                        (detected ??= new List<int>()).Add(child.Id);
                }
                if (detected == null || detected.Count < 2)
                    continue; // 0 or 1 detected → nothing to group (fog-honest)

                // Representative = the flagship if it's among the detected ships, else the LOWEST detected id (stable
                // frame-to-frame so the surviving blip doesn't jump as the detected set flickers).
                int representative = fleetDB.FlagShipID;
                if (representative == -1 || !detected.Contains(representative))
                {
                    representative = detected[0];
                    for (int i = 1; i < detected.Count; i++)
                        if (detected[i] < representative) representative = detected[i];
                }

                repToDetectedCount[representative] = detected.Count;
                foreach (var id in detected)
                    if (id != representative)
                        hidden.Add(id);
            }

            return hidden;
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
