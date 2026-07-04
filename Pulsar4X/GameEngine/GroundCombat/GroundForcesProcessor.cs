using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar4X.Engine;
using Pulsar4X.Interfaces;
using Pulsar4X.Colonies;
using Pulsar4X.Galaxy;
using Pulsar4X.Hazards;

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

        // FORTIFICATION (5h) is now DESIGN-DRIVEN — a building fortifies its region (and projects to adjacent friendly
        // regions) only if its design carries a GroundDefenseAtb (a Bunker, not a solar panel). The math + the
        // colony→component resolver live in GroundFortification; ResolveRegionCombat applies it to the defender.

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

            // 1) MOVEMENT: advance in-transit units.
            //    - HEX PATH (H2): a unit with a plotted hex route walks it hex-by-hex, each step costing its
            //      terrain-weighted time; it arrives when the last hex is reached (the London→Paris transit).
            //    - REGION HOP (5b, coarse fallback): a unit ordered region→region lands at the new region's centre
            //      when its crossing time elapses (kept working until the client is hex-native).
            foreach (var unit in forces.Units)
            {
                if (unit.Path != null && unit.Path.Count > 0) { AdvanceHexPath(unit, deltaSeconds); continue; }

                if (unit.MovingToRegion < 0) continue;
                unit.TransitSecondsRemaining -= deltaSeconds;
                if (unit.TransitSecondsRemaining <= 0)
                {
                    unit.RegionIndex = unit.MovingToRegion;
                    unit.MovingToRegion = -1;
                    unit.TransitSecondsRemaining = 0;
                    unit.HexQ = 0; unit.HexR = 0;   // a coarse region hop musters at the new region's patch centre
                }
            }

            // 1b) ENVIRONMENTAL ATTRITION (E3): a unit STANDING in a region with a DAMAGING environmental hazard
            //     (fire tornadoes, corrosive superstorm, radiation zone) bleeds health each tick — the ground twin
            //     of a ship taking damage inside a space hazard. Per-hour magnitude, scaled by the tick. The stat
            //     effects (SensorJam / MovementDrag) are carried by the data + generator but applied in a later wire
            //     (ground detection/movement hooks) — this slice lands the attrition.
            if (body.TryGetDataBlob<PlanetEnvironmentsDB>(out var envDB))
            {
                foreach (var unit in forces.Units)
                {
                    if (unit.MovingToRegion >= 0 || unit.Health <= 0) continue;
                    foreach (var env in envDB.ForRegion(unit.RegionIndex))
                    {
                        if (!IsDamageEffect(env.Effect)) continue;
                        // E4: the unit's environmental GEAR negates a fraction of the hazard's attrition (heat-shielding,
                        // hazmat sealing…) — the ground echo of a ship's HazardResistanceAtb. 1.0 resistance = immune.
                        double resist = unit.ResistanceTo(env.Effect);
                        unit.Health -= env.Magnitude * (deltaSeconds / 3600.0) * (1.0 - resist);
                        if (unit.Health < 0) unit.Health = 0;
                    }
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

            // Build the design-driven fortification resolver once (installation id → GroundDefenseAtb, from the body's
            // colonies) — so a Bunker in a region hardens its defender (and shields adjacent friendly regions).
            var allRegions = regionsDB?.Regions;
            var fortResolve = GroundFortification.BuildResolver(body);

            // 2) COMBAT (5c) + 3) REGION CAPTURE (5d), per region.
            foreach (var kv in byRegion)
            {
                var units = kv.Value;

                var factions = new HashSet<int>();
                foreach (var u in units) factions.Add(u.FactionOwnerID);
                if (factions.Count >= 2)
                {
                    Region region = (regionsDB != null && kv.Key >= 0 && kv.Key < regionsDB.Regions.Count)
                        ? regionsDB.Regions[kv.Key] : null;
                    ResolveRegionCombat(forces, units, region, allRegions, fortResolve);
                }

                // Whoever holds the only live units here now owns the region.
                var holders = new HashSet<int>();
                foreach (var u in units) if (u.Health > 0) holders.Add(u.FactionOwnerID);
                if (holders.Count == 1 && regionsDB != null && kv.Key >= 0 && kv.Key < regionsDB.Regions.Count)
                    regionsDB.Regions[kv.Key].OwnerFactionID = holders.First();
            }

            // Remove destroyed units.
            forces.Units.RemoveAll(u => u.Health <= 0);

            // FORMATIONS: a formation whose LEADER just died reassigns leadership to a surviving member (fleet-like —
            // the flagship echo; no combat penalty, per the locked design). An empty formation keeps LeaderUnitId = -1.
            MaintainFormations(forces);

            // 4) WHOLE-PLANET CAPTURE (5d — the "take a planet" moment): if EVERY region is held by a single
            //    faction that isn't the colony's current owner, the planet's colony flips to that faction.
            TryCapturePlanet(body, regionsDB);
        }

        /// <summary>One salvo in a contested region — TERRAIN- and TYPE-aware (5f/5g). Each faction takes the
        /// combined attack of all others, but each attacker's damage is scaled by the weapon TRIANGLE (vs the enemy's
        /// composition) and its TERRAIN affinity (armour bad in rough, artillery loves high ground); the region's
        /// OWNER (the defender) additionally divides its incoming by the terrain COVER. Simultaneous (snapshot before
        /// apply), deterministic, no overkill (pool carries). Reads <see cref="GroundTerrain"/> — the ground twin of
        /// SpaceHazardTools.</summary>
        private static void ResolveRegionCombat(GroundForcesDB forces, List<GroundUnit> units, Region region,
            List<Region> allRegions, System.Func<int, GroundDefenseAtb> fortResolve)
        {
            var terrain = GroundTerrain.Classify(region);
            int defenderFaction = region != null ? region.OwnerFactionID : -1;

            var byFaction = new Dictionary<int, List<GroundUnit>>();
            foreach (var u in units)
            {
                if (!byFaction.TryGetValue(u.FactionOwnerID, out var l)) { l = new List<GroundUnit>(); byFaction[u.FactionOwnerID] = l; }
                l.Add(u);
            }

            // Incoming damage pool per faction, computed from the PRE-salvo state (simultaneous exchange).
            var incoming = new Dictionary<int, double>();
            foreach (var g in byFaction.Keys) incoming[g] = 0.0;

            foreach (var f in byFaction.Keys)
            {
                foreach (var g in byFaction.Keys)
                {
                    if (f == g) continue;
                    double pool = 0.0;
                    foreach (var u in byFaction[f])
                    {
                        if (u.Health <= 0) continue;
                        // × terrain affinity × the formation STANCE's attack mult (offensive hits harder, 5h doctrine).
                        double atk = u.Attack * GroundTerrain.TerrainAttackMult(u.UnitType, terrain) * GroundFormationDoctrine.AttackMult(forces, u);
                        pool += atk * AvgTriangleVs(u.UnitType, byFaction[g]);
                    }
                    pool *= SalvoScale;
                    // The defender (region owner) divides incoming by terrain COVER × FORTIFICATION (its Bunkers, local
                    // + adjacent-friendly projection — GroundFortification, design-driven).
                    if (g == defenderFaction)
                        pool /= (GroundTerrain.CoverDefenseMult(terrain) * GroundFortification.DefenseMult(region, allRegions, defenderFaction, fortResolve));
                    incoming[g] += pool;
                }
            }

            // Apply each faction's incoming, focus-fired across its units (pool carries — no overkill waste). A unit's
            // formation STANCE scales the DAMAGE IT TAKES (defensive soaks more raw pool per point of health; offensive
            // dies faster) — the ground echo of the space resolver's per-ship ToughnessMult, 5h doctrine.
            foreach (var g in byFaction.Keys)
            {
                double pool = incoming[g];
                if (pool <= 0) continue;
                foreach (var u in byFaction[g])
                {
                    if (u.Health <= 0) continue;
                    if (pool <= 0) break;
                    double dtm = GroundFormationDoctrine.DamageTakenMult(forces, u);
                    if (dtm <= 0) dtm = 1.0;
                    // Raw pool this unit can absorb before dying = Health / dtm; the health it loses = raw × dtm.
                    double raw = Math.Min(u.Health / dtm, pool);
                    u.Health -= raw * dtm;
                    pool -= raw;
                }
            }
        }

        /// <summary>Health-weighted average triangle multiplier of an attacker TYPE against an enemy faction's mix —
        /// so bringing the right counter to their composition pays off.</summary>
        private static double AvgTriangleVs(GroundUnitType attackerType, List<GroundUnit> enemies)
        {
            double totalH = 0.0, acc = 0.0;
            foreach (var t in enemies)
            {
                if (t.Health <= 0) continue;
                totalH += t.Health;
                acc += t.Health * GroundTerrain.TriangleMult(attackerType, t.UnitType);
            }
            return totalH > 0 ? acc / totalH : 1.0;
        }

        /// <summary>Walk a unit along its plotted HEX path (H2): count down the current step's time and, each time it
        /// elapses, step INTO the next hex — updating its region + (q,r) — carrying any overshoot into the following
        /// step (so a long tick can cross several hexes at once, and a full march totals the derived crossing time).
        /// When the last hex is reached the path is consumed and the unit stops (MovingToRegion cleared).</summary>
        private static void AdvanceHexPath(GroundUnit unit, int deltaSeconds)
        {
            unit.TransitSecondsRemaining -= deltaSeconds;
            while (unit.Path.Count > 0 && unit.TransitSecondsRemaining <= 0)
            {
                var step = unit.Path[0];
                unit.RegionIndex = step.Region;
                unit.HexQ = step.Q;
                unit.HexR = step.R;
                unit.Path.RemoveAt(0);
                if (unit.Path.Count > 0)
                    unit.TransitSecondsRemaining += unit.Path[0].Seconds;   // carry the overshoot into the next hop
                else
                {
                    unit.TransitSecondsRemaining = 0;
                    unit.MovingToRegion = -1;   // arrived — no longer in transit
                }
            }
        }

        /// <summary>Keep each formation's leader valid after casualties: if the leader unit is gone, leadership passes
        /// to a surviving member (or -1 if the formation was wiped). Fleet-like reassignment, no penalty.</summary>
        private static void MaintainFormations(GroundForcesDB forces)
        {
            if (forces.Formations == null || forces.Formations.Count == 0) return;
            foreach (var f in forces.Formations)
            {
                bool leaderAlive = false;
                int firstMemberId = -1;
                foreach (var u in forces.Units)
                {
                    if (u.FormationId != f.FormationId) continue;
                    if (firstMemberId < 0) firstMemberId = u.UnitId;
                    if (u.UnitId == f.LeaderUnitId) { leaderAlive = true; break; }
                }
                if (!leaderAlive) f.LeaderUnitId = firstMemberId;   // -1 if the formation is now empty
            }
        }

        private static bool IsDamageEffect(HazardEffectType t)
            => t == HazardEffectType.HeatDamage || t == HazardEffectType.RadiationDamage
            || t == HazardEffectType.KineticDamage || t == HazardEffectType.CorrosiveDamage
            || t == HazardEffectType.EMDamage || t == HazardEffectType.GravimetricDamage;

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
