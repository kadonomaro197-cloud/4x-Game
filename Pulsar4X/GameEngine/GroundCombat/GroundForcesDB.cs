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
        /// <summary>Strike RANGE in HEXES (H3) — max hex-distance this unit can hit an enemy (snapshot of the design's
        /// <c>Range</c>). Directed fire: a unit only damages enemies within this reach, so a longer-ranged unit hits a
        /// closing shorter-ranged one without being hit back. 0 = same hex only.</summary>
        [JsonProperty] public int Range { get; internal set; }
        /// <summary>SYSTEM ① survivability-by-dodge — chance to avoid a hit (0..1), snapshot of the design's Σ augment
        /// evasion. Carried now (slice B plumbing); the resolver consumes it in the damage×defence matrix (slice A).
        /// A dodger (Jedi / Zergling) is high here; a walking bunker is ~0.</summary>
        [JsonProperty] public double Evasion { get; internal set; }
        /// <summary>SYSTEM ① survivability-by-shield — a flat incoming-damage soak pool (energy shield / Force ward),
        /// snapshot of Σ augment shield. Carried now; consumed in slice A.</summary>
        [JsonProperty] public double Shield { get; internal set; }
        /// <summary>SYSTEM ① — this unit's primary damage flavour (from its heaviest weapon), for the future
        /// damage×defence matrix. Carried now; consumed in slice A.</summary>
        [JsonProperty] public GroundWeaponMode DamageType { get; internal set; } = GroundWeaponMode.Ballistic;
        /// <summary>The region this unit is MARCHING to (-1 = standing still). While in transit it doesn't fight (5b).</summary>
        [JsonProperty] public int MovingToRegion { get; internal set; } = -1;
        /// <summary>Game-seconds left in the current march; counts down to 0 = arrived (the region's crossing time).</summary>
        [JsonProperty] public double TransitSecondsRemaining { get; internal set; }

        // ── HEX POSITION + FINE MOVEMENT (H2) — where the unit stands WITHIN its region's hex patch, and its
        //    hex-by-hex march. The coarse region march above (MovingToRegion) hops whole regions; this walks the fine
        //    grid inside one. A unit is raised at the patch centre (0,0). Design: docs/HEX-GROUND-AND-ORDERS-DESIGN.md.
        /// <summary>Axial Q of the hex this unit stands on within its region's patch (patch centre = 0,0).</summary>
        [JsonProperty] public int HexQ { get; internal set; }
        /// <summary>Axial R of the hex this unit stands on within its region's patch.</summary>
        [JsonProperty] public int HexR { get; internal set; }
        /// <summary>The remaining hex STEPS of a fine march (ordered, current→destination), each a deep copy carrying its
        /// terrain so the processor can time the step without a lookup. null / empty = not hex-marching. Set by
        /// <see cref="GroundForces.OrderMoveToHex"/>, walked down by <c>GroundForcesProcessor</c>.</summary>
        [JsonProperty] public List<Pulsar4X.Galaxy.GroundHex> HexPath { get; internal set; }
        /// <summary>Game-seconds left to reach the FRONT hex of <see cref="HexPath"/> (counts to 0 = that hex reached).</summary>
        [JsonProperty] public double HexTransitSecondsRemaining { get; internal set; }
        /// <summary>Per-open-hex base crossing time for the region this march runs in (captured at order time from the
        /// region's crossing-time datum). A step's time = this × the entered hex's terrain move-multiplier. Stable for
        /// the march because a fine march stays within one region.</summary>
        [JsonProperty] public double HexStepBaseSeconds { get; internal set; }

        // ── GLOBAL GRID POSITION + MOVEMENT (G-track, G3) — the unit's place on the ONE continuous cylinder
        //    (Q = longitude column, R = latitude row; region = a column BAND) and its global hex march via
        //    HexPathfinder.FindGlobalPath (no edge gates — crossing a band border is just the next column). ADDITIVE
        //    alongside the per-region HexQ/HexR above during the migration. Design: docs/GLOBAL-HEX-GRID-DESIGN.md.
        /// <summary>Global longitude column on the body's <c>SurfaceGrid</c> (-1 until placed on the grid).</summary>
        [JsonProperty] public int GlobalQ { get; internal set; } = -1;
        /// <summary>Global latitude row on the body's <c>SurfaceGrid</c>.</summary>
        [JsonProperty] public int GlobalR { get; internal set; } = -1;
        /// <summary>Remaining GLOBAL hex steps (current→destination) of a cylinder march; null/empty = not global-marching.</summary>
        [JsonProperty] public List<Pulsar4X.Galaxy.GroundHex> GlobalPath { get; internal set; }
        /// <summary>Game-seconds left to reach the FRONT hex of <see cref="GlobalPath"/>.</summary>
        [JsonProperty] public double GlobalTransitSecondsRemaining { get; internal set; }
        /// <summary>Per-open-hex base crossing time for the global march (from the region band's crossing-time datum).</summary>
        [JsonProperty] public double GlobalStepBaseSeconds { get; internal set; }

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
            UnitType = o.UnitType; Attack = o.Attack; Defense = o.Defense; MaxHealth = o.MaxHealth; Health = o.Health; Range = o.Range;
            Evasion = o.Evasion; Shield = o.Shield; DamageType = o.DamageType;
            MovingToRegion = o.MovingToRegion; TransitSecondsRemaining = o.TransitSecondsRemaining;
            HexQ = o.HexQ; HexR = o.HexR; HexTransitSecondsRemaining = o.HexTransitSecondsRemaining; HexStepBaseSeconds = o.HexStepBaseSeconds;
            if (o.HexPath != null)
            {
                HexPath = new List<Pulsar4X.Galaxy.GroundHex>();
                foreach (var h in o.HexPath) HexPath.Add(new Pulsar4X.Galaxy.GroundHex(h));
            }
            GlobalQ = o.GlobalQ; GlobalR = o.GlobalR;
            GlobalTransitSecondsRemaining = o.GlobalTransitSecondsRemaining; GlobalStepBaseSeconds = o.GlobalStepBaseSeconds;
            if (o.GlobalPath != null)
            {
                GlobalPath = new List<Pulsar4X.Galaxy.GroundHex>();
                foreach (var h in o.GlobalPath) GlobalPath.Add(new Pulsar4X.Galaxy.GroundHex(h));
            }
            if (o.EnvResistance != null) EnvResistance = new Dictionary<HazardEffectType, double>(o.EnvResistance);
        }
    }

    /// <summary>
    /// A formation's RULES OF ENGAGEMENT — the movement intent a commander sets, the ground echo of the space
    /// CLOSING model (docs/FLEET-COMBAT-CLOSING-DESIGN.md: a fast long-range fleet kites, a brawler forces the merge).
    /// It tells the surface processor how a formation should MANEUVER relative to the enemy each tick, so the H3 range
    /// advantage is used automatically instead of by micro:
    /// </summary>
    public enum GroundEngagementStance : byte
    {
        /// <summary>Hold ground — stand and fight where you are (the default; no auto-movement).</summary>
        HoldGround,
        /// <summary>Close to the enemy — advance toward the nearest enemy until in your own strike range (the brawler /
        /// the zerg rush). A short-ranged unit uses this to CLOSE the gap a longer-ranged enemy is exploiting.</summary>
        CloseToEngage,
        /// <summary>Stand off — keep the enemy at arm's length: back away when an enemy is within ITS range, so a
        /// longer-ranged unit hits from beyond the enemy's reach (the clone kiting the zerg). Auto-kite.</summary>
        StandOff,
    }

    /// <summary>The kind of queued formation order (O1). A formation carries an ordered LIST of these and executes them
    /// one at a time, in sequence — a real "then" waypoint chain ("move to London, THEN move to Paris, THEN dig in"),
    /// the thing the fleet action-lane model doesn't give. New order kinds are added here.</summary>
    public enum GroundOrderType : byte
    {
        MoveToHex,      // march the formation to a hex within its region (fine grid)
        MoveToRegion,   // march the formation to an adjacent region (coarse ring hop)
        HoldFor,        // hold position for a set number of game-seconds (a timed wait / dig-in pause)
        SetStance,      // switch the formation's combat stance (from the GroundStance catalog)
        SetEngagement,  // switch the formation's ROE (Hold / Close / Stand-off)
    }

    /// <summary>
    /// One queued order for a <see cref="GroundFormation"/> (O1) — the ground echo of an <c>EntityCommand</c>, kept as a
    /// save-safe DATA object (formations aren't entities, so their orders aren't <c>EntityCommand</c>s either — the same
    /// data-object choice the formation itself makes). A formation's <see cref="GroundFormation.Orders"/> list runs these
    /// in sequence; each carries only the fields its <see cref="Type"/> needs. Design: docs/HEX-GROUND-AND-ORDERS-DESIGN.md (O1).
    /// </summary>
    public class GroundOrder
    {
        [JsonProperty] public GroundOrderType Type { get; internal set; }
        /// <summary>Set once the processor has kicked this order off (so a march isn't re-issued every tick).</summary>
        [JsonProperty] public bool Issued { get; internal set; }
        [JsonProperty] public int TargetQ { get; internal set; }        // MoveToHex
        [JsonProperty] public int TargetR { get; internal set; }        // MoveToHex
        [JsonProperty] public int TargetRegion { get; internal set; }   // MoveToRegion
        [JsonProperty] public double SecondsRemaining { get; internal set; }   // HoldFor (counts down)
        [JsonProperty] public string StanceId { get; internal set; }    // SetStance (catalog id)
        [JsonProperty] public GroundEngagementStance Engagement { get; internal set; }   // SetEngagement

        public GroundOrder() { }
        public GroundOrder(GroundOrder o)
        {
            Type = o.Type; Issued = o.Issued; TargetQ = o.TargetQ; TargetR = o.TargetR;
            TargetRegion = o.TargetRegion; SecondsRemaining = o.SecondsRemaining; StanceId = o.StanceId; Engagement = o.Engagement;
        }

        public static GroundOrder MoveHex(int q, int r) => new GroundOrder { Type = GroundOrderType.MoveToHex, TargetQ = q, TargetR = r };
        public static GroundOrder MoveRegion(int region) => new GroundOrder { Type = GroundOrderType.MoveToRegion, TargetRegion = region };
        public static GroundOrder Hold(double seconds) => new GroundOrder { Type = GroundOrderType.HoldFor, SecondsRemaining = seconds };
        public static GroundOrder Stance(string stanceId) => new GroundOrder { Type = GroundOrderType.SetStance, StanceId = stanceId };
        public static GroundOrder Roe(GroundEngagementStance e) => new GroundOrder { Type = GroundOrderType.SetEngagement, Engagement = e };

        /// <summary>Short human label for a readout ("→ hex (3,-1)", "dig in 2h", "ROE: StandOff").</summary>
        public string Describe()
        {
            switch (Type)
            {
                case GroundOrderType.MoveToHex: return $"→ hex ({TargetQ},{TargetR})";
                case GroundOrderType.MoveToRegion: return $"→ region {TargetRegion + 1}";
                case GroundOrderType.HoldFor: return $"hold {SecondsRemaining / 3600.0:0.#}h";
                case GroundOrderType.SetStance: return $"stance: {StanceId}";
                case GroundOrderType.SetEngagement: return $"ROE: {Engagement}";
                default: return Type.ToString();
            }
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
        /// <summary>Parent formation's <see cref="FormationId"/> (-1 = top-level) — the sub-fleet TREE (System ③), the
        /// ground echo of a sub-fleet nesting under a fleet via <c>TreeHierarchyDB</c>. A "Battle Group" parents a
        /// "Front Line" + an "Artillery" sub-formation, each with its OWN stance, all commanded as one block.</summary>
        [JsonProperty] public int ParentFormationId { get; internal set; } = -1;

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

        /// <summary>The RULES OF ENGAGEMENT — how this formation MANEUVERS relative to the enemy (the ground echo of the
        /// space closing model). Default <see cref="GroundEngagementStance.HoldGround"/> = stand and fight (no
        /// auto-movement), so a formation with no ROE set behaves exactly as before. Set via
        /// <c>GroundFormationDoctrine.SetEngagementStance</c>; read by <c>GroundForcesProcessor</c>'s maneuver step.</summary>
        [JsonProperty] public GroundEngagementStance Engagement { get; internal set; } = GroundEngagementStance.HoldGround;

        /// <summary>The formation's ORDER QUEUE (O1) — a sequence of <see cref="GroundOrder"/>s run one at a time, in
        /// order ("move to London, THEN Paris, THEN dig in"). Empty = the formation is free (ROE auto-maneuver, if any,
        /// takes over). The processor pops the front order when it completes. Deep-copied for save-safety.</summary>
        [JsonProperty] public List<GroundOrder> Orders { get; internal set; } = new List<GroundOrder>();

        public GroundFormation() { }
        public GroundFormation(GroundFormation o)
        {
            FormationId = o.FormationId; Name = o.Name; FactionOwnerID = o.FactionOwnerID; LeaderUnitId = o.LeaderUnitId;
            ParentFormationId = o.ParentFormationId;
            StanceId = o.StanceId; StanceFamily = o.StanceFamily; AttackMult = o.AttackMult; DamageTakenMult = o.DamageTakenMult;
            SwitchableAfter = o.SwitchableAfter; Engagement = o.Engagement;
            Orders = new List<GroundOrder>();
            if (o.Orders != null) foreach (var ord in o.Orders) Orders.Add(new GroundOrder(ord));
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
                Attack = design.Attack,
                Defense = design.Defense,
                MaxHealth = design.HitPoints,
                Health = design.HitPoints,
                // Strike range in hexes (H3): the design's, or a per-type default if the design left it unset.
                Range = design.Range > 0 ? design.Range : GroundRangeTools.DefaultRangeFor(design.UnitType),
                Evasion = design.Evasion,
                Shield = design.Shield,
                DamageType = design.DamageType,
                // Snapshot the design's environmental gear onto the unit (E4) — like the combat stats above.
                EnvResistance = (design.EnvironmentalResistance != null && design.EnvironmentalResistance.Count > 0)
                    ? new Dictionary<HazardEffectType, double>(design.EnvironmentalResistance)
                    : null,
            };
            unit.UnitId = forces.NextUnitId++;   // stable id (the ground echo of a ship's entity id)
            // G3: also place the unit on the ONE continuous grid — at its region BAND's centre column (the global twin
            // of the disk's (0,0) muster). Additive; the per-region HexQ/HexR (0,0) is unchanged.
            StampGlobalMuster(body, unit, regionIndex);
            forces.Units.Add(unit);
            return unit;
        }

        /// <summary>Place an EXISTING unit (one that came off a ship's bay — transport landing, T1b) onto
        /// <paramref name="body"/>'s region <paramref name="regionIndex"/>, keeping its identity and health. Unlike
        /// <see cref="RaiseUnit"/> this does NOT build a fresh full-health unit — it re-homes the same object: it gets a
        /// fresh <see cref="GroundUnit.UnitId"/> for THIS body's roster (ids are per-body), drops its old formation
        /// (formations are per-body too), clears any in-flight march, and musters at the region's centre hex. Creates
        /// the roster on demand. This is how a landed invader appears on a (possibly enemy) world. Never throws.</summary>
        public static GroundUnit PlaceExistingUnit(Entity body, GroundUnit unit, int regionIndex)
        {
            if (body == null || unit == null) return null;
            if (!body.TryGetDataBlob<GroundForcesDB>(out var forces))
            {
                forces = new GroundForcesDB();
                body.SetDataBlob(forces);
            }
            unit.RegionIndex = regionIndex < 0 ? 0 : regionIndex;
            unit.FormationId = -1;                 // formations are per-body — a landed unit arrives unformed
            unit.MovingToRegion = -1; unit.TransitSecondsRemaining = 0;
            unit.HexPath = null; unit.HexQ = 0; unit.HexR = 0; unit.HexTransitSecondsRemaining = 0;
            unit.GlobalPath = null; unit.GlobalQ = -1; unit.GlobalR = -1; unit.GlobalTransitSecondsRemaining = 0;
            unit.UnitId = forces.NextUnitId++;     // fresh id on this body's roster
            StampGlobalMuster(body, unit, unit.RegionIndex);
            forces.Units.Add(unit);
            return unit;
        }

        /// <summary>Place a unit at its region BAND's centre column on the body's global <c>SurfaceGrid</c> (G3 muster —
        /// the global twin of the disk's (0,0)). Generates the grid on demand. Defensive: no region layer / no grid →
        /// leaves GlobalQ/GlobalR at -1.</summary>
        private static void StampGlobalMuster(Entity body, GroundUnit unit, int regionIndex)
        {
            if (body == null || !body.TryGetDataBlob<Pulsar4X.Galaxy.PlanetRegionsDB>(out var regionsDB) || regionsDB.Regions.Count == 0) return;
            var grid = Pulsar4X.Galaxy.PlanetGridFactory.EnsureGridForBody(body);
            if (grid == null || grid.Cols <= 0 || grid.Rows <= 0) return;
            unit.GlobalQ = Pulsar4X.Galaxy.PlanetGridFactory.BandCentreColumn(regionIndex, grid.Cols, regionsDB.Regions.Count);
            unit.GlobalR = grid.Rows / 2;
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

        // ───────────────────────── FINE HEX MOVEMENT (H2 — the London→Paris march) ─────────────────────────
        // The coarse OrderMove above hops whole regions; this walks the hex grid WITHIN the unit's region. A* over the
        // region's hex patch (terrain-weighted), stored on the unit, walked hex-by-hex by GroundForcesProcessor.

        /// <summary>
        /// Order a unit to march to hex (<paramref name="destQ"/>,<paramref name="destR"/>) WITHIN its current region,
        /// pathing around rough terrain (A*). Lazily generates the body's hex patches if it hasn't become a theatre yet
        /// (ordering a hex move IS "the tactical view was opened here"). Returns false — no move — if the body has no
        /// region layer, the unit's region is out of range, the destination isn't in the patch, it's already there, or
        /// no route exists. Cross-region hex marches use the coarse <see cref="OrderMove"/> to hop the border, then a
        /// fresh hex order in the new region (each region's patch has its own local origin — border-stitching is a
        /// documented follow-on).
        /// </summary>
        public static bool OrderMoveToHex(Entity body, GroundUnit unit, int destQ, int destR)
        {
            if (unit == null || body == null) return false;
            if (unit.MovingToRegion >= 0) return false;   // can't hex-march while crossing a region border (coarse hop wins)
            if (!body.TryGetDataBlob<Pulsar4X.Galaxy.PlanetRegionsDB>(out var regionsDB)) return false;
            if (unit.RegionIndex < 0 || unit.RegionIndex >= regionsDB.Regions.Count) return false;

            // This body is now a theatre — make sure its hex patches exist (idempotent, no-op if already generated).
            Pulsar4X.Galaxy.PlanetHexFactory.EnsureHexesForBody(body);

            var region = regionsDB.Regions[unit.RegionIndex];
            var path = HexPathfinder.FindPath(region.Hexes, unit.HexQ, unit.HexR, destQ, destR);
            if (path.Count == 0) return false;   // already there / unreachable / dest off-patch

            // Store deep copies (don't alias the region's live hex objects), and capture the region's per-hex base time.
            unit.HexPath = new List<Pulsar4X.Galaxy.GroundHex>(path.Count);
            foreach (var h in path) unit.HexPath.Add(new Pulsar4X.Galaxy.GroundHex(h));
            unit.HexStepBaseSeconds = HexPathfinder.PerHexBaseSeconds(region);
            unit.HexTransitSecondsRemaining = unit.HexStepBaseSeconds * HexPathfinder.HexMoveMult(unit.HexPath[0].Terrain);
            return true;
        }

        // ───────────────────────── GLOBAL HEX MOVEMENT (G-track, G3 — one continuous world, no edge gates) ─────────
        // The G-track twin of OrderMoveToHex: march the unit across the ONE continuous SurfaceGrid to a GLOBAL (Q,R),
        // crossing region BAND borders with no stitching (it's just the next column). Additive alongside the per-region
        // path above; walked by GroundForcesProcessor's global-path step. Design: docs/GLOBAL-HEX-GRID-DESIGN.md.

        /// <summary>
        /// Order a unit to march to GLOBAL grid hex (<paramref name="destQ"/>,<paramref name="destR"/>) on the body's
        /// cylinder <c>SurfaceGrid</c> — pathing around ocean, crossing region-band borders seamlessly (no edge gates).
        /// Generates the grid on demand. Returns false — no move — if the body has no region layer / grid, the unit isn't
        /// on the grid yet, it's already there, the destination is impassable/off-grid, or no route exists.
        /// </summary>
        public static bool OrderMoveToGlobalHex(Entity body, GroundUnit unit, int destQ, int destR)
        {
            if (unit == null || body == null) return false;
            if (unit.MovingToRegion >= 0) return false;   // a coarse region hop wins
            var grid = Pulsar4X.Galaxy.PlanetGridFactory.EnsureGridForBody(body);
            if (grid == null || grid.Cols <= 0) return false;
            if (unit.GlobalQ < 0 || unit.GlobalR < 0) StampGlobalMuster(body, unit, unit.RegionIndex);   // ensure it's on the grid
            if (unit.GlobalQ < 0) return false;

            var path = HexPathfinder.FindGlobalPath(grid, unit.GlobalQ, unit.GlobalR, destQ, destR);
            if (path.Count == 0) return false;   // already there / unreachable / dest off-grid or impassable

            unit.GlobalPath = new List<Pulsar4X.Galaxy.GroundHex>(path.Count);
            foreach (var h in path) unit.GlobalPath.Add(new Pulsar4X.Galaxy.GroundHex(h));
            unit.GlobalStepBaseSeconds = GlobalStepSecondsFor(body, unit.GlobalQ);
            unit.GlobalTransitSecondsRemaining = unit.GlobalStepBaseSeconds * HexPathfinder.HexMoveMult(unit.GlobalPath[0].Terrain);
            return true;
        }

        /// <summary>Per-open-hex base march time on the global grid, derived from the region BAND's crossing-time datum
        /// (no new constant): a band is <c>Cols/RegionCount</c> columns wide, so one open hex ≈ the band's crossing time
        /// ÷ that width. Uses the band the unit starts in.</summary>
        private static double GlobalStepSecondsFor(Entity body, int globalQ)
        {
            if (!body.TryGetDataBlob<Pulsar4X.Galaxy.PlanetRegionsDB>(out var regionsDB) || regionsDB.SurfaceGrid == null) return 0.0;
            var grid = regionsDB.SurfaceGrid;
            int rc = regionsDB.Regions.Count;
            int region = Pulsar4X.Galaxy.PlanetGridFactory.RegionOfColumn(globalQ, grid.Cols, rc);
            int bandWidth = System.Math.Max(1, grid.Cols / System.Math.Max(1, rc));
            double crossing = (region >= 0 && region < regionsDB.Regions.Count) ? regionsDB.Regions[region].CrossingTimeSeconds : 0.0;
            return crossing / bandWidth;
        }

        /// <summary>March a whole formation to hex (<paramref name="destQ"/>,<paramref name="destR"/>) as a block (the
        /// hex-scale echo of <see cref="OrderFormationMove"/>): every member standing with the LEADER (in the leader's
        /// region, not already marching) paths to the destination via <see cref="OrderMoveToHex"/>. Members path
        /// independently to the same hex (v1 — spreading a block across adjacent hexes is a refinement). Returns how
        /// many units set out.</summary>
        public static int OrderFormationMoveToHex(Entity body, GroundFormation formation, int destQ, int destR)
        {
            if (formation == null || !body.TryGetDataBlob<GroundForcesDB>(out var forces)) return 0;
            int rallyRegion = LeaderRegion(forces, formation);
            if (rallyRegion < 0) return 0;

            int moved = 0;
            foreach (var u in forces.Units.ToArray())
            {
                if (u.FormationId != formation.FormationId) continue;
                if (u.RegionIndex != rallyRegion || u.MovingToRegion >= 0) continue;
                if (u.HexPath != null && u.HexPath.Count > 0) continue;   // already hex-marching
                if (OrderMoveToHex(body, u, destQ, destR)) moved++;
            }
            return moved;
        }

        // ───────────────────────── FORMATIONS (the ground echo of fleet grouping) ─────────────────────────
        // Mirrors the FleetOrder verbs (Create / AssignShip / UnassignShip / SetFlagShip / Disband), one level over
        // from entities to data objects. Membership lives on the unit (GroundUnit.FormationId), like a ship's parent
        // fleet id; the formation record holds the name + leader (the flagship echo).

        /// <summary>Create a named, empty formation on <paramref name="body"/> (the ground echo of
        /// <c>FleetFactory.Create</c>). Creates the roster on demand and hands out a stable FormationId.</summary>
        public static GroundFormation CreateFormation(Entity body, int factionId, string name, int parentFormationId = -1)
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
                ParentFormationId = parentFormationId,   // -1 = top-level; a valid id nests it as a sub-formation (System ③)
            };
            forces.Formations.Add(formation);
            return formation;
        }

        /// <summary>Re-parent <paramref name="child"/> under <paramref name="parentId"/> (-1 = make it top-level) — the
        /// sub-fleet nesting op (System ③). Refuses a cycle (can't parent a formation under itself or one of its own
        /// descendants). Returns false if the child is null or the move would create a cycle. Never throws.</summary>
        public static bool SetParentFormation(GroundForcesDB forces, GroundFormation child, int parentId)
        {
            if (forces == null || child == null) return false;
            if (parentId == child.FormationId) return false;                 // can't parent to itself
            // walk UP from the proposed parent; if we reach the child, this would make a cycle
            int walk = parentId;
            int guard = 0;
            while (walk >= 0 && guard++ < 512)
            {
                if (walk == child.FormationId) return false;                 // proposed parent is a descendant of child
                var pf = FindFormation(forces, walk);
                if (pf == null) break;
                walk = pf.ParentFormationId;
            }
            child.ParentFormationId = parentId;
            return true;
        }

        private static GroundFormation FindFormation(GroundForcesDB forces, int formationId)
        {
            if (forces?.Formations == null) return null;
            foreach (var f in forces.Formations) if (f.FormationId == formationId) return f;
            return null;
        }

        /// <summary>Order a whole formation TREE (a formation and every sub-formation under it) to march to a hex — the
        /// command-hierarchy payoff of nesting: one order moves the entire battle group, sub-groups and all. Returns the
        /// number of units set marching. Never throws.</summary>
        public static int OrderFormationTreeMoveToHex(Entity body, GroundForcesDB forces, GroundFormation formation, int destQ, int destR)
        {
            if (body == null || forces == null || formation == null) return 0;
            int moved = 0;
            foreach (var f in GroundFormationTools.SubtreeFormations(forces, formation))
                moved += OrderFormationMoveToHex(body, f, destQ, destR);
            return moved;
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

        // ───────────────────────── FORMATION ORDER QUEUE (O1 — sequential "then" waypoints) ─────────────────────────

        /// <summary>Append an order to a formation's queue (it runs after everything already queued). The ground echo of
        /// giving a fleet a waypoint. Returns false if the formation is null.</summary>
        public static bool QueueFormationOrder(GroundFormation formation, GroundOrder order)
        {
            if (formation == null || order == null) return false;
            (formation.Orders ??= new List<GroundOrder>()).Add(order);
            return true;
        }

        /// <summary>Replace a formation's whole queue with a single order (the "do THIS now, forget the rest" verb).</summary>
        public static void SetFormationOrder(GroundFormation formation, GroundOrder order)
        {
            if (formation == null) return;
            formation.Orders = new List<GroundOrder>();
            if (order != null) formation.Orders.Add(order);
        }

        /// <summary>Clear a formation's order queue (cancel the plan). In-transit units keep their current march until it
        /// completes; only the QUEUE is emptied.</summary>
        public static void ClearFormationOrders(GroundFormation formation)
        {
            if (formation == null) return;
            formation.Orders = new List<GroundOrder>();
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

        // ── SUB-FORMATION TREE (System ③, the sub-fleet nesting) ──────────────────────────────────────────────────
        /// <summary>The DIRECT sub-formations of <paramref name="parent"/> (its immediate children).</summary>
        public static List<GroundFormation> ChildFormations(GroundForcesDB forces, GroundFormation parent)
        {
            var list = new List<GroundFormation>();
            if (forces?.Formations == null || parent == null) return list;
            foreach (var f in forces.Formations)
                if (f.ParentFormationId == parent.FormationId) list.Add(f);
            return list;
        }

        /// <summary>A formation AND every sub-formation beneath it (the whole subtree, the formation itself first).
        /// The set a tree-order (move / stance-broadcast) applies to. Cycle-safe (visited guard).</summary>
        public static List<GroundFormation> SubtreeFormations(GroundForcesDB forces, GroundFormation root)
        {
            var list = new List<GroundFormation>();
            if (forces?.Formations == null || root == null) return list;
            var seen = new HashSet<int>();
            var stack = new Stack<GroundFormation>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var f = stack.Pop();
                if (f == null || !seen.Add(f.FormationId)) continue;   // cycle / duplicate guard
                list.Add(f);
                foreach (var c in ChildFormations(forces, f)) stack.Push(c);
            }
            return list;
        }

        /// <summary>Every unit in a formation's whole subtree (its members + all sub-formations' members) — what a
        /// battle-group-level command touches.</summary>
        public static List<GroundUnit> SubtreeUnits(GroundForcesDB forces, GroundFormation root)
        {
            var units = new List<GroundUnit>();
            if (forces == null || root == null) return units;
            var ids = new HashSet<int>();
            foreach (var f in SubtreeFormations(forces, root)) ids.Add(f.FormationId);
            foreach (var u in forces.Units)
                if (ids.Contains(u.FormationId)) units.Add(u);
            return units;
        }
    }
}
