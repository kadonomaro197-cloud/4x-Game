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

        // --- FORTIFICATION (5h): buildings in a region make its DEFENDER (the region owner) harder to dislodge — the
        //     "fortify your capital / dig in" decision, and the combat payoff for placing a base via the tactical map
        //     (each map-placed installation lands in Region.InstallationIds). A modest, CAPPED edge (like terrain
        //     cover), not a wall. v1 counts ALL located installations equally; a dedicated ground-defence component
        //     attribute (a real bunker/bastion, so a solar panel doesn't fortify) is the depth pass. ---
        public const double FortifyPerBuilding = 0.15;   // each located installation: +15% defender protection
        public const double FortifyMaxBonus = 1.0;       // capped at +100% (a fully built-up region halves incoming)

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
                    ResolveRegionCombat(units, region);
                }

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

        /// <summary>One salvo in a contested region — TERRAIN- and TYPE-aware (5f/5g). Each faction takes the
        /// combined attack of all others, but each attacker's damage is scaled by the weapon TRIANGLE (vs the enemy's
        /// composition) and its TERRAIN affinity (armour bad in rough, artillery loves high ground); the region's
        /// OWNER (the defender) additionally divides its incoming by the terrain COVER. Simultaneous (snapshot before
        /// apply), deterministic, no overkill (pool carries). Reads <see cref="GroundTerrain"/> — the ground twin of
        /// SpaceHazardTools.</summary>
        private static void ResolveRegionCombat(List<GroundUnit> units, Region region)
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
                        double atk = u.Attack * GroundTerrain.TerrainAttackMult(u.UnitType, terrain);
                        pool += atk * AvgTriangleVs(u.UnitType, byFaction[g]);
                    }
                    pool *= SalvoScale;
                    // The defender (region owner) divides incoming by terrain COVER × FORTIFICATION (its buildings).
                    if (g == defenderFaction) pool /= (GroundTerrain.CoverDefenseMult(terrain) * FortificationDefenseMult(region));
                    incoming[g] += pool;
                }
            }

            // Apply each faction's incoming, focus-fired across its units (pool carries — no overkill waste).
            foreach (var g in byFaction.Keys)
            {
                double pool = incoming[g];
                if (pool <= 0) continue;
                foreach (var u in byFaction[g])
                {
                    if (u.Health <= 0) continue;
                    if (pool <= 0) break;
                    double take = Math.Min(u.Health, pool);
                    u.Health -= take;
                    pool -= take;
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

        /// <summary>The DEFENDER's fortification multiplier from the buildings located in this region (5h): its
        /// incoming damage is divided by this, so a built-up region is a fortress. 1.0 = no buildings; capped so it's
        /// an edge, not an impregnable wall. Public so the gauge can assert the curve directly.</summary>
        public static double FortificationDefenseMult(Region region)
        {
            if (region == null || region.InstallationIds == null || region.InstallationIds.Count == 0) return 1.0;
            double bonus = Math.Min(FortifyMaxBonus, region.InstallationIds.Count * FortifyPerBuilding);
            return 1.0 + bonus;
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
