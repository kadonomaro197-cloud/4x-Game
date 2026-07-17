using System.Collections.Generic;
using NUnit.Framework;
using Pulsar4X.DataStructures;
using Pulsar4X.Galaxy;
using Pulsar4X.GeoSurveys;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Ground fog of war — slice 2 (`docs/ground/SURFACE-FOG-AND-RECON-DESIGN.md`): the space-survey → surface-fog link.
    /// Gauges <see cref="SurveyReveal.RevealWorldTo"/> — the reveal a completed orbital geo-survey grants the SURVEYING
    /// faction: the world's GEOGRAPHY (per-faction region reveal) + each deposit's LOCATION/TYPE at the PARTIAL tier
    /// (located, but the assay AMOUNT stays masked until a ground scout walks the hex — slice 3), while a NON-surveying
    /// faction still reads full fog. This is the pure core the `GeoSurveyProcessor` completion path calls; testing it
    /// directly avoids standing up the whole survey processor (an IInstanceProcessor needing a fleet + target + points).
    /// </summary>
    [TestFixture]
    public class SurveyRevealTests
    {
        // Faction BIT masks (FactionInfoDB.FactionMask = 1 << index) vs the raw faction IDs the region layer keys on —
        // deliberately different values so a bit-vs-id slip in the wiring shows up.
        private const int A_Id = 1, A_Mask = 1 << 3;   // surveyor
        private const int B_Id = 2, B_Mask = 1 << 5;   // never surveys
        private const long Tonnes = 5_000;

        private static PlanetRegionsDB WorldWithDeposits()
        {
            var regions = new List<Region>();
            for (int i = 0; i < 4; i++) regions.Add(new Region { Index = i });
            var db = new PlanetRegionsDB(regions);

            // A continuous grid with two deposit hexes (seeded HIDDEN, exactly as HexMinerals does) + non-deposit hexes.
            var grid = new SurfaceGrid(3, 2);
            var hexes = new List<GroundHex>();
            for (int i = 0; i < 6; i++)
            {
                var h = new GroundHex(i % 3, i / 3, RegionFeatureType.Mountains);
                if (i == 1 || i == 4) // two deposit hexes
                {
                    h.DepositMineralId = 7;
                    h.DepositAmount = Tonnes;
                    h.DepositAssay = new Masked<long>(Tonnes, AccessLevel.None);
                }
                hexes.Add(h);
            }
            grid.Hexes = hexes;
            db.SurfaceGrid = grid;
            return db;
        }

        [Test]
        [Description("A completed space survey reveals geography + deposit LOCATION (assay masked) to the surveying faction ONLY.")]
        public void SpaceSurvey_RevealsGeographyAndDepositLocation_ToSurveyorOnly_AssayStaysMasked()
        {
            var db = WorldWithDeposits();

            // Before: full fog for everyone.
            Assert.That(db.IsRegionRevealedFor(A_Id, 0), Is.False, "un-surveyed world reads fogged");
            foreach (var h in db.SurfaceGrid.Hexes)
                if (h.DepositMineralId >= 0)
                    Assert.That(h.AssayFor(A_Mask), Is.Null, "un-surveyed deposit's amount reads hidden");

            // A surveys from orbit.
            Assert.That(SurveyReveal.RevealWorldTo(db, A_Id, A_Mask), Is.True, "the survey reveals something");

            // GEOGRAPHY — A sees every region; B (never surveyed) sees none (per-faction).
            for (int r = 0; r < 4; r++)
            {
                Assert.That(db.IsRegionRevealedFor(A_Id, r), Is.True, $"surveyor A sees region {r}");
                Assert.That(db.IsRegionRevealedFor(B_Id, r), Is.False, $"B never surveyed — region {r} stays fogged for it");
            }

            // DEPOSITS — A knows each deposit is HERE but the amount is UN-ASSAYED (Partial); B sees nothing.
            int depositHexes = 0;
            foreach (var h in db.SurfaceGrid.Hexes)
            {
                if (h.DepositMineralId < 0) continue;
                depositHexes++;
                var forA = h.DepositAssay.Resolve(A_Mask);
                Assert.That(forA.Access, Is.EqualTo(AccessLevel.Partial), "space survey = located, not assayed");
                Assert.That(forA.IsKnown, Is.True);
                Assert.That(forA.IsExact, Is.False, "orbit does NOT reveal the exact tonnage");
                Assert.That(h.AssayFor(A_Mask), Is.EqualTo(0L), "the obscured amount reads 0 at the space-survey tier");
                Assert.That(h.AssayFor(B_Mask), Is.Null, "B surveyed nothing — the deposit stays hidden from it");
            }
            Assert.That(depositHexes, Is.EqualTo(2), "the fixture seeded two deposit hexes");

            // ADDITIVE: the world-level Surveyed flag is UNTOUCHED (the processor's separate RevealAll() owns that).
            foreach (var region in db.Regions)
                Assert.That(region.Surveyed, Is.False, "the per-faction reveal must NOT flip the world-level Surveyed flag");
        }

        [Test]
        [Description("Defensive: a null layer / a world with no surface grid does not throw and reveals what it can.")]
        public void RevealWorldTo_IsDefensive_NullAndGridless()
        {
            Assert.That(SurveyReveal.RevealWorldTo(null, A_Id, A_Mask), Is.False, "null layer → no-op, no throw");

            var regions = new List<Region>();
            for (int i = 0; i < 4; i++) regions.Add(new Region { Index = i });
            var db = new PlanetRegionsDB(regions);   // no SurfaceGrid generated yet
            Assert.That(SurveyReveal.RevealWorldTo(db, A_Id, A_Mask), Is.True, "still reveals the region geography");
            Assert.That(db.IsRegionRevealedFor(A_Id, 2), Is.True, "regions revealed even with no hex grid");
        }
    }
}
