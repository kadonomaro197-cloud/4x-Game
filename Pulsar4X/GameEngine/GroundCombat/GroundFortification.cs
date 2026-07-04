using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Colonies;
using Pulsar4X.Datablobs;
using Pulsar4X.Components;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// Turns the buildings in a region into the defender's FORTIFICATION multiplier — now DESIGN-DRIVEN (the
    /// developer's call: "depending on base design"). A building fortifies only if its design carries a
    /// <see cref="GroundDefenseAtb"/>: its <see cref="GroundDefenseAtb.LocalFortify"/> hardens the region it stands in,
    /// and its <see cref="GroundDefenseAtb.AdjacentProjection"/> shields each ADJACENT region the same faction holds.
    /// So a Bunker fortifies + projects; a solar panel does nothing.
    ///
    /// The value is resolved through a <c>resolve</c> function (installation id → its <see cref="GroundDefenseAtb"/> or
    /// null) — the processor builds the real one from the body's colonies (<see cref="BuildResolver"/>); a test injects
    /// a stub, so the fortification MATH is unit-testable without any JSON. The total is capped at <see cref="Cap"/>,
    /// so it stays an edge, not an impregnable wall.
    /// </summary>
    public static class GroundFortification
    {
        /// <summary>Max fortification bonus (defender's incoming is divided by 1 + min(Cap, sum)). +1.0 = halves incoming.</summary>
        public const double Cap = 1.0;

        /// <summary>The defender's fortification multiplier for <paramref name="region"/> (its incoming damage is divided
        /// by this). 1.0 = none. Sums this region's buildings' local value + adjacent same-faction regions' projection,
        /// capped. <paramref name="ownerFaction"/> is the defender (the region owner); only regions it also owns project.</summary>
        public static double DefenseMult(Region region, List<Region> regions, int ownerFaction, Func<int, GroundDefenseAtb> resolve)
        {
            if (region == null || resolve == null) return 1.0;

            double bonus = SumLocal(region, resolve);

            if (ownerFaction >= 0 && regions != null && region.Neighbors != null)
            {
                foreach (var n in region.Neighbors)
                {
                    if (n < 0 || n >= regions.Count) continue;
                    var adj = regions[n];
                    if (adj == null || adj.OwnerFactionID != ownerFaction) continue;   // only YOUR neighbours project
                    bonus += SumAdjacent(adj, resolve);
                }
            }

            if (bonus < 0) bonus = 0;
            return 1.0 + Math.Min(Cap, bonus);
        }

        private static double SumLocal(Region r, Func<int, GroundDefenseAtb> resolve)
        {
            double s = 0;
            if (r.InstallationIds != null)
                foreach (var id in r.InstallationIds)
                {
                    var a = resolve(id);
                    if (a != null) s += a.LocalFortify;
                }
            return s;
        }

        private static double SumAdjacent(Region r, Func<int, GroundDefenseAtb> resolve)
        {
            double s = 0;
            if (r.InstallationIds != null)
                foreach (var id in r.InstallationIds)
                {
                    var a = resolve(id);
                    if (a != null) s += a.AdjacentProjection;
                }
            return s;
        }

        /// <summary>Build the real installation-id → <see cref="GroundDefenseAtb"/> resolver for a planet body: walks the
        /// colonies on the body and indexes every installed component that carries the attribute by its instance ID
        /// (the id <c>PlaceInstallationInRegionOrder</c> records in <c>Region.InstallationIds</c>). Defensive — never
        /// throws; returns a resolver that yields null for anything not a ground-defence building.</summary>
        public static Func<int, GroundDefenseAtb> BuildResolver(Entity body)
        {
            var map = new Dictionary<int, GroundDefenseAtb>();
            try
            {
                if (body?.Manager != null)
                {
                    foreach (var colony in body.Manager.GetAllEntitiesWithDataBlob<ColonyInfoDB>())
                    {
                        if (!colony.TryGetDataBlob<ColonyInfoDB>(out var ci) || ci.PlanetEntity == null || ci.PlanetEntity.Id != body.Id)
                            continue;
                        if (!colony.TryGetDataBlob<ComponentInstancesDB>(out var comps)) continue;
                        if (!comps.TryGetComponentsByAttribute<GroundDefenseAtb>(out var list)) continue;
                        foreach (var inst in list)
                        {
                            var atb = inst.Design.GetAttribute<GroundDefenseAtb>();
                            if (atb != null) map[inst.ID] = atb;
                        }
                    }
                }
            }
            catch { /* a bad colony/component is skipped — fortification just omits it, never crashes the hotloop */ }

            return id => map.TryGetValue(id, out var a) ? a : null;
        }
    }
}
