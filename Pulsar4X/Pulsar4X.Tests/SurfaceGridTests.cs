using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.GroundCombat;
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

        // ── G2 — global A* on the wrapping cylinder (no edge gates) ────────────────────────────────────────────────

        private static SurfaceGrid PlainsGrid(int cols, int rows)
        {
            var g = new SurfaceGrid(cols, rows);
            for (int r = 0; r < rows; r++)
                for (int q = 0; q < cols; q++)
                    g.Hexes.Add(new GroundHex(q, r, RegionFeatureType.Plains));
            return g;
        }

        [Test]
        [Description("G2: a straight walk over open ground is the direct number of steps (excludes start, includes dest).")]
        public void FindGlobalPath_StraightWalk_OpenGround()
        {
            var g = PlainsGrid(10, 5);
            var path = HexPathfinder.FindGlobalPath(g, 2, 2, 5, 2);
            Assert.That(path.Count, Is.EqualTo(3), "three steps east: (3,2)(4,2)(5,2)");
            Assert.That(path.Last().Q, Is.EqualTo(5));
            Assert.That(path.Last().R, Is.EqualTo(2));
        }

        [Test]
        [Description("G2: a march routes AROUND ocean — the direct corridor is blocked, so the path detours and never steps on water.")]
        public void FindGlobalPath_RoutesAroundOcean()
        {
            var g = PlainsGrid(12, 6);
            g.HexAt(3, 2).Terrain = RegionFeatureType.Ocean;   // block the straight row-2 corridor from (2,2) to (6,2)
            g.HexAt(4, 2).Terrain = RegionFeatureType.Ocean;
            g.HexAt(5, 2).Terrain = RegionFeatureType.Ocean;

            var path = HexPathfinder.FindGlobalPath(g, 2, 2, 6, 2);
            Assert.That(path, Is.Not.Empty, "still reachable around the water");
            Assert.That(path.Any(h => h.Terrain == RegionFeatureType.Ocean), Is.False, "never steps onto open water");
            Assert.That(path.Count, Is.GreaterThan(4), "the detour is longer than the 4-step direct line");
            Assert.That((path.Last().Q, path.Last().R), Is.EqualTo((6, 2)), "arrives at the destination");
        }

        [Test]
        [Description("G2 THE POINT — the path WRAPS THE SEAM: from a low column to a high one the shortest route goes the short way across the longitude seam (no edge gates), not the long way around.")]
        public void FindGlobalPath_WrapsTheSeam()
        {
            var g = PlainsGrid(10, 5);
            // Column 1 → column 8: the long way (1→2→…→8) is 7 steps; the short way across the seam (1→0→9→8) is 3.
            var path = HexPathfinder.FindGlobalPath(g, 1, 2, 8, 2);
            Assert.That(path.Count, Is.EqualTo(3), "it goes the SHORT way across the seam, not the long way round");
            Assert.That(path.Any(h => h.Q == 0 || h.Q == 9), Is.True, "the route crosses the longitude seam (through column 0/9)");
        }

        [Test]
        [Description("G2: a destination on open water is unreachable (empty path).")]
        public void FindGlobalPath_DestOnOcean_Unreachable()
        {
            var g = PlainsGrid(10, 5);
            g.HexAt(5, 2).Terrain = RegionFeatureType.Ocean;
            Assert.That(HexPathfinder.FindGlobalPath(g, 2, 2, 5, 2), Is.Empty, "can't march onto open water");
        }
    }
}
