using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Colonies;
using Pulsar4X.Galaxy;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// The ground layer's one hotloop — it does everything that happens ON the surface each tick: units MARCH
    /// (5b), garrisons FIGHT (5c), and cleared regions are CAPTURED (5d). It is a SINGLE processor on purpose:
    /// only one hotloop may be keyed to a DataBlob type (landmine L9), so movement + combat + capture all live
    /// here rather than as three processors on <see cref="GroundForcesDB"/>.
    ///
    /// Keyed on <see cref="GroundForcesDB"/> (its own blob — no other processor owns it), so it processes every
    /// planet body that has ground forces and sleeps on those that don't. Defensive to the core: a throw in a
    /// hotloop crashes the whole game loop (landmine L4), so <see cref="ProcessEntity"/> swallows and moves on.
    ///
    /// Combat model (mirrors the space <c>AutoResolve</c> salvo loop, but over <see cref="GroundUnit"/> data
    /// objects, not entities): each tick is ONE salvo — every faction in a contested region takes the COMBINED
    /// attack of all other factions there, focus-fired across its units; units at 0 health are removed. Simple,
    /// deterministic (no RNG), and cheap. Design: docs/GROUND-COMBAT-MAP-DESIGN.md (slices 5b–5d).
    /// </summary>
    public class GroundForcesProcessor : IHotloopProcessor
    {
        public TimeSpan RunFrequency { get; } = TimeSpan.FromHours(1);
        public TimeSpan FirstRunOffset { get; } = TimeSpan.FromHours(1);
        public Type GetParameterType { get; } = typeof(GroundForcesDB);

        /// <summary>Fraction of a faction's attack that lands per salvo — the ground combat-pace dial (mirrors the
        /// space <c>SalvoDamageScale</c>). 1.0 = full; lower stretches a battle over more ticks. Tune live.</summary>
        public const double SalvoScale = 1.0;

        public void Init(Game game) { }

        public void ProcessEntity(Entity entity, int deltaSeconds)
        {
            try { ProcessBody(entity, deltaSeconds); }
            catch { /* a hotloop must never throw (L4) — a bad body is skipped, the sim keeps running */ }
        }

        public int ProcessManager(EntityManager manager, int deltaSeconds)
        {
            var bodies = manager.GetAllEntitiesWithDataBlob<GroundForcesDB>();
            foreach (var body in bodies)
                ProcessEntity(body, deltaSeconds);
            return bodies.Count;
        }

        private static void ProcessBody(Entity body, int deltaSeconds)
        {
            if (!body.TryGetDataBlob<GroundForcesDB>(out var forces) || forces.Units.Count == 0)
                return;

            // 1) MOVEMENT (5b): advance in-transit units; a unit arrives when its crossing time has elapsed.
            foreach (var unit in forces.Units)
            {
                if (unit.MovingToRegion < 0) continue;
                unit.TransitSecondsRemaining -= deltaSeconds;
                if (unit.TransitSecondsRemaining <= 0)
                {
                    unit.RegionIndex = unit.MovingToRegion;
                    unit.MovingToRegion = -1;
                    unit.TransitSecondsRemaining = 0;
                }
            }

            body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB);

            // Group the units that are STANDING (not on the road) and alive, by region.
            var byRegion = new Dictionary<int, List<GroundUnit>>();
            foreach (var unit in forces.Units)
            {
                if (unit.MovingToRegion >= 0 || unit.Health <= 0) continue;
                if (!byRegion.TryGetValue(unit.RegionIndex, out var list))
                {
                    list = new List<GroundUnit>();
                    byRegion[unit.RegionIndex] = list;
                }
                list.Add(unit);
            }

            // 2) COMBAT (5c) + 3) REGION CAPTURE (5d), per region.
            foreach (var kv in byRegion)
            {
                var units = kv.Value;

                var factions = new HashSet<int>();
                foreach (var u in units) factions.Add(u.FactionOwnerID);
                if (factions.Count >= 2)
                    ResolveRegionCombat(units);

                // Whoever holds the only live units here now owns the region.
                var holders = new HashSet<int>();
                foreach (var u in units) if (u.Health > 0) holders.Add(u.FactionOwnerID);
                if (holders.Count == 1 && regionsDB != null && kv.Key >= 0 && kv.Key < regionsDB.Regions.Count)
                    regionsDB.Regions[kv.Key].OwnerFactionID = holders.First();
            }

            // Remove destroyed units.
            forces.Units.RemoveAll(u => u.Health <= 0);

            // 4) WHOLE-PLANET CAPTURE (5d — the "take a planet" moment): if EVERY region is held by a single
            //    faction that isn't the colony's current owner, the planet's colony flips to that faction.
            TryCapturePlanet(body, regionsDB);
        }

        /// <summary>One salvo in a contested region: snapshot each faction's total attack, then every faction takes
        /// the combined attack of all OTHERS, focus-fired across its units (whole-unit health loss). Simultaneous
        /// (snapshot before apply) and deterministic. Defense is a v1 hook the terrain/type passes will read.</summary>
        private static void ResolveRegionCombat(List<GroundUnit> units)
        {
            var attackByFaction = new Dictionary<int, double>();
            foreach (var u in units)
            {
                attackByFaction.TryGetValue(u.FactionOwnerID, out var a);
                attackByFaction[u.FactionOwnerID] = a + u.Attack;
            }
            double totalAttackAll = 0;
            foreach (var v in attackByFaction.Values) totalAttackAll += v;

            // Snapshot incoming damage per faction BEFORE applying any of it (simultaneous exchange).
            var incomingByFaction = new Dictionary<int, double>();
            foreach (var f in attackByFaction.Keys)
                incomingByFaction[f] = (totalAttackAll - attackByFaction[f]) * SalvoScale;

            foreach (var f in incomingByFaction.Keys)
            {
                double pool = incomingByFaction[f];
                if (pool <= 0) continue;
                foreach (var u in units)
                {
                    if (u.FactionOwnerID != f || u.Health <= 0) continue;
                    if (pool <= 0) break;
                    double take = Math.Min(u.Health, pool);
                    u.Health -= take;
                    pool -= take;
                }
            }
        }

        private static void TryCapturePlanet(Entity body, PlanetRegionsDB regionsDB)
        {
            if (regionsDB == null || regionsDB.Regions.Count == 0) return;

            int owner = regionsDB.Regions[0].OwnerFactionID;
            if (owner < 0) return;
            foreach (var r in regionsDB.Regions)
                if (r.OwnerFactionID != owner) return;   // not uniformly held → not taken

            var manager = body.Manager;
            if (manager == null) return;
            foreach (var colony in manager.GetAllEntitiesWithDataBlob<ColonyInfoDB>())
            {
                if (colony.TryGetDataBlob<ColonyInfoDB>(out var info)
                    && info.PlanetEntity != null && info.PlanetEntity.Id == body.Id
                    && colony.FactionOwnerID != owner)
                {
                    colony.FactionOwnerID = owner;   // the planet is taken (v1: ownership flip; deeper transfer later)
                }
            }
        }
    }
}
