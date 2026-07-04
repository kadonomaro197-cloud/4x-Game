using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.DataStructures;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// The ONE continuous cylinder hex grid (G-track, G1 — additive foundation). Proves the grid generates with
    /// scaled dimensions and clean region bands, that its terrain is COHERENT and WRAPS seamlessly at the longitude
    /// seam (the whole point — a continuous world, no discontinuity where region 4 meets region 1), that column
    /// lookups wrap, and that it's save-safe. The per-region disks are untouched by this slice (additive).
    /// Design: docs/GLOBAL-HEX-GRID-DESIGN.md.
    /// </summary>
    [TestFixture]
    public class SurfaceGridTests
    {
        [Test]
        [Description("G1: the cylinder grid generates with dimensions scaled to the planet and DIVISIBLE by the region count (clean bands); it's row-major and idempotent.")]
        public void Grid_Generates_ScaledAndBanded_Idempotent()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            Assert.That(body.TryGetDataBlob<PlanetRegionsDB>(out var regionsDB), Is.True);
            Assert.That(regionsDB.SurfaceGrid, Is.Null, "no grid until generated (lazy)");

            var grid = PlanetGridFactory.EnsureGridForBody(body);
            Assert.That(grid, Is.Not.Null);
            int regionCount = regionsDB.Regions.Count;
            Assert.That(grid.Cols % regionCount, Is.EqualTo(0), "columns divide evenly into region bands");
            Assert.That(grid.Cols, Is.GreaterThan(0));
            Assert.That(grid.Rows, Is.GreaterThan(0));
            Assert.That(grid.Hexes.Count, Is.EqualTo(grid.Cols * grid.Rows), "row-major, fully populated");

            Assert.That(PlanetGridFactory.EnsureGridForBody(body), Is.SameAs(grid), "idempotent — re-ensuring returns the same grid");
        }

        [Test]
        [Description("G1 THE POINT — the world is continuous AND wraps: adjacent columns mostly share terrain (coherent, not random), and the SEAM (last column ↔ column 0) is just as continuous as the interior — no discontinuity where the ring closes.")]
        public void Grid_TerrainIsCoherent_AndWrapsSeamlessly()
        {
            var s = TestScenario.CreateWithColony();
            var grid = PlanetGridFactory.EnsureGridForBody(s.StartingBody);
            Assert.That(grid, Is.Not.Null);

            double interiorMatch = 0, interiorPairs = 0, seamMatch = 0, seamPairs = 0;
            for (int r = 0; r < grid.Rows; r++)
                for (int q = 0; q < grid.Cols; q++)
                {
                    var a = grid.HexAt(q, r);
                    var b = grid.HexAt(q + 1, r);   // q = Cols-1 wraps to column 0 — the seam
                    bool match = a.Terrain == b.Terrain;
                    if (q == grid.Cols - 1) { seamPairs++; if (match) seamMatch++; }
                    else { interiorPairs++; if (match) interiorMatch++; }
                }

            double interiorFrac = interiorMatch / interiorPairs;
            double seamFrac = seamMatch / seamPairs;
            // Coherent (adjacent hexes usually match — random over ~14 terrains would be < 0.1).
            Assert.That(interiorFrac, Is.GreaterThan(0.4), "adjacent columns share terrain — a coherent world, not a random smatter");
            // Seamless: the wrap seam is continuous just like the interior (not a hard edge).
            Assert.That(seamFrac, Is.GreaterThanOrEqualTo(interiorFrac - 0.25),
                "the longitude seam (region 4 ↔ region 1) flows as smoothly as anywhere else — one continuous world");
        }

        [Test]
        [Description("G1: column lookups WRAP (col Cols == col 0, col -1 == col Cols-1); out-of-range rows return null.")]
        public void Grid_HexAt_WrapsColumns_ClampsRows()
        {
            var s = TestScenario.CreateWithColony();
            var grid = PlanetGridFactory.EnsureGridForBody(s.StartingBody);
            Assert.That(grid, Is.Not.Null);

            Assert.That(grid.HexAt(grid.Cols, 0), Is.SameAs(grid.HexAt(0, 0)), "column Cols wraps to 0");
            Assert.That(grid.HexAt(-1, 0), Is.SameAs(grid.HexAt(grid.Cols - 1, 0)), "column -1 wraps to Cols-1");
            Assert.That(grid.HexAt(0, -1), Is.Null, "row -1 is off the poles");
            Assert.That(grid.HexAt(0, grid.Rows), Is.Null, "row Rows is off the poles");
        }

        [Test]
        [Description("G1: region-band math — a column maps to its region band, and a band's centre column (the muster point) lands inside that band.")]
        public void RegionBands_MapCleanly()
        {
            const int cols = 100, rc = 4;   // 25 columns per band
            Assert.That(PlanetGridFactory.RegionOfColumn(0, cols, rc), Is.EqualTo(0));
            Assert.That(PlanetGridFactory.RegionOfColumn(24, cols, rc), Is.EqualTo(0));
            Assert.That(PlanetGridFactory.RegionOfColumn(25, cols, rc), Is.EqualTo(1));
            Assert.That(PlanetGridFactory.RegionOfColumn(99, cols, rc), Is.EqualTo(3));
            Assert.That(PlanetGridFactory.RegionOfColumn(100, cols, rc), Is.EqualTo(0), "column count wraps");

            for (int region = 0; region < rc; region++)
            {
                int cc = PlanetGridFactory.BandCentreColumn(region, cols, rc);
                Assert.That(PlanetGridFactory.RegionOfColumn(cc, cols, rc), Is.EqualTo(region),
                    "a band's centre column belongs to that band");
            }
        }

        [Test]
        [Description("G1: the grid is save-safe — it deep-copies on clone (mutating a clone's hex doesn't touch the original).")]
        public void Grid_ClonesDeep()
        {
            var s = TestScenario.CreateWithColony();
            var grid = PlanetGridFactory.EnsureGridForBody(s.StartingBody);
            Assert.That(grid, Is.Not.Null);

            var clone = new SurfaceGrid(grid);
            Assert.That(clone.Hexes.Count, Is.EqualTo(grid.Hexes.Count));
            clone.Hexes[0].InstallationIds.Add(4242);
            Assert.That(grid.Hexes[0].InstallationIds, Does.Not.Contain(4242), "deep copy — the clone's hex is its own object");
        }
    }
}
