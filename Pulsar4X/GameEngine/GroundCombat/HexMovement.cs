using Pulsar4X.Galaxy;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// How a unit crosses ground — the thing that makes terrain cost depend on the UNIT, not just the hex (the
    /// developer's steer: "a land vehicle has problems on rough/water; an air vehicle doesn't unless it hits a
    /// hazard; same logic for water"). A snapshot on each <see cref="GroundUnit"/> (from its design), read by
    /// <see cref="HexMovement.TerrainCost"/> when the pathfinder weighs a hex.
    ///
    /// v1 wires <see cref="Land"/> fully (today's Infantry/Armor/Artillery are all land); <see cref="Water"/> and
    /// <see cref="Air"/> cost rules exist so the pathfinder is domain-aware from day one, but naval/air UNITS
    /// (their own types, designs, and cradle-to-grave) are a later build. Per-hex HAZARD cost (the "unless it hits
    /// a hazard" half) lands in H3, when hazards move from the region onto the hex.
    /// </summary>
    public enum MovementDomain : byte
    {
        Land,   // treads/boots — slowed by rough ground, stopped by open water
        Water,  // hulls — only on ocean/coast
        Air     // wings — flies over any terrain (hazards bite in H3)
    }

    /// <summary>
    /// The terrain-weighted movement cost — the multiplier on a hex-step's base time, by the moving unit's
    /// <see cref="MovementDomain"/> and the hex's terrain. <see cref="Impassable"/> (∞) means the pathfinder
    /// routes AROUND that hex (a land unit will not enter open ocean). Pure + static, so it's trivially testable.
    ///
    /// The numbers are the developer-approved scheme (docs/HEX-GROUND-AND-ORDERS-DESIGN.md, H2): open ground ×1,
    /// vegetated ×1.6, elevated ×2.5, ice ×2, ocean impassable to land; ocean-only for water; flat for air.
    /// </summary>
    public static class HexMovement
    {
        public const double Impassable = double.PositiveInfinity;

        /// <summary>Cost multiplier for <paramref name="domain"/> to enter a hex of <paramref name="terrain"/>.
        /// <see cref="Impassable"/> = cannot enter (pathfinder detours).</summary>
        public static double TerrainCost(MovementDomain domain, RegionFeatureType terrain)
        {
            switch (domain)
            {
                case MovementDomain.Air:
                    // Flies over everything at a flat rate; per-hex hazards will add cost in H3.
                    return 1.0;

                case MovementDomain.Water:
                    // Hulls only float on water (a coast is the water/land boundary — passable to both).
                    return (terrain == RegionFeatureType.Ocean || terrain == RegionFeatureType.Coast) ? 1.0 : Impassable;

                case MovementDomain.Land:
                default:
                    switch (terrain)
                    {
                        // Open ground — full speed.
                        case RegionFeatureType.Plains:
                        case RegionFeatureType.Coast:
                        case RegionFeatureType.Barren:
                        case RegionFeatureType.Tundra:
                        case RegionFeatureType.Desert:
                            return 1.0;
                        // Vegetated — slower going.
                        case RegionFeatureType.Forest:
                        case RegionFeatureType.Jungle:
                        case RegionFeatureType.Wetland:
                            return 1.6;
                        // Elevated / broken ground — a real drag.
                        case RegionFeatureType.Highlands:
                        case RegionFeatureType.Mountains:
                        case RegionFeatureType.Volcanic:
                            return 2.5;
                        case RegionFeatureType.Ice:
                            return 2.0;
                        // No footing for a land unit.
                        case RegionFeatureType.Ocean:
                        case RegionFeatureType.GasLayers:
                        case RegionFeatureType.Unknown:
                            return Impassable;
                        default:
                            return 1.0;
                    }
            }
        }

        /// <summary>True if <paramref name="domain"/> can enter a hex of <paramref name="terrain"/> at all.</summary>
        public static bool IsPassable(MovementDomain domain, RegionFeatureType terrain)
            => !double.IsPositiveInfinity(TerrainCost(domain, terrain));
    }
}
