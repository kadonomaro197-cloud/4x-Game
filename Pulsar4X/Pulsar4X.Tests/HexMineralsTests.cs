using System.Linq;
using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Galaxy;
using Pulsar4X.Industry;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for LOCATED mineral deposits (the LOCKED PRINCIPLE applied to minerals): a body's deposits are seeded onto
    /// specific surface HEXES — "there are resources HERE" — so the map can flag them post-scan and a mine gets built on
    /// the actual deposit. Asserts that generating a body's surface grid seeds terrain-flavored deposit hexes: they
    /// exist, carry a real amount, land on MINEABLE terrain (never open ocean or ice), and span several minerals. The
    /// planet-view flagging + build-on-deposit + per-hex mining are the following slices; mining stays colony-wide here.
    /// </summary>
    [TestFixture]
    public class HexMineralsTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[hex-minerals] " + m);

        [Test]
        [Description("Generating a body's surface grid seeds terrain-flavored mineral deposits onto hexes: deposits exist with real amounts, never on ocean/ice, spanning several minerals.")]
        public void SurfaceGrid_SeedsTerrainFlavoredDepositHexes()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            Assert.That(body.HasDataBlob<MineralsDB>(), Is.True, "the homeworld has mineral deposits to locate");

            var grid = PlanetGridFactory.EnsureGridForBody(body);   // generates the grid AND seeds deposits
            Assert.That(grid, Is.Not.Null, "the body gets a surface grid");

            var depositHexes = grid.Hexes.Where(h => h.DepositMineralId >= 0).ToList();
            Assert.That(depositHexes.Count, Is.GreaterThan(0), "deposits are located on hexes — 'resources here'");
            Assert.That(depositHexes.All(h => h.DepositAmount > 0), Is.True, "every located deposit carries a real amount");

            // Terrain-flavored: deposits never sit on open ocean or ice (unmineable terrain).
            Assert.That(depositHexes.Any(h => h.Terrain == RegionFeatureType.Ocean || h.Terrain == RegionFeatureType.Ice),
                Is.False, "no deposit is placed on open ocean or ice");

            int distinctMinerals = depositHexes.Select(h => h.DepositMineralId).Distinct().Count();
            Assert.That(distinctMinerals, Is.GreaterThanOrEqualTo(3), "several different minerals are located across the surface");

            Log($"located {depositHexes.Count} deposit hex(es) across {distinctMinerals} mineral(s) on {grid.Hexes.Count} total hexes");
        }

        [Test]
        [Description("Seeding is idempotent — re-ensuring the grid doesn't duplicate or move deposits.")]
        public void SeedDeposits_IsIdempotent()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var grid = PlanetGridFactory.EnsureGridForBody(body);
            int before = grid.Hexes.Count(h => h.DepositMineralId >= 0);

            HexMinerals.SeedDeposits(body, grid);   // explicit re-seed — must be a no-op
            int after = grid.Hexes.Count(h => h.DepositMineralId >= 0);

            Assert.That(after, Is.EqualTo(before), "re-seeding an already-seeded grid changes nothing");
        }
    }
}
