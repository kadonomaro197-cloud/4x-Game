using System;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// The MINI-HEX tactical position math (docs/combat/MINI-HEX-TACTICAL-GRID-DESIGN.md, M1). A unit's exact place on a
    /// planet is TWO tiers: its coarse global hex (<c>GroundUnit.GlobalQ/GlobalR</c> on the body's <c>SurfaceGrid</c> —
    /// "which ~477 km neighbourhood") PLUS its mini-hex offset within that hex's <c>CityGrid</c>
    /// (<c>GroundUnit.MiniQ/MiniR</c> — "where in the ~37 km battlefield", the SAME mini-hexes the infrastructure/city
    /// view uses). This collapses those two tiers into ONE continuous real position in kilometres, so the real gap
    /// between two units is measured ACROSS coarse-hex boundaries — two units at the shared EDGE of adjacent coarse hexes
    /// read a SMALL gap (the developer's "transitional hexes" continuity: they can see + engage each other), with no
    /// wall. It's the ground echo of space's <c>FleetCombatStateDB.Separation_m</c>: one real number, not "same cell or
    /// not."
    ///
    /// <b>ADDITIVE + UNREAD by the resolver in M1</b> (it still gates on hex distance) → byte-identical; M2 flips the
    /// range gate to read <see cref="RealGapMetres"/> against a weapon's real <c>Range_m</c>. Pure/deterministic (no
    /// entity or manager reads) so it is directly unit-testable. Never throws.
    /// </summary>
    public static class GroundMiniHex
    {
        /// <summary>Mini-hexes ACROSS one coarse hex = <c>2·radius + 1</c> (a radius-6 <c>CityGrid</c> → 13 across), so one
        /// mini-hex spans <c>coarsePitchKm / (2·radius+1)</c> — ~37 km under a ~477 km Earth coarse hex, in the
        /// developer's 10–50 km band. Raise the radius (or shrink the coarse grid) for finer mini-hexes. 0 for a
        /// non-positive pitch. Never throws, never divides by zero.</summary>
        public static double MiniPitchKm(double coarsePitchKm, int cityRadius)
        {
            if (coarsePitchKm <= 0) return 0.0;
            int across = 2 * Math.Max(1, cityRadius) + 1;
            return coarsePitchKm / across;
        }

        /// <summary>Axial hex (q,r) → a flat 2D pixel offset in km, given the centre-to-centre neighbour distance
        /// <paramref name="pitchKm"/>. Chosen so ANY adjacent hex is exactly <paramref name="pitchKm"/> away: neighbour
        /// (1,0) → (pitch, 0); neighbour (0,1) → (0.5·pitch, (√3/2)·pitch), both at distance pitch. Consistent with how
        /// the rest of the ground code treats a hex step as one pitch of real distance.</summary>
        private static (double x, double y) AxialToKm(int q, int r, double pitchKm)
            => (pitchKm * (q + r * 0.5), pitchKm * r * (Math.Sqrt(3.0) / 2.0));

        /// <summary>A unit's CONTINUOUS real position on the body, in km — its coarse global hex centre (at
        /// <paramref name="coarsePitchKm"/>) PLUS its mini-hex offset within that hex (at the derived mini pitch). The two
        /// tiers add into one flat plane, so distance spans coarse-hex borders with no wall. (M1 does not yet wrap the
        /// cylinder longitude seam — the M1 gauge uses non-seam coordinates; seam wrap is M2/M3, mirroring
        /// <c>PlanetViewWindow.WrapDelta</c>.)</summary>
        public static (double x, double y) ContinuousPosKm(
            int globalQ, int globalR, int miniQ, int miniR, double coarsePitchKm, int cityRadius)
        {
            var (cx, cy) = AxialToKm(globalQ, globalR, coarsePitchKm);
            var (mx, my) = AxialToKm(miniQ, miniR, MiniPitchKm(coarsePitchKm, cityRadius));
            return (cx + mx, cy + my);
        }

        /// <summary>The real gap in METRES between two units given their coarse (Global) + mini positions and the body's
        /// coarse hex pitch. This is exactly what M2's range gate compares to a weapon's real <c>Range_m</c>: a unit
        /// FIRES when this ≤ its weapon reach. Because it's built from continuous positions, two units at a shared
        /// coarse-hex edge read a small gap regardless of which coarse hex each is filed under. Never throws.</summary>
        public static double RealGapMetres(
            int aGlobalQ, int aGlobalR, int aMiniQ, int aMiniR,
            int bGlobalQ, int bGlobalR, int bMiniQ, int bMiniR,
            double coarsePitchKm, int cityRadius)
        {
            var (ax, ay) = ContinuousPosKm(aGlobalQ, aGlobalR, aMiniQ, aMiniR, coarsePitchKm, cityRadius);
            var (bx, by) = ContinuousPosKm(bGlobalQ, bGlobalR, bMiniQ, bMiniR, coarsePitchKm, cityRadius);
            double dx = ax - bx, dy = ay - by;
            return Math.Sqrt(dx * dx + dy * dy) * 1000.0;
        }
    }
}
