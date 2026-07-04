using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Colonies;
using Pulsar4X.Datablobs;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// Gives a colony's EXISTING installations a home region (item #5, the LOCKED-principle reconciliation:
    /// "everything I build that's selectable in space is a real building on the ground"). A colony's installations
    /// live in its <see cref="ComponentInstancesDB"/> with no location; the tactical map (<c>PlanetViewWindow</c>) and
    /// fortification (<see cref="GroundFortification"/>) only see what's in a <see cref="Region.InstallationIds"/>. This
    /// drops every not-yet-located installation into the colony's **capital region** (region 0) so it draws on the map
    /// and counts for fortification (any that carry a <see cref="GroundDefenseAtb"/>). Idempotent — an id already placed
    /// in any region is skipped, so re-running never duplicates (and map-placed buildings keep their chosen region).
    /// </summary>
    public static class GroundInstallations
    {
        /// <summary>Locate a colony's un-placed installations into its capital region (region 0). Returns how many were
        /// newly located. Defensive: a colony with no planet / no region layer / no components does nothing.</summary>
        public static int LocateColonyInstallations(Entity colony)
        {
            if (colony == null || !colony.TryGetDataBlob<ColonyInfoDB>(out var ci)) return 0;
            var body = ci.PlanetEntity;
            if (body == null || !body.IsValid || !body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB) || regionsDB.Regions.Count == 0)
                return 0;
            if (!colony.TryGetDataBlob<ComponentInstancesDB>(out var comps)) return 0;

            var capital = regionsDB.Regions[0];

            // Everything already located anywhere (so we don't double-place, and map-placed buildings keep their region).
            var located = new HashSet<int>();
            foreach (var r in regionsDB.Regions)
                if (r.InstallationIds != null)
                    foreach (var id in r.InstallationIds) located.Add(id);

            int added = 0;
            foreach (var inst in comps.AllComponents.Values)
            {
                if (located.Contains(inst.ID)) continue;
                capital.InstallationIds.Add(inst.ID);
                located.Add(inst.ID);
                added++;
            }
            return added;
        }
    }
}
