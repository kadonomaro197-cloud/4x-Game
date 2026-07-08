using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Components;
using Pulsar4X.Factions;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// Units-as-entities (Option A) — SLICE 2: give a raised <see cref="GroundUnit"/> a BACKING ENTITY that carries its
    /// design's components as real <see cref="ComponentInstance"/>s — the SAME <see cref="ComponentInstancesDB"/> store a
    /// ship has. Once the unit carries its components, every ability (radar-reveal / speed / crew / weapons) falls out via
    /// <c>TryGetComponentsByAttribute</c>, exactly like a ship, with no per-ability special-casing. This is the fix for
    /// "why doesn't the ability just fall out" — the flat stat-snapshot threw the components away; now they're kept.
    ///
    /// The store is populated LOW-LEVEL (<see cref="ComponentInstancesDB.AddComponentInstance"/>) rather than
    /// <c>Entity.AddComponent</c>, so it does NOT fire install hooks or <c>ReCalcAbilities</c> — the ground component
    /// atbs are inert on install and no processor iterates <c>ComponentInstancesDB</c>, so the backing entity is
    /// genuinely inert: it holds queryable components and nothing else (no position/orbit/name → invisible to the map,
    /// combat, sensors). Fully defensive: any failure yields no backing (-1), never throws in the raise hotloop (L4).
    ///
    /// v1 backs designs that carry a component list (<see cref="GroundUnitDesign.ComponentDesignIds"/>, from the
    /// assembler). The monolithic base-mod units (a single <c>GroundUnitAtb</c> component) get their backing in a later
    /// slice. Design: docs/GROUND-UNITS-AS-ENTITIES-DESIGN.md.
    /// </summary>
    public static class GroundUnitEntity
    {
        /// <summary>Build the backing entity for a raised unit from its design's component list; returns the entity id
        /// (-1 if the design carries no components or anything fails). Never throws.</summary>
        public static int BuildBacking(Entity body, GroundUnitDesign design, int factionId)
        {
            try
            {
                if (body?.Manager == null || design?.ComponentDesignIds == null || design.ComponentDesignIds.Count == 0)
                    return -1;
                var game = body.Manager.Game;
                if (game == null || !game.Factions.TryGetValue(factionId, out var factionEntity)
                    || !factionEntity.TryGetDataBlob<FactionInfoDB>(out var factionInfo))
                    return -1;

                // Create the entity + its component store first (ParentEntity resolution needs the entity in a manager).
                var e = Entity.Create();
                e.FactionOwnerID = factionId;
                body.Manager.AddEntity(e, new List<BaseDataBlob> { new ComponentInstancesDB() });
                var cidb = e.GetDataBlob<ComponentInstancesDB>();
                if (cidb == null) return -1;

                foreach (var kv in design.ComponentDesignIds)
                {
                    if (kv.Value < 1) continue;
                    if (!factionInfo.IndustryDesigns.TryGetValue(kv.Key, out var d) || d is not ComponentDesign cd)
                        continue;   // a component the faction no longer holds — skip it, keep the rest
                    for (int i = 0; i < kv.Value; i++)
                    {
                        var inst = new ComponentInstance(cd);
                        inst.ParentEntity = e;               // resolves ParentInstances = cidb (no auto-add)
                        cidb.AddComponentInstance(inst);     // populates the by-attribute index — no install hook / ReCalc
                    }
                }
                return e.Id;
            }
            catch { return -1; }   // a unit without its backing still works off the flat snapshot — never break the raise
        }

        /// <summary>Resolve a unit's backing entity (the one carrying its components). False if it has none. Never throws.</summary>
        public static bool TryGetBacking(Entity body, GroundUnit unit, out Entity backing)
        {
            backing = null;
            try
            {
                if (body?.Manager == null || unit == null || unit.BackingEntityId < 0) return false;
                return body.Manager.TryGetEntityById(unit.BackingEntityId, out backing);
            }
            catch { return false; }
        }
    }
}
