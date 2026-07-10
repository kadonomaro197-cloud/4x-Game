using Pulsar4X.Galaxy;

namespace Pulsar4X.GroundCombat
{
    /// <summary>Terrain classes a region's geography sorts into for combat. The ground echo of a space hazard's
    /// character (a gas cloud vs a debris field vs a corona) — see <see cref="GroundTerrain"/>.</summary>
    public enum GroundTerrainClass : byte { Open, Cover, Rough }

    /// <summary>
    /// TERRAIN as the ground twin of a SPACE HAZARD (`GameEngine/Hazards/`) — the developer's insight: *"terrain
    /// mirrors space environments too."* A region's FEATURES are its environmental effects, exactly as a
    /// `SpaceHazardDB` region carries a list of typed `HazardEffect`s; this helper is the ground echo of
    /// `SpaceHazardTools` — read a region, get how its environment bends a fight.
    ///
    /// It **shares the hazard EFFECT VOCABULARY where the concepts overlap**, but stays INDEPENDENT of the (green,
    /// CI-tested) hazard engine — the locked decision (`docs/GROUND-COMBAT-MAP-DESIGN.md` → "terrain mirrors space
    /// environments"): mirror the pattern, don't refactor. The vocabulary map:
    ///   • **MovementDrag** ↔ the region's `CrossingTimeSeconds` (already the "terrain slows a march" effect, 5b).
    ///   • **Concealment** ↔ hazard `SensorJam` (forest/jungle hides units — ground fog of war; a later slice).
    ///   • **EnvironmentalHazard** ↔ hazard `HeatDamage`/`Corrosive` (a hostile region attrits unprotected units;
    ///     the "defend an environment / hostile-world attrition" slice, later).
    ///   • **Cover** ↔ ground-specific — a defender in rough terrain takes LESS: the combat effect wired here (5f).
    ///
    /// A unit's TYPE is its innate terrain affinity — the ground echo of a ship's `HazardResistanceAtb` (innate in
    /// v1; a researched "mountaineering / amphibious" GEAR component that overrides it is the cradle-to-grave
    /// follow-up, exactly as a hazard counter is a component). Weight is MODERATE (the developer's call): a real
    /// edge, not a wall. All magnitudes are `const` dials, mirroring hazard magnitudes being tunable data.
    /// </summary>
    public static class GroundTerrain
    {
        // --- the ground weapon triangle: Armor ▸ Infantry ▸ Artillery ▸ Armor (the developer's call) ---
        public const double TriangleStrong = 1.5;    // attacker's edge over the type it beats
        public const double TriangleWeak   = 0.67;   // and its disadvantage vs the type that beats it (≈1/1.5)

        // --- COVER: a DEFENDER (the region's owner) has its incoming damage DIVIDED by this (>1 = protection) ---
        public const double Cover_Open  = 0.9;    // open ground slightly FAVOURS the attacker (plains)
        public const double Cover_Cover = 1.25;   // forest / jungle / wetland — real cover
        public const double Cover_Rough = 1.5;    // mountains / highlands / volcanic — a fortress edge

        /// <summary>Sort a region into a terrain class by its DOMINANT feature (the highest-coverage one) — the ground
        /// echo of reading a hazard's effect list. A featureless / null region is Open.</summary>
        public static GroundTerrainClass Classify(Region region)
        {
            if (region == null || region.Features == null || region.Features.Count == 0)
                return GroundTerrainClass.Open;

            RegionFeatureType dominant = RegionFeatureType.Plains;
            double best = -1.0;
            foreach (var f in region.Features)
                if (f.Coverage > best) { best = f.Coverage; dominant = f.Type; }

            switch (dominant)
            {
                case RegionFeatureType.Mountains:
                case RegionFeatureType.Highlands:
                case RegionFeatureType.Volcanic:
                    return GroundTerrainClass.Rough;
                case RegionFeatureType.Forest:
                case RegionFeatureType.Jungle:
                case RegionFeatureType.Wetland:
                    return GroundTerrainClass.Cover;
                default:   // Plains / Desert / Barren / Coast / Ocean / Ice / Tundra / GasLayers / Unknown
                    return GroundTerrainClass.Open;
            }
        }

        /// <summary>The classic cycle Armor ▸ Infantry ▸ Artillery ▸ Armor: the winning attacker deals
        /// <see cref="TriangleStrong"/>, the losing (reverse) pairing <see cref="TriangleWeak"/>, same type 1.0.</summary>
        public static double TriangleMult(GroundUnitType attacker, GroundUnitType target)
        {
            if (attacker == target) return 1.0;
            if ((attacker == GroundUnitType.Armor     && target == GroundUnitType.Infantry) ||
                (attacker == GroundUnitType.Infantry  && target == GroundUnitType.Artillery) ||
                (attacker == GroundUnitType.Artillery && target == GroundUnitType.Armor))
                return TriangleStrong;
            return TriangleWeak;   // the three reverse pairings
        }

        /// <summary>How a unit TYPE fights in this terrain (its innate affinity — the `HazardResistanceAtb` echo):
        /// armor loves the open and struggles in rough/cover; artillery gains from high ground (rough); infantry
        /// holds any ground evenly.</summary>
        public static double TerrainAttackMult(GroundUnitType type, GroundTerrainClass terrain)
        {
            switch (type)
            {
                case GroundUnitType.Armor:
                    return terrain == GroundTerrainClass.Open ? 1.3 : (terrain == GroundTerrainClass.Rough ? 0.7 : 0.75);
                case GroundUnitType.Artillery:
                    return terrain == GroundTerrainClass.Rough ? 1.3 : (terrain == GroundTerrainClass.Cover ? 0.85 : 1.0);
                default: // Infantry
                    return 1.0;
            }
        }

        /// <summary>BALANCE dial (Propulsion ⚙2): how far a unit's designed rough-terrain handling swings its combat
        /// output on constrained (Rough/Cover) ground. ⚠ FLAGGED — tunable.</summary>
        public const double LocomotionCombatSpread = 0.5;

        /// <summary>Propulsion ⚙2 (Traction) — a unit's DESIGNED locomotion gives it a COMBAT edge on its preferred
        /// ground, not just a movement one. On CONSTRAINED terrain (Rough/Cover) an all-terrain drive (high
        /// <see cref="GroundLocomotionAtb.RoughHandling"/>) fights better and a road-bound one (low) is bogged down; the
        /// OPEN is neutral (everyone moves freely, so mobility doesn't gate the fight there — armour's open edge is the
        /// <see cref="TerrainAttackMult"/> term). Centred on the neutral handling 0.5 → ×1.0, so a unit with NO designed
        /// locomotion (<see cref="GroundMobility.RoughHandlingForUnit"/> returns 0.5) is BYTE-IDENTICAL — every current
        /// unit. handling 1.0 → ×(1+spread/2), 0.0 → ×(1−spread/2); clamped ≥0.1. PURE.</summary>
        public static double LocomotionTerrainMult(double roughHandling, GroundTerrainClass terrain)
        {
            if (terrain == GroundTerrainClass.Open) return 1.0;   // open ground: mobility doesn't gate the fight
            double m = 1.0 + (roughHandling - 0.5) * LocomotionCombatSpread;
            return m < 0.1 ? 0.1 : m;
        }

        /// <summary>The COVER effect: a defender in this terrain has its incoming damage divided by this (Rough &gt; Cover &gt; Open).</summary>
        public static double CoverDefenseMult(GroundTerrainClass terrain)
        {
            switch (terrain)
            {
                case GroundTerrainClass.Rough: return Cover_Rough;
                case GroundTerrainClass.Cover: return Cover_Cover;
                default: return Cover_Open;
            }
        }
    }
}
