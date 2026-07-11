using NUnit.Framework;
using Pulsar4X.Engine;
using Pulsar4X.Factions;
using Pulsar4X.Hazards;
using Pulsar4X.Ships;

namespace Pulsar4X.Tests
{
    /// <summary>
    /// Defense ⚙5 ▸ HARDENING — a WARP STABILIZER (S6). Hardening is surviving the ENVIRONMENT, not the enemy's guns:
    /// a warp-inhibitor field (a nebula / hazard) strands an unstabilized hull, and a warp stabilizer lets you jump
    /// through it anyway. It's the documented next preset in the Hazards subsystem ("a Warp Stabiliser vs WarpInhibit,
    /// each a JSON template") — pure DATA on the existing generic <see cref="HazardResistanceAtb"/> counter (a new
    /// resistance is JSON, not C#), consumed by <c>WarpMoveProcessor</c> via <see cref="SpaceHazardTools.ResistanceFraction"/>.
    ///
    /// Additive / byte-identical: a component nothing else on the ship reads (it only resists a hazard when one is
    /// present); the new Pathfinder Nebula Runner is a new example ship (no fixture perturbed). Engine-only → CI.
    /// </summary>
    [TestFixture]
    public class ShipWarpStabilizerTests
    {
        private const string Pathfinder = "default-ship-design-test-pathfinder"; // carries the warp stabilizer
        private const string Aegis = "default-ship-design-test-warship";         // no hardening
        private static void Log(string m) => TestContext.Progress.WriteLine("[warp-stabilizer] " + m);

        private static Entity Build(TestScenario s, string designId, string name)
        {
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            return ShipFactory.CreateShip(designs[designId], s.Faction, s.StartingBody, name);
        }

        [Test]
        [Description("Cradle-to-grave: the base-mod Pathfinder builds from JSON and its warp stabilizer resists the WarpInhibit hazard by its design fraction (0.6) through the real consumer path (SpaceHazardTools.ResistanceFraction, which WarpMoveProcessor reads). The JSON warp-stabilizer template + design + earth.json entries wired up (six-point registration).")]
        public void ThePathfinder_ResistsWarpInhibit()
        {
            var s = TestScenario.CreateWithColony();
            var designs = s.Faction.GetDataBlob<FactionInfoDB>().ShipDesigns;
            Assert.That(designs.ContainsKey(Pathfinder), Is.True,
                "the Pathfinder loads onto the faction — the JSON warp-stabilizer template + design + earth.json entries wired up");

            var ship = Build(s, Pathfinder, "Pathfinder");
            double resist = SpaceHazardTools.ResistanceFraction(ship, HazardEffectType.WarpInhibit);
            Log($"Pathfinder WarpInhibit resistance = {resist:0.00} (design 0.6)");

            Assert.That(resist, Is.EqualTo(0.6).Within(1e-9),
                "the warp stabilizer resists WarpInhibit by its design fraction (JSON → HazardResistanceAtb wired, consumed by the hazard read path)");
        }

        [Test]
        [Description("Byte-identical: a ship with no hardening (the Aegis) has zero WarpInhibit resistance — and the stabilizer resists ONLY WarpInhibit, not an unrelated hazard kind — so nothing else is perturbed.")]
        public void AShipWithout_HasNoResistance_AndItsKindSpecific()
        {
            var s = TestScenario.CreateWithColony();
            var aegis = Build(s, Aegis, "Aegis");
            Assert.That(SpaceHazardTools.ResistanceFraction(aegis, HazardEffectType.WarpInhibit), Is.EqualTo(0.0),
                "a ship with no hardening resists nothing → byte-identical");

            var pathfinder = Build(s, Pathfinder, "Pathfinder");
            Assert.That(SpaceHazardTools.ResistanceFraction(pathfinder, HazardEffectType.SensorJam), Is.EqualTo(0.0),
                "the warp stabilizer resists WarpInhibit only — it does NOT resist an unrelated hazard (SensorJam)");
        }
    }
}
