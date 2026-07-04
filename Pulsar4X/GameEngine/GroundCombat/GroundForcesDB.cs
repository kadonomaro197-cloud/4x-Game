using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Pulsar4X.Engine;
using Pulsar4X.Datablobs;

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

        public GroundUnit() { }
        public GroundUnit(GroundUnit o)
        {
            DesignId = o.DesignId; Name = o.Name; FactionOwnerID = o.FactionOwnerID; RegionIndex = o.RegionIndex;
            UnitType = o.UnitType; Attack = o.Attack; Defense = o.Defense; MaxHealth = o.MaxHealth; Health = o.Health;
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

        public GroundForcesDB() { }
        public GroundForcesDB(GroundForcesDB other)
        {
            Units = new List<GroundUnit>();
            foreach (var u in other.Units) Units.Add(new GroundUnit(u));
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
            };
            forces.Units.Add(unit);
            return unit;
        }
    }
}
