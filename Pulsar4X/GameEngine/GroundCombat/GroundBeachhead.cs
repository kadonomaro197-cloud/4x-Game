using System;
using System.Collections.Generic;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Components;
using Pulsar4X.Factions;
using Pulsar4X.Galaxy;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// THE BEACHHEAD — a landed COMBAT ENGINEER building a base on an enemy world with NO colony present (the missing
    /// rung the A5 ground-campaign ledger flagged: "colony-free building — MISSING, so a beachhead is impossible today").
    /// This is the CONSUME half of the G1 combat-engineer chain: G1.1 landed crated parts on the surface
    /// (<see cref="GroundParts"/>) and made a chassis a combat engineer by mounting a <see cref="GroundConstructorAtb"/>;
    /// this slice (G1.2) turns those two into a real, placed footprint building on the ground you hold — the forward
    /// operating base (FOB) an invader plants after it clears a region.
    ///
    /// In plain English: an engineer unit standing on ground its own side holds, with crated building-parts landed
    /// beside it and no enemy troops in the region, erects a bunker over a few days — the way a Seabee battalion throws
    /// up a forward base after a landing. The rate it works at is the engineer's own <see cref="GroundConstructorAtb.BuildRate"/>
    /// (build-points per day); the effort a structure takes is that building's own industry cost. When the work is done,
    /// one crate is consumed and the building appears — it fortifies the region (the same <see cref="GroundDefenseAtb"/>
    /// path a colony's Bunker uses) and becomes a capture/bombard target on the war map (the grave rung).
    ///
    /// THE OWNERSHIP PROBLEM + the chosen solution (a developer decision — see LANE-GROUND-NOTES §G1.2): every building in
    /// the game is a <see cref="ComponentInstance"/> that lives in a <b>host store</b> (a colony's
    /// <see cref="ComponentInstancesDB"/>), and the fortification / bombard / readout code all resolve a building id back
    /// to its host by walking the body's colonies. On an enemy world the invader has NO colony, so there is no host. The
    /// least-invasive fix — verified against the codebase, not assumed — is a <b>per-faction beachhead OUTPOST</b>: a
    /// faction-owned entity carrying a bare <see cref="ComponentInstancesDB"/>, created on demand
    /// (<see cref="EnsureOutpost"/>) and registered on the body's <see cref="GroundForcesDB.OutpostEntityIds"/>. This is
    /// the EXACT store a ground unit's backing entity already uses (<see cref="GroundUnitEntity"/>) — proven inert (no
    /// processor iterates <see cref="ComponentInstancesDB"/>; no position/name → invisible to map/combat/sensors) — so it
    /// adds no new machinery and no new save risk. The fortification resolver, the bombard index, and the client readouts
    /// were pointed at a single shared list of "every store on the body, colonies AND outposts"
    /// (<see cref="GroundBuildings.BodyComponentStores"/>), so a beachhead building fortifies / bombs / reads out exactly
    /// like a colony's, with the colony-only path byte-identical when no outpost exists.
    ///
    /// Folded into the ground tick as a STEP (like ground upkeep) — never a second hotloop (landmine L9:
    /// <see cref="GroundForcesProcessor"/> already owns <see cref="GroundForcesDB"/>). Defensive to the core (L4: a throw
    /// in the hotloop crashes the sim). Byte-identical until an engineer unit exists AND lands: with no combat engineer
    /// standing anywhere, <see cref="TickBuilds"/> takes a cheap read-only pass and returns having changed nothing.
    /// </summary>
    public static class GroundBeachhead
    {
        /// <summary>The floor on a footprint building's on-site ASSEMBLY EFFORT (build-points) — used when the design's own
        /// <c>IndustryPointCosts</c> is smaller, so even a trivially-cheap structure still takes an engineer a little time
        /// to erect. The effort itself is the building's own industry cost (no NEW per-building number); this is only the
        /// floor. FLAGGED balance value.</summary>
        public const double MinAssemblyEffort = 100.0;   // FLAGGED balance value

        /// <summary>The per-tick beachhead build step (folded into <see cref="GroundForcesProcessor"/>'s ProcessBody, L9).
        /// For each (faction, region) that has an IDLE combat engineer standing in it, the region is FRIENDLY-HELD and
        /// ENEMY-FREE, and a buildable FOOTPRINT part is landed there, accrue Σ engineer <see cref="GroundConstructorAtb.BuildRate"/>
        /// × elapsed days onto that site; when it reaches the building's assembly effort, consume one crate and place the
        /// footprint building into the faction's beachhead outpost + the region/hex war map. No engineer standing anywhere
        /// → a cheap read-only pass, no state change (byte-identical). Never throws (L4).</summary>
        public static void TickBuilds(Entity body, int deltaSeconds)
        {
            try
            {
                if (deltaSeconds <= 0) return;
                if (body?.Manager == null || !body.TryGetDataBlob<GroundForcesDB>(out var forces) || forces.Units == null) return;
                var game = body.Manager.Game;
                if (game == null) return;
                // A region building needs a region to live in — no region layer → no on-site build (nothing to place into).
                if (!body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB) || regionsDB.Regions == null) return;

                // 1) Which (faction, region) cell has idle combat engineers, and the Σ build-rate they lay down there.
                var rateByCell = new Dictionary<(int faction, int region), double>();
                foreach (var unit in forces.Units)
                {
                    if (unit == null || unit.Health <= 0) continue;
                    if (unit.MovingToRegion >= 0) continue;                           // marching between regions → not building
                    if (unit.HexPath != null && unit.HexPath.Count > 0) continue;     // repositioning on the battlefield → not building
                    if (unit.GlobalPath != null && unit.GlobalPath.Count > 0) continue;
                    double rate = BestBuildRate(body, unit);
                    if (rate <= 0) continue;                                          // not a combat engineer
                    var cell = (unit.FactionOwnerID, unit.RegionIndex);
                    rateByCell[cell] = rateByCell.TryGetValue(cell, out var r) ? r + rate : rate;
                }
                if (rateByCell.Count == 0) return;   // no engineer standing anywhere → byte-identical fast path (the common case)

                double days = deltaSeconds / 86400.0;

                foreach (var kv in rateByCell)
                {
                    int faction = kv.Key.faction, region = kv.Key.region;
                    if (region < 0 || region >= regionsDB.Regions.Count) continue;

                    // FRIENDLY-HELD: you build only on ground you hold (the region owner is the engineer's faction).
                    if (regionsDB.Regions[region].OwnerFactionID != faction) continue;
                    // ENEMY-FREE: a contested region (a live enemy ground unit present) is a battlefield, not a build site.
                    if (RegionHasLiveEnemy(forces, region, faction)) continue;

                    // A buildable FOOTPRINT part landed here (the crated structure to assemble). None → nothing to build.
                    var part = FirstBuildableFootprintPart(game, faction, forces, region);
                    if (part == null) continue;

                    // Accrue build-points on the site for (region, that design); complete when it reaches the required effort.
                    var site = FindOrCreateSite(forces, region, part.Value.designId, part.Value.effort);
                    site.ProgressPoints += kv.Value * days;
                    if (site.ProgressPoints < site.RequiredPoints) continue;   // still rising — accrues over more ticks

                    // COMPLETE: consume ONE crate (check-then-place) and erect the footprint building into the beachhead.
                    if (GroundParts.ConsumePart(body, region, part.Value.designId, 1))
                        PlaceBuilding(body, faction, region, part.Value.design, regionsDB);
                    forces.BuildSites.Remove(site);   // done (or the crate vanished) — clear it so it can't busy-loop
                }
            }
            catch { /* the on-site build is a nicety — never break the ground hotloop over it (L4) */ }
        }

        /// <summary>Find (or create) this faction's BEACHHEAD OUTPOST host on the body — a faction-owned entity carrying a
        /// bare <see cref="ComponentInstancesDB"/> (the SAME proven-inert store a ground unit's backing entity uses, so it
        /// adds no new machinery), registered on the body's <see cref="GroundForcesDB.OutpostEntityIds"/> so the shared
        /// <see cref="GroundBuildings.BodyComponentStores"/> index finds it. Returns null on failure. Never throws.</summary>
        public static Entity EnsureOutpost(Entity body, int factionId)
        {
            try
            {
                if (body?.Manager == null) return null;
                if (!body.TryGetDataBlob<GroundForcesDB>(out var forces))
                {
                    forces = new GroundForcesDB();
                    body.SetDataBlob(forces);
                }
                // Reuse this faction's existing outpost host if it's still alive.
                foreach (var oid in forces.OutpostEntityIds)
                    if (body.Manager.TryGetEntityById(oid, out var existing)
                        && existing.FactionOwnerID == factionId && existing.HasDataBlob<ComponentInstancesDB>())
                        return existing;

                // Create a bare-store host owned by the faction (mirrors GroundUnitEntity.BuildBacking): it holds
                // queryable components and nothing else — no position/orbit/name, so it's invisible to the map, combat,
                // and sensors, and no processor iterates ComponentInstancesDB. Genuinely inert.
                var e = Entity.Create();
                e.FactionOwnerID = factionId;
                body.Manager.AddEntity(e, new List<BaseDataBlob> { new ComponentInstancesDB() });
                forces.OutpostEntityIds.Add(e.Id);
                return e;
            }
            catch { return null; }
        }

        /// <summary>Does <paramref name="factionId"/> hold a BEACHHEAD (a footprint building its own outpost hosts) in the
        /// region it also owns? This is the FOB "resupply point" read the ground campaign consumes in G2 — a region with a
        /// friendly forward base is a place a unit can rearm/reinforce from. Read-only; never throws.</summary>
        public static bool HasBeachhead(Entity body, int factionId, int regionIndex)
        {
            try
            {
                if (body?.Manager == null || !body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB)
                    || regionIndex < 0 || regionIndex >= regionsDB.Regions.Count) return false;
                var region = regionsDB.Regions[regionIndex];
                if (region.OwnerFactionID != factionId || region.InstallationIds == null || region.InstallationIds.Count == 0) return false;
                if (!body.TryGetDataBlob<GroundForcesDB>(out var forces) || forces.OutpostEntityIds == null) return false;

                var owned = new HashSet<int>();
                foreach (var oid in forces.OutpostEntityIds)
                    if (body.Manager.TryGetEntityById(oid, out var outpost) && outpost.FactionOwnerID == factionId
                        && outpost.TryGetDataBlob<ComponentInstancesDB>(out var store))
                        foreach (var inst in store.AllComponents.Values) owned.Add(inst.ID);
                if (owned.Count == 0) return false;

                foreach (var id in region.InstallationIds) if (owned.Contains(id)) return true;
                return false;
            }
            catch { return false; }
        }

        // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The best <see cref="GroundConstructorAtb.BuildRate"/> mounted on a unit — read off its backing
        /// component store exactly the way <see cref="GroundSensors"/> reads a radar (the ability falls out of the store,
        /// units-as-entities). 0 for a unit that is not a combat engineer (no backing / no constructor). Never throws.</summary>
        private static double BestBuildRate(Entity body, GroundUnit unit)
        {
            if (!GroundUnitEntity.TryGetBacking(body, unit, out var backing)) return 0;
            if (!backing.TryGetDataBlob<ComponentInstancesDB>(out var cidb)) return 0;
            if (!cidb.TryGetComponentsByAttribute<GroundConstructorAtb>(out var kits) || kits.Count == 0) return 0;
            double best = 0;
            foreach (var k in kits)
                if (k.Design != null && k.Design.TryGetAttribute<GroundConstructorAtb>(out var atb) && atb.BuildRate > best)
                    best = atb.BuildRate;
            return best;
        }

        /// <summary>Is there a live enemy ground unit in <paramref name="region"/> (a unit of a different faction)? A
        /// contested region is a battlefield, not a build site.</summary>
        private static bool RegionHasLiveEnemy(GroundForcesDB forces, int region, int factionId)
        {
            foreach (var u in forces.Units)
                if (u != null && u.Health > 0 && u.RegionIndex == region && u.FactionOwnerID != factionId)
                    return true;
            return false;
        }

        /// <summary>The first landed FOOTPRINT part in <paramref name="region"/> the faction can build — its design id, its
        /// resolved <see cref="ComponentDesign"/>, and the assembly effort (the design's <c>IndustryPointCosts</c>, floored
        /// at <see cref="MinAssemblyEffort"/>). Null if none. Resolves the crate's design off the engineer's faction, so a
        /// design the faction can't build is ignored.</summary>
        private static (string designId, ComponentDesign design, double effort)? FirstBuildableFootprintPart(
            Game game, int factionId, GroundForcesDB forces, int region)
        {
            if (forces.SurfaceParts == null || forces.SurfaceParts.Count == 0) return null;
            if (!game.Factions.TryGetValue(factionId, out var factionEntity)) return null;
            if (!factionEntity.TryGetDataBlob<FactionInfoDB>(out var factionInfo)) return null;
            foreach (var p in forces.SurfaceParts)
            {
                if (p.RegionIndex != region || p.Count <= 0) continue;
                if (!factionInfo.IndustryDesigns.TryGetValue(p.DesignId, out var d) || d is not ComponentDesign cd) continue;
                if (!GroundBuildings.IsFootprint(cd)) continue;   // only a footprint building is a beachhead structure
                double effort = Math.Max(MinAssemblyEffort, cd.IndustryPointCosts);
                return (p.DesignId, cd, effort);
            }
            return null;
        }

        /// <summary>Find the in-progress site for (<paramref name="region"/>, <paramref name="designId"/>), or start one.</summary>
        private static GroundBuildSite FindOrCreateSite(GroundForcesDB forces, int region, string designId, double required)
        {
            foreach (var st in forces.BuildSites)
                if (st != null && st.RegionIndex == region && st.DesignId == designId) return st;
            var site = new GroundBuildSite { RegionIndex = region, DesignId = designId, RequiredPoints = required, ProgressPoints = 0 };
            forces.BuildSites.Add(site);
            return site;
        }

        /// <summary>Erect one <paramref name="design"/> building into <paramref name="factionId"/>'s beachhead outpost on
        /// the body, and record it on the region (economy + fortification) and — if it's a footprint — the war-map hexes
        /// (capture/bombard target). Returns the new instance id (-1 on failure).</summary>
        private static int PlaceBuilding(Entity body, int factionId, int region, ComponentDesign design, PlanetRegionsDB regionsDB)
        {
            var outpost = EnsureOutpost(body, factionId);
            if (outpost == null || !outpost.TryGetDataBlob<ComponentInstancesDB>(out var store)) return -1;

            // Host the built structure in the invader's beachhead outpost (colony-free) — the SAME ComponentInstance a
            // colony holds, so it resolves through the shared BodyComponentStores index for fortification / bombard / readout.
            var inst = new ComponentInstance(design);
            inst.ParentEntity = outpost;             // resolves ParentInstances = the outpost's store (no auto-add)
            store.AddComponentInstance(inst);        // populates the by-attribute index — no install hook / ReCalc (like a backing)
            int id = inst.ID;

            // ECONOMY + FORTIFICATION axis: GroundFortification reads Region.InstallationIds.
            var reg = regionsDB.Regions[region];
            if (reg.InstallationIds != null && !reg.InstallationIds.Contains(id)) reg.InstallationIds.Add(id);

            // WAR-MAP axis: a footprint building occupies the region's muster hexes (a capture/bombard target — the grave rung).
            if (GroundBuildings.IsFootprint(design))
                GroundBuildings.LocateInstanceOnHexes(body, regionsDB, region, id);

            return id;
        }
    }
}
