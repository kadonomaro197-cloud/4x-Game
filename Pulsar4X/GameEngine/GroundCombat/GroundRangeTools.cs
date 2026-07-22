using System;
using Pulsar4X.Galaxy;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// Ground-unit RANGE helpers (H3). Two jobs:
    /// <list type="bullet">
    /// <item><b>Defaults</b> — <see cref="DefaultRangeFor"/>: the base-mod strike range (in hexes) for a unit type when
    /// a design doesn't set one. Infantry/Armor fight up close (1 hex); Artillery reaches out (3). Moddable per design.</item>
    /// <item><b>The real-distance readout</b> — <see cref="RealReachKm"/>: converts a hex range to real kilometres ON A
    /// GIVEN BODY. This is the developer's insight made VISIBLE: *"1 hex on Earth doesn't cover the same distance as 1
    /// hex on Io."* Combat is resolved in hex-space (so the rules are the same everywhere), but a 3-hex gun reaches a
    /// very different real distance on a continent-scale world than on a small moon — this surfaces that as a number
    /// the player can read, rather than hiding it.</item>
    /// <item><b>The two-way translation</b> — <see cref="HexesForKm"/>/<see cref="HexesForMetres"/> (km → hexes) and
    /// <see cref="MetresForHexes"/> (hexes → real metres): the INVERSE of RealReach, so a REAL weapon/radar range can be
    /// placed on the hex ruler and back. This is the foundation for making the real km on the gun the truth and the hex
    /// a pure display ruler (docs/combat/REAL-DISTANCE-COMBAT-DESIGN.md, Slice 1 — additive/byte-identical).</item>
    /// </list>
    /// Design: docs/HEX-GROUND-AND-ORDERS-DESIGN.md (H3) + docs/combat/REAL-DISTANCE-COMBAT-DESIGN.md.
    /// </summary>
    public static class GroundRangeTools
    {
        /// <summary>The base-mod default strike range (hexes) for a unit type, used when a design leaves Range unset.</summary>
        public static int DefaultRangeFor(GroundUnitType type)
        {
            switch (type)
            {
                case GroundUnitType.Artillery: return 3;   // reach — hits from several hexes away
                default: return 1;                         // Infantry / Armor — close fight (same + adjacent hex)
            }
        }

        /// <summary>The real centre-to-centre distance between adjacent hexes in this region, in kilometres — derived
        /// from the region's TRUE area spread over its generated hex count (a flat-hexagon area→pitch relation). This is
        /// why a hex is a different real size on Earth vs Io. Returns 0 if the region has no hex patch / no area.</summary>
        public static double HexPitchKm(Region region)
        {
            if (region == null || region.Hexes == null || region.Hexes.Count == 0 || region.Area_km2 <= 0) return 0.0;
            double areaPerHex = region.Area_km2 / region.Hexes.Count;         // km² of one hex
            // A regular (flat) hexagon of area A has centre-to-centre spacing d where A = (sqrt(3)/2)·d² → d = sqrt(2A/√3).
            return Math.Sqrt(2.0 * areaPerHex / Math.Sqrt(3.0));
        }

        /// <summary>A hex strike range expressed as a real distance (km) on this body — the readout that honours
        /// "1 hex ≠ the same distance everywhere." 0 if the region has no hex geometry yet.</summary>
        public static double RealReachKm(int rangeHexes, Region region)
        {
            if (rangeHexes <= 0) return 0.0;
            return rangeHexes * HexPitchKm(region);
        }

        // ── Real-distance combat foundation (docs/combat/REAL-DISTANCE-COMBAT-DESIGN.md, Slice 1) ──────────────────────
        // The LOCKED principle: the real distance (km) on the weapon/entity is the truth; the hex grid is only the
        // display ruler, and a hex means a DIFFERENT real distance on each body. HexPitchKm/RealReachKm already convert
        // hexes → km; these add the INVERSE (km → hexes) so a real weapon/radar range can be placed on the hex ruler and
        // back. Pure, additive, byte-identical — nothing in the resolver reads these yet (Slice 2 flips the range gate).

        /// <summary>How many hexes a real distance of <paramref name="km"/> spans on this body — the inverse of
        /// <see cref="RealReachKm"/> / <see cref="HexPitchKm"/>. This is the "put a real range on the chart" conversion:
        /// a 1 km gatling on Earth (pitch ≈ 560 km/hex) spans ≈ 0.0018 hexes → same-hex-only; the same gun on a small
        /// moon (pitch ≈ 2 km/hex) spans 0.5 hexes → still same-hex, but a 3 km gun there reaches an adjacent hex. Returns
        /// 0 for a non-positive distance or a region with no hex geometry (pitch 0) — never throws, never divides by 0.</summary>
        public static double HexesForKm(double km, Region region)
        {
            if (km <= 0) return 0.0;
            double pitch = HexPitchKm(region);
            if (pitch <= 0) return 0.0;
            return km / pitch;
        }

        /// <summary>The metres twin of <see cref="HexesForKm"/> — how many hexes a real gap of
        /// <paramref name="metres"/> spans on this body. The resolver works in metres (the shared
        /// <c>CombatKernel.Separation_m</c>), so this is the form the range gate will read (Slice 2). Never throws.</summary>
        public static double HexesForMetres(double metres, Region region) => HexesForKm(metres / 1000.0, region);

        /// <summary>The real distance in metres of a hex span on this body — <see cref="RealReachKm"/> in metres, taking
        /// a fractional hex count (the resolver measures gaps as a whole-number <c>HexDist</c>, but a real range lands
        /// between tick marks). 0 if the region has no hex geometry. Never throws. This is the seam the range gate uses to
        /// turn "how many hexes apart" into "how many real metres apart" for the `real gap ≤ real range` compare.</summary>
        public static double MetresForHexes(double hexes, Region region)
        {
            if (hexes <= 0) return 0.0;
            return hexes * HexPitchKm(region) * 1000.0;
        }

        /// <summary>THE SINGLE SEAM where "a weapon's real range in km" is defined for the combat gate. In Slice 1 this
        /// equals the current readout (<see cref="RealReachKm"/> of the authored hex range) so the range a weapon reaches
        /// is byte-identical to today — the resolver still gates on hex-count, and this is only used by the client readout
        /// and tests. Slice 2 substitutes a real per-weapon stat here (a fixed reference distance, independent of the
        /// body's hex pitch), which is the whole behaviour change: the gun's reach stops scaling with the size of a hex.
        /// Keeping it in ONE method means that flip touches one place, not every call site. Never throws.</summary>
        public static double RealRangeKmFor(int rangeHexes, Region region) => RealReachKm(rangeHexes, region);
    }
}
