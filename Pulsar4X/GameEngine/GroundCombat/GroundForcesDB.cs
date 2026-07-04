using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;
using Pulsar4X.Hazards;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// The kind of ground unit — the ground echo of the space weapon triangle (slice 5g gives these a
    /// rock-paper-scissors edge). v1 just tags the unit; the triangle math wires in later.
    /// </summary>
    public enum GroundUnitType : byte
    {
        Infantry,   // cheap, holds ground, the baseline
        Armor,      // shock — strong in the open, weak in rough terrain
        Artillery   // reach — hurts from range, fragile up close
    }

    /// <summary>
    /// One raised ground unit sitting in a region of a planet. A serializable DATA object held in
    /// <see cref="GroundForcesDB"/> (NOT a full entity in v1 — like <see cref="Pulsar4X.Galaxy.RegionFeature"/>),
    /// so a garrison of many units stays cheap and save-safe. Carries its own combat stats (a SNAPSHOT of the
    /// design at build time, the way a ship caches its <c>ShipCombatValueDB</c>) so the ground resolver (slice 5c)
    /// reads them directly without a design lookup.
    /// </summary>
    public class GroundUnit
    {
        /// <summary>A stable id for this unit within its body's roster (the ground echo of a ship's entity Id) —
        /// assigned by <see cref="GroundForces.RaiseUnit"/>. Lets a <see cref="GroundFormation"/> reference its leader
        /// + members by id, the way a <c>FleetDB</c> references ships by entity id. 0 = not yet assigned.</summary>
        [JsonProperty] public int UnitId { get; internal set; }
        /// <summary>The <see cref="GroundFormation"/> this unit belongs to (-1 = unformed) — the ground echo of a ship
        /// holding its parent-fleet id (fleet membership lives on the SHIP side; formation membership lives here).</summary>
        [JsonProperty] public int FormationId { get; internal set; } = -1;
        [JsonProperty] public string DesignId { get; internal set; }
        [JsonProperty] public string Name { get; internal set; }
        /// <summary>Which faction owns this unit — capture flips this (slice 5d), same primitive as a ship.</summary>
        [JsonProperty] public int FactionOwnerID { get; internal set; }
        /// <summary>The region (index into the body's <see cref="Pulsar4X.Galaxy.PlanetRegionsDB"/>) it stands in.</summary>
        [JsonProperty] public int RegionIndex { get; internal set; }
        [JsonProperty] public GroundUnitType UnitType { get; internal set; }
        /// <summary>Hurt dealt per combat round — the ground "Firepower".</summary>
        [JsonProperty] public double Attack { get; internal set; }
        /// <summary>Damage mitigation (v1: a hook the resolver will read; 0 = none).</summary>
        [JsonProperty] public double Defense { get; internal set; }
        /// <summary>Punishment it can absorb — the ground "Toughness".</summary>
        [JsonProperty] public double MaxHealth { get; internal set; }
        /// <summary>Current health; a fresh unit starts full, combat whittles it, 0 = destroyed (slice 5c/5d).</summary>
        [JsonProperty] public double Health { get; internal set; }
        /// <summary>The region this unit is MARCHING to (-1 = standing still). While in transit it doesn't fight (5b).</summary>
        [JsonProperty] public int MovingToRegion { get; internal set; } = -1;
        /// <summary>Game-seconds left in the current march; counts down to 0 = arrived (the region's crossing time).</summary>
        [JsonProperty] public double TransitSecondsRemaining { get; internal set; }

        // ── HEX POSITION (H2) — where the unit stands WITHIN its region's hex patch (Planet → Region → Hex). ──
        /// <summary>Axial Q of the hex this unit occupies in <see cref="RegionIndex"/>'s patch (0,0 = patch centre,
        /// where units muster). The fine-grained position the hex pathfinder plots over (H2).</summary>
        [JsonProperty] public int HexQ { get; internal set; }
        /// <summary>Axial R of the hex this unit occupies in its region's patch.</summary>
        [JsonProperty] public int HexR { get; internal set; }
        /// <summary>How this unit crosses ground — the snapshot that makes terrain cost UNIT-dependent (a tank bogs in
        /// mountains and can't cross ocean; an aircraft flies straight). Snapshotted from the design at raise time.</summary>
        [JsonProperty] public MovementDomain Domain { get; internal set; } = MovementDomain.Land;
        /// <summary>The remaining HEX route this unit is walking (H2b) — the queued waypoints from the pathfinder, front =
        /// the next hex to enter. Non-empty ⇒ the unit is marching hex-by-hex (<see cref="MovingToRegion"/> is set to the
        /// destination region); <c>GroundForcesProcessor</c> pops the front as each hex is reached. null/empty = standing still.</summary>
        [JsonProperty] public List<HexWaypoint> Path { get; internal set; }

        /// <summary>
        /// ENVIRONMENTAL GEAR (E4) — the ground echo of a ship's <c>HazardResistanceAtb</c>: per-hazard-effect
        /// protection this unit carries (heat-shielding, hazmat sealing, mountaineering rig…), keyed by the SHARED
        /// <see cref="HazardEffectType"/> vocabulary. Value 0..1 = the FRACTION of that hazard's attrition negated
        /// (0 = none, 1 = immune). A snapshot from the design at build time (like Attack/HP), so a fielded unit's
        /// protection is fixed — you re-equip by building a better-geared design. null / empty = unprotected.
        /// Consumed by <c>GroundForcesProcessor</c>'s environmental-attrition step.
        /// </summary>
        [JsonProperty] public Dictionary<HazardEffectType, double> EnvResistance { get; internal set; }

        /// <summary>Fraction (0..1) of <paramref name="effect"/>'s attrition this unit's gear negates (0 if none).</summary>
        public double ResistanceTo(HazardEffectType effect)
        {
            if (EnvResistance != null && EnvResistance.TryGetValue(effect, out var r))
                return r < 0 ? 0 : (r > 1 ? 1 : r);
            return 0;
        }

        public GroundUnit() { }
        public GroundUnit(GroundUnit o)
        {
            UnitId = o.UnitId; FormationId = o.FormationId;
            DesignId = o.DesignId; Name = o.Name; FactionOwnerID = o.FactionOwnerID; RegionIndex = o.RegionIndex;
            UnitType = o.UnitType; Attack = o.Attack; Defense = o.Defense; MaxHealth = o.MaxHealth; Health = o.Health;
            MovingToRegion = o.MovingToRegion; TransitSecondsRemaining = o.TransitSecondsRemaining;
            HexQ = o.HexQ; HexR = o.HexR; Domain = o.Domain;
            if (o.Path != null)
            {
                Path = new List<HexWaypoint>(o.Path.Count);
                foreach (var w in o.Path) Path.Add(new HexWaypoint(w));
            }
            if (o.EnvResistance != null) EnvResistance = new Dictionary<HazardEffectType, double>(o.EnvResistance);
        }
    }

    /// <summary>
    /// A named GROUPING of <see cref="GroundUnit"/>s that move and fight as one — the ground echo of a <c>FleetDB</c>,
    /// mirroring its SHAPE within the data-object model (units aren't entities in v1, so a formation is a serializable
    /// data object too, not an entity with a tree). Like a fleet it has a NAME, a LEADER (the ground echo of the
    /// flagship — <see cref="LeaderUnitId"/>), and MEMBERS (tracked on the unit side via <see cref="GroundUnit.FormationId"/>,
    /// exactly as a ship holds its parent-fleet id). One order marches the whole block (<see cref="GroundForces.OrderFormationMove"/>).
    ///
    /// Deliberately mirrors the fleet's CORE grouping; the layers a fleet adds on top — a DOCTRINE/stance with combat
    /// multipliers (<c>FleetDoctrineDB</c>) and nesting SUB-formations (the fleet tree) — are follow-up formation slices
    /// (each its own gauged step), not folded in here. Design: docs/GROUND-COMBAT-MAP-DESIGN.md (slice 5h formations).
    /// </summary>
    public class GroundFormation
    {
        /// <summary>Stable id within the roster (the ground echo of the fleet entity id).</summary>
        [JsonProperty] public int FormationId { get; internal set; }
        /// <summary>Player-facing name — "1st Armoured", "Home Guard".</summary>
        [JsonProperty] public string Name { get; internal set; }
        [JsonProperty] public int FactionOwnerID { get; internal set; }
        /// <summary>The leader unit's <see cref="GroundUnit.UnitId"/> — the ground echo of <c>FleetDB.FlagShipID</c>.
        /// -1 = no leader (empty formation). On the leader's death it REASSIGNS to a surviving member (fleet-like), no
        /// combat penalty — see <c>GroundForcesProcessor</c>.</summary>
        [JsonProperty] public int LeaderUnitId { get; internal set; } = -1;

        // ── STANCE (the ground echo of FleetDoctrineDB) — set from the moddable GroundStance catalog via
        //    GroundFormationDoctrine.TrySetStance; read-time mults on the resolver, so switching is reversible. ──
        /// <summary>The active stance's catalog id ("" = none = Balanced/neutral).</summary>
        [JsonProperty] public string StanceId { get; internal set; } = "";
        /// <summary>Offensive | Defensive | Balanced — the stance family (for the readout).</summary>
        [JsonProperty] public string StanceFamily { get; internal set; } = "";
        /// <summary>Multiplier on this formation's units' ATTACK (1.0 = neutral).</summary>
        [JsonProperty] public double AttackMult { get; internal set; } = 1.0;
        /// <summary>Multiplier on the DAMAGE this formation's units TAKE (1.0 = neutral; &gt;1 = more, &lt;1 = less).</summary>
        [JsonProperty] public double DamageTakenMult { get; internal set; } = 1.0;
        /// <summary>Game time at/after which this formation may switch stance again (the switch cooldown clock).</summary>
        [JsonProperty] public DateTime SwitchableAfter { get; internal set; } = DateTime.MinValue;

        public GroundFormation() { }
        public GroundFormation(GroundFormation o)
        {
            FormationId = o.FormationId; Name = o.Name; FactionOwnerID = o.FactionOwnerID; LeaderUnitId = o.LeaderUnitId;
            StanceId = o.StanceId; StanceFamily = o.StanceFamily; AttackMult = o.AttackMult; DamageTakenMult = o.DamageTakenMult;
            SwitchableAfter = o.SwitchableAfter;
        }
    }

    /// <summary>
    /// A planet's ground forces — the roster of <see cref="GroundUnit"/>s standing on its surface. Attached to the
    /// PLANET body entity (the parallel to <see cref="Pulsar4X.Galaxy.PlanetRegionsDB"/>: forces are OF the planet,
    /// so an unowned world can hold a defending garrison to fight over). Each unit knows its own region + owner, so
    /// one roster covers a contested world with both sides present. Fully persistent (<see cref="Clone"/> +
    /// [JsonProperty] + deep-copy ctors) from day one — the discipline the old colony hex map lacked.
    ///
    /// Design: docs/GROUND-COMBAT-MAP-DESIGN.md (slice 5a).
    /// </summary>
    public class GroundForcesDB : BaseDataBlob
    {
        [JsonProperty] public List<GroundUnit> Units { get; internal set; } = new List<GroundUnit>();
        /// <summary>The body's ground FORMATIONS (the ground echo of a faction's fleets). Members are tracked on the
        /// unit side (<see cref="GroundUnit.FormationId"/>), so this holds only the formation records themselves.</summary>
        [JsonProperty] public List<GroundFormation> Formations { get; internal set; } = new List<GroundFormation>();
        /// <summary>Next stable <see cref="GroundUnit.UnitId"/> to hand out (save-safe id seed, mirrors the entity id generator).</summary>
        [JsonProperty] public int NextUnitId { get; internal set; } = 1;
        /// <summary>Next stable <see cref="GroundFormation.FormationId"/> to hand out.</summary>
        [JsonProperty] public int NextFormationId { get; internal set; } = 1;

        public GroundForcesDB() { }
        public GroundForcesDB(GroundForcesDB other)
        {
            Units = new List<GroundUnit>();
            foreach (var u in other.Units) Units.Add(new GroundUnit(u));
            Formations = new List<GroundFormation>();
            foreach (var f in other.Formations) Formations.Add(new GroundFormation(f));
            NextUnitId = other.NextUnitId;
            NextFormationId = other.NextFormationId;
        }

        public override object Clone() => new GroundForcesDB(this);
    }

    /// <summary>The "place a raised unit on the surface" primitive — the ground echo of a ship being launched into a
    /// system. Creates the body's <see cref="GroundForcesDB"/> on demand.</summary>
    public static class GroundForces
    {
        /// <summary>
        /// Raise one unit from a design onto <paramref name="body"/>'s region <paramref name="regionIndex"/>, owned by
        /// <paramref name="factionId"/>. Returns the placed unit. Creates the ground-forces roster if the body lacks one.
        /// </summary>
        public static GroundUnit RaiseUnit(Entity body, GroundUnitDesign design, int factionId, int regionIndex, string name = null)
        {
            if (!body.TryGetDataBlob<GroundForcesDB>(out var forces))
            {
                forces = new GroundForcesDB();
                body.SetDataBlob(forces);
            }

            var unit = new GroundUnit
            {
                DesignId = design.UniqueID,
                Name = name ?? design.Name,
                FactionOwnerID = factionId,
                RegionIndex = regionIndex,
                UnitType = design.UnitType,
                Domain = design.Domain,           // snapshot the movement domain (H2) — like the combat stats
                Attack = design.Attack,
                Defense = design.Defense,
                MaxHealth = design.HitPoints,
                Health = design.HitPoints,
                // Snapshot the design's environmental gear onto the unit (E4) — like the combat stats above.
                EnvResistance = (design.EnvironmentalResistance != null && design.EnvironmentalResistance.Count > 0)
                    ? new Dictionary<HazardEffectType, double>(design.EnvironmentalResistance)
                    : null,
            };
            unit.UnitId = forces.NextUnitId++;   // stable id (the ground echo of a ship's entity id)
            forces.Units.Add(unit);
            return unit;
        }

        /// <summary>
        /// Order a unit to MARCH to an ADJACENT region (5b). Sets the transit clock to the current region's
        /// crossing time — the "units take thousands of miles / logical time" datum. Returns false (no move) if the
        /// body has no region layer, the target is out of range, it's the same region, or the target is not a
        /// neighbour (v1: one hop at a time along the ring; multi-hop pathing is a later refinement).
        /// </summary>
        public static bool OrderMove(Entity body, GroundUnit unit, int toRegion)
        {
            if (unit == null) return false;
            if (!body.TryGetDataBlob<Pulsar4X.Galaxy.PlanetRegionsDB>(out var regionsDB)) return false;
            var regions = regionsDB.Regions;
            if (unit.RegionIndex < 0 || unit.RegionIndex >= regions.Count) return false;
            if (toRegion < 0 || toRegion >= regions.Count || toRegion == unit.RegionIndex) return false;
            if (!regions[unit.RegionIndex].Neighbors.Contains(toRegion)) return false;   // must be adjacent

            unit.MovingToRegion = toRegion;
            unit.TransitSecondsRemaining = regions[unit.RegionIndex].CrossingTimeSeconds;
            return true;
        }

        /// <summary>
        /// Order a unit to march to a specific HEX — <paramref name="toQ"/>,<paramref name="toR"/> in region
        /// <paramref name="toRegion"/> (H2). Plots a terrain-weighted A* route from the unit's current hex
        /// (<see cref="HexPathfinder"/>, honouring its <see cref="GroundUnit.Domain"/>), stores it as the unit's
        /// <see cref="GroundUnit.Path"/>, and the processor walks it hex-by-hex over ticks — the London→Paris transit,
        /// crossing region borders as the route requires. Returns false if there's no region layer or no route exists
        /// (e.g. a land unit asked to reach an ocean-locked hex). This is the fine-grained twin of the region-hop
        /// <see cref="OrderMove(Entity, GroundUnit, int)"/> overload (kept as the coarse fallback).
        /// </summary>
        public static bool OrderMove(Entity body, GroundUnit unit, int toRegion, int toQ, int toR)
        {
            if (unit == null) return false;
            if (!body.TryGetDataBlob<Pulsar4X.Galaxy.PlanetRegionsDB>(out var regionsDB)) return false;

            var steps = HexPathfinder.FindPath(regionsDB, unit.RegionIndex, unit.HexQ, unit.HexR,
                toRegion, toQ, toR, unit.Domain);
            if (steps.Count == 0) return false;

            var path = new List<HexWaypoint>(steps.Count);
            foreach (var s in steps) path.Add(new HexWaypoint(s.RegionIndex, s.Q, s.R, s.Seconds));
            unit.Path = path;
            unit.TransitSecondsRemaining = path[0].Seconds;
            unit.MovingToRegion = toRegion;   // in-transit flag (the final destination region)
            return true;
        }

        // ───────────────────────── FORMATIONS (the ground echo of fleet grouping) ─────────────────────────
        // Mirrors the FleetOrder verbs (Create / AssignShip / UnassignShip / SetFlagShip / Disband), one level over
        // from entities to data objects. Membership lives on the unit (GroundUnit.FormationId), like a ship's parent
        // fleet id; the formation record holds the name + leader (the flagship echo).

        /// <summary>Create a named, empty formation on <paramref name="body"/> (the ground echo of
        /// <c>FleetFactory.Create</c>). Creates the roster on demand and hands out a stable FormationId.</summary>
        public static GroundFormation CreateFormation(Entity body, int factionId, string name)
        {
            if (!body.TryGetDataBlob<GroundForcesDB>(out var forces))
            {
                forces = new GroundForcesDB();
                body.SetDataBlob(forces);
            }
            var formation = new GroundFormation
            {
                FormationId = forces.NextFormationId++,
                Name = string.IsNullOrWhiteSpace(name) ? $"Formation {forces.NextFormationId - 1}" : name,
                FactionOwnerID = factionId,
            };
            forces.Formations.Add(formation);
            return formation;
        }

        /// <summary>Add a unit to a formation (the ground echo of <c>FleetOrder.AssignShip</c>). The FIRST unit
        /// assigned becomes the leader (the flagship default). Only same-faction units join (a formation is one
        /// faction's, like a fleet). Returns false if the unit is null or a different faction.</summary>
        public static bool AssignUnit(GroundFormation formation, GroundUnit unit)
        {
            if (formation == null || unit == null) return false;
            if (unit.FactionOwnerID != formation.FactionOwnerID) return false;
            unit.FormationId = formation.FormationId;
            if (formation.LeaderUnitId < 0) formation.LeaderUnitId = unit.UnitId;   // first in = flagship
            return true;
        }

        /// <summary>Remove a unit from its formation (the ground echo of <c>FleetOrder.UnassignShip</c>). If it was the
        /// leader, leadership passes to another member (fleet-like), or -1 if the formation is now empty.</summary>
        public static void UnassignUnit(GroundForcesDB forces, GroundFormation formation, GroundUnit unit)
        {
            if (formation == null || unit == null) return;
            unit.FormationId = -1;
            if (formation.LeaderUnitId == unit.UnitId)
                formation.LeaderUnitId = FirstMemberId(forces, formation.FormationId);
        }

        /// <summary>Set the formation's leader to a member unit (the ground echo of <c>FleetOrder.SetFlagShip</c>).
        /// No-op if the unit isn't a member of this formation.</summary>
        public static bool SetLeader(GroundFormation formation, GroundUnit unit)
        {
            if (formation == null || unit == null || unit.FormationId != formation.FormationId) return false;
            formation.LeaderUnitId = unit.UnitId;
            return true;
        }

        /// <summary>Disband a formation (the ground echo of <c>FleetOrder.Disband</c>): its members become unformed,
        /// and the record is removed. The units themselves are untouched.</summary>
        public static void DisbandFormation(GroundForcesDB forces, GroundFormation formation)
        {
            if (forces == null || formation == null) return;
            foreach (var u in forces.Units)
                if (u.FormationId == formation.FormationId) u.FormationId = -1;
            forces.Formations.Remove(formation);
        }

        /// <summary>March a whole formation ONE hop as a block (the ground echo of a fleet move order): every member
        /// standing with the LEADER (in the leader's region, not already in transit) is ordered to <paramref name="toRegion"/>
        /// via <see cref="OrderMove"/> (which validates adjacency). Members separated from the leader don't move (v1 —
        /// keeping the block together is the "formation moves as one" contract). Returns how many units marched.</summary>
        public static int OrderFormationMove(Entity body, GroundFormation formation, int toRegion)
        {
            if (formation == null || !body.TryGetDataBlob<GroundForcesDB>(out var forces)) return 0;
            int rallyRegion = LeaderRegion(forces, formation);
            if (rallyRegion < 0) return 0;

            int moved = 0;
            foreach (var u in forces.Units.ToArray())
            {
                if (u.FormationId != formation.FormationId) continue;
                if (u.RegionIndex != rallyRegion || u.MovingToRegion >= 0) continue;
                if (OrderMove(body, u, toRegion)) moved++;
            }
            return moved;
        }

        /// <summary>March a whole formation to a target HEX (H2) — the fine-grained twin of the region-hop
        /// <see cref="OrderFormationMove(Entity, GroundFormation, int)"/>. Every member standing with the LEADER
        /// (in the leader's region, not already moving) is given its OWN terrain-weighted A* route to
        /// <paramref name="toRegion"/>,<paramref name="toQ"/>,<paramref name="toR"/> (each from its own hex, so the
        /// block converges on the objective), transiting hex-by-hex over ticks. Returns how many members marched.</summary>
        public static int OrderFormationMove(Entity body, GroundFormation formation, int toRegion, int toQ, int toR)
        {
            if (formation == null || !body.TryGetDataBlob<GroundForcesDB>(out var forces)) return 0;
            int rallyRegion = LeaderRegion(forces, formation);
            if (rallyRegion < 0) return 0;

            int moved = 0;
            foreach (var u in forces.Units.ToArray())
            {
                if (u.FormationId != formation.FormationId) continue;
                if (u.RegionIndex != rallyRegion || u.MovingToRegion >= 0 || (u.Path != null && u.Path.Count > 0)) continue;
                if (OrderMove(body, u, toRegion, toQ, toR)) moved++;
            }
            return moved;
        }

        /// <summary>The region the formation's LEADER stands in (its rally point) — or the first member's region if the
        /// leader is unset/missing, or -1 if the formation has no members.</summary>
        public static int LeaderRegion(GroundForcesDB forces, GroundFormation formation)
        {
            if (forces == null || formation == null) return -1;
            GroundUnit first = null;
            foreach (var u in forces.Units)
            {
                if (u.FormationId != formation.FormationId) continue;
                if (first == null) first = u;
                if (u.UnitId == formation.LeaderUnitId) return u.RegionIndex;
            }
            return first?.RegionIndex ?? -1;
        }

        private static int FirstMemberId(GroundForcesDB forces, int formationId)
        {
            if (forces == null) return -1;
            foreach (var u in forces.Units)
                if (u.FormationId == formationId) return u.UnitId;
            return -1;
        }
    }

    /// <summary>Read-only helpers for the formation layer — the ground echo of <c>FleetTools</c> (the client draws +
    /// commands formations through these). Pure queries, defensive; no mutation.</summary>
    public static class GroundFormationTools
    {
        /// <summary>Every unit in a formation.</summary>
        public static List<GroundUnit> MembersOf(GroundForcesDB forces, GroundFormation formation)
        {
            var list = new List<GroundUnit>();
            if (forces == null || formation == null) return list;
            foreach (var u in forces.Units)
                if (u.FormationId == formation.FormationId) list.Add(u);
            return list;
        }

        /// <summary>How many units are in a formation.</summary>
        public static int MemberCount(GroundForcesDB forces, GroundFormation formation)
        {
            int n = 0;
            if (forces == null || formation == null) return 0;
            foreach (var u in forces.Units)
                if (u.FormationId == formation.FormationId) n++;
            return n;
        }

        /// <summary>The leader unit of a formation, or null if unset/missing.</summary>
        public static GroundUnit Leader(GroundForcesDB forces, GroundFormation formation)
        {
            if (forces == null || formation == null || formation.LeaderUnitId < 0) return null;
            foreach (var u in forces.Units)
                if (u.UnitId == formation.LeaderUnitId) return u;
            return null;
        }

        /// <summary>A faction's formations on this body.</summary>
        public static List<GroundFormation> FormationsFor(GroundForcesDB forces, int factionId)
        {
            var list = new List<GroundFormation>();
            if (forces == null) return list;
            foreach (var f in forces.Formations)
                if (f.FactionOwnerID == factionId) list.Add(f);
            return list;
        }
    }
}
