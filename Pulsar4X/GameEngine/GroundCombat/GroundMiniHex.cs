using System;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;

namespace Pulsar4X.GroundCombat
{
    /// <summary>
    /// The MINI-HEX tactical position math (docs/ground/GROUND-SURFACE-MAP-DESIGN.md Layer 5, M1). A unit's exact place on a
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

        /// <summary>K2 — the CONTINUOUS position including the unit's real sub-mini-hex OFFSET (<c>MiniOffX_km</c>,
        /// <c>MiniOffY_km</c>): the coarse-hex centre PLUS the mini-hex centre PLUS the real km offset within that mini-hex.
        /// This is what makes the field genuinely continuous below the ~37 km mini-tile — two units in the SAME mini-hex
        /// read a real gap equal to the difference of their offsets, so a real conventional weapon range (&lt; a mini-tile)
        /// can decide the fight. Offsets (0,0) → this equals the offset-free <see cref="ContinuousPosKm(int,int,int,int,double,int)"/>
        /// (byte-identical). Pure/no entity reads; never throws.</summary>
        public static (double x, double y) ContinuousPosKm(
            int globalQ, int globalR, int miniQ, int miniR, double miniOffX_km, double miniOffY_km, double coarsePitchKm, int cityRadius)
        {
            var (x, y) = ContinuousPosKm(globalQ, globalR, miniQ, miniR, coarsePitchKm, cityRadius);
            return (x + miniOffX_km, y + miniOffY_km);
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

        /// <summary>K2 — the real gap in METRES between two units INCLUDING their sub-mini-hex offsets
        /// (<c>MiniOffX_km</c>/<c>MiniOffY_km</c>), so two units in the same mini-hex read the difference of their offsets
        /// (not 0). This is the form the K3 real-distance resolver gate + closing model measure. Offsets (0,0) on both →
        /// this equals the offset-free <see cref="RealGapMetres(int,int,int,int,int,int,int,int,double,int)"/> above
        /// (byte-identical). Pure/no entity reads; never throws.</summary>
        public static double RealGapMetres(
            int aGlobalQ, int aGlobalR, int aMiniQ, int aMiniR, double aOffX_km, double aOffY_km,
            int bGlobalQ, int bGlobalR, int bMiniQ, int bMiniR, double bOffX_km, double bOffY_km,
            double coarsePitchKm, int cityRadius)
        {
            var (ax, ay) = ContinuousPosKm(aGlobalQ, aGlobalR, aMiniQ, aMiniR, aOffX_km, aOffY_km, coarsePitchKm, cityRadius);
            var (bx, by) = ContinuousPosKm(bGlobalQ, bGlobalR, bMiniQ, bMiniR, bOffX_km, bOffY_km, coarsePitchKm, cityRadius);
            double dx = ax - bx, dy = ay - by;
            return Math.Sqrt(dx * dx + dy * dy) * 1000.0;
        }

        // ── Entity-facing overloads (the resolver's M2 consumers) — defensive, never throw ──────────────────────────

        /// <summary>The real km size of ONE coarse global hex on this body — the body's true surface area spread over the
        /// global grid's hex count (the SAME flat-hex area→pitch relation as <c>GroundRangeTools.HexPitchKm</c>, just on
        /// the global cylinder grid instead of a region patch). ~477 km on Earth. Falls back to 0 if the body has no grid;
        /// callers substitute a sane default. Never throws.</summary>
        public static double CoarseHexPitchKmForBody(Entity body)
        {
            try
            {
                if (body == null) return 0.0;
                double radiusKm = (body.TryGetDataBlob<MassVolumeDB>(out var mv) ? mv.RadiusInM : 6.371e6) / 1000.0;
                double surface_km2 = 4.0 * Math.PI * radiusKm * radiusKm;
                int count = 0;
                if (body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB) && regionsDB.SurfaceGrid != null)
                    count = regionsDB.SurfaceGrid.Cols * regionsDB.SurfaceGrid.Rows;
                if (count <= 0 || surface_km2 <= 0) return 0.0;
                double areaPerHex = surface_km2 / count;
                return Math.Sqrt(2.0 * areaPerHex / Math.Sqrt(3.0));
            }
            catch { return 0.0; }
        }

        /// <summary>The real gap in METRES between two ground units on a body — reads their coarse (Global) + mini
        /// positions and the body's real coarse-hex pitch. This is the entity-facing form the resolver's M2 range gate
        /// calls: a unit FIRES when this ≤ its weapon's real <c>Range_m</c>. Two units in the SAME coarse global hex read
        /// gap 0 (the developer's "same hex = combat"); mini-hex spread (M3) opens a real sub-hex gap and lets edge units
        /// in ADJACENT coarse hexes engage across the border. Defensive: a null unit → <see cref="double.MaxValue"/> (out
        /// of everyone's range); a body with no grid → a ~Earth fallback pitch so it never divides by zero. Never throws.
        /// (M2 note: the global coords are used as axial — exact enough for the same-hex / different-hex fight decision;
        /// an odd-r→axial refinement for precise cross-hex distance rides M3, mirroring <c>PlanetViewWindow.OddRToAxial</c>.)</summary>
        public static double RealGapMetres(GroundUnit a, GroundUnit b, Entity body)
        {
            if (a == null || b == null) return double.MaxValue;
            double coarsePitchKm = CoarseHexPitchKmForBody(body);
            if (coarsePitchKm <= 0) coarsePitchKm = 477.0;   // ~Earth fallback — never 0-divide, never wall combat
            // K2 — include each unit's sub-mini-hex real offset, so two units in the same mini-hex read the difference of
            // their offsets (a real conventional weapon range < a mini-tile can decide the fight). Offsets default (0,0)
            // → this is byte-identical to the offset-free gap (the M2/M3a co-located case reads 0 as before).
            return RealGapMetres(a.GlobalQ, a.GlobalR, a.MiniQ, a.MiniR, a.MiniOffX_km, a.MiniOffY_km,
                                 b.GlobalQ, b.GlobalR, b.MiniQ, b.MiniR, b.MiniOffX_km, b.MiniOffY_km,
                                 coarsePitchKm, CityGridFactory.CityPatchRadius);
        }
    }
}
