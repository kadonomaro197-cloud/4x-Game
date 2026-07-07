using System.Linq;
using NUnit.Framework;
using Pulsar4X.Galaxy;
using Pulsar4X.Industry;
using Pulsar4X.Factions;
using Pulsar4X.Components;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Gauge for building a MINE ON a located deposit hex (the "build a mine on that deposit" connection). Deposits are
    /// located on individual <see cref="GroundHex"/>es (slice 1); this asserts <see cref="PlaceInstallationOnHexOrder"/>
    /// installs a real, faction-unlocked MINE design on the colony AND records that instance on the exact deposit hex
    /// (<see cref="GroundHex.InstallationIds"/>) — the physical "a building is a real building on the planet" placement.
    /// v1 keeps mining body-wide; the per-hex mining pass (a mine works the deposit on its OWN hex) is the follow-up.
    /// </summary>
    [TestFixture]
    public class HexMiningPlacementTests
    {
        private static void Log(string m) => TestContext.Progress.WriteLine("[hex-mine-place] " + m);

        [Test]
        [Description("Placing a mine on a located deposit hex installs it on the colony AND records it on that hex.")]
        public void PlaceMineOnDepositHex_InstallsAndLocatesOnTheHex()
        {
            var s = TestScenario.CreateWithColony();
            var body = s.StartingBody;
            var colony = s.Colony;

            var grid = PlanetGridFactory.EnsureGridForBody(body);   // builds the grid AND seeds deposits
            Assert.That(grid, Is.Not.Null, "the body has a surface grid");

            var depositHex = grid.Hexes.FirstOrDefault(h => h.DepositMineralId >= 0);
            Assert.That(depositHex, Is.Not.Null, "there is a located deposit hex to build a mine on");

            // A real, faction-unlocked MINE design (the start colony unlocks default-design-mine → a ComponentDesign
            // carrying MineResourcesAtbDB). The order looks the design up by its IndustryDesigns key.
            var mineKv = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns
                .FirstOrDefault(kv => kv.Value is ComponentDesign cd && cd.HasAttribute<MineResourcesAtbDB>());
            Assert.That(mineKv.Value, Is.Not.Null, "the faction has a buildable mine design");
            string mineId = mineKv.Key;

            int before = depositHex.InstallationIds.Count;

            var order = PlaceInstallationOnHexOrder.CreateCommand(colony, depositHex.Q, depositHex.R, mineId);
            order.Execute(s.Game.TimePulse.GameGlobalDateTime);

            Assert.That(order.GetIsFinished, Is.True, "the placement completed");
            Assert.That(depositHex.InstallationIds.Count, Is.EqualTo(before + 1),
                "the mine instance is now recorded ON the deposit hex");

            Log($"placed mine '{((ComponentDesign)mineKv.Value).Name}' on deposit hex ({depositHex.Q},{depositHex.R}); " +
                $"hex installations {before} -> {depositHex.InstallationIds.Count}");
        }

        [Test]
        [Description("Placing on an out-of-range/invalid hex is a safe no-op — never throws, doesn't finish.")]
        public void PlaceOnInvalidHex_IsASafeNoOp()
        {
            var s = TestScenario.CreateWithColony();
            var colony = s.Colony;

            var mineKv = s.Faction.GetDataBlob<FactionInfoDB>().IndustryDesigns
                .FirstOrDefault(kv => kv.Value is ComponentDesign cd && cd.HasAttribute<MineResourcesAtbDB>());
            string mineId = mineKv.Key;

            // Row far below the grid (rows are bounded, unlike wrapping columns) → HexAt returns null → no-op.
            var order = PlaceInstallationOnHexOrder.CreateCommand(colony, 0, 100000, mineId);
            Assert.DoesNotThrow(() => order.Execute(s.Game.TimePulse.GameGlobalDateTime));
            Assert.That(order.GetIsFinished, Is.False, "an invalid hex placement does not complete");
        }
    }
}
