using Pulsar4X.Galaxy;

namespace Pulsar4X.GeoSurveys
{
    /// <summary>
    /// The space-survey → surface-fog link (ground-fog slice 2, docs/ground/SURFACE-FOG-AND-RECON-DESIGN.md, path A).
    /// A completed geological survey FROM ORBIT reveals, TO THE SURVEYING FACTION ONLY:
    ///   • the world's GEOGRAPHY — every region's terrain, via the per-faction
    ///     <see cref="PlanetRegionsDB.RevealAllRegionsFor"/> (keyed by faction id), and
    ///   • each mineral deposit's LOCATION + TYPE — but the ASSAY amount stays MASKED, via
    ///     <see cref="GroundHex.RevealDepositLocation"/> (the <see cref="Pulsar4X.DataStructures.AccessLevel.Partial"/>
    ///     tier, keyed by the faction BIT mask): "there's space-crete at hex 5,99" but NOT "50 units at 0.1/day".
    /// The ground scout (slice 3) later unmasks the assay to Full when it walks the hex. This is the PER-FACTION twin of
    /// the world-level <see cref="PlanetRegionsDB.RevealAll"/> that <see cref="GeoSurveyProcessor"/> STILL calls (kept
    /// additive/byte-identical for the old world-level consumers until the client migrates to the per-faction read in
    /// slice 2b). It mirrors <c>MineralsDB.GrantFactionPartialAccess</c> — the body-wide mineral pool's own
    /// space-survey Partial grant — at region/hex granularity.
    ///
    /// Pure (operates on the region layer directly, no Entity/manager), so it's gauged without standing up the full
    /// survey processor. Defensive/no-throw (runs inside the survey hotloop): a null layer / an ungenerated grid simply
    /// reveals what it can.
    /// </summary>
    public static class SurveyReveal
    {
        /// <summary>
        /// Reveal <paramref name="regionsDB"/>'s GEOGRAPHY + each deposit's LOCATION/TYPE (assay masked) to
        /// <paramref name="factionId"/> / <paramref name="factionMask"/> — the space-survey tier. <paramref name="factionId"/>
        /// keys the per-faction REGION reveal (<see cref="PlanetRegionsDB.PerFactionRevealed"/>); <paramref name="factionMask"/>
        /// is the faction BIT mask (<c>FactionInfoDB.FactionMask</c>) the per-hex <c>Masked&lt;long&gt;</c> assay reads.
        /// Leaves the assay AMOUNT masked (Partial) — only a ground scout (slice 3) unmasks it. Does NOT flip the
        /// world-level <see cref="Region.Surveyed"/> bool (that stays the processor's separate <see cref="PlanetRegionsDB.RevealAll"/>
        /// call, additive). Returns true if anything was newly revealed.
        /// </summary>
        public static bool RevealWorldTo(PlanetRegionsDB regionsDB, int factionId, int factionMask)
        {
            if (regionsDB == null) return false;

            // 1) Geography — reveal every region to the surveying faction (per-faction, not world-level).
            bool changed = regionsDB.RevealAllRegionsFor(factionId);

            // 2) Deposit LOCATION + TYPE — grant the Partial tier on every deposit hex's masked assay, so the faction
            //    learns a deposit is HERE (+ its type via the now-revealed region) while the AMOUNT stays hidden.
            var grid = regionsDB.SurfaceGrid;
            if (grid?.Hexes != null)
            {
                foreach (var hex in grid.Hexes)
                {
                    if (hex == null || hex.DepositMineralId < 0) continue;
                    hex.RevealDepositLocation(factionMask);   // → AccessLevel.Partial (located, un-assayed)
                    changed = true;
                }
            }
            return changed;
        }
    }
}
