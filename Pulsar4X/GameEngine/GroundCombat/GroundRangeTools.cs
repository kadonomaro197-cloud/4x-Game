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
    /// </list>
    /// Design: docs/HEX-GROUND-AND-ORDERS-DESIGN.md (H3).
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
    }
}
